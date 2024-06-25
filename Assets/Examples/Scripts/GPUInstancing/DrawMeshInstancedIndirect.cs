using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatDarkGame.GPUInstancingSample
{
    public class DrawMeshInstancedIndirect : MonoBehaviour
    {
        private static readonly int ObjectBufferID = Shader.PropertyToID("_ObjectBuffer");
        private struct ObjectBuffer
        {
            public Matrix4x4 objectToWorld;
            public Vector4   baseColor;
        }
        
        public Mesh mesh;
        public Material sharedMaterial;
        public int objectCount = 10;
        
        private Material _instanceMaterial;
        private ComputeBuffer _objectBuffer;
        private ComputeBuffer _indirectArgsBuffer;
        private Bounds _bounds;
    
        private void Awake()
        {
            Init_InstancingData();
        }

        private void OnDestroy()
        {
            if(_instanceMaterial) DestroyImmediate(_instanceMaterial);
            _instanceMaterial = null;
            _objectBuffer?.Release(); 
            _objectBuffer = null;
            _indirectArgsBuffer?.Release();    
            _indirectArgsBuffer = null;
        }
        
        private void Init_InstancingData()
        {
            // Setup Data
            Matrix4x4[] localToWorldMatrixs = CommonUtils.GetRandomLocalToWorldMatrices(objectCount);
            _bounds = new Bounds();
            ObjectBuffer[] objectBufferData = new ObjectBuffer[objectCount];
            for (int i = 0; i < objectCount; i++)
            {
                Color color = CommonUtils.GetRandomColor();   // 랜덤 색상 생성
                Vector4 color_Gamma = CommonUtils.ConvertLinearToGamma(color);  // Linear ColorSpace 환경에서 Gamma 변환 필요
                objectBufferData[i] = new ObjectBuffer 
                {
                    objectToWorld = localToWorldMatrixs[i], 
                    baseColor = color_Gamma
                };
                Vector3 position = new Vector3(localToWorldMatrixs[i].m03, localToWorldMatrixs[i].m13, localToWorldMatrixs[i].m23);
                _bounds.Expand(position);
            }
            
            // Setup StructuredBuffer
            int bufferSize = Marshal.SizeOf<ObjectBuffer>();
            _objectBuffer = new ComputeBuffer(objectCount, bufferSize);
            _objectBuffer.SetData(objectBufferData);
            
            // Setup ArgumentBuffer
            int subMeshIndex = 0;
            uint[] indirectArgsDatas = new uint[] 
            {
                mesh.GetIndexCount(subMeshIndex),   // Index Count PerInstance
                (uint)objectCount,                  // Instance Count (Object Count)
                mesh.GetIndexStart(subMeshIndex),   // Start Index Location
                mesh.GetBaseVertex(subMeshIndex),   // Start Vertex Location
                0,                                  // Start Instance Location
            };
            _indirectArgsBuffer = new ComputeBuffer(1, sizeof(uint) * indirectArgsDatas.Length, ComputeBufferType.IndirectArguments);
            _indirectArgsBuffer.SetData(indirectArgsDatas);
            
            // Setup Material
            _instanceMaterial = new Material(sharedMaterial);
            _instanceMaterial.enableInstancing = true;
            _instanceMaterial.hideFlags = HideFlags.HideAndDontSave;
            _instanceMaterial.SetBuffer(ObjectBufferID, _objectBuffer);
        }

        private void Update()
        {
            if (!IsNotNullRefs()) return;
            Graphics.DrawMeshInstancedIndirect(mesh, 0, _instanceMaterial, _bounds, _indirectArgsBuffer);
        }
        
        private bool IsNotNullRefs()
        {
            if (!mesh || 
                !_instanceMaterial || 
                _objectBuffer == null ||
                _indirectArgsBuffer == null) return false;
            return true;
        }
    }
}