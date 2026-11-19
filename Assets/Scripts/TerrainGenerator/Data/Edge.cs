using System;
using UnityEngine;

namespace TerrainGenerator
{
    /// <summary>
    /// Представляє ребро (грань) багатокутника Зони А в локальних координатах чанка.
    /// </summary>
    [Serializable]
    public class Edge
    {
        [Tooltip("Початкова точка відрізка в локальних координатах")]
        public Vector3 startPoint;

        [Tooltip("Кінцева точка відрізка в локальних координатах")]
        public Vector3 endPoint;

        [Tooltip("Зовнішня нормаль ребра у площині XZ (нормалізована)")]
        public Vector3 normal;

        [Tooltip("Довжина ребра у площині XZ")]
        public float length;

        [Tooltip("Тег типу стикування (наприклад, 'Default', 'SnowyPlain', 'Terminal')")]
        public string edgeTag = "Default";

        [Tooltip("Чи вже приєднано сюди інший чанк")]
        public bool isConnected = false;

        public Edge() { }

        public Edge(Vector3 start, Vector3 end, string tag = "Default")
        {
            startPoint = start;
            endPoint = end;
            edgeTag = string.IsNullOrEmpty(tag) ? "Default" : tag;
            isConnected = false;
            Recalculate();
        }

        public Edge(Edge source)
        {
            if (source == null) return;
            startPoint = source.startPoint;
            endPoint = source.endPoint;
            normal = source.normal;
            length = source.length;
            edgeTag = source.edgeTag;
            isConnected = source.isConnected;
        }

        /// <summary>
        /// Перераховує довжину та нормаль у площині XZ.
        /// Припускається порядок вершин проти годинникової стрілки (CCW), де зовнішня нормаль спрямована праворуч від вектора (start -> end).
        /// </summary>
        public void Recalculate(bool ccwOutward = true)
        {
            Vector2 s2 = new Vector2(startPoint.x, startPoint.z);
            Vector2 e2 = new Vector2(endPoint.x, endPoint.z);
            Vector2 dir = e2 - s2;
            length = dir.magnitude;

            if (length > 0.0001f)
            {
                dir /= length;
                // Праворуч від напрямку для CCW: (dy, -dx)
                // Ліворуч для CW: (-dy, dx)
                Vector2 n2 = ccwOutward ? new Vector2(dir.y, -dir.x) : new Vector2(-dir.y, dir.x);
                normal = new Vector3(n2.x, 0f, n2.y).normalized;
            }
            else
            {
                normal = Vector3.forward;
            }
        }

        public Vector3 GetWorldStart(Transform transform) => transform.TransformPoint(startPoint);
        public Vector3 GetWorldEnd(Transform transform) => transform.TransformPoint(endPoint);
        public Vector3 GetWorldNormal(Transform transform) => transform.TransformDirection(normal).normalized;
    }

    /// <summary>
    /// Представляє активне ребро у світових координатах під час генерації.
    /// </summary>
    public class WorldEdge
    {
        public ChunkObject chunk;
        public int localEdgeIndex;
        public Vector3 worldStart;
        public Vector3 worldEnd;
        public Vector3 worldNormal;
        public float length;
        public string edgeTag;
        public bool isConnected;

        public Vector3 Midpoint => (worldStart + worldEnd) * 0.5f;
        public Vector3 Direction => length > 0.0001f ? (worldEnd - worldStart).normalized : Vector3.forward;

        public WorldEdge(ChunkObject chunkInstance, int edgeIndex, Edge localEdge)
        {
            chunk = chunkInstance;
            localEdgeIndex = edgeIndex;
            Transform t = chunkInstance.transform;
            worldStart = localEdge.GetWorldStart(t);
            worldEnd = localEdge.GetWorldEnd(t);
            worldNormal = localEdge.GetWorldNormal(t);
            length = localEdge.length;
            edgeTag = localEdge.edgeTag;
            isConnected = localEdge.isConnected;
        }
    }
}
