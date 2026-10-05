using System.Collections.Generic;
using UnityEngine;

public static class NormalSolver
{
    public static void RecalculateNormals(Mesh mesh, float angleThreshold)
    {
        float cosineThreshold = Mathf.Cos(angleThreshold * Mathf.Deg2Rad);
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = new Vector3[vertices.Length];
        int[] triangles = mesh.triangles;
        Vector3[] triNormals = new Vector3[triangles.Length / 3];

        Dictionary<Vector3, List<int>> vertexDict = new Dictionary<Vector3, List<int>>();
        for (int i = 0; i < vertices.Length; i++)
        {
            if (!vertexDict.ContainsKey(vertices[i])) vertexDict.Add(vertices[i], new List<int>());
            vertexDict[vertices[i]].Add(i);
        }

        for (int i = 0; i < triangles.Length; i += 3)
            triNormals[i / 3] = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).normalized;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            for (int j = 0; j < 3; j++)
            {
                int vertexIndex = triangles[i + j];
                Vector3 currentNormal = triNormals[i / 3];
                Vector3 sumNormal = currentNormal;

                foreach (int sharedVertex in vertexDict[vertices[vertexIndex]])
                {
                    if (sharedVertex == vertexIndex) continue;
                    for (int k = 0; k < triangles.Length; k += 3)
                    {
                        if (triangles[k] == sharedVertex || triangles[k + 1] == sharedVertex || triangles[k + 2] == sharedVertex)
                        {
                            Vector3 adjNormal = triNormals[k / 3];
                            if (Vector3.Dot(currentNormal, adjNormal) >= cosineThreshold) sumNormal += adjNormal;
                            break;
                        }
                    }
                }
                normals[vertexIndex] = sumNormal.normalized;
            }
        }
        mesh.normals = normals;
    }
}