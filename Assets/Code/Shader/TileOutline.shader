Shader "Custom/TileOutline"
{
    Properties
    {
        [HDR] _Color      ("Outline Color",  Color)        = (1.6, 1.6, 1.6, 1)
        _PulseSpeed ("Pulse Speed",    Range(0.5, 8))  = 3.0
        _PulseMin   ("Pulse Min",      Range(0,   1))  = 0.20
        _PulseMax   ("Pulse Max",      Range(0,   3))  = 1.40
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent+20"
        }

        Pass
        {
            Name "OutlinePulse"
            Tags { "LightMode" = "UniversalForward" }

            // Additive blending → glow effect, never darkens the planet
            Blend One One
            ZWrite Off
            ZTest  LEqual
            Cull   Off              // visible from both sides
            Offset -2, -2           // draw slightly in front of the tile surface

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                float  _PulseSpeed;
                float  _PulseMin;
                float  _PulseMax;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Smooth sine pulse between _PulseMin and _PulseMax
                float t     = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                float pulse = lerp(_PulseMin, _PulseMax, t);
                return half4(_Color.rgb * pulse, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
