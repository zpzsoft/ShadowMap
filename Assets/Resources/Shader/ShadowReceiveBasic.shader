// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "ShadowMap/ShadowMapReveiverBasic"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "UnityCG.cginc"	
			#include "Assets/Resources/Shader/Inc/ShadowCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
			};

			sampler2D _ShadowDepthTex;
			sampler2D _MainTex;
			matrix _LightViewClipMatrix;
			float4 _MainTex_ST;

			float4 ComputeScreenPosInLight(float4 pos)
			{
				float4 o = pos * 0.5f;
				o.xy = o.xy + o.w;
				o.zw = pos.zw;

				return o;
			}
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				//Comppute in light space.
				float4 posInLight = ComputeScreenPosInLight(mul(_LightViewClipMatrix, float4(i.worldPos, 1)));
				float depth = tex2D(_ShadowDepthTex, posInLight.xy/ posInLight.w).r;
				fixed4 col = tex2D(_MainTex, i.uv);

				//Discard border.
				float white = posInLight.x < 0.0 ? 1.0 : (posInLight.x > 1.0 ? 1.0 : (posInLight.y < 0.0 ? 1.0 : (posInLight.y > 1.0 ? 1.0 : 0.0)));
				if (white > 0.5) depth = 1.f;

				return col * depth;
			}
			ENDCG
		}
	}
}
