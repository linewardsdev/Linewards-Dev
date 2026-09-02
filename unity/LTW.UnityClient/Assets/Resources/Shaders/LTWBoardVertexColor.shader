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
//
// Procedural surface break-up (2026-09 live render review, finding #4): the baked mesh is one flat
// colour per face, which reads as a vertex-coloured blockout at the shipped camera distance. Rather
// than author a texture set - there is no art or texture-generation pipeline in this environment,
// and a texture would need per-tile UVs the mesh does not carry - the fragment stage fakes the
// break-up procedurally from input.positionWS. The board is static and axis-aligned, so world-space
// XZ is a perfectly stable, seam-free coordinate to hash and sample noise from: no UVs required, and
// it still costs one shared material for the whole board (BoardMeshBuilder's remarks explain why
// that matters: 1,730 renderers collapsed to 8 meshes on one material would be undone by anything
// that needs a per-tile texture or per-object material). Everything below is ALU-only.
Shader "LTW/Board Vertex Color"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        [Header(Procedural Surface Noise)]
        _NoiseScale ("Albedo Noise Frequency", Float) = 3.0
        _NoiseStrength ("Albedo Noise Strength", Range(0,1)) = 0.12
        _TileSize ("Tile Cell Size (world units)", Float) = 1.0
        _TileVariantStrength ("Tile Variant Strength", Range(0,0.5)) = 0.06
        _BumpStrength ("Fake Normal Bump Strength", Range(0,2)) = 0.35

        [Header(Edge Wear)]
        _EdgeWearWidth ("Edge Wear Width (fraction of cell)", Range(0,0.5)) = 0.05
        _EdgeWearStrength ("Edge Wear Darkening", Range(0,1)) = 0.3

        [Header(Path Inlay)]
        _PathLuminanceThreshold ("Path Luminance Threshold", Range(0,1)) = 0.1
        _PathGlowStrength ("Path Inlay Emissive Strength", Range(0,4)) = 0.6
        _PathPulseSpeed ("Path Pulse Speed", Float) = 1.5
        _PathPulseFrequency ("Path Pulse Frequency", Float) = 2.0
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

                float _NoiseScale;
                float _NoiseStrength;
                float _TileSize;
                float _TileVariantStrength;
                float _BumpStrength;

                float _EdgeWearWidth;
                float _EdgeWearStrength;

                float _PathLuminanceThreshold;
                float _PathGlowStrength;
                float _PathPulseSpeed;
                float _PathPulseFrequency;
            CBUFFER_END

            // ---- Cheap procedural noise -------------------------------------------------------
            // Hash-based value noise (frac(sin(dot(...))*43758.5453)-style) rather than a texture
            // lookup: the board has no UVs and is meant to run on mobile every frame, so this stays
            // a handful of ALU ops with no samplers.
            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453123);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 3-octave fractal value noise. Output range is roughly [0, 0.875]; callers that want a
            // signed [-1,1] variation recentre it themselves.
            float FBM(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * ValueNoise(p);
                    p *= 2.03;
                    amplitude *= 0.5;
                }

                return value;
            }

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

                float tileSize = max(_TileSize, 0.0001);
                float2 worldXZ = input.positionWS.xz;
                float2 noiseP = worldXZ * _NoiseScale;

                // 1. Albedo break-up: layered value noise, recentred to a signed multiplier so it
                // can brighten and darken around the baked vertex colour rather than only darken.
                float baseNoise = FBM(noiseP);
                float noiseSigned = (baseNoise / 0.875 - 0.5) * 2.0;
                float albedoNoise = 1.0 + noiseSigned * _NoiseStrength;

                // Cheap "3-4 tile variants" without UVs: floor world position into cells the same
                // size as a board tile (see BoardMeshBuilder/UnityVerticalSliceRenderer.Board.cs,
                // which lay tiles out on unit centres) and hash the cell to pick one of 4 subtle
                // brightness multipliers, so neighbouring panels read as distinct pieces.
                float2 cell = floor(worldXZ / tileSize);
                float variantIndex = floor(Hash21(cell + 0.5) * 4.0);
                float variantMul = lerp(1.0 - _TileVariantStrength, 1.0 + _TileVariantStrength, variantIndex / 3.0);

                half3 albedo = input.color.rgb * _Color.rgb * albedoNoise * variantMul;

                // 2. Procedural normal perturbation: finite-difference the noise field to get a
                // fake gradient and tilt the surface normal with it, so light catches the panel
                // unevenly instead of the mesh's perfectly flat per-face normal.
                float eps = 0.05 * tileSize;
                float hL = FBM((worldXZ + float2(-eps, 0.0)) * _NoiseScale);
                float hR = FBM((worldXZ + float2(eps, 0.0)) * _NoiseScale);
                float hD = FBM((worldXZ + float2(0.0, -eps)) * _NoiseScale);
                float hU = FBM((worldXZ + float2(0.0, eps)) * _NoiseScale);
                float2 grad = float2(hR - hL, hU - hD) / (2.0 * eps);
                float3 bumpedNormalWS = input.normalWS + float3(grad.x, 0.0, grad.y) * _BumpStrength;

                // 3. Edge/seam wear: darken a thin band near every tile boundary. BoardMeshBuilder
                // already bakes a groundShade darkening on raised boxes' bottom edge, but that only
                // covers vertical faces; this covers the flat tops that make up ~90% of the frame,
                // using the same tile grid as the variant hash above so wear lines land on seams.
                float2 cellFrac = frac(worldXZ / tileSize);
                float2 distToEdge = min(cellFrac, 1.0 - cellFrac) * tileSize;
                float edgeDist = min(distToEdge.x, distToEdge.y);
                float edgeMask = 1.0 - smoothstep(0.0, max(_EdgeWearWidth, 0.0001) * tileSize, edgeDist);
                albedo *= 1.0 - edgeMask * _EdgeWearStrength;

                // 4. Path inlay: BoardPalette already gives the route's guide rails, ribs and
                // direction triangles (RouteGuideColor/RouteRibColor/RouteTriangleColor) a distinctly
                // brighter, bluer vertex colour than the surrounding stone (RouteBandColor/
                // RouteInlayColor/RouteRecessColor/RouteWearColor stay close to the board's own
                // value). Keying an emissive tint off luminance + blue bias picks out exactly those
                // bright guide markings without touching C#/BoardPalette, and reuses the vertex
                // colour itself as the glow tint so it stays correct per lane without new inputs.
                // Layered on top is a slow along-path pulse (position along world Z modulated by
                // time) rather than a flat static glow, since it was cheap (one extra sin).
                float luminance = dot(input.color.rgb, float3(0.2126, 0.7152, 0.0722));
                float blueBias = saturate(input.color.b - max(input.color.r, input.color.g));
                float pathMask = saturate((luminance - _PathLuminanceThreshold) * 5.0) * saturate(blueBias * 6.0);
                float pathPulse = 0.85 + 0.15 * sin(worldXZ.y * _PathPulseFrequency - _Time.y * _PathPulseSpeed);
                half3 pathEmission = input.color.rgb * _PathGlowStrength * pathMask * pathPulse;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(bumpedNormalWS);
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
                surfaceData.emission = pathEmission;

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
