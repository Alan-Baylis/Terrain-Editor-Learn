Shader "Projector/BrushOverlay" 
{
	Properties 
	{
		_Color ("Main Color", Color) = (1,1,1,1)
		_Strength ("Strength", Range(0,2)) = 1
		_StrengthCurvature ("Strength Curvature", Range (0,2)) = 1
		
		_OutlineRadius ("Outline Radius", Range (0,1)) = 0.48
		_OutlineThickness ("Outline Thickness", Range(0.002,0.02)) = 0.005

		_brushTex ("Brush Texture", 2D) = "black" {}
		_brushType ("Brush Type (0/1)", int) = 0

		_intensity ("Intensity", Range (0,1)) = 1
		_size ("Size", Range(0,1)) = 1
		_falloff ("Falloff", Range(0,1)) = 0
		_hardness ("Hardness", Range(1,4)) = 1

		sizeX ("CanvasSizeX", int) = 1024
		sizeY ("CanvasSizeY", int) = 1024
	}
	
	Subshader 
	{
		Tags { "Queue"="Transparent" }
		Pass 
		{

			Lighting Off 
			Blend SrcAlpha OneMinusSrcAlpha 
			Cull Off 
			ZWrite Off 
			Offset -1, -1

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "PaintUtility.cginc"

			uniform float4x4 unity_Projector; // unity_Projector

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
			
			
			struct v2f 
			{
				float4 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};
			
			v2f vert (float4 vertex : POSITION)
			{
				v2f o;
				o.pos = UnityObjectToClipPos (vertex);
				o.uv = mul (unity_Projector, vertex);
				return o;
			}
			
			half4 frag (v2f IN) : SV_Target
			{
				// Calculate Brush distance
				float2 brushDistance = float2(0.5, 0.5)-UNITY_PROJ_COORD(IN.uv);
				brushDistance.y *= ((float)sizeY/sizeX);
				// Rotate brush
				brushDistance = mul (brushDistance, _rotationMatrix);
				// Calculate Brush value
				float intensity = lerp (_intensity, -(_intensity-1)*(_intensity-1)+1, _StrengthCurvature);//lerp (_intensity, 1.7389*pow(_intensity,3) - 3.6666*pow(_intensity,2) + 2.9273*_intensity, _StrengthCurvature);//lerp (_intensity, -(_intensity-1)*(_intensity-1)+1, _StrengthCurvature);
				float brushValue = sampleBrush (_brushType, intensity, brushDistance, _falloff, _hardness, _brushTex);

				// Outline
				float thickness = lerp (_OutlineThickness, _OutlineThickness/_size, 0.02);
				float outlineValue = foldThickness01 (length(brushDistance), _OutlineRadius, thickness, 2);// clamp (-abs(length(brushDistance)-_OutlineRadius)/thickness*2 + 2, 0, 1);

				// Mix and tint
				float4 projCol = lerp (brushValue, outlineValue, outlineValue);
				projCol.rgb = lerp (projCol.rgb, projCol.rgb + 1, 1-projCol.a);
				projCol *= _Color;
				projCol.a *= _Strength;
				return projCol;
			}
			ENDCG
		}
	}
}