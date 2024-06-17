using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatDarkGame.GPUInstancingSample
{
    public class DrawMeshInstancedIndirect_Culling : MonoBehaviour
    {
        private static class ShaderPropertyID
        {
            public static readonly int ObjectBufferID = Shader.PropertyToID("_ObjectInstanceData");
        }
        
        private static class ComputePropertyID
        {
            public static readonly string KernelName = "CSSortingBufferIndex";
            public static readonly Vector3Int ThreadGroupSize = new Vector3Int(32, 1, 1);
            
            public static readonly int ObjectDataBufferID = Shader.PropertyToID("_ObjectDataBuffer");
            public static readonly int ObjectSharedBufferID = Shader.PropertyToID("_ObjectSharedBuffer");
            public static readonly int VisibleIndexBufferID = Shader.PropertyToID("_VisibleIndexBuffer");
            public static readonly int IndirectArgsBufferID = Shader.PropertyToID("_IndirectArgsBuffer");
            public static readonly int VisibleCountID = Shader.PropertyToID("_VisibleCount");
        }
        
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
        
        private readonly struct BoundsData
        {
            public readonly int objectBufferIndex;
            public readonly Bounds bounds;
            public BoundsData(int objectBufferIndex, Bounds bounds)
            {
                this.objectBufferIndex = objectBufferIndex;
                this.bounds = bounds;
            }
        }
        
        public Mesh mesh;
        public Material sharedMaterial;
        public int objectCount = 10;
        public ComputeShader appendBufferCompute;
        public Camera targetCamera;
        
        private Material _instanceMaterial;
        private ObjectBuffer[] _objectDatas;
        private ComputeBuffer _objectDataBuffer;    // 오브젝트 전체 데이터 버퍼
        private ComputeBuffer _objectSharedBuffer;  // 드로우 오브젝트 데이터 버퍼 (InFrustum)
        private ComputeBuffer _visibleIndexBuffer;  // 드로우 오브젝트 인덱스 버퍼 (InFrustum)
        private ComputeBuffer _indirectArgsBuffer;  // Argument 버퍼
        private Bounds _bounds;
        private BoundsData[] _boundsDatas;
        private Plane[] _frustumPlanes;
        private int _appendBufferComputeKernelId;
        private int _bufferCount;
        
        private void Awake()
        {
            Init_ObjectData();
            Init_ComputeBuffer();
           
            // Setup InstanceMaterial
            _instanceMaterial = new Material(sharedMaterial);
            _instanceMaterial.enableInstancing = true;
            _instanceMaterial.hideFlags = HideFlags.HideAndDontSave;
            _instanceMaterial.SetBuffer(ShaderPropertyID.ObjectBufferID, _objectSharedBuffer);
        }

        private void OnDestroy()
        {
            if(_instanceMaterial) DestroyImmediate(_instanceMaterial);
            _instanceMaterial = null;
            _objectDataBuffer?.Release(); 
            _objectDataBuffer = null;
            _objectSharedBuffer?.Release();
            _objectSharedBuffer = null;
            _visibleIndexBuffer?.Release();
            _visibleIndexBuffer = null;
            _indirectArgsBuffer?.Release();
            _indirectArgsBuffer = null;
            _objectDatas = null;
            _frustumPlanes = null;
            _boundsDatas = null;
        }

        private void Update()
        {
            if (!IsNotNullRefs()) return;
            int visibleCount = FrustumCulling(out uint[] visibleInstances);
            if (visibleCount <= 0) return;
            Update_VisibleBuffer(visibleInstances, visibleCount);
            Graphics.DrawMeshInstancedIndirect(mesh, 0, _instanceMaterial, _bounds, _indirectArgsBuffer);
        }

        private void Init_ObjectData()
        {
            Vector3 boundsSizePerObject = Vector3.one * 1.0f;
            Matrix4x4[] localToWorldMatrixs = CommonUtils.GetRandomLocalToWorldMatrices(objectCount);
            List<BoundsData> boundsDataList = new List<BoundsData>();
            _objectDatas = new ObjectBuffer[objectCount];
            _bounds = new Bounds();
            for (int i = 0; i < objectCount; i++)
            {
                Color color = CommonUtils.GetRandomColor();   // 랜덤 색상 생성
                Vector4 color_Gamma = CommonUtils.ConvertLinearToGamma(color);  // Linear ColorSpace 환경에서 Gamma 변환 필요
                _objectDatas[i] = new ObjectBuffer(localToWorldMatrixs[i], color_Gamma);
                Vector3 position = new Vector3(localToWorldMatrixs[i].m03, localToWorldMatrixs[i].m13, localToWorldMatrixs[i].m23);
                _bounds.Expand(position);
                Bounds bounds = new Bounds(position, boundsSizePerObject);
                boundsDataList.Add(new BoundsData(i, bounds));
            }
            _boundsDatas = boundsDataList.ToArray();
        }

        private void Init_ComputeBuffer()
        {
            int bufferSize = Marshal.SizeOf<ObjectBuffer>();
            // ComputeShader ThreadGroup 최소 사이즈 예외 처리
            _bufferCount = Mathf.Max(objectCount, ComputePropertyID.ThreadGroupSize.x);
            uint[] argDataIdentity = GetArgDataIdentity(mesh, 0);
            _appendBufferComputeKernelId = appendBufferCompute.FindKernel(ComputePropertyID.KernelName);
            
            _objectDataBuffer = new ComputeBuffer(_bufferCount, bufferSize, ComputeBufferType.Default);
            _objectSharedBuffer = new ComputeBuffer(_bufferCount, bufferSize, ComputeBufferType.Default);
            _visibleIndexBuffer = new ComputeBuffer(_bufferCount, sizeof(uint), ComputeBufferType.Default);
            _indirectArgsBuffer = new ComputeBuffer(1, sizeof(uint) * argDataIdentity.Length, ComputeBufferType.IndirectArguments);
            _objectDataBuffer.SetData(_objectDatas);
            _indirectArgsBuffer.SetData(argDataIdentity);
            appendBufferCompute.SetBuffer(_appendBufferComputeKernelId, ComputePropertyID.ObjectDataBufferID, _objectDataBuffer);
            appendBufferCompute.SetBuffer(_appendBufferComputeKernelId, ComputePropertyID.ObjectSharedBufferID, _objectSharedBuffer);
            appendBufferCompute.SetBuffer(_appendBufferComputeKernelId, ComputePropertyID.VisibleIndexBufferID, _visibleIndexBuffer);
            appendBufferCompute.SetBuffer(_appendBufferComputeKernelId, ComputePropertyID.IndirectArgsBufferID, _indirectArgsBuffer);
        }
        
        private int FrustumCulling(out uint[] visibleInstances)
        {
            if (!targetCamera)
            {
                visibleInstances = null;
                return 0;
            }
            _frustumPlanes = GeometryUtility.CalculateFrustumPlanes(targetCamera);
            visibleInstances = new uint[objectCount];
            int visibleCount = 0;
            for (int i = 0; i < objectCount; i++)
            {
                Bounds bounds = _boundsDatas[i].bounds;
                if (GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                {
                    visibleInstances[visibleCount++] = (uint)i;
                }
            }
            return visibleCount;
        }

        private void Update_VisibleBuffer(uint[] visibleInstances, int visibleCount)
        {
            if (visibleCount <= 0 || visibleInstances == null || visibleInstances.Length <= 0) return;
            _visibleIndexBuffer.SetData(visibleInstances, 0, 0, visibleCount);
            appendBufferCompute.SetInt(ComputePropertyID.VisibleCountID, visibleCount);
            appendBufferCompute.Dispatch(_appendBufferComputeKernelId, _bufferCount / ComputePropertyID.ThreadGroupSize.x, ComputePropertyID.ThreadGroupSize.y, ComputePropertyID.ThreadGroupSize.z);
        }
        
        private bool IsNotNullRefs()
        {
            if (!mesh || 
                !_instanceMaterial || 
                _objectDataBuffer == null ||
                _objectSharedBuffer == null ||
                _visibleIndexBuffer == null ||
                _indirectArgsBuffer == null) return false;
            return true;
        }
        
        private static uint[] GetArgDataIdentity(Mesh mesh, int subMeshIndex = 0)
        {
            uint[] argDatas = new uint[] 
            {
                mesh.GetIndexCount(subMeshIndex),
                0,
                mesh.GetIndexStart(subMeshIndex),
                mesh.GetBaseVertex(subMeshIndex),
                0, // startInstanceIndex
            };
            return argDatas;
        }
    }
}