#ifndef CATDARKGAME_SIMPLELIT_INPUT
#define CATDARKGAME_SIMPLELIT_INPUT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float4 positionOS    : POSITION;
    float3 normalOS      : NORMAL;
    float2 uv            : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS    : SV_POSITION;
    float3 normalWS      : normalWS;
    float2 uv            : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
}; 

#include "SimpleLit_Instancing.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
CBUFFER_END
TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);

#define _BaseColor INSTANCING_PROP(_BaseColor)

#endif