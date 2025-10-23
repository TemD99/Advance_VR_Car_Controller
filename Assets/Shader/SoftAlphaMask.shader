Shader "Masked/SoftAlphaMask_VR"
{
    Properties
    {
        _MainTex        ("Main Texture (RGB)", 2D) = "white" {}
        _Color          ("Tint", Color) = (1,1,1,1)
        _MaskTex        ("Mask (Grayscale)", 2D) = "white" {}

        // Edge controls
        _Cutoff         ("Mask Cutoff (0-1)", Range(0,1)) = 0.5

        // Use EITHER Softness (UV) OR SoftnessPixels (pixel units). If both are set, pixels wins.
        _Softness       ("Edge Softness (UV 0-0.2)", Range(0,0.2)) = 0.02
        _SoftnessPixels ("Edge Softness (pixels)", Float) = 0.0

        // Workflow
        _UsePremul      ("Premultiplied Alpha Input (0/1)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // --- VR / Stereo support ---
            // Works with multipass and Single-Pass Instanced (double-wide).
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _MainTex_TexelSize;

            sampler2D _MaskTex;
            float4    _MaskTex_ST;
            float4    _MaskTex_TexelSize;

            float4 _Color;
            float  _Cutoff;
            float  _Softness;
            float  _SoftnessPixels;
            float  _UsePremul;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float2 muv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                o.muv = TRANSFORM_TEX(v.uv, _MaskTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Sample textures
                fixed4 mainCol = tex2D(_MainTex, i.uv) * _Color;
                fixed  maskVal = tex2D(_MaskTex, i.muv).r;

                // Compute softness in UV units.
                // If _SoftnessPixels > 0, convert pixels -> UV using mask texel size.
                float softnessUV = _Softness;
                if (_SoftnessPixels > 0.0)
                {
                    // Use X texel size (assumes roughly square pixels); choose mask so the edge follows mask resolution.
                    softnessUV = _SoftnessPixels * _MaskTex_TexelSize.x;
                }

                // Anti-aliased edge via smoothstep
                fixed a = smoothstep(_Cutoff - softnessUV, _Cutoff + softnessUV, maskVal);

                // Premultiplied vs straight alpha workflows
                if (_UsePremul > 0.5)
                {
                    mainCol.a *= a;
                    return mainCol;
                }
                else
                {
                    mainCol.rgb *= a;
                    mainCol.a   *= a;
                    return mainCol;
                }
            }
            ENDCG
        }
    }

    FallBack Off
}
