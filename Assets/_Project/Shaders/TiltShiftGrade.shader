// The game's one full-screen pass, exactly the prototype's: tilt-shift blur, filmic tone curve,
// gamma, saturation, warm tint, vignette and flash. Values come from globals set by PostController.
// _PostMode 1 renders catalogue thumbnails: tone-mapped, un-premultiplied, alpha kept, no blur or grade.
Shader "Squishy/TiltShiftGrade"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "TiltShiftGrade"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            float _PostFocus, _PostBand, _PostWarm, _PostFlash, _PostMode;

            float3 Filmic(float3 c)
            {
                c = saturate(c) * 0.92;
                c = (c * (2.51 * c + 0.03)) / (c * (2.43 * c + 0.59) + 0.14);
                return pow(saturate(c), 1.0 / 2.2);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                if (_PostMode > 0.5)
                {
                    float4 s = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                    float3 t = s.a > 0.0 ? Filmic(s.rgb / s.a) : 0;
                    return half4(t, s.a);
                }

                float2 texel = _BlitTexture_TexelSize.xy;
                float amt = 18.0 * (_BlitTexture_TexelSize.w / 800.0);
                float d = max(0.0, abs(uv.y - _PostFocus) - _PostBand);
                float rad = d * amt;
                float3 c;
                if (rad < 0.7)
                {
                    c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                }
                else
                {
                    c = 0;
                    [unroll] for (int i = 0; i < 10; i++)
                    {
                        float fi = i;
                        float a = fi * 2.39996;
                        float r = sqrt((fi + 0.5) / 10.0) * rad;
                        c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(cos(a), sin(a)) * r * texel).rgb;
                    }
                    c *= 0.1;
                }
                c = Filmic(c);
                c = lerp(dot(c, float3(0.299, 0.587, 0.114)).xxx, c, 1.1);
                c *= lerp(float3(1, 1, 1), float3(1.05, 0.98, 0.9), 0.4 + _PostWarm * 0.6);
                c *= 1.0 - 0.14 * smoothstep(0.4, 0.95, distance(uv, float2(0.5, 0.47)));
                c = lerp(c, float3(1.0, 0.97, 0.88), _PostFlash);
                // c is a display (gamma) value; the sRGB back buffer encodes, so hand it back linear.
                return half4(SRGBToLinear(saturate(c)), 1.0);
            }
            ENDHLSL
        }
    }
}
