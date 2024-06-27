#ifndef CATDARKGAME_SIMPLELIT_PASS_FORWARD
#define CATDARKGAME_SIMPLELIT_PASS_FORWARD

#include "SimpleLit_Input.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

Varyings LitPassVertexSimple(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input); 
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    SETUP_VERTEX_INPUT(input);

    float4 positionOS = input.positionOS;
    float3 positionWS = TransformObjectToWorld(positionOS.xyz);
    float4 positionCS = TransformWorldToHClip(positionWS);

    float3 normalOS = input.normalOS;
    float3 normalWS = TransformObjectToWorldNormal(normalOS);
    
    output.positionCS = positionCS;
    output.normalWS = normalWS;
    output.uv = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
    return output;
}

half4 LitPassFragmentSimple(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    Light light = GetMainLight();
    half NdotL = dot(normalWS, light.direction);
    half lambert = saturate(NdotL);//NdotL * 0.5h + 0.5h;
    
    float2 baseMapUV = input.uv.xy;
    half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseMapUV);
    half3 albedo = baseCol.rgb * _BaseColor.rgb;
    half alpha = baseCol.a * _BaseColor.a;

    half3 lightDiffuseColor = lambert * albedo * light.color.rgb;
    
    half4 finalColor = 1.0h;
    finalColor.rgb = lightDiffuseColor;
    finalColor.a = alpha;
    return finalColor;
}

#endif