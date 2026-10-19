Shader "BrokenSovereign/UI/ResourceOrb"
{
    // HUD 자원 구슬 (입체 표현): 구 음영, 위에서 내려다본 타원형 수면, 수면 가장자리 띠,
    // 빈 유리 부분, 유리 반사·테두리. 수면은 항상 수평이며, 값이 바뀌면 수면 높이가 잠깐 위아래로 흔들림(_Bounce).
    // 텍스처 없이 UV만으로 계산합니다. uGUI Image에 머티리얼로 적용합니다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Fill ("Fill", Range(0, 1)) = 1
        _LiquidColor ("Liquid Color", Color) = (0.75, 0.08, 0.08, 1)
        _SurfaceColor ("Surface Color", Color) = (1, 0.55, 0.45, 1)
        _EmptyColor ("Empty Color", Color) = (0.05, 0.04, 0.06, 0.92)
        _RimColor ("Rim Color", Color) = (0.78, 0.6, 0.28, 1)
        _RimWidth ("Rim Width", Range(0.005, 0.15)) = 0.03
        _ViewTilt ("View Tilt", Range(0.05, 0.8)) = 0.3
        _Bounce ("Bounce", Range(0, 1)) = 0
        _Locked ("Locked", Range(0, 1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

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
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _Fill;
            fixed4 _LiquidColor;
            fixed4 _SurfaceColor;
            fixed4 _EmptyColor;
            fixed4 _RimColor;
            float _RimWidth;
            float _ViewTilt;
            float _Bounce;
            float _Locked;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float aa = fwidth(r) * 1.5;
                float circle = 1.0 - smoothstep(1.0 - aa, 1.0, r);

                // 구 표면 법선과 조명 (왼쪽 위에서 비추는 빛)
                float z = sqrt(saturate(1.0 - r * r));
                float3 n = normalize(float3(p, z + 0.0001));
                float3 lightDir = normalize(float3(-0.45, 0.6, 0.65));
                float diffuse = saturate(dot(n, lightDir)) * 0.6 + 0.4;

                // 구를 살짝 위에서 내려다본 시점: 화면 좌표 (x, y, 앞쪽 z)의 실제 높이 = y*cos + z*sin
                // 시선이 구 앞면에서 뒷면으로 지나가는 동안 높이가 수면(h)보다 낮아지는 지점을 판정 → 수면은 항상 수평
                float hasLiquid = step(0.001, _Fill);
                float tiltSin = _ViewTilt;
                float tiltCos = sqrt(1.0 - tiltSin * tiltSin);
                float h = (_Fill * 2.0 - 1.0) + _Bounce * sin(_Time.y * 9.0) * 0.04;

                float frontHeight = p.y * tiltCos + z * tiltSin;
                float backHeight = p.y * tiltCos - z * tiltSin;
                float edge = fwidth(frontHeight) * 1.5 + 0.0005;

                // 앞쪽 유리 면이 액체 안: 액체 몸통
                float bodyMask = (1.0 - smoothstep(h - edge, h + edge, frontHeight)) * hasLiquid;
                // 앞면은 액체 위, 뒷면은 액체 안: 시선이 수면을 통과 → 수면이 보임
                float surfaceMask = (1.0 - smoothstep(h - edge, h + edge, backHeight)) * hasLiquid * (1.0 - bodyMask);

                // 액체 몸통: 구 음영 + 수면에서 멀수록 진해짐 + 가장자리 어둡게
                float depth = saturate((h - frontHeight) * 0.5);
                fixed3 body = _LiquidColor.rgb * diffuse * lerp(1.15, 0.6, depth);
                body *= lerp(1.0, 0.55, smoothstep(0.6, 1.0, r));

                // 수면: 시선이 수면과 만나는 지점(깊이 zHit)으로 앞·뒤와 가장자리 판정
                float zHit = (h - p.y * tiltCos) / max(tiltSin, 0.001);
                float sectionRadiusSq = max(1.0 - h * h, 0.0001);
                float hitRadiusSq = p.x * p.x + p.y * p.y + zHit * zHit - h * h;
                float rimRatio = sqrt(saturate(hitRadiusSq / sectionRadiusSq));
                float back = saturate(-zHit / sqrt(sectionRadiusSq) * 0.5 + 0.5);
                fixed3 surface = _LiquidColor.rgb * lerp(0.7, 1.05, back);
                surface = lerp(surface, _SurfaceColor.rgb, smoothstep(0.88, 1.0, rimRatio) * 0.75);

                // 빈 유리 부분
                fixed3 empty = _EmptyColor.rgb * (0.6 + 0.4 * diffuse);

                fixed3 col = empty;
                col = lerp(col, body, bodyMask);
                col = lerp(col, surface, surfaceMask);
                float alpha = lerp(_EmptyColor.a, 1.0, saturate(bodyMask + surfaceMask));

                // 잠김: 채도 제거 + 어둡게
                float gray = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, gray.xxx * 0.45, _Locked);

                // 유리 반사: 날카로운 하이라이트 + 넓은 광택 + 가장자리 프레넬
                float3 halfDir = normalize(lightDir + float3(0.0, 0.0, 1.0));
                float nh = saturate(dot(n, halfDir));
                col += pow(nh, 48.0) * 0.8 + pow(nh, 6.0) * 0.1;
                col += pow(1.0 - z, 3.0) * 0.12;

                // 금속 테두리
                float rim = smoothstep(1.0 - _RimWidth - 0.015, 1.0 - _RimWidth, r);
                col = lerp(col, _RimColor.rgb * lerp(1.0, 0.5, _Locked) * (0.7 + 0.5 * diffuse), rim);
                alpha = lerp(alpha, 1.0, rim);

                fixed4 result = fixed4(col, alpha * circle);
                result *= i.color;
                return result;
            }
            ENDCG
        }
    }
}
