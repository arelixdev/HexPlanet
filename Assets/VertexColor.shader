// VertexColor.shader
// Place ce fichier dans Assets/Shaders/
// Assigne le material créé avec ce shader à PlanetMaterial dans l'inspector

Shader "Custom/VertexColor"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
        _Ambient ("Ambient", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        struct Input
        {
            float4 vertColor : COLOR;
        };

        half _Glossiness;
        half _Ambient;

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertColor = v.color;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = IN.vertColor.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
