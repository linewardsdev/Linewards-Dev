// The shared stylized shader for every tower and creep on the board.
//
// ---------------------------------------------------------------------------------------
// Why this exists
// ---------------------------------------------------------------------------------------
// 139 of 149 materials in this project are stock URP/Lit. GRAPHICS_AA_UPLIFT.md section 4
// compared that against the reference and reached a conclusion worth restating exactly,
// because it inverts the obvious instinct:
//
//   "The single highest-value art change is authored roughness with simple albedo, not more
//    detail."
//
// and, from direct observation of reference frames in section 4.1:
//
//   "The reference has NO sharp specular highlights anywhere. Metal reads as metal purely
//    through a broad, smooth value gradient plus a cool rim on the silhouette edge."
//
// Stock URP/Lit cannot produce that look at any parameter setting. It is a physically-based
// shader: it gives a specular lobe whose tightness is tied to roughness, so the two clusters
// this project shipped are the only two things it can do — smoothness 1.0 reads wet, 0.12
// reads dead. The reference look is PBR deliberately broken, which needs a shader that will
// break it.
//
// So this is not "a nicer Lit". It replaces four things Lit does with authored equivalents:
//
//   1. Diffuse falloff        -> a wrapped, smoothstep-ramped gradient (broad, no terminator)
//   2. Specular               -> an optional soft lobe with a floor on roughness (no hotspot)
//   3. Ambient                -> an AO-tinted cool shade colour instead of flat SH
//   4. Silhouette             -> an explicit fresnel rim plus a darkened contour
//
// Items 3 and 4 are the two the reference study singled out as carrying the richness, and
// neither is available from Lit at all.
//
// ---------------------------------------------------------------------------------------
// Simple albedo, without reauthoring 21 texture sets
// ---------------------------------------------------------------------------------------
// The reference keeps albedo to a couple of flat colour variations and lets roughness and
// light do the work. Ours is Meshy's generated output: busy, photographic, and carrying baked
// lighting it should not have. Reauthoring every map is weeks of work.
//
// _AlbedoFlatten is the cheap approximation: it desaturates the sampled albedo toward its own
// luminance and then tints that toward _BaseColor. At 0 it is the Meshy map untouched, at 1 it
// is a flat authored colour that still keeps the map's value structure. Somewhere around
// 0.5-0.7 keeps the panel detail while killing the photographic colour noise. This is a knob
// to tune per role against a capture, not a number to adopt on trust.
//
// ---------------------------------------------------------------------------------------
// SRP Batcher
// ---------------------------------------------------------------------------------------
// Every property lives inside CBUFFER_START(UnityPerMaterial). This is the case the batcher
// exists for: ~30 units, many distinct materials, one shader. Note the deliberate contrast
// with LTWFillBar, which stays batcher-INCOMPATIBLE on purpose because its consumers drive it
// through MaterialPropertyBlocks and it wants the instanced path instead. Units do not use
// property blocks for their body materials, so the batcher is the right path here.
//
// If you add a property, it MUST go in the CBUFFER or the batcher silently rejects the whole
// shader with no error and every unit falls back to per-object setup.
Shader "LTW/Stylized Unit"
{
    Properties
    {
        [Header(Surface)]
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Base Tint", Color) = (1,1,1,1)
        _AlbedoFlatten ("Albedo Flatten", Range(0,1)) = 0.0

        _Smoothness ("Smoothness", Range(0,1)) = 0.42
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _SpecStrength ("Specular Strength", Range(0,1)) = 0.25
        _SpecRoughFloor ("Specular Roughness Floor", Range(0.05,1)) = 0.35

        [Header(Shading Ramp)]
        _ShadeColor ("Shade Colour", Color) = (0.34,0.40,0.56,1)
        _ShadeStrength ("Shade Strength", Range(0,1)) = 0.85
        _RampStart ("Ramp Start", Range(0,1)) = 0.18
        _RampEnd ("Ramp End", Range(0,1)) = 0.85

        [Header(Occlusion)]
        _OcclusionMap ("Occlusion (R)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1.0
        _AOTint ("AO Tint", Color) = (0.28,0.32,0.45,1)

        [Header(Silhouette)]
        _RimColor ("Rim Colour", Color) = (0.55,0.80,1.0,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 2.6
        _RimStrength ("Rim Strength", Range(0,3)) = 0.9
        _ContourColor ("Contour Colour", Color) = (0.05,0.06,0.10,1)
        _ContourPower ("Contour Power", Range(1,16)) = 6.0
        _ContourStrength ("Contour Strength", Range(0,1)) = 0.35

        [Header(Emission)]
        _EmissionMap ("Emission", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Colour", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _AlbedoFlatten;
                half   _Smoothness;
                half   _Metallic;
                half   _SpecStrength;
                half   _SpecRoughFloor;
                half4  _ShadeColor;
                half   _ShadeStrength;
                half   _RampStart;
                half   _RampEnd;
                half   _OcclusionStrength;
                half4  _AOTint;
                half4  _RimColor;
                half   _RimPower;
                half   _RimStrength;
                half4  _ContourColor;
                half   _ContourPower;
                half   _ContourStrength;
                half4  _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(input.normalOS);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS   = nrm.normalWS;
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor  = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            // The gradient that replaces Lit's diffuse falloff.
            //
            // Wrapped first (ndl*0.5+0.5) so the terminator is not a hard line across the form —
            // that wrap is most of what makes the reference read as "broad and smooth". The
            // smoothstep then places where the ramp actually turns, which is the artist-facing
            // control: a narrow Start..End is a graphic, posterised look, a wide one is soft.
            half StylizedRamp(half ndl, half shadow, half rampStart, half rampEnd)
            {
                half wrapped = saturate(ndl * 0.5h + 0.5h);
                half ramp    = smoothstep(rampStart, rampEnd, wrapped);
                // Shadow multiplies rather than min()s, so a shadowed surface still keeps some
                // of its form gradient instead of flattening into a silhouette.
                return ramp * lerp(0.35h, 1.0h, shadow);
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // Simple albedo, approximated. Desaturate toward the map's own luminance, then
                // tint toward the authored colour. Keeps value structure, discards Meshy's
                // photographic colour noise. See the header note on _AlbedoFlatten.
                half  lum  = dot(sampled.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 flat = lerp(sampled.rgb, lum.xxx * _BaseColor.rgb, _AlbedoFlatten);
                half3 albedo = flat * _BaseColor.rgb;

                half ao = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r;
                ao = lerp(1.0h, ao, _OcclusionStrength);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(input.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light main = GetMainLight(shadowCoord);
                half  ndl  = dot(N, main.direction);
                half  ramp = StylizedRamp(ndl, main.shadowAttenuation * main.distanceAttenuation,
                                          _RampStart, _RampEnd);

                // Ambient is an authored cool colour darkened by AO, not flat SH. The reference
                // study named AO as "the single largest contributor to units reading as solid
                // objects rather than lit shapes", and this is where that lands.
                half3 shade = _ShadeColor.rgb * lerp(1.0h, _AOTint.rgb, (1.0h - ao) * _ShadeStrength);
                half3 lit   = main.color;
                half3 diffuse = albedo * lerp(shade, lit, ramp) * ao;

                // Soft specular with a floor on roughness so it can never pull to a hotspot.
                // _SpecStrength 0 turns it off entirely, which is a legitimate setting here.
                half rough = max(1.0h - _Smoothness, _SpecRoughFloor);
                half3 H = normalize(main.direction + V);
                half  ndh = saturate(dot(N, H));
                half  spec = pow(ndh, max(1.0h, (1.0h - rough) * 48.0h)) * _SpecStrength;
                half3 specColor = lerp(half3(1,1,1), albedo, _Metallic) * main.color;
                half3 color = diffuse + spec * specColor * ramp;

                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light add = GetAdditionalLight(i, input.positionWS);
                    half  addRamp = StylizedRamp(dot(N, add.direction),
                                                 add.shadowAttenuation * add.distanceAttenuation,
                                                 _RampStart, _RampEnd);
                    color += albedo * add.color * addRamp * ao;
                }
                #endif

                // Silhouette. Contour darkens first and rim adds over it, so a unit gets a dark
                // edge against a light board AND a bright edge against a dark one. The reference
                // frames showed both doing work; LTW has had neither.
                half fres    = 1.0h - saturate(dot(N, V));
                half contour = pow(fres, _ContourPower) * _ContourStrength;
                color = lerp(color, _ContourColor.rgb, contour);

                half rim = pow(fres, _RimPower) * _RimStrength;
                color += _RimColor.rgb * rim;

                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Hand-written rather than including URP's ShadowCasterPass.hlsl, which expects the Lit
        // input layout. Units must cast shadows: the contact-shadow blob grounds them, but the
        // cast shadow is what puts them in the same space as each other.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _AlbedoFlatten;
                half   _Smoothness;
                half   _Metallic;
                half   _SpecStrength;
                half   _SpecRoughFloor;
                half4  _ShadeColor;
                half   _ShadeStrength;
                half   _RampStart;
                half   _RampEnd;
                half   _OcclusionStrength;
                half4  _AOTint;
                half4  _RimColor;
                half   _RimPower;
                half   _RimStrength;
                half4  _ContourColor;
                half   _ContourPower;
                half   _ContourStrength;
                half4  _EmissionColor;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct SVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            SVaryings shadowVert (SAttributes input)
            {
                SVaryings output = (SVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 dir = normalize(_LightPosition - positionWS);
                #else
                    float3 dir = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, dir));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 shadowFrag (SVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth and DepthNormals keep the unit visible to URP's depth-based features. Without
        // DepthNormals, screen-space AO cannot see these units at all - which would be a poor
        // outcome for a shader whose whole argument is that AO carries the richness.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DAttributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DVaryings   { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            DVaryings depthVert (DAttributes input)
            {
                DVaryings output = (DVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 depthFrag (DVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex dnVert
            #pragma fragment dnFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct NAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct NVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            NVaryings dnVert (NAttributes input)
            {
                NVaryings output = (NVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 dnFrag (NVaryings input) : SV_Target
            {
                return half4(normalize(input.normalWS) * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
