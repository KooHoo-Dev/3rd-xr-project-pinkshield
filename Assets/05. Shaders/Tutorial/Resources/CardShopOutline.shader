// 튜토리얼 대상 외곽선 (FIX C2): 뒤집은 껍데기(inverted hull). 물체를 법선 방향으로 _Width(m)만큼 부풀려 뒷면만 그림.
// 화면 후처리 없이 그리기 1번만 추가 (Quest 부담 낮음). Resources 폴더에 있어야 빌드에 포함됨 (OutlineMarker가 Resources.Load).
// 11-20: 카드처럼 아주 얇은 물건은 법선으로 부풀려도 테가 안 보여서, 메시 가운데(_Center)에서 _Grow 비율만큼 키우는 방식도 씀 (InteractableOutline).
Shader "CardShop/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.55, 0.75, 1)
        _Width ("Width (m)", Float) = 0.008
        _Grow ("Grow from center (ratio)", Float) = 0
        _Center ("Mesh center (object space)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+1" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
                float _Grow;
                float4 _Center;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionOS = _Center.xyz + (v.positionOS.xyz - _Center.xyz) * (1.0 + _Grow); // 얇은 물건: 가운데에서 키움
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                o.positionCS = TransformWorldToHClip(positionWS + normalWS * _Width); // 월드 기준 두께라 물체 크기와 상관없음
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return _Color;
            }
            ENDHLSL
        }
    }
}
