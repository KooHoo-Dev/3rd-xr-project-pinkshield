// 카드 앞면: 프레임 + 배경 + 캐릭터 레이어를 한 패스에서 합성합니다.
// - 패럴랙스: 시선 방향에 따라 배경/캐릭터 레이어를 서로 다른 깊이만큼 밀어 입체감을 냅니다.
// - 등급 효과: 프레임 반짝임, 일러스트 홀로, 스크롤 패턴, 윤곽광, 글로우 (CardRarityStyles에서 조정).
// 앞면은 오브젝트 공간 -Z를 바라봅니다 (CardMesh 기준). Quest 싱글패스 스테레오를 지원합니다.
Shader "CardShop/CardFront"
{
    Properties
    {
        _CharTex ("Character", 2D) = "black" {}
        _BgTex ("Background", 2D) = "white" {}
        _HasChar ("Has Character", Float) = 0
        _HasBg ("Has Background", Float) = 0
        _BgAspect ("Background Aspect (w/h)", Float) = 1.3333

        _FrameColor ("Frame Color", Color) = (0.78, 0.78, 0.80, 1)
        _AccentColor ("Accent Color", Color) = (0.6, 0.6, 0.65, 1)
        _ElementColor ("Element Color", Color) = (1, 0.55, 0.25, 1)
        _PanelColor ("Panel Color", Color) = (0.98, 0.96, 0.92, 1)

        _FullArt ("Full Art", Float) = 0
        _PopOut ("Pop Out", Float) = 0
        _CharScale ("Character Scale", Float) = 1
        _CharOffsetY ("Character Offset Y", Float) = 0

        _FrameSparkle ("Frame Sparkle", Range(0, 1)) = 0
        _ArtHolo ("Art Holo", Range(0, 1)) = 0
        _Scroll ("Scroll Pattern", Range(0, 1)) = 0
        _Rim ("Rim", Range(0, 1)) = 0
        _Glow ("Glow", Range(0, 2)) = 0

        _MarkCount ("Rarity Mark Count", Float) = 1
        _MarkColor ("Rarity Mark Color", Color) = (0.66, 0.68, 0.72, 1)
        _MarkRainbow ("Rarity Mark Rainbow", Float) = 0
        _MarkSparkle ("Rarity Mark Sparkle", Float) = 0

        _IconTex ("Element Icons (horizontal atlas)", 2D) = "black" {}
        _IconIndex ("Element Icon Index", Float) = 0
        _IconCount ("Element Icon Count", Float) = 7
        _HasIcon ("Has Icon", Float) = 0

        _CardSize ("Card Size (m)", Vector) = (0.063, 0.088, 0, 0)
        _Border ("Border (fraction of width)", Float) = 0.045
        _ArtRect ("Art Window (xMin, yMin, xMax, yMax)", Vector) = (0.075, 0.42, 0.925, 0.875)
        _BgDepth ("Background Depth (m)", Float) = 0.008
        _CharDepth ("Character Depth (m)", Float) = 0.003
        _Smoothness ("Smoothness", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _CharTex_ST;
            float4 _BgTex_ST;
            float _HasChar;
            float _HasBg;
            float _BgAspect;
            half4 _FrameColor;
            half4 _AccentColor;
            half4 _ElementColor;
            half4 _PanelColor;
            float _FullArt;
            float _PopOut;
            float _CharScale;
            float _CharOffsetY;
            half _FrameSparkle;
            half _ArtHolo;
            half _Scroll;
            half _Rim;
            half _Glow;
            float _MarkCount;
            half4 _MarkColor;
            float _MarkRainbow;
            float _MarkSparkle;
            float4 _IconTex_ST;
            float _IconIndex;
            float _IconCount;
            float _HasIcon;
            float4 _CardSize;
            float _Border;
            float4 _ArtRect;
            float _BgDepth;
            float _CharDepth;
            half _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CharTex); SAMPLER(sampler_CharTex);
            TEXTURE2D(_BgTex);   SAMPLER(sampler_BgTex);
            TEXTURE2D(_IconTex); SAMPLER(sampler_IconTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 viewTS     : TEXCOORD3; // 카드 면 기준 시선 (x=u, y=v, z=면 바깥)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                float3 viewOS = TransformWorldToObjectDir(GetWorldSpaceViewDir(o.positionWS), false);
                o.viewTS = float3(viewOS.x, viewOS.y, -viewOS.z);
                return o;
            }

            float RectMask(float2 p, float4 r, float2 aa)
            {
                float2 lo = smoothstep(r.xy - aa, r.xy + aa, p);
                float2 hi = 1.0 - smoothstep(r.zw - aa, r.zw + aa, p);
                return lo.x * lo.y * hi.x * hi.y;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half3 Rainbow(float h)
            {
                return saturate(abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
            }

            // 등급 마크 하나: 슬라임 돔(반원) 모양. q = 바닥 가운데 기준, 폭 반·높이로 나눈 좌표.
            float DomeMask(float2 pM, float2 baseM, float2 halfSizeM, float aaM)
            {
                float2 q = (pM - baseM) / halfSizeM;
                float d = (length(q) - 1.0) * halfSizeM.y;          // 둥근 윗부분까지 거리 (m)
                float body = 1.0 - smoothstep(-aaM, aaM, d);
                float bottom = smoothstep(-aaM, aaM, (pM.y - baseM.y));
                return body * bottom;
            }

            // 네 갈래 반짝임
            float SparkMask(float2 pM, float2 cM, float rM, float aaM)
            {
                float2 a = abs(pM - cM) / rM;
                float s = sqrt(a.x) + sqrt(a.y);
                return 1.0 - smoothstep(1.0 - aaM / rM * 3.0, 1.0 + aaM / rM * 3.0, s);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 uv = i.uv;
                float2 size = _CardSize.xy;
                float2 aa = fwidth(uv) * 0.75;
                float3 V = normalize(i.viewTS);
                // 깊이 1m당 카드 표면에서 밀리는 거리 (m). 비스듬한 각도는 제한.
                float2 par = -V.xy / max(V.z, 0.3);

                // ---- 영역
                float2 b = float2(_Border, _Border * size.x / size.y);
                float4 innerR = float4(b, 1.0 - b);
                float inner = RectMask(uv, innerR, aa);
                float frame = 1.0 - inner;
                float4 artR = _FullArt > 0.5 ? innerR : _ArtRect;
                float art = RectMask(uv, artR, aa);

                float2 winMin = artR.xy * size;
                float2 winSize = (artR.zw - artR.xy) * size;
                float2 pM = uv * size; // 카드 위 위치 (m)

                // ---- 배경 레이어 (cover 맞춤 + 패럴랙스 여유 확대)
                float2 bgP = (pM + par * _BgDepth - winMin) / winSize;
                float winAspect = winSize.x / winSize.y;
                float2 bgUV = bgP - 0.5;
                if (_BgAspect > winAspect) bgUV.x *= winAspect / _BgAspect;
                else bgUV.y *= _BgAspect / winAspect;
                bgUV = bgUV / 1.08 + 0.5;
                half3 bgCol;
                if (_HasBg > 0.5)
                {
                    bgCol = SAMPLE_TEXTURE2D(_BgTex, sampler_BgTex, bgUV).rgb;
                }
                else
                {
                    half3 light = lerp(_ElementColor.rgb, 1.0, 0.65);
                    half3 deep = lerp(_ElementColor.rgb, 1.0, 0.2);
                    float2 c = bgP - float2(0.5, 0.55);
                    bgCol = lerp(deep, light, saturate(bgP.y * 0.8 + 0.25));
                    bgCol = lerp(bgCol, 1.0, 0.25 * saturate(1.0 - length(c) * 1.6));
                }

                // ---- 캐릭터 레이어
                float charSizeM = _CharScale * min(winSize.x, winSize.y);
                float2 charCenter = winMin + winSize * 0.5 + float2(0.0, _CharOffsetY * winSize.y);
                float2 charUV = (pM + par * _CharDepth - charCenter) / charSizeM + 0.5;
                half4 ch = SAMPLE_TEXTURE2D(_CharTex, sampler_CharTex, charUV);
                float inSquare = RectMask(charUV, float4(0, 0, 1, 1), fwidth(charUV));
                float charA = ch.a * inSquare * _HasChar * (_PopOut > 0.5 ? 1.0 : art);

                // ---- 기본 합성
                half3 col = _FrameColor.rgb;
                // 프레임 안쪽 액센트 라인
                float4 lineR = float4(innerR.xy - b * 0.25, innerR.zw + b * 0.25);
                float innerLine = RectMask(uv, lineR, aa) - inner;
                col = lerp(col, _AccentColor.rgb, saturate(innerLine) * 0.8);
                // 안쪽 판: 위는 속성 색을 옅게, 아래로 갈수록 종이색 (카드 캔버스 v2)
                half3 elemLight = lerp(_ElementColor.rgb, 1.0, 0.78);
                half3 elemDeep = _ElementColor.rgb * 0.82;
                half3 panel = lerp(_PanelColor.rgb, elemLight, smoothstep(0.3, 1.0, uv.y));
                col = lerp(col, panel, inner);
                // 일러스트 창 테두리 (창 모드): 속성 색
                float4 winLineR = float4(artR.xy - 0.006, artR.zw + 0.006);
                float winLine = (RectMask(uv, winLineR, aa) - art) * (1.0 - _FullArt);
                col = lerp(col, elemDeep, saturate(winLine));
                // 정보 띠 (창 아래, 가운데가 진하고 양끝이 흐려짐)
                float strip = RectMask(uv, float4(0.09, 0.362, 0.91, 0.402), aa) * (1.0 - _FullArt);
                strip *= 1.0 - pow(abs(uv.x - 0.5) / 0.41, 3.0);
                col = lerp(col, lerp(elemLight, _ElementColor.rgb, 0.18), saturate(strip));
                col = lerp(col, bgCol, art);

                // 풀아트: 텍스트 영역 가독성용 그늘
                // 아래 그늘은 정보 띠(0.36~0.40)와 설명을 덮도록 0.5~0.3에서 진해짐 (카드 캔버스 v2)
                float shade = (smoothstep(0.5, 0.3, uv.y) * 0.72 + smoothstep(0.84, 0.97, uv.y) * 0.5) * _FullArt * inner;

                // 배경에만 들어가는 홀로 (캐릭터 아래)
                float t = _Time.y;
                half3 rainbow = Rainbow(dot(uv, float2(0.6, 0.9)) * 1.2 + V.x * 0.9 + V.y * 0.6);
                float band = frac(dot(uv, float2(0.7, 1.0)) + V.x * 0.9 - V.y * 0.5);
                band = pow(saturate(1.0 - abs(band - 0.5) * 2.4), 2.0);
                half3 holo = rainbow * _ArtHolo * (0.05 + 0.28 * band) * art * (1.0 - shade);

                col = col * (1.0 - shade);
                col = lerp(col, ch.rgb, charA);

                // ---- 바닥 줄: 구분선 + 등급 마크 (가격 자리, 카드 캔버스 v2). 캐릭터보다 위에 그림.
                float aaM = max(fwidth(pM.x), 1e-5);
                float sepLine = RectMask(uv, float4(0.08, 0.116, 0.92, 0.119), aa);
                col = lerp(col, _FullArt > 0.5 ? half3(1, 1, 1) : elemDeep, sepLine * 0.35);

                float2 markHalf = float2(0.0016, 0.0024);           // 돔 하나: 폭 3.2mm, 높이 2.4mm
                float markStep = 0.0037;
                float2 markBase = float2(0.09, 0.072) * size + float2(markHalf.x, 0.0);
                float marks = 0.0;
                float markHi = 0.0;
                half3 markCol = _MarkColor.rgb;
                [unroll] for (int m = 0; m < 3; m++)
                {
                    float on = step((float)m + 0.5, _MarkCount);
                    float2 baseM = markBase + float2(markStep * m, 0.0);
                    marks = max(marks, DomeMask(pM, baseM, markHalf, aaM) * on);
                    // 돔 왼쪽 위 광택 (작은 흰 타원)
                    float2 hl = (pM - (baseM + float2(-markHalf.x * 0.35, markHalf.y * 0.62))) / float2(markHalf.x * 0.32, markHalf.y * 0.18);
                    markHi = max(markHi, (1.0 - smoothstep(0.7, 1.0, length(hl))) * 0.6 * on);
                }
                if (_MarkRainbow > 0.5)
                    markCol = Rainbow((pM.x - markBase.x) / (markHalf.x * 2.0) * 0.8 + 0.5) * 0.85 + 0.15;
                col = lerp(col, markCol, marks);
                col = lerp(col, 1.0, markHi * marks);
                // 반짝임 (유니크 · 레전더리): 마지막 마크 오른쪽 위
                float lastX = markBase.x + markStep * (max(_MarkCount, 1.0) - 1.0);
                float spark = SparkMask(pM, float2(lastX + markHalf.x + 0.0012, markBase.y + markHalf.y * 0.9), 0.0011, aaM) * _MarkSparkle;
                col = lerp(col, half3(1.0, 0.97, 0.8), spark);

                // ---- 속성 배지 (오른쪽 위 원 + 흰 아이콘). 아이콘은 가로로 늘어선 아틀라스에서 _IconIndex 칸을 씀.
                float2 badgeC = float2(0.875, 0.92) * size;
                float badgeD = length(pM - badgeC);
                float badge = 1.0 - smoothstep(0.0027 - aaM, 0.0027 + aaM, badgeD);
                float badgeRing = (1.0 - smoothstep(0.0032 - aaM, 0.0032 + aaM, badgeD)) - badge;
                col = lerp(col, half3(1, 1, 1), badgeRing);
                col = lerp(col, _ElementColor.rgb * 0.66, badge);   // 옅은 속성 색(무속성 분홍 등)에서도 흰 아이콘이 보이게 더 진하게
                float2 iconLocal = (pM - badgeC) / 0.0042 + 0.5;                 // 아이콘 칸: 지름 4.2mm
                float iconIn = step(0.0, iconLocal.x) * step(iconLocal.x, 1.0) * step(0.0, iconLocal.y) * step(iconLocal.y, 1.0);
                float2 iconUV = float2((_IconIndex + saturate(iconLocal.x)) / max(_IconCount, 1.0), saturate(iconLocal.y));
                float icon = SAMPLE_TEXTURE2D(_IconTex, sampler_IconTex, iconUV).a * iconIn * _HasIcon;
                col = lerp(col, half3(1, 1, 1), icon * badge);

                // ---- 조명 (가독성을 위해 기본 밝기를 깔고 빛 반응을 더함)
                float3 N = normalize(i.normalWS);
                float3 Vws = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(N, mainLight.direction));
                half3 ambient = SampleSH(N);
                half3 lit = col * (0.6 + 0.4 * (ambient + ndl * mainLight.color));
                float3 H = normalize(mainLight.direction + Vws);
                half spec = pow(saturate(dot(N, H)), exp2(10.0 * _Smoothness + 1.0)) * 0.35;
                lit += spec * mainLight.color;

                // ---- 등급 효과 (발광으로 더함)
                lit += holo * (1.0 - charA);

                // 반짝이: 셀마다 작은 둥근 점, 보는 각도에 따라 깜빡임
                float2 cellP = uv * float2(90.0, 126.0);
                float rnd = Hash21(floor(cellP));
                float dotMask = smoothstep(0.32, 0.05, length(frac(cellP) - 0.5));
                float twinkle = pow(saturate(sin(rnd * 60.0 + V.x * 18.0 + V.y * 14.0)), 16.0) * step(0.7, rnd) * dotMask;
                lit += (rainbow * 0.5 + 0.7) * twinkle * _FrameSparkle * frame * 1.4;
                lit = lerp(lit, lit * (0.7 + rainbow * 0.6), frame * _FrameSparkle * 0.35);

                float stripes = frac((uv.x + uv.y) * 12.0 + t * 0.2);
                stripes = smoothstep(0.42, 0.5, stripes) - smoothstep(0.5, 0.58, stripes);
                lit += rainbow * stripes * _Scroll * 0.12 * (art * (1.0 - charA) * (1.0 - shade) + frame);

                float edgeD = min(min(uv.x, 1.0 - uv.x) * size.x, min(uv.y, 1.0 - uv.y) * size.y);
                float edgeGlow = exp(-edgeD / 0.0025);
                float grazing = pow(1.0 - saturate(V.z), 2.0);
                lit += _AccentColor.rgb * _Rim * edgeGlow * (0.35 + 1.2 * grazing);

                float pulse = 0.75 + 0.25 * sin(t * 2.0);
                lit += _AccentColor.rgb * _Glow * (0.3 * edgeGlow + 0.12 * charA * pulse);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
