// A sweeping foil highlight, drawn as an ADDITIVE OVERLAY above a card.
//
// It emits only the moving highlight — never the underlying texture — so the art
// beneath shows through untouched and the overlay adds light rather than covering
// it. Built on Unity's UI-Default so it keeps everything a Canvas element needs:
// stencil masking, RectMask2D clipping, vertex tint and CanvasGroup alpha. The
// band is gated by the sampled alpha, so it cannot bleed past rounded corners.
Shader "Coup/UI Card Shine"
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

        [Header(Shine)]
        _ShineColor ("Shine Colour", Color) = (1, 0.96, 0.82, 1)
        _ShineWidth ("Width", Range(0.02, 1)) = 0.22
        _ShineSoftness ("Softness", Range(0.05, 4)) = 0.9
        _ShineAngle ("Angle", Range(-2, 2)) = 0.72
        _ShineIntensity ("Intensity", Range(0, 4)) = 1.1
        _ShineInterval ("Auto Interval (seconds)", Float) = 4
        // Slides the band with the card tilt so the gloss tracks the pointer.
        _ShineShift ("Shift", Range(-0.5, 0.5)) = 0
        // Per-instance phase shift. Without it every card on screen shares the
        // same clock and they all flash in unison, which reads as a glitch.
        _TimeOffset ("Time Offset", Float) = 0
        // Below zero the sweep runs on a timer; at or above zero it is driven
        // explicitly, which is how one-shot sweeps on reveal are triggered.
        _Progress ("Manual Progress", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

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
        // Additive: the band brightens whatever is underneath.
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _ShineColor;
            float _ShineWidth;
            float _ShineSoftness;
            float _ShineAngle;
            float _ShineIntensity;
            float _ShineInterval;
            float _ShineShift;
            float _TimeOffset;
            float _Progress;

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                // The sampled texture is used only as a mask for where the highlight
                // may appear. Its colour is never emitted, so the card shows through.
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                float mask = tex.a * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                mask *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                // A silk body, a hotter core and a thin leading lip. Every layer is
                // the same hue — the core is only brighter — so the foil stays the
                // colour of the card instead of washing out to white.
                float2 dir = normalize(float2(1.0, _ShineAngle));
                float proj = dot(IN.texcoord - 0.5, dir) + 0.5;

                float t = _Progress >= 0.0
                    ? saturate(_Progress)
                    : frac((_Time.y + _TimeOffset) / max(_ShineInterval, 0.0001));

                float centre = lerp(-_ShineWidth, 1.0 + _ShineWidth, t) + _ShineShift;
                float d = abs(proj - centre);
                float width = max(_ShineWidth, 0.0001);

                float body = saturate(1.0 - d / width);
                body = pow(body, max(_ShineSoftness, 0.0001));

                float core = saturate(1.0 - d / (width * 0.28));
                core = pow(core, 1.5);

                float lip = saturate(1.0 - abs(d - width * 0.72) / (width * 0.16));
                lip = pow(lip, 2.0);

                // Kept under a full-white core so the hue is what you see, not a blown-out bar.
                float amount = (body * 0.8 + core * 0.5 + lip * 0.12) * _ShineIntensity;
                amount *= lerp(0.82, 1.0, saturate(1.0 - abs(IN.texcoord.y - 0.5) * 0.7));
                amount = saturate(amount) * mask;

                // The band takes the graphic's own colour. A masked UI element is drawn
                // with a stencil copy of the material, and that copy does not pick up
                // later changes to _ShineColor — the vertex colour does, every frame.
                half3 rgb = IN.color.rgb;

                half4 shine;
                shine.rgb = rgb;
                shine.a = saturate(amount * IN.color.a);

                #ifdef UNITY_UI_ALPHACLIP
                clip (shine.a - 0.001);
                #endif

                return shine;
            }
        ENDCG
        }
    }

    Fallback "UI/Default"
}
