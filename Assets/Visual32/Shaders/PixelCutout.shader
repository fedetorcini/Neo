Shader "Neo/Visual32/PixelCutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Cutoff ("Opaque pixel threshold", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _Cutoff;
            CBUFFER_END
            struct Input { float3 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Output Vert(Input v)
            {
                Output o; o.position = TransformObjectToHClip(v.position); o.uv = v.uv; o.color = v.color; return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                clip(c.a - _Cutoff); c.a = 1; return c;
            }
            ENDHLSL
        }
    }
}
