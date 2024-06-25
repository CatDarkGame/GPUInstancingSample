Shader "CatDarkGame/GPUInstancingSample/DrawMeshInstancedProcedural"
{
    Properties
    { 
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue"="Geometry" "RenderPipeline" = "UniversalPipeline" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
            #include "Instancing.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END
            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            
            #define _BaseColor INSTANCING_PROP(_BaseColor)
            
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
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input); 
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4 positionOS = input.positionOS;
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);

                float3 normalOS = input.normalOS;
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                
                output.positionCS = positionCS;
                output.normalWS = normalWS;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                
                float2 baseMapUV = input.uv.xy;
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseMapUV);
                half3 albedo = baseCol.rgb * _BaseColor.rgb;
                half alpha = baseCol.a * _BaseColor.a;

                Light light = GetMainLight();
                half NdotL = dot(normalWS, light.direction);
                half lambert = NdotL * 0.5h + 0.5h;
                lambert = step(0.5h, lambert);
                half3 indirectDiffuse = SampleSH(normalWS) * albedo;
                half3 lightDiffuseColor = lambert * albedo * light.color.rgb;
                lightDiffuseColor += indirectDiffuse;
                
                half4 finalColor = 1.0h;
                finalColor.rgb = lightDiffuseColor;
                finalColor.a = alpha;
                return finalColor;
            }
            
            ENDHLSL
        }
    }
}