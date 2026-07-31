// Additive particle shader for the impact/spawn/kill effect system.
//
// Soft round falloff computed from UV rather than sampled from a sprite, for the same
// reason LTWContactShadow does it: no texture asset to author, import, keep in sync or ship,
// and at the size these particles occupy on a phone screen a procedural disc is
// indistinguishable from an authored one. It also means the whole VFX system is code and
// carries no binary art dependency.
//
// Colour comes from the vertex stream, not a material property. ParticleSystem writes each
// particle's current colour-over-lifetime value into vertex colour, so one material serves
// every effect in the game and the per-effect tint costs nothing.
//
// Additive rather than alpha-blended because these are light events — a muzzle flash, a
// spark burst, an energy hit. Additive keeps them reading as emission over the board rather
// than as paint on top of it, and it needs no sorting between overlapping particles, which
// matters when a heavy-pressure frame has several hundred of them.
Shader "LTW/Particle Additive"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        // Fraction of the radius spent fading out. High values read as a glow, low as a dot.
        _Softness ("Softness", Range(0.01, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        // One, not SrcAlpha/OneMinusSrcAlpha: alpha scales the contribution and nothing is
        // ever darkened, so overlapping particles accumulate instead of occluding.
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ParticleAdditive"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 offset = input.uv * 2.0 - 1.0;
                float radius = saturate(length(offset));
                float falloff = 1.0 - smoothstep(saturate(1.0 - _Softness), 1.0, radius);

                // Squared so the core stays bright and the skirt falls off fast. A linear
                // falloff at additive blend reads as a soft grey smear rather than a spark.
                falloff *= falloff;

                half4 tint = input.color * _Color;
                return half4(tint.rgb * tint.a * falloff, tint.a * falloff);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
