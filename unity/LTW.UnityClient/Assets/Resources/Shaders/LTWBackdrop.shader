// Static ambient underlayer behind the board (2026-09-01 live render review, finding #13):
// "eight board slabs floating in a flat navy clear colour... no horizon, no backdrop layer, no
// architecture connecting the lanes, and no depth cue at all." This is the fix's ground plate: a
// large dark stone slab with a radial vignette and faint glowing circuit-inlay traces that pick up
// the board's own lane-accent colours.
//
// Unlit rather than Lit deliberately: this sits behind everything, renders every frame at
// whatever size the plate is, and should read as a quiet floor rather than compete with the
// board's own three-point rig. Its "lighting" is baked into the authored base/edge colours
// instead, which are handed in from LocalVerticalSliceLauncher's own ambient trilight constants so
// the backdrop reads as part of the same scene rather than a mismatched insert.
//
// No texture asset: there is no art/texture pipeline in this environment (see
// LTWBoardVertexColor's remarks for the same constraint), so the vignette and circuit lines are
// both computed from world-space position, exactly like LTWBoardVertexColor's procedural surface
// break-up. World space rather than UV: the backdrop is one big static axis-aligned plane, so
// world XZ is a stable, seam-free coordinate that needs no UVs and stays correct at any plate
// scale.
//
// Every custom property lives inside the UnityPerMaterial CBUFFER, matching every other shader in
// this project, so a single shared material stays SRP-Batcher compatible.
Shader "LTW/Backdrop"
{
    Properties
    {
        _BaseColor ("Stone Plate Color (center)", Color) = (0.11, 0.125, 0.15, 1)
        _EdgeColor ("Edge / Void Color", Color) = (0.06, 0.08, 0.12, 1)

        [Header(Vignette)]
        // Normalized distance from the plate center (1 = the plate's own half-extent). Kept well
        // inside 1 so the fade-to-void finishes long before the mesh's literal rectangular edge or
        // any camera-framing-dependent depth clipping could ever be visible.
        _VignetteInnerRadius ("Vignette Inner Radius", Range(0, 1.5)) = 0.1
        _VignetteOuterRadius ("Vignette Outer Radius", Range(0, 2)) = 0.32
        // World-space XZ center/half-extents of the plate, set from BoardBackdrop.cs so the
        // vignette always matches wherever/however large the plate mesh actually is.
        _PlateCenterWS ("Plate Center (world XZ)", Vector) = (34.5, 0, 7.5, 0)
        _PlateExtentsWS ("Plate Half Extents (world XZ)", Vector) = (100, 0, 80, 0)

        [Header(Circuit Inlay)]
        _CircuitScale ("Circuit Cell Size (world units)", Float) = 9.0
        _CircuitLineWidth ("Circuit Line Width (fraction of cell)", Range(0.001, 0.25)) = 0.022
        // A single sine perturbation on the X trace so the grid reads as a wandering circuit trace
        // rather than a rigid graph-paper grid, for one extra ALU op.
        _CircuitWobble ("Circuit Wobble Amount (world units)", Float) = 0.6
        _CircuitWobbleFreq ("Circuit Wobble Frequency", Float) = 0.18
        _CircuitGlowStrength ("Circuit Glow Strength", Range(0, 4)) = 0.32
        _CircuitPulseSpeed ("Circuit Pulse Speed", Float) = 0.6
        // Two lane-accent tones rather than eight: a single static plate cannot show all eight
        // lanes' colours at once anyway, so this mixes the player's own signal colour (accent A —
        // matches UnityVerticalSliceRenderer's MintSignal / player portal accent, the one lane
        // that's always active/visible) with a cooler secondary tone that rhymes with the board's
        // own route-guide blue family (accent B), rather than wiring in all eight per-lane colours.
        _AccentColorA ("Lane Accent A (player)", Color) = (0.349, 0.882, 0.714, 1)
        _AccentColorB ("Lane Accent B (secondary)", Color) = (0.3, 0.55, 0.82, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "Backdrop"

            ZWrite On
            ZTest LEqual
            Cull Back

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
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half _VignetteInnerRadius;
                half _VignetteOuterRadius;
                float4 _PlateCenterWS;
                float4 _PlateExtentsWS;
                float _CircuitScale;
                half _CircuitLineWidth;
                float _CircuitWobble;
                float _CircuitWobbleFreq;
                half _CircuitGlowStrength;
                float _CircuitPulseSpeed;
                half4 _AccentColorA;
                half4 _AccentColorB;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Radial (elliptical) vignette: normalized world-space distance from the plate's
                // own center, so it needs no camera-relative math and stays correct however the
                // camera is framed.
                float2 delta = input.positionWS.xz - _PlateCenterWS.xz;
                float2 normalizedDelta = delta / max(_PlateExtentsWS.xz, float2(0.001, 0.001));
                float radialDistance = length(normalizedDelta);
                float vignette = 1.0 - smoothstep(_VignetteInnerRadius, _VignetteOuterRadius, radialDistance);

                half3 color = lerp(_EdgeColor.rgb, _BaseColor.rgb, vignette);

                // Circuit inlay: a two-axis line grid in world units, with one sine wobble on the
                // X trace so it reads as a wandering circuit rather than graph paper. Cheap:
                // one sin, one frac per axis, no loops, no texture samples.
                float wobble = sin(input.positionWS.z * _CircuitWobbleFreq + _Time.y * 0.05) * _CircuitWobble;
                float2 grid = float2(input.positionWS.x + wobble, input.positionWS.z) / max(_CircuitScale, 0.001);
                float2 cell = frac(grid) - 0.5;
                float lineX = 1.0 - smoothstep(0.0, _CircuitLineWidth, abs(cell.x));
                float lineY = 1.0 - smoothstep(0.0, _CircuitLineWidth, abs(cell.y));
                float traceMask = max(lineX, lineY);

                // Slow per-cell colour drift between the two lane-accent tones, and a slow global
                // pulse, both single sine calls gated by the trace mask so they cost nothing off
                // the lines themselves.
                float accentMix = sin(grid.x * 0.83 + grid.y * 1.31) * 0.5 + 0.5;
                half3 accent = lerp(_AccentColorA.rgb, _AccentColorB.rgb, accentMix);
                float pulse = 0.75 + 0.25 * sin(_Time.y * _CircuitPulseSpeed + input.positionWS.x * 0.1 + input.positionWS.z * 0.1);

                half3 circuitGlow = accent * traceMask * _CircuitGlowStrength * pulse * vignette;

                return half4(color + circuitGlow, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
