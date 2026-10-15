Shader "Qingxi/Flat Cleanup UI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Illustration", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float4 world:TEXCOORD1; };
            sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _Color; float4 _ClipRect;
            v2f vert(appdata v) { v2f o; o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*4;
                for(int sx=-1;sx<=1;sx++) for(int sy=-1;sy<=1;sy++)
                    if(sx!=0 || sy!=0) c+=tex2D(_MainTex,i.uv+float2(sx,sy)*_MainTex_TexelSize.xy*2);
                c/=12;
                const float3 palette[8]={float3(.976,.953,.875),float3(.78,.61,.29),float3(.16,.25,.20),float3(.63,.34,.22),
                    float3(.41,.29,.18),float3(.21,.45,.44),float3(.56,.76,.81),float3(.77,.83,.70)};
                float best=100; float3 flat=0;
                for(int p=0;p<8;p++)
                {
                    float3 swatch=palette[p];
                    #ifndef UNITY_COLORSPACE_GAMMA
                    swatch=GammaToLinearSpace(swatch);
                    #endif
                    float distance=dot(c.rgb-swatch,c.rgb-swatch);
                    if(distance<best) { best=distance; flat=swatch; }
                }
                c.rgb=flat*i.color.rgb; c.a*=i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
