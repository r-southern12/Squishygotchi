// three.js MeshBasicMaterial: unlit colour times map, with blend, cull and depth-write set per material
// (blush, rim glow, light rays, selection ring). Instanced draws can tint each instance (confetti).
Shader "Squishy/ThreeBasic"
{
    Properties
    {
        _BaseColor ("Color (linear, alpha = opacity)", Color) = (1, 1, 1, 1)
        _BaseMap ("Map", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
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
            CBUFFER_END
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
                float4 c = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.tint;
                return c;
            }
            ENDHLSL
        }
    }
}
