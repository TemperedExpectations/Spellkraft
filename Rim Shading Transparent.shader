Shader "Custom/Rim Shading Transparent"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
		_TintColor ("Tint Color (RGBA)", Color) = (1, 1, 1, 1)

        _RimColor ("Rim Color", Color) = (0,1,0,1)
        _RimPower ("Rim Power", Float) = .5
        _RimFac ("Rim Fac", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "IgnoreProjector"="True" "Queue"="Transparent" "LightMode"="Always" "PreviewType"="Plane" }
        LOD 100

        Pass
        {

            Lighting Off
			ZWrite Off
			Blend SrcAlpha One
			ColorMask RGBA

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
			
			fixed4 _TintColor;
			fixed4 _RimColor;
			half _RimPower, _RimFac;


            struct appdata
            {
                float4 vertex : POSITION;
				float3 normal : NORMAL;
				fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
				float4 worldPos : TEXCOORD1;
				float3 normal : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = (v.color * _TintColor);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.normal = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

				float3 normal = normalize(i.normal * i.vertex);

				float3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
				half rim = 1.0 - saturate(dot (worldViewDir, i.normal));
				float rimWeight =  pow (rim, _RimPower) * _RimFac;

				col = _RimColor * rimWeight + col * (1-rimWeight);

                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
