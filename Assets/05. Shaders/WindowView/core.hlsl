// 창밖 풍경 효과 본체. 건물 안에서 창 Quad 를 통해 내다본 거리를 시선으로 계산해 그린다.
// 창 평면 뒤쪽 깊이마다 층(맞은편 건물, 인도, 가로등, 도로 위 차량 두 차선)을 두고, 눈에서 창 픽셀을 지나는 시선이 각 층과 만나는 점으로 모양을 그린다.
// 맞은편 건물의 창은 인테리어 매핑(창 칸마다 방 상자와 시선 교차)으로 입체 방을 보여 준다.

// SG_STEP: 중간 결과를 꺼내는 표시. Unity 에서는 아무 일도 하지 않는다.
#ifndef SG_STEP
#define SG_STEP(n, rgb)
#endif

// 아틀라스 2×2, 방 한 칸 128 px 가운데 둘레 4 px 여백을 뺀 120 px 에 방 그림이 있다 (256 px 기준 비율).
#define VIEW_ATLAS_PAD 0.015625
#define VIEW_ATLAS_SPAN 0.46875
// 맞은편 건물 한 층 높이(m)
#define VIEW_FLOOR_HEIGHT 3.2

// 0~1 격자 난수 (2D). 건물, 방, 차량, 사람마다 고정된 값을 고르는 데 쓴다.
float viewHash(float2 p)
{
    float3 q = frac(float3(p.x, p.y, p.x) * 0.1031);
    q = q + dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

// 2D 모양 거리 함수 (안쪽이 음수, 단위는 미터).
float viewBox(float2 p, float2 b)
{
    float2 d = abs(p) - b;
    return length(max(d, float2(0.0, 0.0))) + min(max(d.x, d.y), 0.0);
}

float viewCapsule(float2 p, float2 a, float2 b, float r)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / dot(ba, ba));
    return length(pa - ba * h) - r;
}

// 모양 거리 -> 가려지는 정도. aa 는 화면 한 픽셀이 차지하는 미터.
float viewCover(float d, float aa)
{
    return 1.0 - smoothstep(-aa, aa, d);
}

// 맞은편 창 한 칸의 방 안쪽을 그린다. f 는 칸 안 좌표(0~1), glassUV 는 유리 안쪽 좌표(커튼용),
// ray 는 방 상자 좌표로 맞춘 시선(xy 는 칸 -1~1, z 는 깊이 0~1 로 가는 비율).
float3 viewRoom(float2 f, float2 glassUV, float3 ray, float2 roomId, float cellAspect, float outside)
{
    float2 start = f * 2.0 - 1.0;
    // 시선이 나가는 쪽 옆벽(x), 바닥·천장(y), 뒷벽(z)까지의 거리. 가장 가까운 면이 먼저 닿는 면이다 (min).
    float2 dirSign = step(float2(0.0, 0.0), ray.xy) * 2.0 - 1.0;
    float2 tWall = (float2(1.0, 1.0) - start * dirSign) / max(abs(ray.xy), float2(0.00001, 0.00001));
    float tBack = 1.0 / max(ray.z, 0.00001);
    float t = min(min(tWall.x, tWall.y), tBack);
    float3 hit = float3(start + ray.xy * t, saturate(ray.z * t));
    // 그림은 창에서 방 깊이만큼 물러난 눈으로 본 한 점 투시이므로 q = xy / (1 + 깊이 비율).
    float2 q = hit.xy / (1.0 + hit.z);

    // 방마다 다른 그림과 불: 방 번호로 아틀라스 칸, 좌우 반전, 불 켜짐, 커튼을 고른다.
    float rKind = viewHash(roomId);
    float rFlip = viewHash(roomId + float2(5.3, 9.1));
    float rLight = viewHash(roomId + float2(11.9, 2.7));
    float rCurtain = viewHash(roomId + float2(3.1, 17.3));
    float rCurtainLook = viewHash(roomId + float2(23.5, 6.2));

    float kind = min(floor(rKind * 4.0), 3.0);
    float2 atlasCell = float2(kind - 2.0 * floor(kind * 0.5), floor(kind * 0.5));
    float2 qRoom = float2(lerp(q.x, -q.x, step(0.5, rFlip)), q.y);
    float2 atlasUV = atlasCell * 0.5 + float2(VIEW_ATLAS_PAD, VIEW_ATLAS_PAD) + (qRoom * 0.5 + float2(0.5, 0.5)) * VIEW_ATLAS_SPAN;
    float3 albedo = SG_TEX(_RoomAtlas, atlasUV).rgb;

    // 불빛: 천장 가운데 등에 가까울수록 밝다. 방마다 고정 난수와 비율을 비교해 비율이 오르면 새 방만 켜진다.
    float night = saturate(_Night);
    float lit = step(rLight, _LightsOn) * step(0.001, _LightsOn);
    float3 roomPos = float3(hit.x * 0.5, hit.y * 0.5 * cellAspect, hit.z * _RoomDepth);
    float3 lampPos = float3(0.0, 0.5 * cellAspect, 0.5 * _RoomDepth);
    float3 toLamp = roomPos - lampPos;
    float glow = 1.0 / (1.0 + 8.0 * dot(toLamp, toLamp));
    float fixture = step(0.999, hit.y) * (1.0 - smoothstep(0.08, 0.11, length(float2(roomPos.x, roomPos.z - lampPos.z))));
    float dayLight = 0.6 * outside;
    float lamp = lit * lerp(0.3, 1.0, night);
    float3 lampColor = _LightColor.rgb * lamp;
    float3 room = albedo * (float3(dayLight, dayLight, dayLight) + lampColor * (0.2 + 2.4 * glow)) + lampColor * fixture * 1.5;

    // 커튼: 창 평면에 붙은 반투명 주름 천. 켜진 방의 커튼은 뒤에서 등빛을 받아 비친다.
    float hasCurtain = step(rCurtain, _CurtainAmount) * step(0.001, _CurtainAmount);
    float curtainW = lerp(0.14, 0.3, frac(rCurtainLook * 7.0));
    float panel = max(1.0 - smoothstep(curtainW - 0.02, curtainW, glassUV.x), smoothstep(1.0 - curtainW, 1.0 - curtainW + 0.02, glassUV.x));
    float folds = 0.78 + 0.22 * sin(glassUV.x * 48.0);
    float3 curtainColor = lerp(float3(0.62, 0.48, 0.32), float3(0.25, 0.33, 0.45), step(0.55, rCurtainLook));
    float3 curtain = curtainColor * folds * (float3(dayLight, dayLight, dayLight) * 1.2 + lampColor * 0.9);
    return lerp(room, curtain, panel * hasCurtain * 0.85);
}

// 차량 한 대를 col 위에 그린다. p 는 차 앞쪽이 +x 인 지면 기준 위치(m), alpha 는 이 차가 있는지(0 이면 그리지 않는다).
float3 viewCar(float3 col, float2 p, float aa, float seed, float alpha, float ambient, float night)
{
    float3 paint = float3(0.25, 0.25, 0.25) + 0.7 * float3(viewHash(float2(seed, 1.1)), viewHash(float2(seed, 2.2)), viewHash(float2(seed, 3.3)));
    float body = viewBox(p - float2(0.0, 0.65), float2(1.88, 0.23)) - 0.12;
    float cabin = viewBox(p - float2(-0.15, 1.2), float2(0.85, 0.13)) - 0.15;
    float glassD = viewBox(p - float2(-0.15, 1.2), float2(0.7, 0.07)) - 0.1;
    float wheelA = length(p - float2(1.3, 0.35)) - 0.35;
    float wheelB = length(p - float2(-1.3, 0.35)) - 0.35;
    float shade = 0.65 + 0.35 * smoothstep(0.2, 1.5, p.y);

    col = lerp(col, float3(0.03, 0.03, 0.035) * ambient, viewCover(min(wheelA, wheelB), aa) * alpha);
    col = lerp(col, paint * shade * ambient, viewCover(min(body, cabin), aa) * alpha);
    col = lerp(col, float3(0.12, 0.16, 0.2) * ambient + float3(0.12, 0.14, 0.16) * (1.0 - night), viewCover(glassD, aa) * alpha);

    // 밤에는 앞 등(노랑 하양)과 뒷 등(빨강)이 켜지고 번진다.
    float head = length(p - float2(1.95, 0.72));
    float tail = length(p - float2(-1.95, 0.72));
    float on = night * alpha;
    col += float3(1.0, 0.92, 0.7) * on * (viewCover(head - 0.1, aa) + 0.5 * exp(-head * head * 18.0));
    col += float3(1.0, 0.1, 0.05) * on * (viewCover(tail - 0.07, aa) + 0.35 * exp(-tail * tail * 22.0));
    return col;
}

// 사람 한 명을 col 위에 그린다. p 는 발 기준 위치(m), phase 는 걸음 위상(다리가 벌어지는 정도).
float3 viewPerson(float3 col, float2 p, float aa, float seed, float phase, float ambient)
{
    float swing = sin(phase) * 0.2;
    float3 shirt = float3(0.2, 0.2, 0.2) + 0.7 * float3(viewHash(float2(seed, 4.4)), viewHash(float2(seed, 5.5)), viewHash(float2(seed, 6.6)));
    float legA = viewCapsule(p, float2(0.0, 0.85), float2(swing, 0.07), 0.065);
    float legB = viewCapsule(p, float2(0.0, 0.85), float2(-swing, 0.07), 0.065);
    float torso = viewCapsule(p, float2(0.0, 0.92), float2(0.0, 1.3), 0.17);
    float head = length(p - float2(0.0, 1.6)) - 0.12;
    col = lerp(col, float3(0.1, 0.11, 0.17) * ambient, viewCover(min(legA, legB), aa));
    col = lerp(col, shirt * ambient, viewCover(torso, aa));
    col = lerp(col, float3(0.85, 0.66, 0.52) * ambient, viewCover(head, aa));
    return col;
}

float4 sgEffect(SgInput i)
{
    float night = saturate(_Night);
    // 바깥 빛은 2 의 거듭제곱으로 줄인다. 선형으로 줄이면 sRGB 변환이 어두운 쪽을 들어 올려 밤에도 환하다.
    float outside = exp2(-6.0 * night);
    float ambient = lerp(0.2, 1.0, outside);

    // 0. 시선 설정. 창 평면의 오른쪽 축(rightWS), 창에서 바깥으로 가는 깊이 방향(-normalWS)으로 시선을 풀어 미터 단위로 계산한다.
    //    창 Quad 의 크기나 비율이 어떻든 바깥 풍경의 실제 크기가 같다. 눈은 창 앞(법선 쪽)에 있다.
    float3 nWS = normalize(i.normalWS);
    float3 rightWS = normalize(cross(nWS, float3(0.0, 1.0, 0.0)));
    float3 dir = -i.viewWS;
    float dx = dot(dir, rightWS);
    float dy = dir.y;
    float dz = max(dot(dir, -nWS), 0.0001);
    float3 rel = i.positionWS - i.centerWS;
    float px = dot(rel, rightWS);
    float py = rel.y;
    // 깊이 1 m 마다 옆과 위로 얼마나 움직이는가. 깊이 Z 에서 시선이 지나는 점은 (px + sx * Z, py + sy * Z).
    float sx = dx / dz;
    float sy = dy / dz;
    float G = _EyeHeight;

    float D = _BuildingDistance;
    float zRoadNear = 3.0;
    float zRoadFar = D - 3.0;
    float zCenter = (zRoadNear + zRoadFar) * 0.5;

    // 1. 하늘: 낮은 연한 하늘색에서 짙은 파랑으로, 밤은 별이 뜬다.
    float elev = saturate(dy);
    float3 skyDay = lerp(float3(0.78, 0.86, 0.95), float3(0.28, 0.52, 0.9), saturate(elev * 2.5));
    float3 skyNight = lerp(float3(0.05, 0.06, 0.12), float3(0.008, 0.012, 0.04), saturate(elev * 2.5));
    float3 sky = lerp(skyNight, skyDay, outside);
    float2 starP = float2(sx, sy) * 45.0;
    float starR = viewHash(floor(starP));
    float star = step(0.985, starR) * saturate(1.0 - length(frac(starP) - 0.5) * 3.0) * (0.6 + 0.4 * sin(i.time * 2.0 + starR * 60.0));
    sky += float3(1.0, 1.0, 1.0) * star * (1.0 - outside) * step(0.05, sy);
    SG_STEP(1, sky);

    // 2. 맞은편 건물: 깊이 D 의 평면. 건물 한 동 폭마다 동 번호로 높이와 벽 색을 고르고, 창 칸마다 방을 그린다.
    float hx = px + sx * D;
    float hy = py + sy * D;
    float by = hy + G;
    float bw = max(_BuildingWidth, 1.0);
    float bId = floor(hx / bw);
    float xl = hx - bId * bw;
    float rH = viewHash(float2(bId, 2.0));
    float rTone = viewHash(float2(bId, 5.0));
    float floors = max(floor(_WindowsY * (0.45 + 0.55 * rH)), 2.0);
    float buildingTop = floors * VIEW_FLOOR_HEIGHT;

    float cols = max(floor(_WindowsX + 0.5), 1.0);
    float colW = bw / cols;
    float cellAspect = VIEW_FLOOR_HEIGHT / colW;
    float2 cellPos = float2(xl / colW, by / VIEW_FLOOR_HEIGHT);
    float2 cellId = floor(cellPos);
    float2 f = frac(cellPos);

    // 창틀: 칸 가장자리에서 두께만큼은 외벽, 그 안쪽의 얇은 띠는 어두운 창틀. 경계는 화면 한 픽셀 폭으로 부드럽게 한다.
    float2 edgeDist = min(f, float2(1.0, 1.0) - f);
    float frameW = _FrameWidth * 0.5;
    float2 aaF = max(float2(fwidth(cellPos.x), fwidth(cellPos.y)), float2(0.00001, 0.00001));
    float2 openAxis = smoothstep(float2(frameW, frameW) - aaF, float2(frameW, frameW) + aaF, edgeDist);
    float opening = openAxis.x * openAxis.y;
    float innerW = frameW + 0.05;
    float2 glassAxis = smoothstep(float2(innerW, innerW) - aaF, float2(innerW, innerW) + aaF, edgeDist);
    float glassMask = glassAxis.x * glassAxis.y;
    float2 glassUV = saturate((f - float2(innerW, innerW)) / max(float2(1.0, 1.0) - 2.0 * float2(innerW, innerW), float2(0.001, 0.001)));

    // 칸의 방 상자 좌표 시선: 칸 한 칸을 xy -1~1, 깊이를 칸 폭 x _RoomDepth 로 맞춘다.
    float3 roomRay = float3(dx * 2.0 / colW, dy * 2.0 / VIEW_FLOOR_HEIGHT, dz / (max(_RoomDepth, 0.01) * colW));
    float2 roomId = cellId + float2(bId * 17.3, bId * 7.9);
    float3 interior = viewRoom(f, glassUV, roomRay, roomId, cellAspect, outside);
    SG_STEP(2, interior * glassMask);

    float3 wall = _WallColor.rgb * (0.7 + 0.6 * rTone) * (0.03 + 0.97 * outside);
    float3 frameColor = float3(0.07, 0.07, 0.075) * (0.03 + 0.97 * outside);
    // 유리에는 하늘이 조금 비친다.
    float3 glass = lerp(interior, lerp(skyNight, skyDay, outside) , saturate(_GlassReflect * 0.35));
    float3 bld = lerp(wall, frameColor, opening);
    bld = lerp(bld, glass, glassMask);
    // 멀리 있어 약간 뿌옇게 하늘색으로 섞는다.
    bld = lerp(bld, sky, 0.08);

    // 3. 땅: 시선이 높이 -G(길 위)에 닿는 깊이 Zg. 건물 앞(by < 0)에서 땅을 먼저 만난다.
    float Zg = (-G - py) / min(sy, -0.0001);
    float gx = px + sx * Zg;
    float aaZ = max(fwidth(Zg), 0.001);
    float detail = 1.0 - smoothstep(0.15, 0.8, aaZ);
    float2 tile = frac(float2(gx, Zg) / 1.4);
    float tileLine = 1.0 - smoothstep(0.0, 0.05, min(tile.x, tile.y));
    float3 sidewalk = float3(0.5, 0.5, 0.52) * (1.0 - 0.18 * tileLine * detail);
    float3 asphalt = float3(0.12, 0.12, 0.13);
    // 중앙선: 차선 사이의 흰 점선. 가장자리의 연석은 밝은 선.
    float dash = (1.0 - smoothstep(0.05, 0.1, abs(Zg - zCenter))) * step(frac(gx / 5.0), 0.55) * detail;
    float curb = max(1.0 - smoothstep(0.06, 0.14, abs(Zg - zRoadNear)), 1.0 - smoothstep(0.06, 0.14, abs(Zg - zRoadFar))) * detail;
    float onRoad = step(zRoadNear, Zg) * step(Zg, zRoadFar);
    float3 ground = lerp(sidewalk, asphalt, onRoad);
    ground = lerp(ground, float3(0.85, 0.85, 0.8), dash * onRoad);
    ground = lerp(ground, float3(0.7, 0.7, 0.7), curb);
    ground *= ambient;
    // 가로등 불빛이 만드는 바닥의 빛 웅덩이: 12 m 마다 하나, 밤에만.
    float zLamp = zRoadFar + 0.5;
    float lampGx = gx - 12.0 * floor(gx / 12.0 + 0.5);
    float pool = exp(-(lampGx * lampGx * 0.06 + (Zg - zLamp) * (Zg - zLamp) * 0.12)) * (1.0 - outside);
    ground += float3(1.0, 0.78, 0.45) * pool * 0.45;
    ground = lerp(ground, sky, 0.1 * saturate(Zg / (D * 2.0)));
    SG_STEP(3, ground);

    // 건물이냐 땅이냐 하늘이냐: by < 0 이면 땅, 0 이상이고 건물 높이 안이면 건물, 그 위는 하늘.
    float isGround = step(by, 0.0);
    float isBuilding = (1.0 - isGround) * step(by, buildingTop);
    float3 col = lerp(sky, bld, isBuilding);
    col = lerp(col, ground, isGround);

    // 4. 사람: 맞은편 인도(깊이 D - 1.5)를 3 명이 제각각의 범위와 속도로 왔다 갔다 한다. 방향을 바꿀 때 한 걸음씩 멈춰 보이지 않게 삼각파로 위치를 정한다.
    float zWalk = D - 1.5;
    float2 hW = float2(px + sx * zWalk, py + sy * zWalk);
    float aaW = (max(fwidth(hW.x), fwidth(hW.y)) * 0.75 + 0.0001) / _ActorScale;
    [unroll]
    for (int k = 0; k < 3; k++)
    {
        float seed = 11.0 + k * 3.7;
        float range = 3.0 + 3.0 * viewHash(float2(seed, 0.5));
        float center = (k - 1.0) * 6.0 + 2.0 * viewHash(float2(seed, 1.5));
        float w = i.time * _WalkSpeed / (4.0 * range) + viewHash(float2(seed, 2.5));
        float tri = abs(frac(w) * 2.0 - 1.0);
        float pos = center + (tri * 2.0 - 1.0) * range;
        float2 pp = float2(hW.x - pos, hW.y + G) / _ActorScale;
        col = viewPerson(col, pp, aaW, seed, i.time * _WalkSpeed * 4.5 + k * 2.0, ambient);
    }

    // 5. 가로등: 인도 쪽 12 m 마다 하나. 기둥과 머리, 밤에는 머리가 빛난다.
    float2 hL = float2(px + sx * zLamp, py + sy * zLamp);
    float aaL = max(fwidth(hL.x), fwidth(hL.y)) * 0.75 + 0.0001;
    float lx = hL.x - 12.0 * floor(hL.x / 12.0 + 0.5);
    float lv = hL.y + G;
    float pole = viewCover(abs(lx) - 0.06, aaL) * step(0.0, lv) * step(lv, 5.0);
    col = lerp(col, float3(0.08, 0.09, 0.1) * ambient, pole);
    float lampHead = length(float2(lx, lv - 5.0));
    col += float3(1.0, 0.82, 0.5) * (1.0 - outside) * (viewCover(lampHead - 0.18, aaL) + 0.7 * exp(-lampHead * lampHead * 5.0));

    // 6. 차량: 두 차선(안쪽 차선이 창에 가깝다). 한 차선에 _CarPeriod 초마다 한 대씩 지나가고, 차선마다 방향이 반대이며 반 주기 어긋난다.
    //    한 번 지나가는 데 주기보다 오래 걸려도 겹치지 않게 지금 주기와 바로 전 주기 두 대를 계산한다. 가끔(4분의 1) 한 대가 빠져 규칙적이지 않게 한다.
    [unroll]
    for (int lane = 0; lane < 2; lane++)
    {
        float Z = zCenter + ((lane == 0) ? 1.6 : -1.6);
        float2 hC = float2(px + sx * Z, py + sy * Z);
        float aaC = (max(fwidth(hC.x), fwidth(hC.y)) * 0.75 + 0.0001) / _ActorScale;
        float dirS = (lane == 0) ? 1.0 : -1.0;
        float T = i.time / max(_CarPeriod, 0.1) + lane * 0.5 + 0.17;
        [unroll]
        for (int j = 0; j < 2; j++)
        {
            float cyc = floor(T) - j;
            float ph = (frac(T) + j) * _CarPeriod;
            float xpos = (-_TrafficRange + _CarSpeed * ph) * dirS;
            float present = step(0.25, viewHash(float2(cyc, lane + 3.0)));
            float2 pp = float2((hC.x - xpos) * dirS, hC.y + G) / _ActorScale;
            col = viewCar(col, pp, aaC, cyc * 2.0 + lane, present, ambient, 1.0 - outside);
        }
    }
    SG_STEP(4, col);

    // 7. 창틀과 유리: 창 Quad 가장자리의 어두운 틀, 비스듬히 볼수록 유리에 밝은 막이 낀다.
    float2 uvEdge = min(i.uv, float2(1.0, 1.0) - i.uv);
    float aaP = max(fwidth(i.uv.x), fwidth(i.uv.y)) + 0.0001;
    float paneEdge = min(uvEdge.x, uvEdge.y);
    float paneFrame = 1.0 - smoothstep(_PaneFrame - aaP, _PaneFrame + aaP, paneEdge);
    float ndv = saturate(dot(nWS, i.viewWS));
    float fresnel = 0.15 + 0.85 * pow(1.0 - ndv, 2.0);
    col += lerp(skyNight, skyDay, outside) * saturate(_GlassReflect * fresnel * 0.25);
    col = lerp(col, float3(0.06, 0.05, 0.045), paneFrame * step(0.0001, _PaneFrame));
    return float4(col, 1.0);
}
