Shader "BrokenSovereign/Field/RoadOverlay"
{
    // 가도·마을 길 덮개 (2026-10-20). 실사 지형 패키지 머티리얼은 텍스처 칠하기를 쓰지 않아 길이 안 보이므로,
    // 길 위에 지형을 따라 얹은 메시에 흙길·돌바닥 텍스처를 그립니다.
    // 정점 색: R = 흙길 비율, G = 돌바닥 비율 (지형 칠하기 값). 가장자리는 노이즈로 흐트러지게 반투명.
    // 텍스처는 월드 XZ 좌표로 반복 (메시 UV 불필요). 주 광원·그림자·환경광·안개 적용.
    Properties
    {
        _DirtTex ("Dirt", 2D) = "white" {}
        _StoneTex ("Stone", 2D) = "white" {}
        _DirtTile ("Dirt Tile (m)", Float) = 5
        _StoneTile ("Stone Tile (m)", Float) = 6
        _DirtTint ("Dirt Tint", Color) = (1, 1, 1, 1)
        _StoneTint ("Stone Tint", Color) = (1, 1, 1, 1)
        _EdgeDarken ("Edge Darken", Range(0, 1)) = 0.3
        _EdgeNoise ("Edge Noise Scale", Float) = 0.35
        _DirtAmount ("Dirt Amount", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RoadOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DirtTex);
            SAMPLER(sampler_DirtTex);
            TEXTURE2D(_StoneTex);
            SAMPLER(sampler_StoneTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DirtTex_ST;
                float4 _StoneTex_ST;
                float _DirtTile;
                float _StoneTile;
                half4 _DirtTint;
                half4 _StoneTint;
                float _EdgeDarken;
                float _EdgeNoise;
                half _DirtAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float fogFactor : TEXCOORD3;
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 xz = input.positionWS.xz;
                half dirt = input.color.r * _DirtAmount;   // 0 = 흙길은 지형 레이어가 표시 (지면 실사화)
                half stone = input.color.g;
                half weight = saturate(dirt + stone);

                half3 dirtColor = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, xz / _DirtTile).rgb * _DirtTint.rgb;
                half3 stoneColor = SAMPLE_TEXTURE2D(_StoneTex, sampler_StoneTex, xz / _StoneTile).rgb * _StoneTint.rgb;
                half3 albedo = lerp(dirtColor, stoneColor, stone / max(weight, 0.001));

                // 가장자리: 노이즈로 들쭉날쭉하게 + 약간 어둡게 (풀에 묻힌 길 가장자리)
                float noise = ValueNoise(xz * _EdgeNoise) * 0.6 + ValueNoise(xz * _EdgeNoise * 3.1) * 0.4;
                half alpha = smoothstep(0.15 + noise * 0.35, 0.55 + noise * 0.35, weight);
                albedo *= lerp(1.0 - _EdgeDarken, 1.0, smoothstep(0.3, 0.9, weight));

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half3 color = albedo * (mainLight.color * ndl * mainLight.shadowAttenuation + SampleSH(normalWS));
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
