Shader "BrokenSovereign/Character/EliteAura"
{
    // 정예 적 금빛 오라 (GDD: 정예는 기존 적 강화 변형, 크기·외형으로 구분).
    // 본 머티리얼 뒤에 두 번째 머티리얼로 붙여 같은 메시를 한 번 더 그립니다.
    // 정점을 법선 방향으로 조금 부풀리고, 가장자리(프레넬)만 가산 합성으로 빛나게 해 몸 둘레에 빛이 감싸는 느낌을 줍니다.
    // 스킨드 메시는 스키닝 후 정점이 들어오므로 애니메이션을 그대로 따라갑니다.
    Properties
    {
        [HDR] _AuraColor ("Aura Color", Color) = (1.6, 1.1, 0.35, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _Extrude ("Extrude (m)", Range(0, 0.1)) = 0.025
        _PulseSpeed ("Pulse Speed", Float) = 2.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "EliteAura"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _AuraColor;
                half _RimPower;
                half _Extrude;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * _Extrude;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(input.viewDirWS);
                half rim = pow(1.0h - saturate(dot(normal, view)), _RimPower);
                half pulse = 1.0h - _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseSpeed));
                return half4(_AuraColor.rgb * rim * pulse, 1.0h);
            }
            ENDHLSL
        }
    }
}
