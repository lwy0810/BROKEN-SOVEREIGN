Shader "BrokenSovereign/Field/CloudSea"
{
    // 부유 석판 아래 구름 바다 (GDD 13장 필드 구성).
    // 텍스처 없이 월드 좌표 fBm 노이즈를 흘려 구름 결을 만들고, 정점을 노이즈 높이만큼 올려 출렁임을 줍니다.
    // 멀어질수록 안개 색에 섞여 수평선이 하늘과 이어집니다 (RenderSettings 안개 사용).
    Properties
    {
        _ColorHigh ("Top Color", Color) = (1, 0.97, 0.93, 1)
        _ColorLow ("Shadow Color", Color) = (0.86, 0.68, 0.76, 1)
        _ColorGlow ("Glow Color", Color) = (1, 0.84, 0.55, 1)
        _Scale ("Noise Scale", Float) = 0.035
        _DetailScale ("Detail Scale", Float) = 0.12
        _Speed ("Scroll Speed (xy: base, zw: detail)", Vector) = (0.8, 0.3, -0.5, 0.6)
        _Contrast ("Contrast", Range(0.3, 4)) = 1.4
        _GlowAmount ("Glow Amount", Range(0, 1)) = 0.35
        _WaveHeight ("Wave Height", Float) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CloudSea"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorHigh;
                half4 _ColorLow;
                half4 _ColorGlow;
                float _Scale;
                float _DetailScale;
                float4 _Speed;
                float _Contrast;
                float _GlowAmount;
                float _WaveHeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float baseNoise : TEXCOORD1;
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
                for (int o = 0; o < 4; o++)
                {
                    sum += ValueNoise(p) * amp;
                    p = p * 2.03 + float2(17.1, 9.7);
                    amp *= 0.5;
                }
                return sum;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y;
                float n = Fbm(positionWS.xz * _Scale + _Speed.xy * t * _Scale);
                positionWS.y += (n - 0.5) * 2.0 * _WaveHeight;

                output.positionWS = positionWS;
                output.baseNoise = n;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float detail = Fbm(input.positionWS.xz * _DetailScale + _Speed.zw * t * _DetailScale);
                float n = saturate(input.baseNoise * 0.7 + detail * 0.45 - 0.08);
                n = pow(n, _Contrast);

                half3 color = lerp(_ColorLow.rgb, _ColorHigh.rgb, n);
                float glow = smoothstep(0.55, 0.95, detail) * _GlowAmount;
                color = lerp(color, _ColorGlow.rgb, glow);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
