Shader "CarPrototype/PremiumVfxAdditive"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+40"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PremiumVfxAdditive"
            Blend One One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Intensity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = sampled.a * input.color.a * _BaseColor.a;
                half3 color = sampled.rgb * input.color.rgb * _BaseColor.rgb * alpha * _Intensity;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
