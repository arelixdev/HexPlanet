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
        _AmbientFloor  ("Ambient Floor",            Range(0,1)) = 0.25
        // Matrice monde->local de la planete, mise a jour chaque frame par FirTreeSnowUpdater.cs
        // Permet de calculer la vraie latitude locale independamment de la rotation monde.
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
                // Les 3 lignes de la matrice 3x3 rotation world->local de la planete
                // (on n'a besoin que de la composante Y locale donc on stocke la ligne Y)
                float4  _PlanetWorldToLocalRow0; // ligne 0 de la matrice world->local
                float4  _PlanetWorldToLocalRow1; // ligne 1 (axe Y local de la planete)
                float4  _PlanetWorldToLocalRow2; // ligne 2
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
                // Convertit la position monde en espace LOCAL de la planete
                // en multipliant par les 3 lignes de la matrice world->local
                float3 posWS = IN.positionWS;
                float3 localPos = float3(
                    dot(_PlanetWorldToLocalRow0.xyz, posWS) + _PlanetWorldToLocalRow0.w,
                    dot(_PlanetWorldToLocalRow1.xyz, posWS) + _PlanetWorldToLocalRow1.w,
                    dot(_PlanetWorldToLocalRow2.xyz, posWS) + _PlanetWorldToLocalRow2.w
                );

                // Latitude = |Y| du vecteur normalise en espace local de la planete
                float latitude = abs(normalize(localPos).y);

                // Facteur de neige selon latitude locale [0->1]
                float latSnow = smoothstep(_SnowLatStart, _SnowLatFull, latitude);

                // Hauteur normalisee du vertex dans l'arbre (alpha encode par ProceduralShapes.cs)
                float heightNorm = IN.vertColor.a;

                // Front de neige descendant depuis la pointe
                float snowThreshold = 1.0 - latSnow * _SnowCoverage;
                float snowMask = smoothstep(snowThreshold,
                                            snowThreshold + _SnowEdgeSoft,
                                            heightNorm);

                // Couleur arbre + blend neige
                half3 treeCol  = IN.vertColor.rgb * _TreeColor.rgb;
                half3 finalCol = lerp(treeCol, _SnowColor.rgb, snowMask);

                // Lighting Lambert + ombres
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(IN.shadowCoord);
                #else
                    Light mainLight = GetMainLight();
                #endif
                float NdotL = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                float light = NdotL * mainLight.shadowAttenuation * (1.0 - _AmbientFloor)
                            + _AmbientFloor;

                return half4(finalCol * light, 1.0);
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
