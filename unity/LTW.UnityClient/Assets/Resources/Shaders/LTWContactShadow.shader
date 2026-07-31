// Soft radial blob used as the contact shadow / grounding decal under towers and creeps.
//
// The match camera is orthographic and almost top-down, so the cast shadow from the key
// light lands nearly underneath each unit and reads as noise. An explicit blob directly
// under the footprint is the strongest grounding cue available at this angle, and it
// costs one instanced quad per unit.
//
// The falloff is computed from UV rather than sampled from a texture so the decal needs
// no asset, and the queue sits just after opaque geometry so the blob paints over the
// board surface but is still depth-rejected by the unit standing on top of it.
//
// Written against URP's shader library rather than UnityCG.cginc so it is SRP-Batcher
// compatible. That matters more here than anywhere else in the project: every unit on the
// board carries one of these, each is coloured through renderer.material (which clones a
// material per instance), and many distinct materials sharing one shader is precisely the
// case the SRP Batcher exists to make cheap. Under the old built-in path each of the ~30
// quads broke the batch instead.
//
// Every material property must live inside the UnityPerMaterial CBUFFER or the batcher
// silently rejects the shader and it falls back to per-object setup with no error.
Shader "LTW/Contact Shadow"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,0.5)
        _Softness ("Softness", Range(0.01, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ContactShadow"

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
                half _Softness;
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

                float2 offset = input.uv * 2.0 - 1.0;
                float radius = saturate(length(offset));
                float falloff = 1.0 - smoothstep(saturate(1.0 - _Softness), 1.0, radius);
                return half4(_Color.rgb, _Color.a * falloff * falloff);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
