Shader "BrokenSovereign/Field/RiverWater"
{
    // 계곡 자갈 강의 얕은 물 (GDD 13장 필드, 2026-10-19).
    // 강 중심선을 따라 만든 띠 메시(uv.x = 하류 방향 거리/10, uv.y = 왼쪽 0 ~ 오른쪽 1)에 그립니다.
    // 월드 좌표 노이즈로 물줄기가 갈라졌다 합쳐지는 갈래(모래톱 사이로 자갈이 비침)를 만들고,
    // 하류 방향으로 흐르는 잔물결과 반짝임을 더합니다. 반투명 (뒤의 자갈 지형이 비침), 안개 적용.
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.62, 0.82, 0.84, 1)
        _DeepColor ("Deep Color", Color) = (0.33, 0.58, 0.68, 1)
        _FoamColor ("Ripple Color", Color) = (0.95, 0.98, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.78
        _BraidScale ("Braid Scale", Float) = 0.045
        _BraidAmount ("Braid Amount", Range(0, 1)) = 0.55
        _RippleScale ("Ripple Scale", Float) = 0.6
        _FlowSpeed ("Flow Speed", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RiverWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _Opacity;
                float _BraidScale;
                float _BraidAmount;
                float _RippleScale;
                float _FlowSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                for (int o = 0; o < 3; o++)
                {
                    sum += ValueNoise(p) * amp;
                    p = p * 2.03 + float2(17.1, 9.7);
                    amp *= 0.5;
                }
                return sum / 0.875;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float across = abs(input.uv.y - 0.5) * 2.0;           // 0 = 중심, 1 = 가장자리

                // 갈래: 중심은 항상 물, 가장자리로 갈수록 노이즈가 높은 곳만 물
                float braid = Fbm(input.positionWS.xz * _BraidScale);
                float channel = smoothstep(across - 0.15, across + 0.15, lerp(1.0, braid * 1.4, _BraidAmount));
                float edge = 1.0 - smoothstep(0.75, 1.0, across);
                float coverage = saturate(channel) * edge;

                // 하류로 흐르는 잔물결 (uv.x 방향)
                float2 flow = float2(input.uv.x * 10.0 - t * _FlowSpeed * 4.0, input.uv.y * 3.0);
                float ripple = Fbm(flow * _RippleScale + float2(0, t * 0.2));
                float sparkle = smoothstep(0.72, 0.9, ripple) * 0.6;

                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate((1.0 - across) * 0.8 + ripple * 0.3 - 0.2));
                color = lerp(color, _FoamColor.rgb, sparkle);
                float alpha = _Opacity * coverage * (0.75 + 0.25 * ripple);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
