// Lane pressure gauge. Replaces a cube that grew along one axis to show fill level —
// growing/shrinking geometry every frame defeats static batching and reads as "stacked
// cube geometry" rather than a gauge. This shader keeps the mesh a fixed size and expresses
// fill as an emissive threshold along the mesh's U coordinate instead.
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _BackgroundColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _Fill)
            UNITY_INSTANCING_BUFFER_END(Props)

            half _EdgeSoftness;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 filled = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                fixed4 empty = UNITY_ACCESS_INSTANCED_PROP(Props, _BackgroundColor);
                half fill = UNITY_ACCESS_INSTANCED_PROP(Props, _Fill);
                half t = smoothstep(fill - _EdgeSoftness, fill + _EdgeSoftness, i.uv.x);
                return lerp(filled, empty, t);
            }
            ENDCG
        }
    }

    FallBack Off
}
