// Lit surface that multiplies the mesh's baked vertex colour into albedo.
//
// The board is one GameObject per cell/decoration collapsed into a single mesh per lane, with
// each piece's colour carried in the vertex stream rather than in a material. That is what lets
// an entire lane draw with one shared material.
//
// Colours arrive already lifted by UnityVerticalSliceRenderer's BoardSurface() helper and already
// decoded to linear by BoardMeshBuilder.ToRenderSpace(). That decode matters: this project renders
// in linear colour space, where a colour set through Material.color is sRGB-decoded on its way to
// the GPU but a colour written into the vertex stream is not. Baking authored values unchanged
// renders the board roughly twice as bright as the per-object materials it replaced.
//
// Rewritten for URP. The original was a Built-in surface shader, a format URP does not support.
// Shadow and depth passes are borrowed from URP's own Lit shader rather than reimplemented, so
// they stay correct as the package updates.
Shader "LTW/Board Vertex Color"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Glossiness;
                float _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 color       : COLOR;
                float  fogCoord    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // The vertex stream is the albedo. _Color stays a global tint so the whole board
                // can be adjusted without rebaking the mesh.
                half3 albedo = input.color.rgb * _Color.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Glossiness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = 1.0h;
                surfaceData.alpha = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
