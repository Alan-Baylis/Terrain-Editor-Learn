Shader "Hidden/GUITextureClip_ChannelControl"
{
	Properties 
	{ 
		_MainTex ("Texture", Any) = "white" {} 
	}

	SubShader 
	{

		Lighting Off 
		Blend SrcAlpha OneMinusSrcAlpha 
		Cull Off 
		ZWrite Off 
		ZTest Always

		Pass 
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			#pragma shader_feature GRAYSCALE

			struct appdata_t {
				float4 vertex : POSITION;
				half4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				float2 clipUV : TEXCOORD1;
			};

			sampler2D _MainTex;
			sampler2D _GUIClipTexture;

			uniform float4 _MainTex_ST;
			uniform fixed4 _Color;
			uniform float4x4 unity_GUIClipTextureMatrix; // unity_GUIClipTextureMatrix

			uniform float4 tintColor = float4 (1, 1, 1, 1);
			// Ints pointing to the channel to represent: 0-black - 1-red - 2-green - 3-blue - 4-alpha - 5-white
			uniform int shuffleR = 1, shuffleG = 2, shuffleB = 3, shuffleA = 4;

			uniform float _grayscale = 1;
			uniform int _alpha = 1;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				float3 eyePos = UnityObjectToViewPos(v.vertex);
				o.clipUV = mul(unity_GUIClipTextureMatrix, float4(eyePos.xy, 0, 1));

				o.color = v.color;
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}

			half4 shuffleChannels (half4 col, int4 shuffle)
			{
				half channels[6] = { 0, col.r, col.g, col.b, col.a, 1 };
				return half4(channels[shuffle.r], channels[shuffle.g], channels[shuffle.b], channels[shuffle.a]);
			}

			half4 frag (v2f i) : SV_Target
			{
				half4 col = tex2D(_MainTex, i.texcoord);
			#if GRAYSCALE
				col = max (col.r, max (col.g, max (col.b, col.a))) * _grayscale;
				col.a = lerp (1, col.a, _alpha);
			#else
				col = shuffleChannels (col, int4(shuffleR, shuffleG, shuffleB, shuffleA)) * tintColor * i.color * 2;
			#endif
				col.a *= tex2D(_GUIClipTexture, i.clipUV).a;
				return col;
			}
			ENDCG
		}
	}
}
