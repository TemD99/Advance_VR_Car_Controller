Shader "Custom/Red Alpha Gradient FX Multiple" {
Properties {

    _FirstColor("Main Color", Color) = (1,1,1,1)
    _FirstMainTex("Base (RGB) Trans (A)", 2D) = "white" {}
    _FirstAlphaMap("Gradient Transparency Map (R)", 2D) = "white" {}
    _FirstGlowColor("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
    _FirstScrollXSpeed("X Scroll Speed", Float) = 2
    _FirstScrollYSpeed("Y Scroll Speed", Float) = 2

    _SecondColor("Main Color", Color) = (1,1,1,1)
    _SecondMainTex("Base (RGB) Trans (A)", 2D) = "white" {}
    _SecondAlphaMap("Gradient Transparency Map (R)", 2D) = "white" {}
    _SecondGlowColor("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
    _SecondScrollXSpeed("X Scroll Speed", Float) = 2
    _SecondScrollYSpeed("Y Scroll Speed", Float) = 2

    _ThirdColor("Main Color", Color) = (1,1,1,1)
    _ThirdMainTex("Base (RGB) Trans (A)", 2D) = "white" {}
    _ThirdAlphaMap("Gradient Transparency Map (R)", 2D) = "white" {}
    _ThirdGlowColor("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
    _ThirdScrollXSpeed("X Scroll Speed", Float) = 2
    _ThirdScrollYSpeed("Y Scroll Speed", Float) = 2
}
SubShader {
    Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
 
    ZWrite Off
    Cull Off
    Blend SrcAlpha One
 
    Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
 
        sampler2D _FirstMainTex;
        sampler2D _FirstAlphaMap;
        float4 _FirstMainTex_ST;
        float4 _FirstAlphaMap_ST;

        sampler2D _SecondMainTex;
        sampler2D _SecondAlphaMap;
        float4 _SecondMainTex_ST;
        float4 _SecondAlphaMap_ST;

        sampler2D _ThirdMainTex;
        sampler2D _ThirdAlphaMap;
        float4 _ThirdMainTex_ST;
        float4 _ThirdAlphaMap_ST;
 
        fixed4 _FirstColor;
        fixed4 _FirstGlowColor;
        float _FirstScrollXSpeed;
        float _FirstScrollYSpeed;

        fixed4 _SecondColor;
        fixed4 _SecondGlowColor;
        float _SecondScrollXSpeed;
        float _SecondScrollYSpeed;

        fixed4 _ThirdColor;
        fixed4 _ThirdGlowColor;
        float _ThirdScrollXSpeed;
        float _ThirdScrollYSpeed;

        struct v2f {
            float4 pos : SV_POSITION;
            float4 texcoord : TEXCOORD0;
            float4 texcoord1 : TEXCOORD1;
            float4 texcoord2 : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID /// Important for GPU instancing must have, use this to access instanced properties in the fragment shader.
            UNITY_VERTEX_OUTPUT_STEREO
        };
 
        v2f vert(appdata_full v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
            o.texcoord.xy = TRANSFORM_TEX(v.texcoord, _FirstMainTex);
            o.texcoord.zw = TRANSFORM_TEX(v.texcoord, _FirstAlphaMap) + frac(float2(_FirstScrollXSpeed, _FirstScrollYSpeed) * _Time.xx);

            o.texcoord1.xy = TRANSFORM_TEX(v.texcoord1, _SecondMainTex);
            o.texcoord1.zw = TRANSFORM_TEX(v.texcoord1, _SecondAlphaMap) + frac(float2(_SecondScrollXSpeed, _SecondScrollYSpeed) * _Time.xx);

            o.texcoord2.xy = TRANSFORM_TEX(v.texcoord2, _ThirdMainTex);
            o.texcoord2.zw = TRANSFORM_TEX(v.texcoord2, _ThirdAlphaMap) + frac(float2(_ThirdScrollXSpeed, _ThirdScrollYSpeed) * _Time.xx);

            return o;
        }
 
        fixed4 frag(v2f i) : SV_Target
        {
            fixed4 color1 = tex2D(_FirstMainTex, i.texcoord.xy).a;
            fixed4 color2 = tex2D(_FirstAlphaMap, i.texcoord.zw) * color1 * _FirstGlowColor;

            fixed4 color3 = tex2D(_SecondMainTex, i.texcoord1.xy).a;
            fixed4 color4 = tex2D(_SecondAlphaMap, i.texcoord1.zw) * color3 * _SecondGlowColor;

            fixed4 color5 = tex2D(_ThirdMainTex, i.texcoord2.xy).a;
            fixed4 color6 = tex2D(_ThirdAlphaMap, i.texcoord2.zw) * color5 * _ThirdGlowColor;

            //return color6;
            return (color2 + color4 + color6) /*+ (color3 * color4));*/;

            //fixed main_mask1 = tex2D(_FirstMainTex, i.texcoord.xy).a;
            //fixed alpha_mask1 = tex2D(_FirstAlphaMap, i.texcoord.zw).r;

            //fixed main_mask2 = tex2D(_SecondMainTex, i.texcoord1.xy).a;
            //fixed alpha_mask2 = tex2D(_SecondAlphaMap, i.texcoord1.zw).r;
 
            ////return fixed4(_FirstGlowColor.rgb, main_mask * alpha_mask);
            //fixed4 color1 = fixed4(_FirstGlowColor.rgb, main_mask1 * alpha_mask1);
            //fixed4 color2 = fixed4(_SecondGlowColor.rgb, main_mask2 * alpha_mask2);
            //return color1 + color2;
        }
        ENDCG
    }
}
}