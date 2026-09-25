// The prototype's two lit materials, matching three.js r128 with legacy (non-physical) lights:
//  - Lambert (default): lighting per vertex, all direct light scaled by the key light's shadow.
//  - Standard (_STANDARD): per-pixel GGX, used only for the squishy and its eyes.
// Lights: the URP main light is the shadowed key; hemisphere, fill and two lamp point lights come
// from globals set by SceneLighting, so every scene lights exactly like the prototype.
Shader "Squishy/ThreeLit"
{
    Properties
    {
        _BaseColor ("Color (linear)", Color) = (1, 1, 1, 1)
        _BaseMap ("Map", 2D) = "white" {}
        _EmissionColor ("Emissive (linear, times intensity)", Color) = (0, 0, 0, 0)
        _Roughness ("Roughness", Range(0, 1)) = 1
        _Metalness ("Metalness", Range(0, 1)) = 0
        _ReceiveShadows ("Receive shadows", Float) = 1
        [Toggle(_STANDARD)] _Standard ("Standard shading", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float4 _EmissionColor;
            float _Roughness;
            float _Metalness;
            float _ReceiveShadows;
            float _Standard;
            float _Cull;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _STANDARD
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Set by SceneLighting (linear colours already multiplied by intensity).
            float4 _HemiSky;
            float4 _HemiGround;
            float4 _FillDir;
            float4 _FillColor;
            float4 _LampPos[2];   // xyz world position, w cut-off distance
            float4 _LampColor[2];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            #ifndef _STANDARD
                float3 lightFront : TEXCOORD3;
                float3 lightBack : TEXCOORD4;
                float3 hemiFront : TEXCOORD5;
                float3 hemiBack : TEXCOORD6;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            void AddLight(float3 n, float3 l, float3 color, inout float3 front, inout float3 back)
            {
                float d = dot(n, l);
                front += saturate(d) * color;
                back += saturate(-d) * color;
            }

            // three.js legacy point light falloff: pow(saturate(1 - d / cutoff), decay), decay 2.
            float3 LampColor(int i, float3 positionWS, out float3 dir)
            {
                float3 v = _LampPos[i].xyz - positionWS;
                float d = length(v);
                dir = v / max(d, 1e-4);
                float f = saturate(1.0 - d / max(_LampPos[i].w, 1e-4));
                return _LampColor[i].rgb * f * f;
            }

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 pws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = normalize(TransformObjectToWorldNormal(v.normalOS));
                o.positionCS = TransformWorldToHClip(pws);
                o.positionWS = pws;
                o.normalWS = nws;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
            #ifndef _STANDARD
                float3 front = 0, back = 0;
                Light key = GetMainLight();
                AddLight(nws, key.direction, key.color, front, back);
                AddLight(nws, _FillDir.xyz, _FillColor.rgb, front, back);
                [unroll] for (int i = 0; i < 2; i++)
                {
                    float3 dir;
                    float3 c = LampColor(i, pws, dir);
                    AddLight(nws, dir, c, front, back);
                }
                o.lightFront = front;
                o.lightBack = back;
                float w = 0.5 * nws.y + 0.5;
                o.hemiFront = lerp(_HemiGround.rgb, _HemiSky.rgb, w);
                o.hemiBack = lerp(_HemiGround.rgb, _HemiSky.rgb, 1.0 - w);
            #endif
                return o;
            }

            float3 SpecularGGX(float3 n, float3 v, float3 l, float3 f0, float roughness)
            {
                float alpha = roughness * roughness;
                float3 h = normalize(l + v);
                float nl = saturate(dot(n, l));
                float nv = saturate(dot(n, v));
                float nh = saturate(dot(n, h));
                float lh = saturate(dot(l, h));
                float fresnel = exp2((-5.55473 * lh - 6.98316) * lh);
                float3 F = (1.0 - f0) * fresnel + f0;
                float a2 = alpha * alpha;
                float gv = nl * sqrt(a2 + (1.0 - a2) * nv * nv);
                float gl = nv * sqrt(a2 + (1.0 - a2) * nl * nl);
                float G = 0.5 / max(gv + gl, 1e-6);
                float dd = nh * nh * (a2 - 1.0) + 1.0;
                float D = a2 / (PI * dd * dd);
                return F * (G * D);
            }

            float3 DirectStandard(float3 n, float3 v, float3 l, float3 color, float3 diffuse, float3 f0, float roughness)
            {
                float3 irradiance = saturate(dot(n, l)) * color;
                return irradiance * diffuse + irradiance * PI * SpecularGGX(n, v, l, f0, roughness);
            }

            half4 Frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 albedo = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                Light key = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float shadow = lerp(1.0, key.shadowAttenuation, _ReceiveShadows);
            #ifndef _STANDARD
                float3 direct = (frontFace ? i.lightFront : i.lightBack) * shadow;
                float3 indirect = frontFace ? i.hemiFront : i.hemiBack;
                float3 c = albedo.rgb * (direct + indirect) + _EmissionColor.rgb;
            #else
                float3 n = normalize(i.normalWS);
                if (!frontFace) n = -n;
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                float3 diffuse = albedo.rgb * (1.0 - _Metalness);
                float3 f0 = lerp(float3(0.04, 0.04, 0.04), albedo.rgb, _Metalness);
                float3 dxy = max(abs(ddx(n)), abs(ddy(n)));
                float roughness = min(max(_Roughness, 0.0525) + max(max(dxy.x, dxy.y), dxy.z), 1.0);
                float3 c = DirectStandard(n, v, key.direction, key.color * shadow, diffuse, f0, roughness);
                c += DirectStandard(n, v, _FillDir.xyz, _FillColor.rgb, diffuse, f0, roughness);
                [unroll] for (int k = 0; k < 2; k++)
                {
                    float3 dir;
                    float3 lc = LampColor(k, i.positionWS, dir);
                    c += DirectStandard(n, v, dir, lc, diffuse, f0, roughness);
                }
                c += diffuse * lerp(_HemiGround.rgb, _HemiSky.rgb, 0.5 * n.y + 0.5);
                c += _EmissionColor.rgb;
            #endif
                return half4(c, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                float4 pos = TransformWorldToHClip(ApplyShadowBias(pws, nws, _LightDirection));
            #if UNITY_REVERSED_Z
                pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
            #else
                pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return pos;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
