// Weapon beam: a hot core inside a soft glow, tapering toward the target.
//
// Replaces the flat unlit box every tower shot used to be drawn as. SpawnBeam scaled a Unity cube
// primitive to 0.06 x 0.06 x distance and tinted it a solid colour, which is a rectangular prism
// that appears and vanishes — the reason the weapons read as cheap lasers. The mesh is still that
// cube, deliberately: the fragment shader measures how close the VIEW RAY passes to the cube's
// central axis and fades on that, so the square cross-section is never visible and the box reads as
// a round tube. That avoids billboarding a quad toward an orthographic camera, which is the usual
// way beam shaders go wrong.
//
// Measuring the ray rather than the fragment is load-bearing, not a refinement. A cube rasterizes
// only its surface, where one of x/y is always exactly +/-0.5 — so "distance from this fragment to
// the axis" is pinned at the maximum for every pixel on the mesh, every beam shades to alpha 0, and
// nothing draws at all. That was the first version of this shader.
//
// Three cues do the work, and they are the ones a flat colour cannot give:
//   - a bright near-white core along the axis, so the beam has an inside rather than being one flat
//     value everywhere;
//   - a soft radial falloff, so it has no hard silhouette edge;
//   - a taper toward the target end, so it reads as fired FROM somewhere rather than as a strut
//     connecting two points.
//
// Additive blending, because every consumer is light — energy, tracers, spores lit from within. It
// also means overlapping shots brighten rather than z-fighting, which matters for the Gatling
// firing every tick and for Tesla's three chained hops landing in one frame.
//
// Written against URP's shader library with every property inside UnityPerMaterial, matching
// LTWContactShadow — the SRP Batcher silently rejects the shader otherwise, and beams are pooled
// per-colour so there will be many materials sharing this one shader.
Shader "LTW/Weapon Beam"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.8, 1, 1)
        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _CoreTightness ("Core Tightness", Range(1, 12)) = 3
        _CoreRadius ("Core Radius", Range(0.05, 1)) = 0.34
        _HaloStrength ("Halo Strength", Range(0, 2)) = 0.7
        _Taper ("Taper To Target", Range(0, 1)) = 0.55
        _Intensity ("Intensity", Range(0, 4)) = 1.4
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
        // Back, not Off: front and back face resolve the same view ray and so shade identically,
        // and additive blending would simply double every beam's brightness for the extra pass.
        Cull Back

        Pass
        {
            Name "WeaponBeam"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _CoreTightness;
                half _CoreRadius;
                half _HaloStrength;
                half _Taper;
                half _Intensity;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // The cube spans -0.5..0.5 on every axis whatever the world scale, so z gives
                // progress along the beam and x/y locate the fragment across it.
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // How close the VIEW RAY passes to the beam's central axis — not how far this
                // fragment is from it. The distinction is the whole trick. A cube only rasterizes
                // its surface, where one of x/y is always exactly +/-0.5, so measuring the fragment
                // itself gives the maximum radius at every single pixel and the beam shades out to
                // nothing. Measuring the ray instead treats the box as the solid tube it is meant
                // to represent: rays through the middle read 0 and light the core, rays clipping
                // the side read 1 and fade out.
                //
                // Object space, deliberately: the non-uniform scale (width, width, length) squashes
                // the direction by the same factors as the geometry, so the measure stays circular
                // in the cube's own frame no matter how long the beam is stretched.
                float3 viewDirOS = TransformWorldToObjectDir(
                    GetWorldSpaceNormalizeViewDir(input.positionWS), false);

                float2 acrossDir = viewDirOS.xy;
                float acrossLength = length(acrossDir);
                float axisDistance;
                if (acrossLength < 1e-4)
                {
                    // Looking straight down the barrel: every ray is the same distance out.
                    axisDistance = length(input.positionOS.xy);
                }
                else
                {
                    // 2D perpendicular distance from the origin to the line through positionOS.xy.
                    acrossDir /= acrossLength;
                    axisDistance = abs(input.positionOS.x * acrossDir.y - input.positionOS.y * acrossDir.x);
                }

                float radial = saturate(axisDistance * 2.0);

                // 0 at the muzzle end, 1 at the target end.
                float along = saturate(input.positionOS.z + 0.5);

                // Narrower toward the target, so the shot has a direction to it.
                float width = lerp(1.0, 1.0 - _Taper, along);
                float shaped = saturate(radial / max(width, 0.05));

                // Two radii, not one. The mesh is deliberately built wider than the beam it draws
                // (see BeamHaloWidthScale), so the halo has somewhere to fade OUT to: the core owns
                // the inner _CoreRadius of the tube and the halo spreads across the whole of it.
                // With a single radius the falloff had no room and every beam came out a hard,
                // uniform line — visually still a cheap laser, just a smoother one.
                float glow = pow(saturate(1.0 - shaped), 2.5) * _HaloStrength;
                float core = pow(saturate(1.0 - shaped / max(_CoreRadius, 0.02)), _CoreTightness);

                // Fades out at the very ends rather than stopping flat, so the beam does not read
                // as a cut length of pipe.
                float endFade = smoothstep(0.0, 0.06, along) * (1.0 - smoothstep(0.94, 1.0, along));

                half3 rgb = _Color.rgb * glow + _CoreColor.rgb * core;
                half alpha = saturate(glow + core) * _Color.a * endFade;
                return half4(rgb * alpha * _Intensity, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
