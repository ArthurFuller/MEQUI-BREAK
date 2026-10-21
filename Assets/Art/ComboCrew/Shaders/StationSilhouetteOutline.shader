Shader "UI/ComboCrewStationSilhouetteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (1, .97, .83, 1)
        _SourceUV ("Sprite UV Bounds", Vector) = (0, 0, 1, 1)
        _SampleStep ("Outline Radius In UV", Vector) = (.01, .01, 0, 0)
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
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
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
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _Color;
            float4 _OutlineColor;
            float4 _SourceUV;
            float4 _SampleStep;
            float4 _ClipRect;

            v2f vert(appdata input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            half SpriteAlpha(float2 uv)
            {
                half2 inside = step(_SourceUV.xy, uv) * step(uv, _SourceUV.zw);
                return tex2D(_MainTex, uv).a * inside.x * inside.y;
            }

            half4 frag(v2f input) : SV_Target
            {
                float2 x = float2(_SampleStep.x, 0);
                float2 y = float2(0, _SampleStep.y);
                float2 diagonal = _SampleStep.xy * .70710678;
                half nearby = SpriteAlpha(input.uv + x);
                nearby = min(nearby, SpriteAlpha(input.uv - x));
                nearby = min(nearby, SpriteAlpha(input.uv + y));
                nearby = min(nearby, SpriteAlpha(input.uv - y));
                nearby = min(nearby, SpriteAlpha(input.uv + diagonal));
                nearby = min(nearby, SpriteAlpha(input.uv - diagonal));
                nearby = min(nearby, SpriteAlpha(input.uv + float2(diagonal.x, -diagonal.y)));
                nearby = min(nearby, SpriteAlpha(input.uv + float2(-diagonal.x, diagonal.y)));
                half edge = SpriteAlpha(input.uv) * (1 - nearby);
                half4 result = _OutlineColor * input.color;
                result.a *= edge;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
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
