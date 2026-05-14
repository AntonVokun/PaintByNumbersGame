Shader "Custom/SpriteCheckerHighlight"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _CellSize ("Cell Size", Float) = 0.25
        _WhiteColor ("White Color", Color) = (1,1,1,1)
        _GrayColor ("Gray Color", Color) = (0.65,0.65,0.65,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _CellSize;
            fixed4 _WhiteColor;
            fixed4 _GrayColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;

                float4 world = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = world.xy;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, i.uv);

                float2 checkerPos = floor(i.worldPos / _CellSize);
                float checker = fmod(checkerPos.x + checkerPos.y, 2);

                fixed4 finalColor = lerp(_WhiteColor, _GrayColor, checker);
                finalColor.a = sprite.a * i.color.a;

                return finalColor;
            }
            ENDCG
        }
    }
}