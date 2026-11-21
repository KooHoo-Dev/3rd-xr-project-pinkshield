// 맞은편 건물 창 한 칸의 방 안쪽 (인테리어 매핑). core.hlsl 이 포함한다.
// 방 그림 아틀라스(InteriorRooms.png, 512 × 256)는 4 × 2 칸, 한 칸 128 px 가운데 둘레 4 px 여백을 뺀 120 px 에 그림이 있다.
//   UV 아래 줄(v 0~0.5): 보통 방 2칸 | MALLANG 네온 간판 (오른쪽 두 칸을 합친 한 장, 가로:세로 2:1)
//   UV 위 줄 (v 0.5~1): 보통 방 2칸 | 슬라임 인형 방 | MALLANG 포스터 방
#ifndef VIEW_ROOM_INCLUDED
#define VIEW_ROOM_INCLUDED

#define VIEW_ATLAS_GRID float2(4.0, 2.0)
#define VIEW_CELL_PAD 0.03125
#define VIEW_CELL_SPAN 0.9375
// 방 그림 한 칸의 픽셀 수 (밉맵 단계 계산용)
#define VIEW_CELL_TEXELS 120.0
// 맞은편 건물 한 층 높이(m)
#define VIEW_FLOOR_HEIGHT 3.2

// 칸 번호(cell)와 칸 안 좌표(-1~1)로 아틀라스 UV 를 만든다.
float2 viewAtlasUV(float2 cell, float2 q)
{
    return (cell + VIEW_CELL_PAD + (q * 0.5 + 0.5) * VIEW_CELL_SPAN) / VIEW_ATLAS_GRID;
}

// 방마다 다른 불빛 색: 대부분은 _LightColor(따뜻한 노랑), _AccentLights 비율만큼은 슬라임 속성 색(분홍 · 민트 · 하늘 · 연보라).
half3 viewRoomLightColor(float2 roomId)
{
    float r = viewHash(roomId + float2(41.3, 13.7));
    float a = floor(frac(r * 7.31) * 4.0);
    half3 accent = (a < 1.0) ? half3(1.0, 0.55, 0.75)
                 : (a < 2.0) ? half3(0.55, 1.0, 0.8)
                 : (a < 3.0) ? half3(0.6, 0.78, 1.0)
                 :             half3(0.8, 0.65, 1.0);
    return (r < _AccentLights) ? accent : (half3)_LightColor.rgb;
}

// f 는 칸 안 좌표(0~1), glassUV 는 유리 안쪽 좌표(커튼용),
// ray 는 방 상자 좌표로 맞춘 시선(xy 는 칸 -1~1, z 는 깊이 0~1 로 가는 비율).
// 분기 안에서 불리므로 텍스처는 화면 미분 대신 core 가 미리 계산한 밉맵 단계(lod)로 읽는다.
half3 viewRoom(float2 f, float2 glassUV, float3 ray, float2 roomId, float cellAspect, half outside, half night, float lod)
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
    float rSlime = viewHash(roomId + float2(31.7, 4.9));

    // 보통 방 4종(왼쪽 2 × 2) 또는 _SlimeRooms 비율만큼 슬라임 방 2종(위 줄 오른쪽 두 칸).
    float kind = min(floor(rKind * 4.0), 3.0);
    float2 roomCell = float2(kind - 2.0 * floor(kind * 0.5), floor(kind * 0.5));
    float2 slimeCell = float2(2.0 + step(0.5, rKind), 1.0);
    float2 cell = (rSlime < _SlimeRooms) ? slimeCell : roomCell;
    float2 qRoom = float2(lerp(q.x, -q.x, step(0.5, rFlip)), q.y);
    half3 albedo = SG_TEX_LOD(_RoomAtlas, viewAtlasUV(cell, qRoom), lod).rgb;

    // 불빛: 천장 가운데 등에 가까울수록 밝다. 방마다 고정 난수와 비율을 비교해 비율이 오르면 새 방만 켜진다.
    half lit = step(rLight, _LightsOn) * step(0.001, _LightsOn);
    float3 roomPos = float3(hit.x * 0.5, hit.y * 0.5 * cellAspect, hit.z * _RoomDepth);
    float3 lampPos = float3(0.0, 0.5 * cellAspect, 0.5 * _RoomDepth);
    float3 toLamp = roomPos - lampPos;
    half glow = 1.0 / (1.0 + 8.0 * dot(toLamp, toLamp));
    half fixture = step(0.999, hit.y) * (1.0 - smoothstep(0.08, 0.11, length(float2(roomPos.x, roomPos.z - lampPos.z))));
    half dayLight = 0.6 * outside;
    half lamp = lit * lerp(0.3, 1.0, night);
    half3 lampColor = viewRoomLightColor(roomId) * lamp;
    half3 room = albedo * (dayLight + lampColor * (0.2 + 2.4 * glow)) + lampColor * fixture * 1.5;

    // 커튼: 창 평면에 붙은 반투명 주름 천. 켜진 방의 커튼은 뒤에서 등빛을 받아 비친다.
    half hasCurtain = step(rCurtain, _CurtainAmount) * step(0.001, _CurtainAmount);
    float curtainW = lerp(0.14, 0.3, frac(rCurtainLook * 7.0));
    half panel = max(1.0 - smoothstep(curtainW - 0.02, curtainW, glassUV.x), smoothstep(1.0 - curtainW, 1.0 - curtainW + 0.02, glassUV.x));
    half folds = 0.78 + 0.22 * sin(glassUV.x * 48.0);
    half3 curtainColor = lerp(half3(0.62, 0.48, 0.32), half3(0.25, 0.33, 0.45), step(0.55, rCurtainLook));
    half3 curtain = curtainColor * folds * (dayLight * 1.2 + lampColor * 0.9);
    return lerp(room, curtain, panel * hasCurtain * 0.85);
}

#endif
