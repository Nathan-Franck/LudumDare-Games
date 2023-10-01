Shader "Hidden/Overlay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float _ScreenBlackout;
            float _ChromaticAberration;
            float _CRT_Distortion;
            sampler2D _MainTex;

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv + _CRT_Distortion * sin(i.uv.y * 300) * float2(0.002, 0);
                fixed r = tex2D(_MainTex, uv + _ChromaticAberration * float2(0.002, 0)).r;
                fixed g = tex2D(_MainTex, uv + _ChromaticAberration * float2(0, 0.002)).g;
                fixed b = tex2D(_MainTex, uv + _ChromaticAberration * float2(-0.002, 0)).b;
                fixed4 col = fixed4(r, g, b, 1);
                col.rgb -= _ScreenBlackout;
                return col;
            }
            ENDCG
        }
    }
}
