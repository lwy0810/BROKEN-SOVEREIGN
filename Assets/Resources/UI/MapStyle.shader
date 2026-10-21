// 지도 그림 표시용 UI 셰이더 (10/21): 맵 생성 때 그린 지도(밝은 길·어두운 지형)를
// 어두운 청회색 바탕 + 베이지 길 + 금빛 윤곽선 스타일로 바꿔 그립니다. 미니맵(Mask) 지원을 위해 UI 스텐실 포함.
Shader "BrokenSovereign/UI/MapStyle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Map", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Background ("Background", Color) = (0.2, 0.22, 0.24, 1)
        _Ground ("Ground", Color) = (0.74, 0.7, 0.6, 1)
        _Road ("Road", Color) = (0.88, 0.84, 0.72, 1)
        _Edge ("Edge", Color) = (0.86, 0.7, 0.36, 1)
        _Water ("Water", Color) = (0.22, 0.32, 0.38, 1)
        _Threshold ("Walkable Threshold", Range(0, 1)) = 0.28
        _RoadThreshold ("Road Threshold", Range(0, 1)) = 0.62
        _EdgeWidth ("Edge Width (texels)", Float) = 1.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 local : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color, _Background, _Ground, _Road, _Edge, _Water;
            float _Threshold, _RoadThreshold, _EdgeWidth;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.local = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float Luma(float2 uv)
            {
                return dot(tex2D(_MainTex, uv).rgb, float3(0.299, 0.587, 0.114));
            }

            float Walk(float2 uv)
            {
                return smoothstep(_Threshold - 0.03, _Threshold + 0.03, Luma(uv));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 source = tex2D(_MainTex, i.uv).rgb;
                float l = dot(source, float3(0.299, 0.587, 0.114));
                float walk = smoothstep(_Threshold - 0.03, _Threshold + 0.03, l);

                // 이웃과 걷기 가능 여부가 다르면 윤곽선
                float2 d = _MainTex_TexelSize.xy * _EdgeWidth;
                float around = Walk(i.uv + float2(d.x, 0)) + Walk(i.uv - float2(d.x, 0))
                    + Walk(i.uv + float2(0, d.y)) + Walk(i.uv - float2(0, d.y));
                float edge = saturate(abs(around * 0.25 - walk) * 3.0);

                // 바탕: 원래 지형 명암을 살짝 남김
                fixed3 background = _Background.rgb * (0.85 + 1.2 * saturate(l - 0.12));
                fixed3 ground = lerp(_Ground.rgb, _Road.rgb, smoothstep(_RoadThreshold - 0.05, _RoadThreshold + 0.05, l));
                ground *= 0.9 + 0.25 * saturate((l - _Threshold) * 2.0);
                float water = saturate((source.b - source.r) * 6.0);
                ground = lerp(ground, _Water.rgb, water);

                fixed3 color = lerp(background, ground, walk);
                color = lerp(color, _Edge.rgb, edge);
                fixed4 result = fixed4(color, 1) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
