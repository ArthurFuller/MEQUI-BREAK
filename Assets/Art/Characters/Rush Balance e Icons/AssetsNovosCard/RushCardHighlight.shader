Shader "UI/RushCardHighlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CardSize ("Card Size", Vector) = (220, 311.32075, 0, 0)
        _HighlightWidth ("Highlight Thickness", Range(1, 12)) = 7
        _BlackWidth ("Black Border Thickness", Range(0, 5)) = 2
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
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
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 localPosition : TEXCOORD1; };
            sampler2D _MainTex;
            float4 _CardSize, _ClipRect;
            float _HighlightWidth, _BlackWidth;

            v2f vert(appdata input)
            {
                v2f output;
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half SpriteAlpha(float2 uv)
            {
                half2 inside = step(0, uv) * step(uv, 1);
                return tex2D(_MainTex, uv).a * inside.x * inside.y;
            }

            half4 frag(v2f input) : SV_Target
            {
                half alpha = SpriteAlpha(input.uv);
                half blackInterior = 1, highlightInterior = 1;
                // Circular offsets in card units keep equal thickness on sides and diagonals.
                float2 unitsToUV = 1.0 / max(_CardSize.xy, float2(1, 1));
                [unroll]
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * (6.28318530718 / 32.0);
                    float2 direction = float2(cos(angle), sin(angle)) * unitsToUV;
                    blackInterior = min(blackInterior, SpriteAlpha(input.uv + direction * _BlackWidth));
                    highlightInterior = min(highlightInterior, SpriteAlpha(input.uv + direction * (_BlackWidth + _HighlightWidth)));
                }
                // Outer black band, then the selectable white/yellow band toward the interior.
                half coverage = 1 - highlightInterior;
                half colorCoverage = max(0, blackInterior - highlightInterior);
                half4 result = half4(input.color.rgb * colorCoverage / max(coverage, .0001), alpha * coverage * input.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
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
