Shader "Hidden/NodePainter_RTPaint" 
{
	Properties 	
	{
		_RTCanvas ("RTCanvas", Any) = "white" {} 
		_brushTex ("Texture", Any) = "white" {} 
	}

	CGINCLUDE
	#pragma vertex vert
	#include "UnityCG.cginc"
	#include "PaintUtility.cginc"

	#pragma multi_compile __ CALC_BRANCHES


	// Canvas Options
	uniform uint sizeX, sizeY;
	uniform sampler2D_float _RTCanvas;
	uniform float4 _channelMask;

	// Blend Parameters
	uniform sampler2D_float _blendTex;
	uniform int _blendMode; // 0-Add 1-Substract 2-Multiply 3-Divide
	uniform float _blendAmount;

	struct v2f
	{
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	v2f vert (appdata_base v)
	{
		v2f OUT;
		OUT.pos = UnityObjectToClipPos (v.vertex);
		OUT.uv = v.texcoord.xy;
		return OUT;
	}

	ENDCG

	SubShader 
	{
		Pass 
		{ // Paint
			ZTest Always Cull Off ZWrite Off Fog { Mode off }

			CGPROGRAM
			#pragma fragment RTPaint

			// General Painting Options
			uniform float _timeStep;
			uniform float4 _brushPos;

			// Painting paramters
			uniform float4 _color;
			uniform sampler2D _brushTex;
			uniform int _brushType; // 0-Image 1-Gaussian 2-SimpleFalloff 3-InvSquare
			uniform int _brushMode; // 0-Add 1-Substract 2-Multiply 3-Divide
			uniform float _size;
			uniform float _intensity;
			uniform float _falloff;
			uniform float _hardness;
			uniform float _smoothenBias; // 0-SmoothenDetails 4-FlattenArea
			uniform float _targetValue;
			uniform float4x4 _rotationMatrix;

			uniform int _clamp01;
			uniform int _clampStroke;

			float4 RTPaint (v2f IN) : COLOR0
			{
				float4 canvasCol = tex2D (_RTCanvas, IN.uv);
				// Calculate Brush distance
				float2 brushDistance = _brushPos.xy-IN.uv;
				brushDistance.y *= ((float)sizeY/sizeX);
				// Rotate brush
				brushDistance = mul (brushDistance, _rotationMatrix);
				// Calculate Brush value
				float brushValue = sampleBrush (_brushType, _intensity * _timeStep, brushDistance/_size, _falloff, _hardness, _brushTex);
				// Calculate medium incase it's needed
				float4 medium = tex2Dbias (_RTCanvas, float4 (IN.uv.x, IN.uv.y, 0, _smoothenBias));
				// Apply brush on all work channels
				canvasCol = lerp (canvasCol, blendPaint (canvasCol, _color, medium, _brushMode, brushValue, _targetValue), _channelMask);
				// Clamp color
				return clampColor (canvasCol, _clamp01);
			}

			ENDCG
		}

		Pass 
		{ // Single Blend Pass
			ZTest Always Cull Off ZWrite Off Fog { Mode off }

			CGPROGRAM
			#pragma fragment RTBlend

			uniform int _clamp01 = 0;

			float4 RTBlend (v2f IN) : COLOR0
			{
				float4 canvasCol = tex2D (_RTCanvas, IN.uv);
				float4 blendCol = tex2D (_blendTex, IN.uv);
				// Blend
				canvasCol = lerp (canvasCol, blendColor (canvasCol, blendCol, 0, _blendMode, _blendAmount, 1), _channelMask);
				// Clamp
				return clampColor (canvasCol, _clamp01);
			}

			ENDCG
		}

		Pass 
		{ // Modification Pass
			ZTest Always Cull Off ZWrite Off Fog { Mode off }

			CGPROGRAM
			#pragma fragment RTModify
			#pragma multi_compile __ MOD_CHANNEL

			// Modification Parameters
			uniform float _brightness = 0, _contrast = 1;
			uniform float4 _tintColor = float4(1,1,1,1);
			// Channel Modification Parameters
			uniform int shuffleR = 1, shuffleG = 2, shuffleB = 3, shuffleA = 4; // 0-black - 1-r - 2-g - 3-b - 4-a - 5-white
			uniform float4 _channelOffset = float4(0,0,0,0);
			uniform float4 _channelScale = float4(1,1,1,1);

			uniform int _clamp01 = 0;

			float4 RTModify (v2f IN) : COLOR0
			{
				float4 canvasCol = tex2D (_RTCanvas, IN.uv);
				// Modify
				canvasCol = modifyColor (canvasCol, _contrast, _brightness, _tintColor, 
					int4 (shuffleR, shuffleG, shuffleB, shuffleA), _channelScale, _channelOffset);
				// Clamp
				return clampColor (canvasCol, _clamp01);
			}

			ENDCG
		}

		Pass 
		{ // Single Blend and Modification Pass
			ZTest Always Cull Off ZWrite Off Fog { Mode off }

			CGPROGRAM
			#pragma fragment RTBlendMod

			#pragma multi_compile __ MOD_CHANNEL

			// Modification Parameters
			uniform float _brightness = 0, _contrast = 1;
			uniform float4 _tintColor = float4(1,1,1,1);
			// Channel Modification Parameters
			uniform int shuffleR = 1, shuffleG = 2, shuffleB = 3, shuffleA = 4; // 0-black - 1-r - 2-g - 3-b - 4-a - 5-white
			uniform float4 _channelOffset = float4(0,0,0,0);
			uniform float4 _channelScale = float4(1,1,1,1);

			uniform int _clamp01 = 0;

			float4 RTBlendMod (v2f IN) : COLOR0
			{
				float4 canvasCol = tex2D (_RTCanvas, IN.uv);
				float4 blendCol = tex2D (_blendTex, IN.uv);
				// Blend
				canvasCol = lerp (canvasCol, blendColor (canvasCol, blendCol, 0, _blendMode, _blendAmount, 1), _channelMask);
				// Modify
				canvasCol = modifyColor (canvasCol, _contrast, _brightness, _tintColor, 
					int4(shuffleR, shuffleG, shuffleB, shuffleA), _channelScale, _channelOffset);
				// Clamp
				return clampColor (canvasCol, _clamp01);
			}

			ENDCG
		}

		Pass 
		{ // Multi Blend Pass
			ZTest Always Cull Off ZWrite Off Fog { Mode off }

			CGPROGRAM
			#pragma fragment RTBlendMod

			#pragma multi_compile __ ENABLE_TEXTURE_ARRAYS

			uniform int _canvasTexCount, _curTexIndex;
			uniform float4 lastTexInvMask;

		#if ENABLE_TEXTURE_ARRAYS
			UNITY_DECLARE_TEX2DARRAY (_canvasTextures);
			uniform float4 _channelColors[128];
		#else
			uniform sampler2D_float _canvasTex1, _canvasTex2, _canvasTex3, _canvasTex4, _canvasTex5, _canvasTex6;
			uniform float4 _channelColors[24];
		#endif

			float4 addChannelColors (float4 canvasCol, float4 curCanvas, float4 texValue, int texIndex) 
			{
				// Decide on channel source
				int texExists = testGreaterEquals (_canvasTexCount, texIndex+1);
				int isCurTex = testEquals (_curTexIndex, texIndex);
				float4 texChannels = lerp (float4(0,0,0,0), lerp (texValue, curCanvas, isCurTex), texExists);
				// Ignore unused channels
				int lastTex = testEquals (_canvasTexCount, texIndex+1);
				texChannels = lerp (texChannels, 0, lastTexInvMask*lastTex);
				// Replace colors
				canvasCol = lerp (canvasCol, _channelColors[texIndex*4+0], texChannels.r);
				canvasCol = lerp (canvasCol, _channelColors[texIndex*4+1], texChannels.g);
				canvasCol = lerp (canvasCol, _channelColors[texIndex*4+2], texChannels.b);
				canvasCol = lerp (canvasCol, _channelColors[texIndex*4+3], texChannels.a);
				return canvasCol;
			}

			float4 RTBlendMod (v2f IN) : COLOR0
			{
				float4 canvasCol = float4(0, 0, 0, 0);
				float4 curCanvas = tex2D (_RTCanvas, IN.uv);
				// Add channel colors based on source canvas textures
			#if ENABLE_TEXTURE_ARRAYS
					for (int i = 0; i < _canvasTexCount; i++)
						canvasCol = addChannelColors (canvasCol, curCanvas, UNITY_SAMPLE_TEX2DARRAY (_canvasTextures, float3(IN.uv, i)), i);
			#else
					canvasCol = addChannelColors (canvasCol, curCanvas, tex2D (_canvasTex1, IN.uv), 0);
					canvasCol = addChannelColors (canvasCol, curCanvas, tex2D (_canvasTex2, IN.uv), 1);
					canvasCol = addChannelColors (canvasCol, curCanvas, tex2D (_canvasTex3, IN.uv), 2);
					canvasCol = addChannelColors (canvasCol, curCanvas, tex2D (_canvasTex4, IN.uv), 3);
					canvasCol = addChannelColors (canvasCol, curCanvas, tex2D (_canvasTex5, IN.uv), 4);
					canvasCol = addChannelColors (canvasCol, curCanvas, tex2D (_canvasTex6, IN.uv), 5);
			#endif
				return canvasCol;
			}

			ENDCG
		}

		Pass 
		{ // Multi-Canvas Blend and Normalization Pass
			ZTest Always Cull Off ZWrite Off Fog { Mode off }

			CGPROGRAM
			#pragma fragment RTBlendMod

			uniform sampler2D_float _curChannelTex;
			uniform int _curChannelIndex;

			uniform int _clamp01 = 0;

			float4 RTBlendMod (v2f IN) : COLOR0
			{
				float4 canvasCol = tex2D (_RTCanvas, IN.uv);
				// Textures only relevant to the current channel
				float blendValue = tex2D (_blendTex, IN.uv)[_curChannelIndex];
				float curChannel = tex2D (_curChannelTex, IN.uv)[_curChannelIndex];
				// Apply blend on current channel and calculate the difference it made on it
				float curBlended = blendColor (curChannel, blendValue, 0, _blendMode, _blendAmount, 1);
				float curDiff = curBlended - curChannel;
				// Substract the proportional difference from all the other channels
				float4 restBlended = canvasCol - (curDiff * canvasCol);
				// Apply current channel mask to get correct treatment based on channel
				canvasCol = lerp (restBlended, curBlended, _channelMask);
				// Clamp
				return clampColor (canvasCol, _clamp01);
			}

			ENDCG
		}

		Pass 
		{ // Blit with target rect
			ZTest Always Cull Off ZWrite Off Fog { Mode off }

			CGPROGRAM
			#pragma fragment RTExpand

			uniform float4 sourceRect;
			uniform float4 targetRect;

			float4 RTExpand (v2f IN) : COLOR0
			{
				float2 uv = IN.uv;

				uv *= sourceRect.zw;
				uv += sourceRect.xy;

				clamp(uv, 0, 1);

				uv -= targetRect.xy;
				uv /= targetRect.zw;

				return tex2D (_RTCanvas, uv);
			}

			ENDCG
		}
	}
	
	Fallback off
}