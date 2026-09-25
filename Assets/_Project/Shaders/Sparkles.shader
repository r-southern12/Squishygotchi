// The squishy's glitter: the prototype's additive point sprites, drawn as camera-facing quads.
// Each quad's four vertices share the sparkle centre (POSITION); UV holds the corner (-1..1),
// TEXCOORD1.x the twinkle phase and COLOR the tint (linear).
Shader "Squishy/Sparkles"
{
    Properties
    {
        _BaseMap ("Sprite", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _SparkTime; // seconds
            float _SparkPx;   // render pixels per CSS pixel (the prototype's pixel ratio)

            struct Attributes { float4 positionOS : POSITION; float2 corner : TEXCOORD0; float2 phase : TEXCOORD1; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 tint : TEXCOORD1; float weight : TEXCOORD2; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 view = TransformWorldToView(TransformObjectToWorld(v.positionOS.xyz));
                float tw = pow(0.5 + 0.5 * sin(_SparkTime * 2.4 + v.phase.x), 9.0);
                o.weight = 0.35 + 1.7 * tw;
                o.tint = v.color.rgb;
                float depth = max(-view.z, 1e-3);
                float sizePx = min(_SparkPx * (0.6 + tw) * (30.0 / depth), 14.0 * _SparkPx);
                float4 clip = TransformWViewToHClip(view);
                clip.xy += v.corner * (sizePx / _ScreenParams.xy) * clip.w;
                o.positionCS = clip;
                o.uv = v.corner * 0.5 + 0.5;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(t.a - 0.02);
                return half4(i.tint * i.weight * t.a, t.a);
            }
            ENDHLSL
        }
    }
}
