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
                float falloff = 1.0 - smoothstep(saturate(1.0 - _Softness), 1.0, radius);
                return fixed4(_Color.rgb, _Color.a * falloff * falloff);
            }
            ENDCG
        }
    }

    FallBack Off
}
