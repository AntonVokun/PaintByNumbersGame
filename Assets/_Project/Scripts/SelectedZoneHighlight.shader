Shader "Custom/SelectedZoneHighlight"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed a=tex2D(_MainTex,i.uv).a;
                float2 dx=ddx(i.uv)*2, dy=ddy(i.uv)*2;
                fixed around=max(max(tex2D(_MainTex,i.uv+dx).a,tex2D(_MainTex,i.uv-dx).a),max(tex2D(_MainTex,i.uv+dy).a,tex2D(_MainTex,i.uv-dy).a));
                float stripe=step(.5,frac((i.vertex.x+i.vertex.y)/12));
                fixed3 fill=lerp(fixed3(0,.68,.78),fixed3(1,1,1),stripe);
                fixed border=saturate(around-a);
                return fixed4(lerp(fill,fixed3(.03,.08,.12),border),max(a*.72,border)*i.color.a);
            }
            ENDCG
        }
    }
}
