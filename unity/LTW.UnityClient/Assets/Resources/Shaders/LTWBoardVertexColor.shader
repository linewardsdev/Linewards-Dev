// Lit surface shader that multiplies the mesh's baked vertex colour into albedo.
//
// The board used to be one GameObject per cell/decoration, each carrying its own
// Standard material instance purely to hold a solid colour. Baking those colours into
// vertices lets the whole lane collapse into a single mesh drawn with one shared
// material, which is what this shader exists for. Metallic/Smoothness defaults match
// Unity's Default-Material so the merged board responds to the light rig exactly the
// way the primitive cubes did.
//
// Colours are handed to this shader already lifted by UnityVerticalSliceRenderer's
// BoardSurface() helper, and already decoded to linear by BoardMeshBuilder.ToRenderSpace().
// That decode matters: this project renders in linear colour space, where a colour set
// through Material.color is sRGB-decoded on its way to the GPU but a colour written into
// the vertex stream is not. Baking authored values unchanged renders the board roughly
// twice as bright as the per-object materials it replaced.
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
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma multi_compile_instancing

        struct Input
        {
            float4 color : COLOR;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = IN.color * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
