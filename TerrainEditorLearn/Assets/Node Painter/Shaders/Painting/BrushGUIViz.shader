Shader "Hidden/BrushGUIViz"
{
	Properties 
	{
		_Color ("Main Color", Color) = (1,1,1,1)
		_Strength ("Strength", float) = 1
		_StrengthCurvature ("Strength Curvature", Range (0,2)) = 1
		
		_OutlineRadius ("Outline Radius", Range (0,1)) = 0.48
		_OutlineThickness ("Outline Thickness", Range(0.002,0.02)) = 0.002

		_brushTex ("Brush Texture", 2D) = "black" {}
		_brushType ("Brush Type (0/1)", int) = 0

		_intensity ("Intensity", float) = 1
		_size ("Size", float) = 1
		_falloff ("Falloff", float) = 0
		_hardness ("Hardness", float) = 1

		sizeX ("CanvasSizeX", int) = 1024
		sizeY ("CanvasSizeY", int) = 1024
	}

	SubShader 
	{
		Tags { "ForceSupported" = "True" }

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
			#include "PaintUtility.cginc"

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

			uniform float4x4 unity_GUIClipTextureMatrix; // unity_GUIClipTextureMatrix
			sampler2D _GUIClipTexture;
			
			uniform half4 _Color;
			uniform float _Strength;
			uniform float _StrengthCurvature;
			
			uniform float _OutlineRadius;
			uniform float _OutlineThickness;
			

			uniform uint sizeX, sizeY;

			// Brush Settings
			uniform sampler2D _brushTex;
			uniform int _brushType; // 0-Image 1-Gaussian 2-SimpleFalloff 3-InvSquare
			uniform float _size;
			uniform float _intensity;
			uniform float _falloff;
			uniform float _hardness;
			uniform float _targetValue;
			uniform float4x4 _rotationMatrix;
			

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				float3 eyePos = UnityObjectToViewPos(v.vertex);
				o.clipUV = mul(unity_GUIClipTextureMatrix, float4(eyePos.xy, 0, 1));

				o.color = v.color;
				o.texcoord = v.texcoord;
				return o;
			}

			half4 frag (v2f i) : SV_Target
			{
				// Calculate Brush distance
				float2 brushDistance = float2(0.5, 0.5)-i.texcoord.xy;
				brushDistance.y *= ((float)sizeY/sizeX);
				// Rotate brush
				brushDistance = mul (brushDistance, _rotationMatrix);
				// Calculate Brush value
				float intensity = lerp (_intensity, -(_intensity-1)*(_intensity-1)+1, _StrengthCurvature);//lerp (_intensity, 1.7389*pow(_intensity,3) - 3.6666*pow(_intensity,2) + 2.9273*_intensity, _StrengthCurvature);//lerp (_intensity, -(_intensity-1)*(_intensity-1)+1, _StrengthCurvature);
				float brushValue = sampleBrush (_brushType, intensity, brushDistance, _falloff, _hardness, _brushTex);

				// Outline
				float thickness = _OutlineThickness/_size;
				float outlineValue = foldThickness01 (length(brushDistance), _OutlineRadius, thickness, 2);//clamp (-abs(length(brushDistance)-_OutlineRadius)/thickness*2 + 2, 0, 1);

				// Mix and tint
				float4 col = lerp (brushValue, outlineValue, outlineValue);
				col *= _Color;
				col.a *= _Strength;
				col.a *= tex2D(_GUIClipTexture, i.clipUV).a;
				return col;
			}
			ENDCG
		}
	}
}
