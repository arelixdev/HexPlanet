Shader "Custom/FirTreeSnow"
{
    Properties
    {
        [HDR] _TreeColor    ("Tree Color (vert)",  Color)       = (0.14, 0.42, 0.10, 1)
        [HDR] _SnowColor    ("Snow Color (blanc)", Color)       = (0.92, 0.96, 1.00, 1)
        _SnowLatStart  ("Snow Lat Start",           Range(0,1)) = 0.55
        _SnowLatFull   ("Snow Lat Full",            Range(0,1)) = 0.85
        _SnowCoverage  ("Snow Max Coverage",        Range(0,1)) = 0.85
        _SnowEdgeSoft  ("Snow Edge Softness",       Range(0.01,0.5)) = 0.18
        _AmbientFloor  ("Ambient Floor (fallback)", Range(0,1)) = 0.25
        // Matrice monde->local de la planete, mise a jour chaque frame par FirTreeSnowUpdater.cs
        _PlanetWorldToLocal ("Planet WorldToLocal", Vector) = (0,1,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            // Needed for SampleSH to pick up environment lighting changes
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 vertColor   : COLOR;
                float4 shadowCoord : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4   _TreeColor;
                half4   _SnowColor;
                float   _SnowLatStart;
                float   _SnowLatFull;
                float   _SnowCoverage;
                float   _SnowEdgeSoft;
                float   _AmbientFloor;
                float4  _PlanetWorldToLocalRow0;
                float4  _PlanetWorldToLocalRow1;
                float4  _PlanetWorldToLocalRow2;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.vertColor   = IN.color;
                OUT.shadowCoord = GetShadowCoord(posInputs);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);

                // ── Latitude locale de la planete ──────────────────
                float3 posWS = IN.positionWS;
                float3 localPos = float3(
                    dot(_PlanetWorldToLocalRow0.xyz, posWS) + _PlanetWorldToLocalRow0.w,
                    dot(_PlanetWorldToLocalRow1.xyz, posWS) + _PlanetWorldToLocalRow1.w,
                    dot(_PlanetWorldToLocalRow2.xyz, posWS) + _PlanetWorldToLocalRow2.w
                );
                float latitude = abs(normalize(localPos).y);

                // ── Calcul neige ───────────────────────────────────
                float latSnow      = smoothstep(_SnowLatStart, _SnowLatFull, latitude);
                float heightNorm   = IN.vertColor.a;
                float snowThreshold = 1.0 - latSnow * _SnowCoverage;
                float snowMask     = smoothstep(snowThreshold,
                                               snowThreshold + _SnowEdgeSoft,
                                               heightNorm);

                half3 treeCol  = IN.vertColor.rgb * _TreeColor.rgb;
                half3 finalCol = lerp(treeCol, _SnowColor.rgb, snowMask);

                // ── Eclairage : SampleSH + lumiere directionnelle ──
                // SampleSH repond aux changements d'Environment Lighting
                // (Intensity Multiplier, Source, etc.)
                float3 ambient = SampleSH(normalWS);

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(IN.shadowCoord);
                #else
                    Light mainLight = GetMainLight();
                #endif

                float NdotL   = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse = mainLight.color * NdotL * mainLight.shadowAttenuation;

                // Combine ambient (environment) + diffuse directionnelle
                // _AmbientFloor sert de plancher minimal si l'env est tres sombre
                float3 lighting = max(ambient + diffuse, _AmbientFloor);

                return half4(finalCol * lighting, 1.0);
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;

            struct AttrShadow
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct VaryShadow
            {
                float4 positionCS : SV_POSITION;
            };

            VaryShadow vertShadow(AttrShadow IN)
            {
                VaryShadow OUT;
                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                posWS = ApplyShadowBias(posWS, normWS, _LightDirection);
                OUT.positionCS = TransformWorldToHClip(posWS);
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return OUT;
            }

            half4 fragShadow(VaryShadow IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
