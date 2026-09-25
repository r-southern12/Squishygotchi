// The game's one full-screen post effect: tilt-shift blur, colour grade and flash in a single pass.
// Driven by TiltShiftController; run by a Full Screen Pass renderer feature on the mobile renderer.
Shader "Squishy/TiltShiftGrade"
{
    Properties
    {
        // _FocusY and _Flash are globals set by TiltShiftController every frame, so they're not listed here.
        _Band ("Sharp band half-height", Range(0, 0.5)) = 0.12
        _Amount ("Blur strength (pixels at 1080p)", Float) = 36
        _Saturation ("Saturation", Range(0, 2)) = 1.1
        _Warmth ("Warmth", Range(-0.5, 0.5)) = 0.04
        _Vignette ("Vignette", Range(0, 1)) = 0.22
    }

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

            float _FocusY, _Band, _Amount, _Saturation, _Warmth, _Vignette, _Flash;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // Blur grows with distance outside the focus band. Ten taps on a golden-angle spiral.
                float d = max(0.0, abs(uv.y - _FocusY) - _Band);
                float radius = d * _Amount * (_ScreenParams.y / 1080.0);
                half3 c;
                if (radius < 0.7)
                {
                    c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                }
                else
                {
                    c = 0;
                    [unroll] for (int k = 0; k < 10; k++)
                    {
                        float a = k * 2.39996;
                        float r = sqrt((k + 0.5) / 10.0) * radius;
                        c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(cos(a), sin(a)) * r * _BlitTexture_TexelSize.xy).rgb;
                    }
                    c *= 0.1;
                }

                // Grade: saturation, warm tint, soft vignette.
                half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = lerp(luma.xxx, c, _Saturation);
                c *= half3(1.0 + _Warmth, 1.0, 1.0 - _Warmth);
                float2 v = (uv - 0.5) * float2(1.2, 1.0);
                c *= 1.0 - _Vignette * saturate(dot(v, v) * 1.6);

                c = lerp(c, 1.0, _Flash);
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
