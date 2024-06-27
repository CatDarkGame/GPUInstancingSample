using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatDarkGame.GPUIndirectDraw
{
    public class GenerateSharedMesh : EditorWindow
    {
        private static readonly string k_EditorTitle = "Generate SharedMesh";
        private static readonly string k_FolderPath = "Assets/GenerateSharedMesh";
        private static readonly string k_AssetName = "SharedMesh";
        private readonly string[] k_TrisCountStr = { "50", "150", "400", "1000", "2000" };
        private readonly int[] k_TrisCount = { 50, 150, 400, 1000, 2000 };
        
        private int _selectTrisCountPopupIndex = 0;
        
        [MenuItem("Tools/GPUIndirectDraw/Generate SharedMesh")]
        public static void ShowWindow()
        {
            GetWindow<GenerateSharedMesh>(k_EditorTitle);
        }

        private void OnGUI()
        {
            _selectTrisCountPopupIndex = EditorGUILayout.Popup("Triangles", _selectTrisCountPopupIndex, k_TrisCountStr);
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Generate SharedMesh"))
            {
                GenerateMeshAsset(k_TrisCount[_selectTrisCountPopupIndex], k_FolderPath, k_AssetName);
            }
            if (GUILayout.Button("Generate SharedMesh All"))
            {
                for (int i = 0; i < k_TrisCount.Length; i++)
                {
                    GenerateMeshAsset(k_TrisCount[i], k_FolderPath, k_AssetName);    
                }
            }
        }
        
        private void GenerateMeshAsset(int trisCount, string folderPath, string assetName)
        {
            int indexCount = trisCount * 3;
            Mesh mesh = new Mesh();
            mesh.name = string.Format("{0}_{1}", assetName, trisCount);
            mesh.indexFormat = 65535 <= indexCount ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.subMeshCount = 1;

            // Setup VertexData
            List<Vector3> vertexList = new List<Vector3>(indexCount);
            List<Vector2> uvList = new List<Vector2>(indexCount);
            List<int> indexList = new List<int>();
            for (int i = 0; i < indexCount; i++)
            {
                indexList.Add(i); 
                vertexList.Add(Vector3.one);
                uvList.Add(new Vector2(i, 0));
            }
            mesh.SetVertices(vertexList);
            mesh.SetUVs(0, uvList);
            mesh.SetIndices(indexList, MeshTopology.Triangles, 0, true);
            mesh.UploadMeshData(true);

            // Generate MeshAsset
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }
            string assetPath = string.Format("{0}/{1}.mesh", folderPath, mesh.name);
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("Generate SharedMesh - " + mesh.name, mesh);
            Selection.activeObject = mesh;
        }
        
        
    }
}