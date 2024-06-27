Shader "CatDarkGame/IndirectDrawSample/SimpleLit"
{
    Properties
    { 
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name  "SimpleLit"
            Tags {"LightMode" = "UniversalForward"}
            
            Cull back
            
            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ConfigureProcedural
            
            #pragma vertex LitPassVertexSimple
            #pragma fragment LitPassFragmentSimple

            #include "HLSL/SimpleLit_ForwardPass.hlsl"

            ENDHLSL
        }
    }
}
