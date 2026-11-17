// 원본: 3rd-study-shader-pinkshield (02. MyShaders/interior-room-mapping_Window_View) 에서 이식. 집 창밖 밤 풍경에 사용 (가게_집_인테리어_설계 6장).
// 창밖 풍경: 창 Quad 하나를 통해 건물 안에서 내다본 거리를 그린다. 맞은편 건물(창마다 입체 방), 도로, 오가는 차량과 사람을 시선으로 계산한다 (URP 17, HLSL).
// 효과 본체는 core.hlsl 에 있고, 이 파일은 Unity 전용 부분(패스, SRP Batcher)만 맡는다. 창은 불투명한 그림이라 조명과 그림자를 쓰지 않는다.
Shader "CardShop/WindowView"
{
    Properties
    {
        // 아틀라스 칸을 셰이더가 직접 고르므로 _RoomAtlas_ST(Tiling, Offset)는 쓰지 않는다. Inspector 에서 칸을 숨긴다.
        [NoScaleOffset] _RoomAtlas ("맞은편 건물 방 그림 아틀라스", 2D) = "white" {}
        _WindowsX ("맞은편 건물 한 동의 가로 창 수", Range(2, 8)) = 4
        _WindowsY ("맞은편 건물 가장 높은 동의 층수", Range(2, 20)) = 8
        _RoomDepth ("맞은편 방 깊이 (칸 폭 배수)", Range(0.2, 2.5)) = 1
        _FrameWidth ("맞은편 창틀 두께", Range(0.04, 0.8)) = 0.3
        _WallColor ("맞은편 외벽 색", Color) = (0.7216, 0.6431, 0.549, 1)
        _GlassReflect ("유리 반사", Range(0, 1)) = 0.5
        _Night ("밤", Range(0, 1)) = 0.35
        _LightsOn ("불 켜진 비율", Range(0, 1)) = 0.5
        _LightColor ("조명 색", Color) = (1, 0.7843, 0.4706, 1)
        _CurtainAmount ("커튼 비율", Range(0, 1)) = 0.35
        _BuildingDistance ("맞은편 건물까지 거리 (m)", Range(8, 40)) = 18
        _BuildingWidth ("건물 한 동의 폭 (m)", Range(8, 30)) = 16
        _EyeHeight ("창 중심의 길 위 높이 (m)", Range(1, 15)) = 4
        _CarPeriod ("차량이 지나가는 주기 (초)", Range(1, 15)) = 6
        _CarSpeed ("차량 속도 (m/초)", Range(2, 20)) = 7
        _TrafficRange ("차량 오가는 범위 (창 중심 좌우 m)", Range(10, 60)) = 26
        _WalkSpeed ("사람 걷는 속도 (m/초)", Range(0.2, 3)) = 1.2
        _ActorScale ("차량과 사람 크기 배율", Range(0.5, 3)) = 1.3
        _PaneFrame ("창틀 두께 (창 크기 대비)", Range(0, 0.2)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        // 어느 쪽에서 봐도 창 그림이 보이도록 양면을 그린다. 뒷면에서는 법선을 뒤집어 같은 그림을 보여 준다.
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // 텍스처 선언은 CBUFFER 밖에 둔다.
        TEXTURE2D(_RoomAtlas);
        SAMPLER(sampler_RoomAtlas);

        // SRP Batcher 호환: 머티리얼 값은 모두 이 버퍼 안에 둔다.
        CBUFFER_START(UnityPerMaterial)
            float4 _RoomAtlas_TexelSize;
            float _WindowsX;
            float _WindowsY;
            float _RoomDepth;
            float _FrameWidth;
            float4 _WallColor;
            float _GlassReflect;
            float _Night;
            float _LightsOn;
            float4 _LightColor;
            float _CurtainAmount;
            float _BuildingDistance;
            float _BuildingWidth;
            float _EyeHeight;
            float _CarPeriod;
            float _CarSpeed;
            float _TrafficRange;
            float _WalkSpeed;
            float _ActorScale;
            float _PaneFrame;
        CBUFFER_END

        // core.hlsl 안에서 텍스처를 읽는 매크로.
        #define SG_TEX(name, uv) SAMPLE_TEXTURE2D(name, sampler##name, uv)

        // core.hlsl 이 받는 입력. 창은 조명을 받지 않으므로 광원 값은 없고, 창 중심(centerWS)이 있다.
        struct SgInput
        {
            float2 uv;
            float3 positionWS;
            float3 normalWS;
            float3 viewWS;
            float3 centerWS;
            float time;
        };

        #include "core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes v)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.uv = v.uv;
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            // Forward 와 Deferred 렌더 경로 모두에서 그려지려면 UniversalForwardOnly 여야 한다.
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            float4 frag(Varyings v) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);

                SgInput i;
                i.uv = v.uv;
                i.positionWS = v.positionWS;
                i.viewWS = GetWorldSpaceNormalizeViewDir(v.positionWS);
                // 보간된 법선은 길이가 1 보다 짧아지므로 다시 정규화하고, 보는 쪽을 향하게 맞춘다 (뒷면에서도 같은 그림).
                float3 n = normalize(v.normalWS);
                i.normalWS = (dot(n, i.viewWS) < 0.0) ? -n : n;
                i.centerWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                i.time = _Time.y;
                return sgEffect(i);
            }
            ENDHLSL
        }

        // 깊이 텍스처를 쓰는 다른 효과와 맞물릴 때 필요하다. 창은 불투명하고 그림자를 드리우지 않는다 (ShadowCaster 패스 없음).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            float4 fragDepth(Varyings v) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                return v.positionCS.z;
            }
            ENDHLSL
        }

        // SSAO 처럼 노멀을 쓰는 렌더러 기능과 맞물릴 때 필요하다.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragNormals
            #pragma multi_compile_instancing
            // URP Unlit 의 Forward 전용 노멀 패스와 같은 키워드. Deferred 의 정확한 G-buffer 노멀과 렌더링 레이어를 켜면 쓰인다.
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            void fragNormals(Varyings v, out float4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float3 normalWS = normalize(v.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    outNormalWS = float4(PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5)), 0.0);
                #else
                    outNormalWS = float4(normalWS, 0.0);
                #endif
                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }
}
