Shader "Custom/Atmosphere"
{
    Properties
    {
        [HDR] _AtmoColor    ("Atmosphere Color",        Color)       = (0.30, 0.55, 1.00, 1)
        [HDR] _RimColor     ("Rim / Halo Color",        Color)       = (0.15, 0.35, 0.90, 1)
        _FresnelPower  ("Fresnel Power (sharpness)",    Range(0.5,8))= 3.0
        _Intensity     ("Intensity",                    Range(0,3))  = 1.2
        _InnerOpacity  ("Inner Opacity",                Range(0,1))  = 0.04
        _RimWidth      ("Rim Width",                    Range(0,1))  = 0.72
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent+10"
        }

        // ── Atmosphere pass ───────────────────────────────────────
        Pass
        {
            Name "AtmosphereForward"
            Tags { "LightMode" = "UniversalForward" }

            // Additive blending → se superpose sans masquer
            Blend SrcAlpha One
            ZWrite Off
            Cull Front          // Rendu depuis l'intérieur pour toujours voir le halo
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _AtmoColor;
                half4  _RimColor;
                float  _FresnelPower;
                float  _Intensity;
                float  _InnerOpacity;
                float  _RimWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = normalize(GetCameraPositionWS() - posInputs.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // Fresnel : 0 au centre (vue tangente) → 1 au bord (vue rasante)
                float NdotV   = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Zone centrale légèrement visible, bord très lumineux
                float rimMask  = smoothstep(1.0 - _RimWidth, 1.0, fresnel);
                float innerAO  = (1.0 - fresnel) * _InnerOpacity;

                half3 col   = lerp(_AtmoColor.rgb, _RimColor.rgb, rimMask);
                float alpha = saturate(fresnel * _Intensity + innerAO);

                return half4(col * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
