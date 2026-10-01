// three.js MeshBasicMaterial: unlit colour times map, with blend, cull and depth-write set per material
// (blush, rim glow, light rays, selection ring). Instanced draws can tint each instance (confetti).
Shader "Squishy/ThreeBasic"
{
    Properties
    {
        _BaseColor ("Color (linear, alpha = opacity, set raw)", Vector) = (1, 1, 1, 1)
        _BaseMap ("Map", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _Fade ("Fade (1 = visible)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _Cull;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
                float _Fade;
            CBUFFER_END

            // Fading pieces out of the way (a piece blocking the zoomed-in view): a 4x4 ordered dither, so opaque materials
            // can dissolve smoothly; the tilt-shift blur softens it into a fade. _Fade: 1 visible, 0 gone.
            static const float kBayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            void FadeClip(float4 positionCS, float fade)
            {
                if (fade >= .999) return;
                int x = (int)fmod(floor(positionCS.x), 4.0), y = (int)fmod(floor(positionCS.y), 4.0);
                clip(fade - (kBayer[y * 4 + x] + .5) / 16.0);
            }

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 tint : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
            #ifdef UNITY_INSTANCING_ENABLED
                o.tint = UNITY_ACCESS_INSTANCED_PROP(Props, _InstColor);
            #else
                o.tint = 1;
            #endif
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                FadeClip(i.positionCS, _Fade);
                float4 c = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.tint;
                return c;
            }
            ENDHLSL
        }
    }
}
