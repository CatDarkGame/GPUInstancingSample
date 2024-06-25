using UnityEngine;

namespace CatDarkGame.GPUInstancingSample
{
    public class DrawMeshInstanced : MonoBehaviour
    {
        public Mesh mesh;
        public Material sharedMaterial;
        public int objectCount = 10;
        
        private Matrix4x4[] _localToWorldMatrixs;
        private Material _instanceMaterial;
        private MaterialPropertyBlock _materialPropertyBlock;
        
        private void Awake()
        {
            _localToWorldMatrixs = CommonUtils.GetRandomLocalToWorldMatrices(objectCount);
            _instanceMaterial = new Material(sharedMaterial);
            _instanceMaterial.enableInstancing = true;
            _instanceMaterial.hideFlags = HideFlags.HideAndDontSave;
            
            _materialPropertyBlock = new MaterialPropertyBlock();
            _materialPropertyBlock.SetVectorArray("_BaseColor", CommonUtils.GetRandomColorVectorArray(objectCount));
        }
        
        private void Update()
        {
            if (!IsNotNullRefs()) return;
            Graphics.DrawMeshInstanced(mesh, 0, _instanceMaterial, _localToWorldMatrixs, objectCount, _materialPropertyBlock);
        }

        private void OnDestroy()
        {
            if(_instanceMaterial) DestroyImmediate(_instanceMaterial);
            _instanceMaterial = null;
            _materialPropertyBlock = null;
        }
        
        private bool IsNotNullRefs()
        {
            if (!mesh || 
                !_instanceMaterial || 
                _localToWorldMatrixs == null || _localToWorldMatrixs.Length <= 0) return false;
            return true;
        }
    }

}
