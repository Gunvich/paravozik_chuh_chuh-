using System.Collections.Generic;
using UnityEngine;

public static class NormalSolver
{
    private struct QuantizedPos : System.IEquatable<QuantizedPos>
    {
        public readonly int x;
        public readonly int y;
        public readonly int z;

        public QuantizedPos(Vector3 v, float cellSize)
        {
            x = Mathf.RoundToInt(v.x / cellSize);
            y = Mathf.RoundToInt(v.y / cellSize);
            z = Mathf.RoundToInt(v.z / cellSize);
        }

        public bool Equals(QuantizedPos other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is QuantizedPos other && Equals(other);
        public override int GetHashCode() => unchecked((x * 73856093) ^ (y * 19349663) ^ (z * 83492791));
    }

    private struct VertexTriPair
    {
        public int vertexIndex;
        public int triangleIndex;

        public VertexTriPair(int vertexIndex, int triangleIndex)
        {
            this.vertexIndex = vertexIndex;
            this.triangleIndex = triangleIndex;
        }
    }

    /// <summary>
    /// Перераховує нормалі меша зі згладжуванням за кутовим порогом та радіусом толерантності.
    /// tolerance: радіус у метрах (наприклад, 0.05f - 0.2f), в межах якого близькі вершини на стиках вважаються спільними.
    /// </summary>
    public static void RecalculateNormals(Mesh mesh, float angleThreshold, float tolerance = 0.05f)
    {
        float cosineThreshold = Mathf.Cos(angleThreshold * Mathf.Deg2Rad);
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = new Vector3[vertices.Length];
        int[] triangles = mesh.triangles;
        int triCount = triangles.Length / 3;
        Vector3[] triNormals = new Vector3[triCount];

        // Крок 1: Обчислити нормаль кожного трикутника
        for (int i = 0; i < triCount; i++)
        {
            int i0 = triangles[i * 3];
            int i1 = triangles[i * 3 + 1];
            int i2 = triangles[i * 3 + 2];
            triNormals[i] = Vector3.Cross(
                vertices[i1] - vertices[i0],
                vertices[i2] - vertices[i0]).normalized;
        }

        // Крок 2: Побудувати просторовий індекс вершин
        float cellSize = Mathf.Max(0.001f, tolerance);
        Dictionary<QuantizedPos, List<VertexTriPair>> positionMap = new Dictionary<QuantizedPos, List<VertexTriPair>>(vertices.Length);

        for (int triIdx = 0; triIdx < triCount; triIdx++)
        {
            for (int j = 0; j < 3; j++)
            {
                int vertIdx = triangles[triIdx * 3 + j];
                QuantizedPos qpos = new QuantizedPos(vertices[vertIdx], cellSize);

                if (!positionMap.TryGetValue(qpos, out var list))
                {
                    list = new List<VertexTriPair>(6);
                    positionMap[qpos] = list;
                }
                list.Add(new VertexTriPair(vertIdx, triIdx));
            }
        }

        // Крок 3: Для кожної вершини згладити нормаль із сусідів
        for (int triIdx = 0; triIdx < triCount; triIdx++)
        {
            for (int j = 0; j < 3; j++)
            {
                int vertIdx = triangles[triIdx * 3 + j];
                Vector3 currentNormal = triNormals[triIdx];
                Vector3 sumNormal = currentNormal;

                QuantizedPos qpos = new QuantizedPos(vertices[vertIdx], cellSize);
                if (positionMap.TryGetValue(qpos, out var neighbors))
                {
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int neighborTriIdx = neighbors[n].triangleIndex;
                        if (neighborTriIdx == triIdx) continue;

                        Vector3 adjNormal = triNormals[neighborTriIdx];
                        if (Vector3.Dot(currentNormal, adjNormal) >= cosineThreshold)
                        {
                            sumNormal += adjNormal;
                        }
                    }
                }

                normals[vertIdx] = sumNormal.normalized;
            }
        }

        mesh.normals = normals;
    }
}