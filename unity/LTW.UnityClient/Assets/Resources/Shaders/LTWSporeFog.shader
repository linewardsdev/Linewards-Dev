// Drifting spore fog for the Spore Cloud Bloom, sized to the tower's attack range.
//
// Dense at the tower and fading to nothing at the rim, so the fog itself tells the player how
// far the tower reaches. The falloff is computed from UV rather than sampled, for the same
// reason LTWContactShadow does it: no texture asset to author, import, or keep in sync, and
// one instanced quad per tower.
//
// The churn is two counter-phased sine bands rather than real noise. A texture lookup or a
// hash-based noise would both look better standing still, but this is a small, heavily
// alpha-faded disc on a mobile target seen at a fixed camera angle — at that size the bands
// are indistinguishable from noise once they are moving, and they cost a handful of ALU with
// no sampler and no dependent read.
//
// Deliberately ZWrite Off and unlit: fog is not a surface, it should not occlude the tower
// standing in it or take lighting as though it were solid.
Shader "LTW/Spore Fog"
{
    Properties
    {
        _Color ("Color", Color) = (0.45, 0.85, 0.35, 0.30)

        // Fraction of the radius spent fading out. 1 = the gradient starts at the very centre,
        // which is what makes this read as fog rather than as a disc with a soft edge.
        _Softness ("Softness", Range(0.01, 1)) = 0.95

        // How strongly the drifting bands modulate density. 0 is a smooth blob.
        _Churn ("Churn", Range(0, 1)) = 0.55

        _Speed ("Drift Speed", Range(0, 4)) = 0.45
    }

    SubShader
    {
        Tags
        {
            // Transparent-90: above the board and above the ground decals that sit in
            // Transparent-100 (contact shadows), so the fog layers over the lane it covers, but
            // still inside the transparent queue so towers and creeps sort against it normally.
            "Queue" = "Transparent-90"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            half _Softness;
            half _Churn;
            half _Speed;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 offset = i.uv * 2.0 - 1.0;
                float radius = saturate(length(offset));

                // Linear-ish rather than squared (which is what the contact shadow uses to stay a
                // tight blob). Fog wants the gradient spread across the whole radius so the edge
                // is never locatable.
                float falloff = 1.0 - smoothstep(saturate(1.0 - _Softness), 1.0, radius);

                float t = _Time.y * _Speed;
                float band = sin(offset.x * 5.3 + t) * cos(offset.y * 4.7 - t * 0.8)
                           + sin((offset.x + offset.y) * 3.9 + t * 1.3);
                float billow = saturate(0.5 + 0.25 * band);

                // Churn only ever THINS the fog, never thickens it past _Color.a, so the authored
                // alpha stays the ceiling and the tower never flashes brighter than intended.
                float density = lerp(1.0 - _Churn, 1.0, billow);

                return fixed4(_Color.rgb, _Color.a * falloff * density);
            }
            ENDCG
        }
    }

    FallBack Off
}
