#ifndef CATDARKGAME_GPUINSTANCINGSAMPLE_INSTANCING
#define CATDARKGAME_GPUINSTANCINGSAMPLE_INSTANCING

#include "InstancingData.hlsl"

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    #define TransformObjectToWorld(positionOS)      TransformObjectToWorld_Instancing(positionOS)
    #define TransformObjectToWorldNormal(normalOS)  TransformObjectToWorldNormal_Instancing(normalOS)
    #define INSTANCING_PROP(var)                    _ObjectBuffer[unity_InstanceID].var

    StructuredBuffer<ObjectBuffer> _ObjectBuffer;

    void ConfigureProcedural (){}

    float3 TransformObjectToWorld_Instancing(float3 positionOS)
    {
        uint id = unity_InstanceID;
        float4x4 objectToWorld = _ObjectBuffer[id].objectToWorld;
 
        return mul(objectToWorld, float4(positionOS, 1.0f)).xyz;
    }

    float4x4 TransformWorldToObject_Instancing()
    {
        uint id = unity_InstanceID;
        float4x4 objectToWorld = _ObjectBuffer[id].objectToWorld;
   
        float3x3 worldToObject3x3;
        worldToObject3x3[0] = objectToWorld[1].yzx * objectToWorld[2].zxy - objectToWorld[1].zxy * objectToWorld[2].yzx;
        worldToObject3x3[1] = objectToWorld[0].zxy * objectToWorld[2].yzx - objectToWorld[0].yzx * objectToWorld[2].zxy;
        worldToObject3x3[2] = objectToWorld[0].yzx * objectToWorld[1].zxy - objectToWorld[0].zxy * objectToWorld[1].yzx;
        float det = dot(objectToWorld[0].xyz, worldToObject3x3[0]);
        worldToObject3x3 = transpose(worldToObject3x3);
        worldToObject3x3 *= rcp(det);

        float3 worldToObjectPosition = mul(worldToObject3x3, -objectToWorld._14_24_34);
        float4x4 worldToObject;
        worldToObject._11_21_31_41 = float4(worldToObject3x3._11_21_31, 0.0f);
        worldToObject._12_22_32_42 = float4(worldToObject3x3._12_22_32, 0.0f);
        worldToObject._13_23_33_43 = float4(worldToObject3x3._13_23_33, 0.0f);
        worldToObject._14_24_34_44 = float4(worldToObjectPosition, 1.0f);
        return worldToObject;
    }

    float3 TransformObjectToWorldNormal_Instancing(float3 normalOS, bool doNormalize = true)
    {
        #ifdef UNITY_ASSUME_UNIFORM_SCALING
            return TransformObjectToWorldDir(normalOS, doNormalize);
        #else
        float3 normalWS = mul(normalOS, (float3x3)TransformWorldToObject_Instancing());
        if (doNormalize) return SafeNormalize(normalWS);
        return normalWS;
        #endif
    }

#else
    #define INSTANCING_PROP(var) var

#endif

#endif



