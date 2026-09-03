// Combat streak: the one shader behind the moving projectile dart, its trail and the impact
// sparks (finding #8 / R4, render review 2026-09-01, Wave 5).
//
// Drawn on CombatVfxResources.TaperedQuadMesh — a flat wedge, wide at v=0 and a point at v=1,
// billboarded toward the camera by the C# side with its long axis along travel. What the frames
// showed was a one-frame straight line from tower to creep, and a line is what a tube mesh IS
// however it is shaded; a short wedge that is somewhere different every frame is what reads as
// a thing in flight.
//
// Two profiles, both procedural so there is no sprite to author:
//   - across (u): a hot core on the centreline inside a soft edge, so the wedge has an inside and
//     no hard silhouette;
//   - along (v): brightness ramps between _WideGlow at the wide end and _PointGlow at the point.
//     The dart runs it hot toward the point; the trail (the same mesh turned round) runs it to
//     zero at the point, which is how one mesh gives both a head and a fading tail.
//
// Additive, like LTWWeaponBeam and LTWParticleAdditive — every consumer is light. Alpha lives
// in _Color.a and is written per frame by the C# fade, so it is a real property rather than a
// vertex colour: the pooled quads are plain MeshRenderers, not a ParticleSystem stream.
//
// Every property is inside UnityPerMaterial for the SRP Batcher: the quads are pooled with one
// material instance each (colour and fade differ per shot) and the batcher needs them all on
// one CBUFFER layout to draw them in one go.
Shader "LTW/Combat VFX"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.8, 1, 1)
        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.4
        _EdgeSoftness ("Edge Softness", Range(0.05, 1)) = 0.55
        _CoreWidth ("Core Width", Range(0.05, 1)) = 0.4
        _WideGlow ("Glow At Wide End", Range(0, 2)) = 0.6
        _PointGlow ("Glow At Point", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        // Off, not Back: the wedge is billboarded by LookRotation and either face can end up
        // toward the camera depending on which side of the travel axis the camera sits.
        Cull Off

        Pass
        {
            Name "CombatVfx"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _Intensity;
                half _EdgeSoftness;
                half _CoreWidth;
                half _WideGlow;
                half _PointGlow;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 0 on the centreline, 1 at the wedge's edge. The point vertex carries u = 0.5,
                // so the tip resolves to full core: a hot head, not a dim one.
                float across = abs(input.uv.x * 2.0 - 1.0);
                // 0 at the wide end, 1 at the point.
                float along = saturate(input.uv.y);

                float edge = 1.0 - smoothstep(saturate(1.0 - _EdgeSoftness), 1.0, across);
                float core = pow(saturate(1.0 - across / max(_CoreWidth, 0.05)), 2.0);
                float glow = lerp(_WideGlow, _PointGlow, along);

                // Fades in over the first sliver of the wide end so the trail never shows a cut
                // edge where it meets the dart.
                float endFade = smoothstep(0.0, 0.08, along);

                half3 rgb = _Color.rgb * edge * 0.8 + _CoreColor.rgb * core;
                half alpha = saturate(edge * 0.8 + core) * glow * endFade * _Color.a;
                return half4(rgb * alpha * _Intensity, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
