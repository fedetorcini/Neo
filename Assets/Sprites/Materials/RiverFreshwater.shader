Shader "Neo/River Freshwater"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment FreshwaterFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float4 _MainTex_TexelSize;

            fixed4 FreshwaterFrag(v2f input) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(input.texcoord);
                // Only the dedicated water sheet is remapped; other terrain atlases
                // on the same Tilemap keep their original palette. Banks stay brown.
                bool waterSheet = abs(_MainTex_TexelSize.z - 512) < .5 && abs(_MainTex_TexelSize.w - 192) < .5;
                if (waterSheet && c.b > c.r * 1.5 && c.b > c.g * 1.15)
                    c.rgb = saturate(float3(c.r * .7, c.g * 1.6 + .10, c.b * 1.25 + .10));
                c *= input.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
