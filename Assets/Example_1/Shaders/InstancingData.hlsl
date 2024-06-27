#ifndef CATDARKGAME_GPUINSTANCINGSAMPLE_INSTANCING_DATA
#define CATDARKGAME_GPUINSTANCINGSAMPLE_INSTANCING_DATA

struct ObjectBuffer
{
    float4x4    objectToWorld;  // 64
    float4      _BaseColor;     // 16
};

#endif