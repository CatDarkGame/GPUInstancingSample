#ifndef CATDARKGAME_SIMPLELIT_INSTANCING_DATA
#define CATDARKGAME_SIMPLELIT_INSTANCING_DATA

struct ObjectBuffer
{
    float4x4    objectToWorld;  // 64
};

struct MaterialPropBuffer
{
    float4      _BaseColor;     // 16
};

struct MeshBuffer
{
    uint startIndexLocation;    // 4
    uint indexCount;            // 4
    uint startVertexLocation;   // 4
    uint dummy_1;               // 4
};

struct VertexBuffer
{
    float3 position;    // 12
    float3 normal;      // 12
    float4 tangent;     // 16
    float2 uv;          // 8
};

#endif