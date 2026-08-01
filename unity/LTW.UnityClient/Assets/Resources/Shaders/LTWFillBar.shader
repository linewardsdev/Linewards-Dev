// Lane pressure gauge. Replaces a cube that grew along one axis to show fill level —
// growing/shrinking geometry every frame defeats static batching and reads as "stacked
// cube geometry" rather than a gauge. This shader keeps the mesh a fixed size and expresses
// fill as an emissive threshold along the mesh's U coordinate instead.
//
// ---------------------------------------------------------------------------------------
// Why this is URP HLSL (open item 30)
// ---------------------------------------------------------------------------------------
// This SubShader has always been tagged "RenderPipeline" = "UniversalPipeline", but its pass was
// written for the BUILT-IN pipeline: CGPROGRAM, UnityCG.cginc, fixed4, UnityObjectToClipPos.
// The pass is now genuine URP HLSL — HLSLPROGRAM, URP's Core.hlsl, half4/float4,
// TransformObjectToHClip — so it is written against the pipeline it claims.
//
// Be clear about what this did and did not fix, because the item that prompted it guessed wrong
// and the guess is worth not repeating. Item 30 blamed the magenta bars in the 2026-08-01 iOS
// build on this shader: FallBack Off, therefore no usable variant, therefore error material.
// That was measured and is false — compiled explicitly for Metal/iOS, the old CGPROGRAM pass
// succeeds on all four variants it has (vertex and fragment, INSTANCING_ON on and off) and emits
// real bytecode. Nothing fell back. The magenta was GameObject.CreatePrimitive handing the creep
// health bars a NULL material in a player build; see RenderCompat.CreatePrimitive.
//
// So this conversion is a latent-defect fix, not the fix for that symptom: a pass declaring a
// pipeline it was not written for is a real bug that happened not to be biting yet, and
// UnityCG.cginc under URP has no future.
//
// ---------------------------------------------------------------------------------------
// GPU instancing, deliberately, NOT the SRP Batcher
// ---------------------------------------------------------------------------------------
// The other three custom shaders in this folder (LTWContactShadow, LTWSporeFog, LTWWeaponBeam)
// put every property inside CBUFFER_START(UnityPerMaterial) so the SRP Batcher can take them.
// This one deliberately does not, and the two paths are mutually exclusive rather than a matter
// of taste:
//
//   - Every consumer of this shader drives it through a MaterialPropertyBlock
//     (UnityVerticalSliceRenderer.SetFillBarProperties). The SRP Batcher SKIPS any renderer
//     carrying a property block, so a UnityPerMaterial layout would buy nothing here.
//   - Worse, it would cost: when a shader IS batcher-compatible the batcher claims the draw and
//     GPU instancing never runs. Staying incompatible is what keeps the instanced path live.
//   - The instanced path is the one that pays. All eight lane gauges share one mesh
//     (BoardRenderResources.FillBarMesh) and one material (BoardRenderResources.FillBarMaterial,
//     enableInstancing = true) and differ only by _Color/_BackgroundColor/_Fill — exactly the
//     shape UNITY_DEFINE_INSTANCED_PROP exists for. (Design intent; the resulting draw-call
//     count has not been measured with the frame debugger, only the per-instance values, which
//     do arrive correctly and differ per lane.)
//   - The SRP Batcher's win is many DISTINCT materials sharing one shader, which is the
//     contact-shadow case (one cloned material per unit). There is exactly one Fill Bar
//     material in the project, so there is no such win to give up.
//
// So open item 15's reasoning is kept and item 30's correction is applied on top: the batching
// choice was right, the pipeline the pass was written for was not. Adding properties to this
// shader means adding them to the instancing buffer AND to the property block writer, not to a
// UnityPerMaterial CBUFFER.
//
// _EdgeSoftness is the exception and stays per-material: it is authored once and never varies
// per gauge, so paying for it in every instance slot would be waste.
Shader "LTW/Fill Bar"
{
    Properties
    {
        _Color ("Filled Color", Color) = (0.3, 1, 0.6, 1)
        _BackgroundColor ("Empty Color", Color) = (0.08, 0.1, 0.1, 0.6)
        _Fill ("Fill", Range(0, 1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.25)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-50"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FillBar"

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

            // Per-instance, written from a MaterialPropertyBlock. See the header: this buffer,
            // not UnityPerMaterial, is what makes the eight gauges one draw call.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BackgroundColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _Fill)
            UNITY_INSTANCING_BUFFER_END(Props)

            CBUFFER_START(UnityPerMaterial)
                half _EdgeSoftness;
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

                half4 filled = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                half4 empty = UNITY_ACCESS_INSTANCED_PROP(Props, _BackgroundColor);
                half fill = UNITY_ACCESS_INSTANCED_PROP(Props, _Fill);
                half t = smoothstep(fill - _EdgeSoftness, fill + _EdgeSoftness, input.uv.x);
                return lerp(filled, empty, t);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
