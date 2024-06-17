using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatDarkGame.GPUInstancingSample
{
    public class DrawMeshInstancedIndirect : MonoBehaviour
    {
        private static readonly int ObjectBufferID = Shader.PropertyToID("_ObjectInstanceData");
        private readonly struct ObjectBuffer
        {
            public readonly Matrix4x4 transformMatrix;
            public readonly Vector4   baseColor;
            public ObjectBuffer(Matrix4x4 transformMatrix, Vector4 baseColor)
            {
                this.transformMatrix = transformMatrix;
                this.baseColor = baseColor;
            }
        }
        
        public Mesh mesh;
        public Material sharedMaterial;
        public int objectCount = 10;
        
        private Material _instanceMaterial;
        private ObjectBuffer[] _objectDatas;
        private ComputeBuffer _objectDataBuffer;
        private ComputeBuffer _indirectArgsBuffer;
        private Bounds _bounds;
    
        private void Awake()
        {
            Init_ObjectData();
            Init_ComputeBuffer();
            
            // Setup InstanceMaterial
            _instanceMaterial = new Material(sharedMaterial);
            _instanceMaterial.enableInstancing = true;
            _instanceMaterial.hideFlags = HideFlags.HideAndDontSave;
            _instanceMaterial.SetBuffer(ObjectBufferID, _objectDataBuffer);
        }

        private void OnDestroy()
        {
            if(_instanceMaterial) DestroyImmediate(_instanceMaterial);
            _instanceMaterial = null;
            _objectDataBuffer?.Release(); 
            _objectDataBuffer = null;
            _indirectArgsBuffer?.Release();    
            _indirectArgsBuffer = null;
            _objectDatas = null;
        }

        private void Update()
        {
            if (!IsNotNullRefs()) return;
            // Graphics.DrawMeshInstancedProcedural(mesh, 0, _instanceMaterial, _bounds, objectCount);
            Graphics.DrawMeshInstancedIndirect(mesh, 0, _instanceMaterial, _bounds, _indirectArgsBuffer);
        }

        private void Init_ObjectData()
        {
            Matrix4x4[] localToWorldMatrixs = CommonUtils.GetRandomLocalToWorldMatrices(objectCount);
            _objectDatas = new ObjectBuffer[objectCount];
            _bounds = new Bounds();
            for (int i = 0; i < objectCount; i++)
            {
                Color color = CommonUtils.GetRandomColor();   // 랜덤 색상 생성
                Vector4 color_Gamma = CommonUtils.ConvertLinearToGamma(color);  // Linear ColorSpace 환경에서 Gamma 변환 필요
                _objectDatas[i] = new ObjectBuffer(localToWorldMatrixs[i], color_Gamma);
                Vector3 position = new Vector3(localToWorldMatrixs[i].m03, localToWorldMatrixs[i].m13, localToWorldMatrixs[i].m23);
                _bounds.Expand(position);
            }
        }

        private void Init_ComputeBuffer()
        {
            int bufferSize = Marshal.SizeOf<ObjectBuffer>();
            _objectDataBuffer = new ComputeBuffer(objectCount, bufferSize);
            _objectDataBuffer.SetData(_objectDatas);
            
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
        }
        
        private bool IsNotNullRefs()
        {
            if (!mesh || 
                !_instanceMaterial || 
                _objectDataBuffer == null ||
                _indirectArgsBuffer == null) return false;
            return true;
        }
    }
}