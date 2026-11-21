// 창밖 풍경의 작은 도구들: 격자 난수, 2D 모양 거리 함수, 차량과 사람 그리기. core.hlsl 이 포함한다.
// 위치·거리 계산은 float(미터, 시간), 색 계산은 half(Quest 에서 빠른 낮은 정밀도)로 한다.
#ifndef VIEW_SHAPES_INCLUDED
#define VIEW_SHAPES_INCLUDED

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
half viewCover(float d, float aa)
{
    return (half)(1.0 - smoothstep(-aa, aa, d));
}

// 차량 한 대를 col 위에 그린다. p 는 차 앞쪽이 +x 인 지면 기준 위치(m), alpha 는 이 차가 있는지(0 이면 그리지 않는다).
half3 viewCar(half3 col, float2 p, float aa, float seed, half alpha, half ambient, half night)
{
    half3 paint = half3(0.25, 0.25, 0.25) + 0.7 * half3(viewHash(float2(seed, 1.1)), viewHash(float2(seed, 2.2)), viewHash(float2(seed, 3.3)));
    float body = viewBox(p - float2(0.0, 0.65), float2(1.88, 0.23)) - 0.12;
    float cabin = viewBox(p - float2(-0.15, 1.2), float2(0.85, 0.13)) - 0.15;
    float glassD = viewBox(p - float2(-0.15, 1.2), float2(0.7, 0.07)) - 0.1;
    float wheelA = length(p - float2(1.3, 0.35)) - 0.35;
    float wheelB = length(p - float2(-1.3, 0.35)) - 0.35;
    half shade = 0.65 + 0.35 * smoothstep(0.2, 1.5, p.y);

    col = lerp(col, half3(0.03, 0.03, 0.035) * ambient, viewCover(min(wheelA, wheelB), aa) * alpha);
    col = lerp(col, paint * shade * ambient, viewCover(min(body, cabin), aa) * alpha);
    col = lerp(col, half3(0.12, 0.16, 0.2) * ambient + half3(0.12, 0.14, 0.16) * (1.0 - night), viewCover(glassD, aa) * alpha);

    // 밤에는 앞 등(노랑 하양)과 뒷 등(빨강)이 켜지고 번진다.
    float head = length(p - float2(1.95, 0.72));
    float tail = length(p - float2(-1.95, 0.72));
    half on = night * alpha;
    col += half3(1.0, 0.92, 0.7) * on * (viewCover(head - 0.1, aa) + 0.5 * exp(-head * head * 18.0));
    col += half3(1.0, 0.1, 0.05) * on * (viewCover(tail - 0.07, aa) + 0.35 * exp(-tail * tail * 22.0));
    return col;
}

// 사람 한 명을 col 위에 그린다. p 는 발 기준 위치(m), phase 는 걸음 위상(다리가 벌어지는 정도).
half3 viewPerson(half3 col, float2 p, float aa, float seed, float phase, half ambient)
{
    float swing = sin(phase) * 0.2;
    half3 shirt = half3(0.2, 0.2, 0.2) + 0.7 * half3(viewHash(float2(seed, 4.4)), viewHash(float2(seed, 5.5)), viewHash(float2(seed, 6.6)));
    float legA = viewCapsule(p, float2(0.0, 0.85), float2(swing, 0.07), 0.065);
    float legB = viewCapsule(p, float2(0.0, 0.85), float2(-swing, 0.07), 0.065);
    float torso = viewCapsule(p, float2(0.0, 0.92), float2(0.0, 1.3), 0.17);
    float head = length(p - float2(0.0, 1.6)) - 0.12;
    col = lerp(col, half3(0.1, 0.11, 0.17) * ambient, viewCover(min(legA, legB), aa));
    col = lerp(col, shirt * ambient, viewCover(torso, aa));
    col = lerp(col, half3(0.85, 0.66, 0.52) * ambient, viewCover(head, aa));
    return col;
}

#endif
