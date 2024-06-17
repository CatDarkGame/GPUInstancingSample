Shader "CatDarkGame/GPUInstancingSample/DrawMeshInstanced"
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
            Name  "DrawMeshInstanced"
            Tags {"LightMode" = "SRPDefaultUnlit"}
            
            Cull back
            
            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl" // Core.hlsl 내부에서 Include

            /*CBUFFER_START(UnityPerMaterial)    SRPBatcher CBUFFER
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END*/
            
            UNITY_INSTANCING_BUFFER_START(MyProperties)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
            UNITY_INSTANCING_BUFFER_END(MyProperties)

            #define _BaseColor     UNITY_ACCESS_INSTANCED_PROP(MyProperties, _BaseColor)    // 사용 편의를 위한 변수명 디파인
            #define _BaseMap_ST    UNITY_ACCESS_INSTANCED_PROP(MyProperties, _BaseMap_ST)

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            
            struct Attributes
            {
                float4 positionOS    : POSITION;
                float2 uv            : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID      // InstanceID 선언 디파인
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float2 uv            : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID      // InstanceID 선언 디파인
            }; 
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);                 // Instance 관련 기능 처리 내장 메크로 함수
                UNITY_TRANSFER_INSTANCE_ID(input, output);      // Fragment에 InstanceID 전달 필요할 때 사용.

                float4 positionOS = input.positionOS;
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                output.positionCS = positionCS;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);                 // Instance 관련 기능 처리 내장 메크로 함수
                float2 baseMapUV = input.uv.xy;
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseMapUV);
                
                half4 finalColor = texColor * _BaseColor;
                return finalColor;
            }

         
                        
            ENDHLSL
        }
    }
}