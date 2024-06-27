#ifndef CATDARKGAME_SIMPLELIT_INSTANCING
#define CATDARKGAME_SIMPLELIT_INSTANCING

#include "SimpleLit_InstancingData.hlsl"

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    #define TransformObjectToWorld(positionOS)      TransformObjectToWorld_Instancing(positionOS)
    #define TransformObjectToWorldNormal(normalOS)  TransformObjectToWorldNormal_Instancing(normalOS)
    #define MATERIALPROP_BUFFER_ID                  _ObjectBuffer[unity_InstanceID].objectToWorld._44
    #define MESH_BUFFER_ID                          _ObjectBuffer[unity_InstanceID].objectToWorld._43
    #define INSTANCING_PROP(var)                    _MaterialPropBuffer[MATERIALPROP_BUFFER_ID].var
    #define SETUP_VERTEX_INPUT(vertexInput)         SetupVertexInput(vertexInput)

    StructuredBuffer<ObjectBuffer> _ObjectBuffer;
    StructuredBuffer<MaterialPropBuffer> _MaterialPropBuffer;
    StructuredBuffer<MeshBuffer> _MeshBuffer;
    StructuredBuffer<VertexBuffer> _VertexBuffer;
    StructuredBuffer<uint> _VertexIndexBuffer;

    void ConfigureProcedural (){}

    float3 TransformObjectToWorld_Instancing(float3 positionOS)
    {
        uint id = unity_InstanceID;
        float4x4 objectToWorld = _ObjectBuffer[id].objectToWorld;
        objectToWorld._41_42_43_44 = float4(0.0f, 0.0f, 0.0f, 1.0f);
        
        return mul(objectToWorld, float4(positionOS, 1.0f)).xyz;
    }

    float4x4 TransformWorldToObject_Instancing()
    {
        uint id = unity_InstanceID;
        float4x4 objectToWorld = _ObjectBuffer[id].objectToWorld;
        objectToWorld._41_42_43_44 = float4(0.0f, 0.0f, 0.0f, 1.0f);
        
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

    void SetupVertexInput(inout Attributes vertexInput)
    {
        uint meshID = MESH_BUFFER_ID;
        MeshBuffer meshBuffer = _MeshBuffer[meshID];
        
        uint indexID = min(vertexInput.uv.x, meshBuffer.indexCount - 1);
        indexID += meshBuffer.startIndexLocation;

        uint vertexID = _VertexIndexBuffer[indexID] + meshBuffer.startVertexLocation;
        VertexBuffer vertexBuffer = _VertexBuffer[vertexID];

        vertexInput.positionOS = float4(vertexBuffer.position.xyz, 1);
        vertexInput.normalOS = vertexBuffer.normal;
        vertexInput.uv = float3(vertexBuffer.uv.xy, 1);
    }

#else
    #define INSTANCING_PROP(var) var
    #define SETUP_VERTEX_INPUT(vertexInput)
#endif

#endif








