// 창밖 풍경 효과 본체. 건물 안에서 창 Quad 를 통해 내다본 거리를 시선으로 계산해 그린다.
// 창 평면 뒤쪽 깊이마다 층(맞은편 건물, 인도, 가로등, 도로 위 차량 두 차선)을 두고, 눈에서 창 픽셀을 지나는 시선이 각 층과 만나는 점으로 모양을 그린다.
// 맞은편 건물의 창은 인테리어 매핑(창 칸마다 방 상자와 시선 교차)으로 입체 방을 보여 준다 (view_room.hlsl).
//
// 최적화 (11-21):
//   - 영역 나누기: 픽셀이 땅 · 건물 · 하늘 중 어디인지 먼저 정하고 그 영역만 계산한다. 하늘 픽셀은 방 계산(가장 무거움)을 건너뛴다.
//   - 띠 검사: 차 · 사람 · 가로등은 화면의 얇은 가로 띠에만 있으므로 그 높이 밖이면 계산하지 않는다.
//   - 화면 미분(fwidth)은 분기 앞에서 한 번만 구한다. 분기 안에서 부르면 옆 픽셀이 다른 길로 가서 값이 깨지기 때문이다.
//     깊이 Z 평면 위 한 픽셀의 크기(m)는 '창 위치의 미분 + 시선 기울기의 미분 × Z' 로 어림한다.
//   - _WINDOW_NIGHT 키워드: 집 창처럼 늘 밤인 창은 낮 계산을 컴파일 때 뺀다.
#include "view_shapes.hlsl"
#include "view_room.hlsl"

// SG_STEP: 중간 결과를 꺼내는 표시. Unity 에서는 아무 일도 하지 않는다.
#ifndef SG_STEP
#define SG_STEP(n, rgb)
#endif

// 하늘 색. 밤은 위쪽 진보라에서 지평선 쪽 분홍빛 도시 불빛으로, 낮은 연한 하늘색에서 짙은 파랑으로.
half3 viewSky(float elev, half outside)
{
    half e = saturate(elev * 2.5);
    half3 night = lerp((half3)_SkyHorizonColor.rgb, (half3)_SkyTopColor.rgb, e);
#if defined(_WINDOW_NIGHT)
    return night;
#else
    half3 day = lerp(half3(0.78, 0.86, 0.95), half3(0.28, 0.52, 0.9), e);
    return lerp(night, day, outside);
#endif
}

float4 sgEffect(SgInput i)
{
#if defined(_WINDOW_NIGHT)
    const half night = 1.0;
    const half outside = 0.0;
#else
    half night = saturate(_Night);
    // 바깥 빛은 2 의 거듭제곱으로 줄인다. 선형으로 줄이면 sRGB 변환이 어두운 쪽을 들어 올려 밤에도 환하다.
    half outside = exp2(-6.0 * night);
#endif
    half ambient = lerp(0.2, 1.0, outside);
    half3 lampLight = (half3)_StreetLampColor.rgb;

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
    float zWalk = D - 1.5;
    float zLamp = zRoadFar + 0.5;

    // 화면 미분: 분기 앞에서 한 번. pixelAt(Z) = fp + fs * Z 가 깊이 Z 평면에서 한 픽셀의 크기(m).
    float4 fw = fwidth(float4(px, py, sx, sy));
    float fp = max(fw.x, fw.y);
    float fs = max(fw.z, fw.w);
    float2 fuv = fwidth(i.uv);

    half3 skyBase = viewSky(saturate(dy), outside);

    // 맞은편 건물 평면(깊이 D) 위의 점. 건물 한 동 폭마다 동 번호로 높이와 벽 색을 고른다.
    float hx = px + sx * D;
    float by = py + sy * D + G;
    float bw = max(_BuildingWidth, 1.0);
    float bId = floor(hx / bw);
    float floors = max(floor(_WindowsY * (0.45 + 0.55 * viewHash(float2(bId, 2.0)))), 2.0);
    float buildingTop = floors * VIEW_FLOOR_HEIGHT;

    half3 col;
    [branch]
    if (by < 0.0)
    {
        // 1. 땅: 시선이 높이 -G(길 위)에 닿는 깊이 Zg. 건물 앞(by < 0)에서 땅을 먼저 만난다.
        float Zg = (-G - py) / min(sy, -0.0001);
        float gx = px + sx * Zg;
        // Zg 의 화면 미분을 식으로 구한다: |dZg| <= (|dpy| + Zg |dsy|) / |sy|.
        float aaZ = max((fw.y + Zg * fw.w) / max(-sy, 0.0001), 0.001);
        half detail = 1.0 - smoothstep(0.15, 0.8, aaZ);
        float2 tile = frac(float2(gx, Zg) / 1.4);
        half tileLine = 1.0 - smoothstep(0.0, 0.05, min(tile.x, tile.y));
        half3 sidewalk = half3(0.5, 0.5, 0.52) * (1.0 - 0.18 * tileLine * detail);
        half3 asphalt = half3(0.12, 0.12, 0.13);
        // 중앙선: 차선 사이의 흰 점선. 가장자리의 연석은 밝은 선.
        half dash = (1.0 - smoothstep(0.05, 0.1, abs(Zg - zCenter))) * step(frac(gx / 5.0), 0.55) * detail;
        half curb = max(1.0 - smoothstep(0.06, 0.14, abs(Zg - zRoadNear)), 1.0 - smoothstep(0.06, 0.14, abs(Zg - zRoadFar))) * detail;
        half onRoad = step(zRoadNear, Zg) * step(Zg, zRoadFar);
        half3 ground = lerp(sidewalk, asphalt, onRoad);
        ground = lerp(ground, half3(0.85, 0.85, 0.8), dash * onRoad);
        ground = lerp(ground, half3(0.7, 0.7, 0.7), curb);
        ground *= ambient;
        // 가로등 불빛이 만드는 바닥의 빛 웅덩이: 12 m 마다 하나, 밤에만.
        float lampGx = gx - 12.0 * floor(gx / 12.0 + 0.5);
        half pool = exp(-(lampGx * lampGx * 0.06 + (Zg - zLamp) * (Zg - zLamp) * 0.12)) * (1.0 - outside);
        ground += lampLight * pool * 0.45;
        col = lerp(ground, skyBase, 0.1 * saturate(Zg / (D * 2.0)));
        SG_STEP(3, col);
    }
    else if (by <= buildingTop)
    {
        // 2. 맞은편 건물: 창 칸마다 방을 그린다.
        float xl = hx - bId * bw;
        half rTone = viewHash(float2(bId, 5.0));
        float cols = max(floor(_WindowsX + 0.5), 1.0);
        float colW = bw / cols;
        float cellAspect = VIEW_FLOOR_HEIGHT / colW;
        float2 cellPos = float2(xl / colW, by / VIEW_FLOOR_HEIGHT);
        float2 cellId = floor(cellPos);
        float2 f = frac(cellPos);
        float pixelD = fp + fs * D;

        // 창틀: 칸 가장자리에서 두께만큼은 외벽, 그 안쪽의 얇은 띠는 어두운 창틀. 경계는 화면 한 픽셀 폭으로 부드럽게 한다.
        float2 edgeDist = min(f, float2(1.0, 1.0) - f);
        float frameW = _FrameWidth * 0.5;
        float2 aaF = max(pixelD / float2(colW, VIEW_FLOOR_HEIGHT), float2(0.00001, 0.00001));
        float2 openAxis = smoothstep(frameW - aaF, frameW + aaF, edgeDist);
        half opening = openAxis.x * openAxis.y;
        float innerW = frameW + 0.05;
        float2 glassAxis = smoothstep(innerW - aaF, innerW + aaF, edgeDist);
        half glassMask = glassAxis.x * glassAxis.y;

        half3 wall = (half3)_WallColor.rgb * (0.7 + 0.6 * rTone) * lerp(half3(0.045, 0.035, 0.06), half3(1.0, 1.0, 1.0), outside);
        half3 frameColor = half3(0.07, 0.07, 0.075) * (0.03 + 0.97 * outside);
        half3 bld = lerp(wall, frameColor, opening);

        // 유리 안쪽(방)은 유리가 보이는 픽셀만 계산한다. 외벽 · 창틀 픽셀은 건너뛴다.
        [branch]
        if (glassMask > 0.0)
        {
            float2 glassUV = saturate((f - innerW) / max(1.0 - 2.0 * innerW, 0.001));
            // 칸의 방 상자 좌표 시선: 칸 한 칸을 xy -1~1, 깊이를 칸 폭 x _RoomDepth 로 맞춘다.
            float3 roomRay = float3(dx * 2.0 / colW, dy * 2.0 / VIEW_FLOOR_HEIGHT, dz / (max(_RoomDepth, 0.01) * colW));
            float2 roomId = cellId + float2(bId * 17.3, bId * 7.9);
            // 밉맵 단계: 한 픽셀이 덮는 방 그림 텍셀 수 (뒷벽 쪽이 더 촘촘하므로 1.5 배).
            float lod = max(log2(pixelD / colW * VIEW_CELL_TEXELS * 1.5), 0.0);
            half3 interior = viewRoom(f, glassUV, roomRay, roomId, cellAspect, outside, night, lod);
            SG_STEP(2, interior * glassMask);
            // 유리에는 하늘이 조금 비친다.
            half3 glass = lerp(interior, skyBase, saturate(_GlassReflect * 0.35));
            bld = lerp(bld, glass, glassMask);
        }
        // 멀리 있어 약간 뿌옇게 하늘색으로 섞는다.
        bld = lerp(bld, skyBase, 0.08);

        // MALLANG 네온 간판: 외벽 앞에 붙은 판 하나(가로:세로 2:1). 가끔 지지직 깜빡이고, 둘레 벽에 분홍 빛이 번진다.
        float2 signHalf = float2(_SignWidth * 0.5, _SignWidth * 0.25);
        float2 sp = float2(hx - _SignOffset, by - _SignHeight);
        float signD = viewBox(sp, signHalf);
        float burst = step(frac(i.time / 9.0), 0.12) * step(viewHash(float2(floor(i.time * 12.0), 7.0)), 0.5);
        half signOn = (1.0 - 0.85 * burst) * lerp(1.0, 0.35, outside);
        bld += (half3)_SignColor.rgb * signOn * 0.3 * exp(-max(signD, 0.0) * 1.5) * (1.0 - outside);
        [branch]
        if (signD < pixelD)
        {
            float2 signUV = float2(0.5 + (VIEW_CELL_PAD + (sp.x / signHalf.x * 0.5 + 0.5) * (2.0 - 2.0 * VIEW_CELL_PAD)) * 0.25,
                                   (VIEW_CELL_PAD + (sp.y / signHalf.y * 0.5 + 0.5) * VIEW_CELL_SPAN) * 0.5);
            float signLod = max(log2(pixelD / (2.0 * signHalf.y) * VIEW_CELL_TEXELS), 0.0);
            half3 sign = SG_TEX_LOD(_RoomAtlas, signUV, signLod).rgb * _SignGlow * signOn;
            bld = lerp(bld, sign, viewCover(signD, pixelD));
        }
        col = bld;
    }
    else
    {
        // 3. 하늘: 밤은 별이 뜬다.
        float2 starP = float2(sx, sy) * 45.0;
        float starR = viewHash(floor(starP));
        half star = step(0.985, starR) * saturate(1.0 - length(frac(starP) - 0.5) * 3.0) * (0.6 + 0.4 * sin(i.time * 2.0 + starR * 60.0));
        col = skyBase + star * (1.0 - outside) * step(0.05, sy);
        SG_STEP(1, col);
    }

    // 4. 사람: 맞은편 인도(깊이 zWalk)를 3 명이 제각각의 범위와 속도로 왔다 갔다 한다. 방향을 바꿀 때 한 걸음씩 멈춰 보이지 않게 삼각파로 위치를 정한다.
    //    사람 키 높이의 띠 안에서만 계산한다.
    float2 hW = float2(px + sx * zWalk, py + sy * zWalk + G) / _ActorScale;
    [branch]
    if (hW.y > -0.2 && hW.y < 1.9)
    {
        float aaW = ((fp + fs * zWalk) * 0.75 + 0.0001) / _ActorScale;
        [unroll]
        for (int k = 0; k < 3; k++)
        {
            float seed = 11.0 + k * 3.7;
            float range = 3.0 + 3.0 * viewHash(float2(seed, 0.5));
            float center = (k - 1.0) * 6.0 + 2.0 * viewHash(float2(seed, 1.5));
            float w = i.time * _WalkSpeed / (4.0 * range) + viewHash(float2(seed, 2.5));
            float tri = abs(frac(w) * 2.0 - 1.0);
            float pos = (center + (tri * 2.0 - 1.0) * range) / _ActorScale;
            col = viewPerson(col, float2(hW.x - pos, hW.y), aaW, seed, i.time * _WalkSpeed * 4.5 + k * 2.0, ambient);
        }
    }

    // 5. 가로등: 인도 쪽 12 m 마다 하나. 기둥과 머리, 밤에는 머리가 빛난다. 기둥 높이 띠 안에서만 계산한다.
    float2 hL = float2(px + sx * zLamp, py + sy * zLamp + G);
    [branch]
    if (hL.y > -0.1 && hL.y < 6.3)
    {
        float aaL = (fp + fs * zLamp) * 0.75 + 0.0001;
        float lx = hL.x - 12.0 * floor(hL.x / 12.0 + 0.5);
        half pole = viewCover(abs(lx) - 0.06, aaL) * step(0.0, hL.y) * step(hL.y, 5.0);
        col = lerp(col, half3(0.08, 0.09, 0.1) * ambient, pole);
        float lampHead = length(float2(lx, hL.y - 5.0));
        col += lampLight * (1.0 - outside) * (viewCover(lampHead - 0.18, aaL) + 0.7 * exp(-lampHead * lampHead * 5.0));
    }

    // 6. 차량: 두 차선(안쪽 차선이 창에 가깝다). 한 차선에 _CarPeriod 초마다 한 대씩 지나가고, 차선마다 방향이 반대이며 반 주기 어긋난다.
    //    한 번 지나가는 데 주기보다 오래 걸려도 겹치지 않게 지금 주기와 바로 전 주기 두 대를 계산한다. 가끔(4분의 1) 한 대가 빠져 규칙적이지 않게 한다.
    //    차 높이(등 번짐 포함) 띠 안에서만 계산한다.
    [unroll]
    for (int lane = 0; lane < 2; lane++)
    {
        float Z = zCenter + ((lane == 0) ? 1.6 : -1.6);
        float2 hC = float2(px + sx * Z, py + sy * Z + G) / _ActorScale;
        [branch]
        if (hC.y > -0.5 && hC.y < 2.0)
        {
            float aaC = ((fp + fs * Z) * 0.75 + 0.0001) / _ActorScale;
            float dirS = (lane == 0) ? 1.0 : -1.0;
            float T = i.time / max(_CarPeriod, 0.1) + lane * 0.5 + 0.17;
            [unroll]
            for (int j = 0; j < 2; j++)
            {
                float cyc = floor(T) - j;
                float ph = (frac(T) + j) * _CarPeriod;
                float xpos = (-_TrafficRange + _CarSpeed * ph) * dirS / _ActorScale;
                half present = step(0.25, viewHash(float2(cyc, lane + 3.0)));
                float2 pp = float2((hC.x - xpos) * dirS, hC.y);
                col = viewCar(col, pp, aaC, cyc * 2.0 + lane, present, ambient, 1.0 - outside);
            }
        }
    }
    SG_STEP(4, col);

    // 7. 창틀과 유리: 창 Quad 가장자리의 어두운 틀, 비스듬히 볼수록 유리에 밝은 막이 낀다.
    float2 uvEdge = min(i.uv, float2(1.0, 1.0) - i.uv);
    float aaP = max(fuv.x, fuv.y) + 0.0001;
    float paneEdge = min(uvEdge.x, uvEdge.y);
    half paneFrame = 1.0 - smoothstep(_PaneFrame - aaP, _PaneFrame + aaP, paneEdge);
    half ndv = saturate(dot(nWS, i.viewWS));
    half rim = 1.0 - ndv;
    half fresnel = 0.15 + 0.85 * rim * rim;
    col += skyBase * saturate(_GlassReflect * fresnel * 0.25);
    col = lerp(col, half3(0.06, 0.05, 0.045), paneFrame * step(0.0001, _PaneFrame));
    return float4(col, 1.0);
}
