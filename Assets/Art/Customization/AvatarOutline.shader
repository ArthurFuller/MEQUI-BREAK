Shader "MequiBreak/UI/AvatarOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 bounds : TEXCOORD1;
                float4 steps : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 bounds : TEXCOORD1;
                float4 steps : TEXCOORD2;
                float4 localPosition : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.localPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv; o.bounds = v.bounds; o.steps = v.steps;
                return o;
            }
            fixed4 SampleSprite(float2 uv, float4 bounds)
            {
                float inside = step(bounds.x, uv.x) * step(uv.x, bounds.z)
                    * step(bounds.y, uv.y) * step(uv.y, bounds.w);
                return (tex2D(_MainTex, clamp(uv, bounds.xy, bounds.zw)) + _TextureSampleAdd) * inside;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sprite = SampleSprite(i.uv, i.bounds);
                fixed alpha = sprite.a;
                // O miolo opaco precisa somente de uma leitura da textura.
                if (alpha < .999)
                {
                    [unroll]
                    for (int n = 0; n < 16; n++)
                    {
                        float angle = n * .3926990817;
                        float2 delta = i.steps.xy * cos(angle) + i.steps.zw * sin(angle);
                        alpha = max(alpha, SampleSprite(i.uv + delta, i.bounds).a);
                    }
                }
                fixed4 result;
                result.rgb = sprite.rgb * i.color.rgb * sprite.a / max(alpha, .0001);
                result.a = alpha * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - .001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
