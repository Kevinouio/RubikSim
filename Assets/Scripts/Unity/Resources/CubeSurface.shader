Shader "RubikSim/ProceduralSurface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Highlight ("Lesson highlight", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 vertex : SV_POSITION; float3 normal : TEXCOORD0; };
            fixed4 _Color;
            float _Highlight;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.normal = UnityObjectToWorldNormal(v.normal); return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float lighting = 0.75 + 0.25 * abs(dot(normalize(i.normal), normalize(float3(0.4, 0.8, 0.6))));
                fixed3 color = lerp(_Color.rgb * lighting, fixed3(0.75, 1, 1), _Highlight * 0.35);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
