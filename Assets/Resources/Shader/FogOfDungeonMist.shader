Shader "BagBack100/FogODungeonMist"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FogMaskTex ("Fog Mask (Auto)", 2D) = "white" {}
        _FogWorldBounds ("Fog World Bounds", Vector) = (0, 0, 100, 100)

        _UnexploredColor ("Unexplored Color", Color) = (0.03, 0.03, 0.05, 1)
        _ExploredColor ("Explored Mist Color", Color) = (0.12, 0.14, 0.20, 0.72)
        _MistHighlightColor ("Mist Cloud Tint", Color) = (0.28, 0.32, 0.42, 1)

        _NoiseScale ("Mist Scale (안개 입자 크기)", Float) = 0.35
        _NoiseSpeed ("Mist Drift Speed (흐르는 속도)", Float) = 0.15
        _MistStrength ("Mist Haziness (뿌연 정도)", Range(0, 1)) = 0.45
        _EdgeNoiseDistort ("Border Organic Distortion (경계선 일렁임)", Range(0, 0.3)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _FogMaskTex;
            float4 _FogWorldBounds;

            fixed4 _UnexploredColor;
            fixed4 _ExploredColor;
            fixed4 _MistHighlightColor;

            float _NoiseScale;
            float _NoiseSpeed;
            float _MistStrength;
            float _EdgeNoiseDistort;

            // 텍스처 에셋 없이 동작하는 2D 절차적 Value Noise & FBM
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash21(i + float2(0.0, 0.0));
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                v += 0.55 * valueNoise(p);
                v += 0.30 * valueNoise(p * 2.1 + float2(1.7, 9.2));
                v += 0.15 * valueNoise(p * 4.3 + float2(8.3, 2.8));
                return v;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 1. 서로 다른 방향으로 천천히 흐르는 2겹의 안개 노이즈 계산
                float t = _Time.y * _NoiseSpeed;
                float2 uvNoise1 = IN.worldPos * _NoiseScale + float2(t, t * 0.6);
                float2 uvNoise2 = IN.worldPos * (_NoiseScale * 1.6) - float2(t * 0.7, -t * 0.4);

                float mist1 = fbm(uvNoise1);
                float mist2 = fbm(uvNoise2);
                float combinedMist = (mist1 + mist2) * 0.5; // 0 ~ 1 사이의 구름 패턴

                // 2. 월드 좌표를 Bilinear 안개 마스크 UV로 변환 (경계선이 구름처럼 살짝 일렁이도록 오프셋)
                float2 noiseOffset = (float2(mist1, mist2) - 0.5) * _EdgeNoiseDistort;
                float2 maskUV = ((IN.worldPos + noiseOffset) - _FogWorldBounds.xy) / max(float2(0.001, 0.001), _FogWorldBounds.zw);

                fixed4 maskSample = tex2D(_FogMaskTex, saturate(maskUV));
                float rawAlpha = maskSample.r;       // 전체 안개 농도
                float unexploredRatio = maskSample.g; // 미탐험(1) vs 탐험완료(0) 비율

                // Smoothstep으로 타일 격자 느낌을 완전히 제거
                float smoothAlpha = smoothstep(0.02, 0.98, rawAlpha);

                // 3. 탐험 완료(반투명) 영역에 뿌연 구름 하이라이트와 농도 변화 추가
                fixed3 baseFogRGB = lerp(_ExploredColor.rgb, _UnexploredColor.rgb, smoothstep(0.0, 1.0, unexploredRatio));
                fixed3 mistyRGB = lerp(baseFogRGB, _MistHighlightColor.rgb, combinedMist * _MistStrength * (1.0 - unexploredRatio * 0.6));

                // 시야 경계 및 반투명 영역에서 안개 알파가 구름처럼 자욱하게 변하도록 처리
                float mistAlphaMod = lerp(1.0 - _MistStrength * 0.35, 1.0 + _MistStrength * 0.25, combinedMist);
                float finalAlpha = saturate(smoothAlpha * lerp(mistAlphaMod, 1.0, unexploredRatio));

                // 완전히 밝은 시야 중심부는 깨끗하게 투명 유지
                finalAlpha *= smoothstep(0.01, 0.12, smoothAlpha);

                return fixed4(mistyRGB, finalAlpha);
            }
            ENDCG
        }
    }
}