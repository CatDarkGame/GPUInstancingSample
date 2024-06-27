using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatDarkGame.GPUInstancingSample
{
    public static class CommonUtils 
    {
        public static Matrix4x4[] GetRandomLocalToWorldMatrices(int count, float randomDistance = 15.0f)
        {
            Matrix4x4[] matrices = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
            {
                // 랜덤 월드 포지션 생성
                Vector3 randomPosition = new Vector3(
                    UnityEngine.Random.Range(-randomDistance, randomDistance), 
                    UnityEngine.Random.Range(-randomDistance, randomDistance), 
                    UnityEngine.Random.Range(-randomDistance, randomDistance)  
                );
                Quaternion defaultRotation = Quaternion.identity; // 기본 회전값
                Vector3 defaultScale = Vector3.one; // 기본 스케일값
                matrices[i] = Matrix4x4.TRS(randomPosition, defaultRotation, defaultScale);
            }
            return matrices;
        }
        
        public static Color GetRandomColor()
        {
            float red = Random.Range(0f, 1f);
            float green = Random.Range(0f, 1f);
            float blue = Random.Range(0f, 1f);
            float alpha = Random.Range(0f, 1f);
            return new Color(red, green, blue, alpha);
        }
        
        public static Vector4[] GetRandomColorVectorArray(int count)
        {
            Vector4[] array = new Vector4[count];
            for (int i = 0; i < count; i++)
                array[i] = GetRandomColor();
            return array;
        }
        
        // 구조화 버퍼로 전달하는 색상 값을 Linear 취급되기 때문에 Gamma로 변환 필요
        public static Color ConvertLinearToGamma(Color color)
        {
            color.r = Mathf.Pow(color.r, 2.2f);
            color.g = Mathf.Pow(color.g, 2.2f);
            color.b = Mathf.Pow(color.b, 2.2f);
            return color;
        }
        
        
        public static Mesh MergeSubMeshes(Mesh mesh)
        {
            if (mesh == null)
            {
                Debug.LogError("No Mesh provided.");
                return null;
            }

            // Get all vertex data
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uv = mesh.uv;
            Color[] colors = mesh.colors;

            // Combine all submeshes into one
            int[] combinedIndices = mesh.GetIndices(0);
            for (int i = 1; i < mesh.subMeshCount; i++)
            {
                int[] subMeshIndices = mesh.GetIndices(i);
                int[] tempIndices = new int[combinedIndices.Length + subMeshIndices.Length];
                combinedIndices.CopyTo(tempIndices, 0);
                subMeshIndices.CopyTo(tempIndices, combinedIndices.Length);
                combinedIndices = tempIndices;
            }

            // Create new mesh
            Mesh combinedMesh = new Mesh();
            combinedMesh.vertices = vertices;
            combinedMesh.normals = normals;
            combinedMesh.uv = uv;
            combinedMesh.colors = colors;
            combinedMesh.SetIndices(combinedIndices, MeshTopology.Triangles, 0);

            return combinedMesh;
        }
        
        
        public static Mesh MergeMeshes(Mesh[] meshes, bool mergeSubMeshs = false)
        {
            Mesh mergedMesh = new Mesh();

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector4> tangents = new List<Vector4>();
            List<Vector2> uv = new List<Vector2>();
            List<Color> colors = new List<Color>();

            List<int> indices = new List<int>();
            List<SubMeshDescriptor> subMeshDescriptors = new List<SubMeshDescriptor>();

            int vertexOffset = 0;

            foreach (Mesh mesh in meshes)
            {
                vertices.AddRange(mesh.vertices);
                normals.AddRange(mesh.normals);
                tangents.AddRange(mesh.tangents);
                uv.AddRange(mesh.uv);
                colors.AddRange(mesh.colors);

                for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                {
                    int[] subMeshIndices = mesh.GetIndices(subMeshIndex);
                    for (int i = 0; i < subMeshIndices.Length; i++)
                    {
                        subMeshIndices[i] += vertexOffset;
                    }

                    indices.AddRange(subMeshIndices);
                    subMeshDescriptors.Add(new SubMeshDescriptor(vertexOffset, subMeshIndices.Length, MeshTopology.Triangles));
                }

                vertexOffset += mesh.vertexCount;
            }

            mergedMesh.SetVertices(vertices);
            mergedMesh.SetNormals(normals);
            mergedMesh.SetTangents(tangents);
            mergedMesh.SetUVs(0, uv);
            mergedMesh.SetColors(colors);
            mergedMesh.subMeshCount = subMeshDescriptors.Count;

            int baseIndex = 0;
            for (int i = 0; i < subMeshDescriptors.Count; i++)
            {
                mergedMesh.SetIndices(indices.GetRange(baseIndex, subMeshDescriptors[i].indexCount).ToArray(), MeshTopology.Triangles, i);
                baseIndex += subMeshDescriptors[i].indexCount;
            }

            return mergeSubMeshs? MergeSubMeshes(mergedMesh) : mergedMesh;
        }
    }
}