using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatDarkGame.GPUIndirectDraw
{
    public class GPUIndirectDrawSample : MonoBehaviour
    {
        public Mesh sharedMesh;
        public Shader instancingShader;
        
        private MeshRenderer[] _targetRenderers;
        
        private InstancingData _instancingData;
        private ComputeBuffer _objectBuffer;
        private ComputeBuffer _materialPropBuffer;
        private ComputeBuffer _meshBuffer;
        private ComputeBuffer _vertexBuffer;
        private ComputeBuffer _vertexIndexBuffer;
        
        private void Awake()
        {
            CollectMeshRenderer();
            Init_InstancingData();
        }

        private void OnDestroy()
        {
            Dispose_InstancingData();
            _targetRenderers = null;
        }
        
        private void Update()
        {
            IndirectDraw();
        }
        
        private void CollectMeshRenderer()
        {
            MeshRenderer[] meshRenderers = transform.GetComponentsInChildren<MeshRenderer>();
            List<MeshRenderer> meshList = new List<MeshRenderer>();
            foreach (var renderer in meshRenderers)
            {
                if(renderer.sharedMaterials.Length>1) continue; // 샘플은 SubMesh 1개만 지원
                meshList.Add(renderer);
            }
            _targetRenderers = meshList.ToArray();
        }
        
        private void Init_InstancingData()
        {
            if (!sharedMesh || !instancingShader || _targetRenderers == null || _targetRenderers.Length < 1) return;
            
            // Setup Data
            int instanceCount = _targetRenderers.Length;
            List<ObjectBuffer> objectBufferList = new List<ObjectBuffer>();
            Dictionary<Material, MaterialPropBuffer> materialPropBufferList = new Dictionary<Material, MaterialPropBuffer>();
            Dictionary<Mesh, MeshBuffer> meshBufferList = new Dictionary<Mesh, MeshBuffer>();
            List<VertexBuffer> vertexBufferList = new List<VertexBuffer>();
            List<uint> vertexIndexBufferList = new List<uint>();
            
            for (int i = 0; i < instanceCount; i++)
            {
                Renderer renderer = _targetRenderers[i];
                Material material = renderer?.sharedMaterial;
                if(!renderer || !material || !IsMaterialBatching(instancingShader, material)) continue;
                MeshFilter meshFilter = renderer?.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter?.sharedMesh;
                if(!mesh) continue;

                int materialPropBufferID = AddMaterialPropBuffer(material, materialPropBufferList);
                int meshBufferCount = meshBufferList.Count;
                int meshBufferID = AddMeshBuffer(mesh, meshBufferList);
                if (meshBufferCount < meshBufferList.Count)
                {
                    AddVertexBuffer(mesh, vertexBufferList);
                    AddVertexIndexBuffer(mesh, vertexIndexBufferList);
                }
                AddObjectBuffer(renderer, meshBufferID, materialPropBufferID, objectBufferList);
       
                renderer.forceRenderingOff = true;
            }
            if (objectBufferList.Count < 1) return;

            // Setup StructuredBuffer
            {
                int bufferCount = Mathf.Max(instanceCount, ThreadGroupSize.x);
                int bufferSize = Marshal.SizeOf<ObjectBuffer>();
                _objectBuffer = new ComputeBuffer(bufferCount, bufferSize, ComputeBufferType.Default);
                _objectBuffer.SetData(objectBufferList.ToArray());
            }
            {
                int bufferCount = materialPropBufferList.Count;
                int bufferSize = Marshal.SizeOf<MaterialPropBuffer>();
                _materialPropBuffer = new ComputeBuffer(bufferCount, bufferSize, ComputeBufferType.Default);
                _materialPropBuffer.SetData(materialPropBufferList.Values.ToArray());
            }
            {
                int bufferCount = meshBufferList.Count;
                int bufferSize = Marshal.SizeOf<MeshBuffer>();
                _meshBuffer = new ComputeBuffer(bufferCount, bufferSize, ComputeBufferType.Default);
                _meshBuffer.SetData(meshBufferList.Values.ToArray());
            }
            {
                int bufferCount = vertexBufferList.Count;
                int bufferSize = Marshal.SizeOf<VertexBuffer>();
                _vertexBuffer = new ComputeBuffer(bufferCount, bufferSize, ComputeBufferType.Default);
                _vertexBuffer.SetData(vertexBufferList.ToArray());
            }
            {
                int bufferCount = vertexIndexBufferList.Count;
                int bufferSize = Marshal.SizeOf<uint>();
                _vertexIndexBuffer = new ComputeBuffer(bufferCount, bufferSize, ComputeBufferType.Default);
                _vertexIndexBuffer.SetData(vertexIndexBufferList.ToArray());
            }
            
            
            // Setup ArgumentBuffer
            int submeshIndex = 0;
            uint[] argDatas = GetIndirectArgumentData(sharedMesh, submeshIndex, vertexIndexBufferList.Count, instanceCount);
            ComputeBuffer argBuffer = new ComputeBuffer(1, sizeof(uint) * argDatas.Length, ComputeBufferType.IndirectArguments);
            argBuffer.SetData(argDatas);
            
            // Setup Material & InstancingData
            Material instanceMaterial = new Material(instancingShader);
            instanceMaterial.enableInstancing = true;
            instanceMaterial.hideFlags = HideFlags.HideAndDontSave;
            instanceMaterial.SetBuffer(StructuredBufferID.ObjectBuffer, _objectBuffer);
            instanceMaterial.SetBuffer(StructuredBufferID.MaterialPropBuffer, _materialPropBuffer);
            instanceMaterial.SetBuffer(StructuredBufferID.MeshBuffer, _meshBuffer);
            instanceMaterial.SetBuffer(StructuredBufferID.VertexBuffer, _vertexBuffer);
            instanceMaterial.SetBuffer(StructuredBufferID.VertexIndexBuffer, _vertexIndexBuffer);
            _instancingData.mesh = sharedMesh;
            _instancingData.material = instanceMaterial;
            _instancingData.argBuffer = argBuffer;
            _instancingData.submeshIndex = submeshIndex;
            _instancingData.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
        }

        private void Dispose_InstancingData()
        {
            _instancingData.Dispose();
            _objectBuffer?.Release();
            _objectBuffer = null;
            _materialPropBuffer?.Release();
            _materialPropBuffer = null;
            _meshBuffer?.Release();
            _meshBuffer = null;
            _vertexBuffer?.Release();
            _vertexBuffer = null;
            _vertexIndexBuffer?.Release();
            _vertexIndexBuffer = null;
        }
        
        private void AddObjectBuffer(Renderer renderer, int meshBufferID, int materialPropBufferID, List<ObjectBuffer> objectBufferList)
        {
            Matrix4x4 objectToWorld = renderer.transform.localToWorldMatrix;
            objectToWorld.m32 = meshBufferID;
            objectToWorld.m33 = materialPropBufferID;
            ObjectBuffer buffer = new ObjectBuffer
            {
                objectToWorld = objectToWorld,
            };
            objectBufferList.Add(buffer);
        }
        
        private int AddMaterialPropBuffer(Material material, Dictionary<Material, MaterialPropBuffer> materialPropBufferList)
        {
            if (material == null || materialPropBufferList == null) return 0;
            int bufferIndex = 0;
            foreach (var buffer in materialPropBufferList)
            {
                if (buffer.Key == material) return bufferIndex;
                bufferIndex++;
            }
            materialPropBufferList.Add(material, GetMaterialPropBufferFromMaterial(material));
            return materialPropBufferList.Count - 1;
        }
        
        private int AddMeshBuffer(Mesh mesh, Dictionary<Mesh, MeshBuffer> meshBufferList)
        {
            if (!mesh || meshBufferList==null) return -1;
            int bufferIndex = 0;
            uint indexTotalCount = 0;
            uint vertexTotalCount = 0;
            foreach (var buffer in meshBufferList)
            {
                if (buffer.Key == mesh) return bufferIndex;
                bufferIndex++;
                Mesh meshKey = buffer.Key;
                indexTotalCount += meshKey.GetIndexCount(0);
                vertexTotalCount += (uint)meshKey.vertexCount;
            }
            
            MeshBuffer meshBuffer = new MeshBuffer
            {
                startIndexLocation = indexTotalCount,
                indexCount = mesh.GetIndexCount(0),
                startVertexLocation = vertexTotalCount,
                dummy_1 = 0,
            };
            meshBufferList.Add(mesh, meshBuffer);
            return meshBufferList.Count - 1;
        }

        private void AddVertexBuffer(Mesh mesh, List<VertexBuffer> vertexBufferList)
        {
            if (!mesh || vertexBufferList==null) return;
            for (int j = 0; j < mesh.vertexCount; j++)
            {
                VertexBuffer buffer = new VertexBuffer
                {
                    position = mesh.vertices[j],
                    normal = (mesh.normals!=null && mesh.normals.Length>0) ? mesh.normals[j] : Vector3.zero,
                    tangent = (mesh.tangents!=null && mesh.tangents.Length>0) ? mesh.tangents[j] : Vector4.zero,
                    uv = (mesh.uv!=null && mesh.uv.Length>0) ? mesh.uv[j] : Vector2.zero,
                };
                vertexBufferList.Add(buffer);
            }
        }
        
        private void AddVertexIndexBuffer(Mesh mesh, List<uint> vertexIndexBufferList)
        {
            if (!mesh || vertexIndexBufferList==null) return;
            for (int i = 0; i < mesh.triangles.Length; i++)
            {
                vertexIndexBufferList.Add((uint)mesh.triangles[i]);
            }
        }

        private MaterialPropBuffer GetMaterialPropBufferFromMaterial(Material material)
        {
            MaterialPropBuffer buffer = new MaterialPropBuffer
            {
                baseColor = ConvertLinearToGamma(material.GetColor(MaterialPropertyID.BaseColor)),
            };
            return buffer;
        }
        
        private void IndirectDraw()
        {
            if (_instancingData.argBuffer==null) return;
            Graphics.DrawMeshInstancedIndirect(_instancingData.mesh, _instancingData.submeshIndex, _instancingData.material, _instancingData.bounds, _instancingData.argBuffer);
        }
        
        private static uint[] GetIndirectArgumentData(Mesh mesh, int submeshIndex, int indexCount, int instanceCount = 0)
        {
            if (!mesh) return null;
            uint[] argsDatas = new uint[] 
            {
                (uint)indexCount,                   // Index Count PerInstance
                (uint)instanceCount,                // Instance Count (Object Count)
                mesh.GetIndexStart(submeshIndex),   // Start Index Location
                mesh.GetBaseVertex(submeshIndex),   // Start Vertex Location
                0,                                  // Start Instance Location
            };
            return argsDatas;
        }
        
        private static bool IsMaterialBatching(Material matA, Material matB)
        {
            if (matA.shader != matB.shader) return false;
            HashSet<string> keywordsA = new HashSet<string>(matA.shaderKeywords);
            HashSet<string> keywordsB = new HashSet<string>(matB.shaderKeywords);
            if (!keywordsA.SetEquals(keywordsB)) return false;
            return true;
        }

        private static bool IsMaterialBatching(Shader targetShader, Material mat)
        {
            if (!mat || !targetShader || 
                targetShader != mat.shader) return false;
            return true;
        }
        
        private static Color ConvertLinearToGamma(Color color)
        {
            color.r = Mathf.Pow(color.r, 2.2f);
            color.g = Mathf.Pow(color.g, 2.2f);
            color.b = Mathf.Pow(color.b, 2.2f);
            return color;
        }
    }
}
