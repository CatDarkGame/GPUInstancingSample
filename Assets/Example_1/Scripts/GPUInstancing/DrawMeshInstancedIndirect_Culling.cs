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
            public static readonly int ObjectBufferID = Shader.PropertyToID("_ObjectBuffer");
        }
        
        private static class ComputePropertyID
        {
            public static readonly string KernelName = "CSUpdateObjectBufferIndex";
            public static readonly Vector3Int ThreadGroupSize = new Vector3Int(32, 1, 1);
            
            public static readonly int ObjectDataBufferID = Shader.PropertyToID("_ObjectBuffer");
            public static readonly int ObjectSharedBufferID = Shader.PropertyToID("_ObjectSharedBuffer");
            public static readonly int VisibleIndexBufferID = Shader.PropertyToID("_VisibleIndexBuffer");
            public static readonly int IndirectArgsBufferID = Shader.PropertyToID("_IndirectArgsBuffer");
            public static readonly int VisibleCountID = Shader.PropertyToID("_VisibleCount");
        }
        
        private struct ObjectBuffer
        {
            public Matrix4x4 objectToWorld;
            public Vector4   baseColor;
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
        private ComputeBuffer _objectBuffer;        // 오브젝트 전체 데이터 버퍼
        private ComputeBuffer _objectSharedBuffer;  // 드로우 오브젝트 데이터 버퍼 (InFrustum)
        private ComputeBuffer _visibleIndexBuffer;  // 드로우 오브젝트 인덱스 버퍼 (InFrustum)
        private ComputeBuffer _indirectArgsBuffer;  // Argument 버퍼
        private uint[] _visibleIndexData;
        private Bounds _bounds;
        private BoundsData[] _boundsDatas;
        private Plane[] _frustumPlanes;
        private int _appendBufferComputeKernelId;
        private int _bufferCount;
        
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
            _objectSharedBuffer?.Release();
            _objectSharedBuffer = null;
            _visibleIndexBuffer?.Release();
            _visibleIndexBuffer = null;
            _indirectArgsBuffer?.Release();
            _indirectArgsBuffer = null;
            _frustumPlanes = null;
            _boundsDatas = null;
            _visibleIndexData = null;
        }

        private void Init_InstancingData()
        {
            // Setup Data
            Vector3 boundsSizePerObject = Vector3.one * 1.0f;
            Matrix4x4[] localToWorldMatrixs = CommonUtils.GetRandomLocalToWorldMatrices(objectCount);
            List<BoundsData> boundsDataList = new List<BoundsData>();
            ObjectBuffer[] objectBufferData = new ObjectBuffer[objectCount];
            _bounds = new Bounds();
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
                Bounds bounds = new Bounds(position, boundsSizePerObject);
                boundsDataList.Add(new BoundsData(i, bounds));
            }
            _boundsDatas = boundsDataList.ToArray();
            
            // Setup StructuredBuffer
            int bufferSize = Marshal.SizeOf<ObjectBuffer>();
            _bufferCount = Mathf.Max(objectCount, ComputePropertyID.ThreadGroupSize.x);
            _objectBuffer = new ComputeBuffer(_bufferCount, bufferSize, ComputeBufferType.Default);
            _objectBuffer.SetData(objectBufferData);
            _objectSharedBuffer = new ComputeBuffer(_bufferCount, bufferSize, ComputeBufferType.Default);
            _visibleIndexBuffer = new ComputeBuffer(_bufferCount, sizeof(uint), ComputeBufferType.Default);
            _visibleIndexData = new uint[objectCount];
            
            // Setup ArgumentBuffer
            uint[] argDataIdentity = GetArgDataIdentity(mesh, 0);
            _indirectArgsBuffer = new ComputeBuffer(1, sizeof(uint) * argDataIdentity.Length, ComputeBufferType.IndirectArguments);
            _indirectArgsBuffer.SetData(argDataIdentity);
            
            // Setup ComputeShader
            _appendBufferComputeKernelId = appendBufferCompute.FindKernel(ComputePropertyID.KernelName);
            appendBufferCompute.SetBuffer(_appendBufferComputeKernelId, ComputePropertyID.ObjectDataBufferID, _objectBuffer);
            appendBufferCompute.SetBuffer(_appendBufferComputeKernelId, ComputePropertyID.ObjectSharedBufferID, _objectSharedBuffer);
            appendBufferCompute.SetBuffer(_appendBufferComputeKernelId, ComputePropertyID.VisibleIndexBufferID, _visibleIndexBuffer);
            appendBufferCompute.SetBuffer(_appendBufferComputeKernelId, ComputePropertyID.IndirectArgsBufferID, _indirectArgsBuffer);
            
            // Setup InstanceMaterial
            _instanceMaterial = new Material(sharedMaterial);
            _instanceMaterial.enableInstancing = true;
            _instanceMaterial.hideFlags = HideFlags.HideAndDontSave;
            _instanceMaterial.SetBuffer(ShaderPropertyID.ObjectBufferID, _objectSharedBuffer);
        }

        private void Update()
        {
            if (!IsNotNullRefs()) return;
            int visibleCount = FrustumCullingWithSorting(ref _visibleIndexData);
            if (visibleCount <= 0) return;
            Update_VisibleBuffer(_visibleIndexData, visibleCount);
            Graphics.DrawMeshInstancedIndirect(mesh, 0, _instanceMaterial, _bounds, _indirectArgsBuffer);
        }
        
        private int FrustumCullingWithSorting(ref uint[] visibleIndexData)
        {
            if (!targetCamera) return 0;
    
            // Frustum Culling
            _frustumPlanes = GeometryUtility.CalculateFrustumPlanes(targetCamera);
            uint[] tempVisibleInstances = new uint[objectCount];
            float[] distances = new float[objectCount];
            int visibleCount = 0;
    
            for (int i = 0; i < objectCount; i++)
            {
                Bounds bounds = _boundsDatas[i].bounds;
                if (GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                {
                    tempVisibleInstances[visibleCount] = (uint)i;
                    distances[visibleCount] = Vector3.Distance(targetCamera.transform.position, bounds.center);
                    visibleCount++;
                }
            }
    
            // 가까운 거리순 인덱스 정렬 (인스턴스 인덱스 순서대로 렌더링되기 때문에 거리 정렬을 통해 불투명 오브젝트 ZTest를 위함)
            for (int i = 0; i < visibleCount - 1; i++)
            {
                for (int j = 0; j < visibleCount - i - 1; j++)
                {
                    if (distances[j] > distances[j + 1])
                    {
                        float tempDistance = distances[j];
                        distances[j] = distances[j + 1];
                        distances[j + 1] = tempDistance;
                        
                        uint tempInstance = tempVisibleInstances[j];
                        tempVisibleInstances[j] = tempVisibleInstances[j + 1];
                        tempVisibleInstances[j + 1] = tempInstance;
                    }
                }
            }
            
            for (int i = 0; i < visibleCount; i++)
            {
                visibleIndexData[i] = tempVisibleInstances[i];
            }
            return visibleCount;
        }

        private void Update_VisibleBuffer(uint[] visibleIndexData, int visibleCount)
        {
            if (visibleCount <= 0 || visibleIndexData == null || visibleIndexData.Length <= 0) return;
            _visibleIndexBuffer.SetData(visibleIndexData, 0, 0, visibleCount);
            appendBufferCompute.SetInt(ComputePropertyID.VisibleCountID, visibleCount);
            appendBufferCompute.Dispatch(_appendBufferComputeKernelId, 
                _bufferCount / ComputePropertyID.ThreadGroupSize.x, 
                ComputePropertyID.ThreadGroupSize.y, 
                ComputePropertyID.ThreadGroupSize.z);
        }
        
        private bool IsNotNullRefs()
        {
            if (!mesh || 
                !_instanceMaterial || 
                _objectBuffer == null ||
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