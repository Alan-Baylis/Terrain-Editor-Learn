#ifndef PAINT_PARAMS
#define PAINT_PARAMS

// Canvas Options
uniform uint sizeX, sizeY;
uniform sampler2D_float _RTCanvas, _RTCanvas2;
uniform float4 _channelCol1, _channelCol2, _channelCol3, _channelCol4, _channelCol5, _channelCol6, _channelCol7, _channelCol8;


// General Painting Options
uniform int _isSeperatePass;
uniform float _timeStep;
uniform float4 _brushPos;
uniform float4 _workChannels;

// Painting paramters
uniform float4 _color;
uniform sampler2D_half _brushTex;
uniform int _brushType; // 0-Image 1-Gaussian 2-SimpleFalloff 3-InvSquare
uniform int _brushMode; // 0-Add 1-Substract 2-Multiply 3-Divide
uniform float _size;
uniform float _intensity;
uniform float _falloff;
uniform float _smoothenBias; // 0-SmoothenDetails 4-FlattenArea
uniform int _clampColorMode; // 0-None 1-0to1 2-0toIntensity


// Blend Parameters
uniform int _blendMode; // 0-Add 1-Substract 2-Multiply 3-Divide
uniform float _blendAmount;


// Modification Parameters
uniform int _clamp;
uniform float _brightness, _contrast;
uniform float4 _tintColor;
// Channel Mod Parameters
// Ints pointing to the channel to represent: 0-black - 1-red - 2-green - 3-blue - 4-alpha - 5-white
uniform int shuffleR = 1, shuffleG = 2, shuffleB = 3, shuffleA = 4;
uniform float4 _channelOffset;
uniform float4 _channelScale;

#endif