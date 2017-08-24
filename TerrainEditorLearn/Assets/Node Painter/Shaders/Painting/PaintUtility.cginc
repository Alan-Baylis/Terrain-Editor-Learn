#ifndef PAINT_UTIL
#define PAINT_UTIL

float gaussian(float sqr, float hardness) 
{
	return max (0, exp(-hardness*sqr));
}

float exponential(float sqr, float hardness) 
{
	return max (0, pow (1-sqr, 7));
}

float simple_falloff(float sqr) 
{
	return max (0, 1-sqrt(sqr));
}

float sqrMgnt(float2 vec) 
{ return abs(vec.x*vec.x + vec.y*vec.y); }
float calcFalloff (float2 dist, float falloff) 
{ return max (0, sqrMgnt (dist) - falloff); }

int testEquals (uniform int value, uniform int target)
{ return max (0, -abs(value-target) + 1); }
int testGreaterEquals (uniform int value, uniform int target)
{ return max (0, min (1, value-target + 1)); }
int testLesserEquals (uniform int value, uniform int target)
{ return max (0, min (1, target-value + 1)); }

float foldThickness01 (float value, float center, float thickness, float factor)
{
	return clamp (-abs(value-center)/thickness*factor + factor, 0, 1);
}

float4 clampColor (float4 col, uniform int clamp01)
{ return lerp (col, clamp (col, 0, 0.99999), clamp (clamp01, 0, 1)); }

float4 shuffleChannels (float4 col, uniform int4 shuffle)
{
	float channels[6] = { col.r, col.g, col.b, col.a, 0, 1 };
	return float4(channels[shuffle.r], channels[shuffle.g], channels[shuffle.b], channels[shuffle.a]);
}

float4 modifyColor (float4 col, uniform float contrast, uniform float brightness, uniform float4 tint, 
					uniform int4 shuffle, uniform float4 chScale, uniform float4 chOffset)
{
#if MOD_CHANNEL
	col = shuffleChannels (col, shuffle) * chScale + chOffset;
#endif
	col = col * contrast + brightness;
	return col * tint;
}

#if CALC_BRANCHES // Calculate all branches instead of branching. Performance-critical

float4 blendPaint (float4 colB, float4 colA, float4 colMed, uniform int blendMode, uniform float intensity, uniform float targetValue)
{
	float4 blendedCol = float4(0,0,0,0);
	blendedCol += testEquals (blendMode, -1) * lerp (colB, colB + colA, intensity); // Add Static
	blendedCol += testEquals (blendMode, 0) * lerp (colB, colB + colA, 0.05f * intensity); // Add
	blendedCol += testEquals (blendMode, 4) * lerp (colB, colA * targetValue, 0.1f * colA * intensity); // Lerp
	blendedCol += testEquals (blendMode, 6) * lerp (colB, colA * targetValue, min (1, max (0, intensity))); // Replace
	blendedCol += testEquals (blendMode, 7) * lerp (colB, colMed, intensity); // Smoothen
	blendedCol += testEquals (blendMode, 8) * lerp (colB, (colB-colMed)*1.1f + colMed, intensity); // Contrast
	return blendedCol;
}

float4 blendColor (float4 colB, float4 colA, float4 colMed, uniform int blendMode, uniform float intensity, uniform float targetValue)
{
	float4 blendedCol = float4(0,0,0,0);
	blendedCol += testEquals (blendMode, -1) * lerp (colB, colB + colA, intensity); // Add Static
	blendedCol += testEquals (blendMode, 0) * lerp (colB, colB + colA, 0.05f * intensity); // Add
	blendedCol += testEquals (blendMode, 1) * lerp (colB, colB - colA, 0.05f * intensity); // Substract
	blendedCol += testEquals (blendMode, 2) * lerp (colB, colB*colA, intensity); // Multiply
	blendedCol += testEquals (blendMode, 3) * lerp (colB, colB/colA, intensity); // Divide
	blendedCol += testEquals (blendMode, 4) * lerp (colB, colA * targetValue, 0.1f * colA * intensity); // Lerp
	blendedCol += testEquals (blendMode, 5) * lerp (colB, ((colA-0.5f)+(colB-0.5f))/2+0.5f, 0.1f * intensity); // Overlay
	blendedCol += testEquals (blendMode, 6) * lerp (colB, colA * targetValue, min (1, max (0, intensity))); // Replace
	blendedCol += testEquals (blendMode, 7) * lerp (colB, colMed, intensity); // Smoothen
	blendedCol += testEquals (blendMode, 8) * lerp (colB, (colB-colMed)*1.1f + colMed, intensity); // Contrast
	return blendedCol;
}

//float4 clampColor (float4 colA, float4 colMax, uniform int clampMode)
//{
//	float4 clampedCol = float4(0,0,0,0);
//	clampedCol += testEquals (clampMode, 0) * colA; // None
//	clampedCol += testEquals (clampMode, 1) * clamp (colA, 0, 0.999999); // Max
//	clampedCol += testEquals (clampMode, 2) * clamp (colA, 0, colMax); // Stroke
//	return clampedCol;
//}

float sampleBrush (uniform int brushType, uniform float intensity, float2 dist, float falloff, float hardness, uniform sampler2D_half brushTex)
{
	float brushValue = 0;
	brushValue += testEquals (brushType, 0) * tex2D (brushTex, float2(0.5, 0.5) + dist).a * intensity;
	brushValue += testEquals (brushType, 1) * gaussian (calcFalloff (dist*4, falloff), hardness) * intensity;
	brushValue += testEquals (brushType, 2) * exponential (calcFalloff (dist*4, falloff), hardness) * intensity;
	return brushValue;
}

#else

float4 blendPaint (float4 colB, float4 colA, float4 colMed, uniform int blendMode, uniform float intensity, uniform float targetValue)
{
	if (blendMode == -1) // Add Static
		return lerp (colB, colB + colA, intensity);
	if (blendMode == 0) // Add
		return lerp (colB, colB + colA, 0.05f * intensity);
	if (blendMode == 4) // Lerp
		return lerp (colB, colA * targetValue, 0.1f * colA * intensity);
	if (blendMode == 6) // Replace
		return lerp (colB, colA * targetValue, min (1, max (0, intensity)));
	if (blendMode == 7) // Smoothen
		return lerp (colB, colMed, intensity);
	if (blendMode == 8) // Contrast
		return lerp (colB, (colB-colMed)*1.1f + colMed, intensity);
	return float4(1, 0, 1, 0);
}

float4 blendColor (float4 colB, float4 colA, float4 colMed, uniform int blendMode, uniform float intensity, uniform float targetValue)
{
	if (blendMode == -1) // Add Static
		return lerp (colB, colB + colA, intensity);
	if (blendMode == 0) // Add
		return lerp (colB, colB + colA, 0.05f * intensity);
	if (blendMode == 1) // Substract
		return lerp (colB, colB - colA, 0.05f * intensity);
	if (blendMode == 2) // Multiply
		return lerp (colB, colB*colA, intensity);
	if (blendMode == 3) // Divide
		return lerp (colB, colB/colA, intensity);
	if (blendMode == 4) // Lerp
		return lerp (colB, colA * targetValue, 0.1f * colA * intensity); 
	if (blendMode == 5) // Overlay
		return lerp (colB, ((colA-0.5f)+(colB-0.5f))/2+0.5f, 0.1f * intensity);
	if (blendMode == 6) // Replace
		return lerp (colB, colA * targetValue, min (1, max (0, intensity)));
	if (blendMode == 7) // Smoothen
		return lerp (colB, colMed, intensity);
	if (blendMode == 8) // Contrast
		return lerp (colB, (colB-colMed)*1.1f + colMed, intensity);
	return float4(1, 0, 1, 0);
}

//float4 clampColor (float4 colA, float4 colMax, uniform int clampMode)
//{
//	if (clampMode == 0) // None
//		return colA;
//	if (clampMode == 1) // Max
//		return clamp (colA, 0, 0.999999);
//	if (clampMode == 2) // Stroke
//		return clamp (colA, 0, colMax);
//	return float4(1, 0, 1, 0);
//}

float sampleBrush (uniform int brushType, uniform float intensity, float2 dist, float falloff, float hardness, uniform sampler2D_half brushTex)
{
	if (brushType == 1) // Gaussian Function
		return gaussian (calcFalloff (dist*4, falloff), hardness) * intensity;
	if (brushType == 2) // Round Function
		return exponential (calcFalloff (dist*4, falloff), hardness) * intensity;
	return tex2D (brushTex, float2(0.5, 0.5) + dist).a * intensity;
}

#endif

#endif