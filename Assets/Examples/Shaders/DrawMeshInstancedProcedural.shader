Shader "CatDarkGame/GPUInstancingSample/DrawMeshInstancedProcedural"
{
    Properties
    { 
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name  "DrawMeshInstancedProcedural"
            Tags {"LightMode" = "SRPDefaultUnlit"}
            
            Cull back
            
            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ConfigureProcedural
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END
            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            
           #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                struct ObjectInstanceData   
                {
                    float4x4    objectToWorld;      // 64
                    float4      baseColor;          // 16
                };
                StructuredBuffer<ObjectInstanceData> _ObjectInstanceData;
            
                void ConfigureProcedural (){}   // instnacing_options procedual에 참조할 더미 함수
            
                void ConfigureProcedural_Vertex (inout ObjectInstanceData outObjectData)    // vertex stage에서 선언한 지역 변수 참조
                {
                    uint id = unity_InstanceID;
                    ObjectInstanceData objectData = _ObjectInstanceData[id];

                    float4x4 objectToWorld = unity_ObjectToWorld;
                    objectToWorld._11_21_31_41 = float4(objectData.objectToWorld._11_21_31, 0.0f);
                    objectToWorld._12_22_32_42 = float4(objectData.objectToWorld._12_22_32, 0.0f);
                    objectToWorld._13_23_33_43 = float4(objectData.objectToWorld._13_23_33, 0.0f);
                    objectToWorld._14_24_34_44 = float4(objectData.objectToWorld._14_24_34, 1.0f);

                    outObjectData.objectToWorld = objectToWorld;
                }

                void ConfigureProcedural_Fragment (inout ObjectInstanceData outObjectData)    // fragment stage에서 선언한 지역 변수 참조
                {
                    uint id = unity_InstanceID;
                    ObjectInstanceData objectData = _ObjectInstanceData[id];
                    outObjectData.baseColor = objectData.baseColor;
                }

                // UNITY_SETUP_INSTANCE_ID 대신 호출할 디파인 함수 선언
                #define SETUP_INSTANCE_ID_VERTEX(input) UnitySetupInstanceID(UNITY_GET_INSTANCE_ID(input));      \
                                                        ObjectInstanceData instanceData = (ObjectInstanceData)0; \
                                                        ConfigureProcedural_Vertex(instanceData);
            
                #define SETUP_INSTANCE_ID_FRAGMENT(input) UnitySetupInstanceID(UNITY_GET_INSTANCE_ID(input));      \
                                                          ObjectInstanceData instanceData = (ObjectInstanceData)0; \
                                                          ConfigureProcedural_Fragment(instanceData);

            
                // Instance 베리언츠 전용 ObjectToWorld 디파인 함수 선언 
                #define TransformObjectToWorld_Instance(positionOS) mul(instanceData.objectToWorld, float4(positionOS.xyz, 1.0)).xyz
                #define _BaseColor instanceData.baseColor;
            #else
                // Instance 베리언츠 상태가 아닐때 호출되는 디파인 함수
                #define SETUP_INSTANCE_ID_VERTEX(input)
                #define SETUP_INSTANCE_ID_FRAGMENT(input)
                #define TransformObjectToWorld_Instance(positionOS) TransformObjectToWorld(positionOS.xyz)
            #endif
            
            
            struct Attributes
            {
                float4 positionOS    : POSITION;
                float2 uv            : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float2 uv            : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            }; 
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                SETUP_INSTANCE_ID_VERTEX(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4 positionOS = input.positionOS;
                float3 positionWS = TransformObjectToWorld_Instance(positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                output.positionCS = positionCS;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                SETUP_INSTANCE_ID_FRAGMENT(input);
                float2 baseMapUV = input.uv.xy;
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseMapUV);
                half4 finalColor = texColor * _BaseColor;
                return finalColor;
            }
            
            ENDHLSL
        }
    }
}