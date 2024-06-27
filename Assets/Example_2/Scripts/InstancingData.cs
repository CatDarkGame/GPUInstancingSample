using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatDarkGame.GPUIndirectDraw
{
    public static class ThreadGroupSize
    {
        public static readonly int x = 1;
        public static readonly int y = 1;
        public static readonly int z = 1;
    }
    
    public static class StructuredBufferID
    {
        public static readonly int ObjectBuffer = Shader.PropertyToID("_ObjectBuffer");
        public static readonly int MaterialPropBuffer = Shader.PropertyToID("_MaterialPropBuffer");
        public static readonly int MeshBuffer = Shader.PropertyToID("_MeshBuffer");
        public static readonly int VertexBuffer = Shader.PropertyToID("_VertexBuffer");
        public static readonly int VertexIndexBuffer = Shader.PropertyToID("_VertexIndexBuffer");
    }

    public static class MaterialPropertyID
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    }
    
    public struct ObjectBuffer
    {
        public Matrix4x4 objectToWorld;
    }
    
    public struct MaterialPropBuffer
    {
        public Vector4 baseColor; 
    };
    
    public struct MeshBuffer
    {
        public uint startIndexLocation;    
        public uint indexCount;            
        public uint startVertexLocation;           
        public uint dummy_1;               
    };
    
    public struct VertexBuffer
    {
        public Vector3 position;    
        public Vector3 normal;      
        public Vector4 tangent;     
        public Vector2 uv;          
    };

    [SerializeField]
    public struct InstancingData
    {
        public Mesh mesh;
        public int submeshIndex;
        public Material material;
        public Bounds bounds;
        public ComputeBuffer argBuffer;

        public void Dispose()
        {
            mesh = null;
            if (material) CoreUtils.Destroy(material);
            material = null;
            argBuffer?.Release();
            argBuffer = null;
        }
    }
    
}