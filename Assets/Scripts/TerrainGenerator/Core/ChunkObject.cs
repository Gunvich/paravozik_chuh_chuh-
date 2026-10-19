using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
    /// <summary>
    /// Основний компонент на префабі чанка.
    /// Зберігає полігон Зони А, список ребер для стикування та візуалізує їх у сцені.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    public class ChunkObject : MonoBehaviour
    {
        [Header("Зона А (Логічний контур чанка у XZ)")]
        [Tooltip("Вершини багатокутника Зони А в локальних координатах (порядок CCW)")]
        public List<Vector3> polygonVertices = new List<Vector3>();

        [Header("Ребра Зони А")]
        [Tooltip("Список ребер чанка. Можна згенерувати автоматично з вершин Зони А")]
        public List<Edge> localEdges = new List<Edge>();

        [Header("Властивості префаба")]
        [Tooltip("Чи є цей чанк заглушкою/краєм світу (наприклад, непрохідні скелі)")]
        public bool isTerminalChunk = false;

        [Tooltip("Чи є цей чанк заповнювачем для тріщин та щілин другого проходу")]
        public bool isFillerChunk = false;

        [Tooltip("Вага вибору в пулі випадкових чанків (чим більша, тим частіше спавниться)")]
        [Range(0.1f, 10f)]
        public float spawnWeight = 1f;

        [Header("Налаштування Gizmos")]
        public bool drawGizmos = true;
        public bool drawNormals = true;
        public float normalLength = 2f;
        public Color zoneAColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        public Color openEdgeColor = new Color(0.1f, 1f, 0.2f, 1f);
        public Color connectedEdgeColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);

        private void OnValidate()
        {
            if (polygonVertices != null && polygonVertices.Count >= 3)
            {
                // Якщо ребра ще не створені або їхня кількість не збігається з вершинами
                if (localEdges == null || localEdges.Count != polygonVertices.Count)
                {
                    RebuildEdgesFromPolygon(preserveTags: true);
                }
                else
                {
                    // Оновлюємо довжини та нормалі існуючих ребер
                    UpdateEdgeCoordinates();
                }
            }
        }

        #region Отримання світових даних

        /// <summary>
        /// Повертає вершини Зони А у світових координатах.
        /// </summary>
        public List<Vector3> GetWorldPolygon()
        {
            List<Vector3> worldPoly = new List<Vector3>(polygonVertices.Count);
            for (int i = 0; i < polygonVertices.Count; i++)
            {
                worldPoly.Add(transform.TransformPoint(polygonVertices[i]));
            }
            return worldPoly;
        }

        /// <summary>
        /// Повертає вершини Зони А у 2D XZ світових координатах.
        /// </summary>
        public List<Vector2> GetWorldPolygon2D()
        {
            List<Vector2> poly2D = new List<Vector2>(polygonVertices.Count);
            for (int i = 0; i < polygonVertices.Count; i++)
            {
                Vector3 w = transform.TransformPoint(polygonVertices[i]);
                poly2D.Add(new Vector2(w.x, w.z));
            }
            return poly2D;
        }

        /// <summary>
        /// Повертає всі відкриті (ще не з'єднані) ребра у світових координатах.
        /// </summary>
        public List<WorldEdge> GetOpenEdges()
        {
            List<WorldEdge> open = new List<WorldEdge>();
            for (int i = 0; i < localEdges.Count; i++)
            {
                if (!localEdges[i].isConnected)
                {
                    open.Add(new WorldEdge(this, i, localEdges[i]));
                }
            }
            return open;
        }

        /// <summary>
        /// Повертає всі ребра чанка у світових координатах.
        /// </summary>
        public List<WorldEdge> GetAllWorldEdges()
        {
            List<WorldEdge> list = new List<WorldEdge>(localEdges.Count);
            for (int i = 0; i < localEdges.Count; i++)
            {
                list.Add(new WorldEdge(this, i, localEdges[i]));
            }
            return list;
        }

        /// <summary>
        /// Повертає розміри чанка (ширина вздовж X, глибина вздовж Z) з вершин Зони А або мешу.
        /// Використовується другим проходом для розбиття тріщин на сітку квадратів/комірок.
        /// </summary>
        public Vector2 GetFootprintSize()
        {
            if (polygonVertices != null && polygonVertices.Count >= 3)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    Vector3 v = polygonVertices[i];
                    if (v.x < minX) minX = v.x;
                    if (v.x > maxX) maxX = v.x;
                    if (v.z < minZ) minZ = v.z;
                    if (v.z > maxZ) maxZ = v.z;
                }
                float w = maxX - minX;
                float d = maxZ - minZ;
                if (w > 0.01f && d > 0.01f)
                {
                    return new Vector2(w, d);
                }
            }

            MeshFilter mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                return new Vector2(Mathf.Max(1f, b.size.x), Mathf.Max(1f, b.size.z));
            }

            return new Vector2(10f, 10f);
        }

        #endregion

        #region Управління геометрією Зони А

        /// <summary>
        /// Перебудовує список ребер на основі вершин полігону polygonVertices.
        /// Зберігає встановлені теги, якщо кількість вершин не змінилася.
        /// </summary>
        public void RebuildEdgesFromPolygon(bool preserveTags = true)
        {
            if (polygonVertices == null || polygonVertices.Count < 3) return;

            // Перевіряємо порядок вершин (має бути CCW)
            EnsureCCW();

            int count = polygonVertices.Count;
            List<string> oldTags = new List<string>();
            if (preserveTags && localEdges != null)
            {
                for (int i = 0; i < localEdges.Count; i++)
                {
                    oldTags.Add(localEdges[i] != null ? localEdges[i].edgeTag : "Default");
                }
            }

            localEdges = new List<Edge>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 vStart = polygonVertices[i];
                Vector3 vEnd = polygonVertices[(i + 1) % count];

                string tag = (preserveTags && i < oldTags.Count) ? oldTags[i] : "Default";
                Edge newEdge = new Edge(vStart, vEnd, tag);
                localEdges.Add(newEdge);
            }
        }

        /// <summary>
        /// Оновлює координати початку, кінця та нормалі для існуючих ребер без скидання налаштувань.
        /// </summary>
        public void UpdateEdgeCoordinates()
        {
            if (localEdges == null || polygonVertices == null) return;
            int count = Mathf.Min(localEdges.Count, polygonVertices.Count);
            for (int i = 0; i < count; i++)
            {
                localEdges[i].startPoint = polygonVertices[i];
                localEdges[i].endPoint = polygonVertices[(i + 1) % polygonVertices.Count];
                localEdges[i].Recalculate();
            }
        }

        /// <summary>
        /// Гарантує, що вершини впорядковані проти годинникової стрілки (CCW) для коректних нормалей.
        /// </summary>
        public void EnsureCCW()
        {
            List<Vector2> poly2D = new List<Vector2>(polygonVertices.Count);
            for (int i = 0; i < polygonVertices.Count; i++)
            {
                poly2D.Add(new Vector2(polygonVertices[i].x, polygonVertices[i].z));
            }

            if (!PolygonMath2D.IsCounterClockwise(poly2D))
            {
                polygonVertices.Reverse();
            }
        }

        /// <summary>
        /// Створює прямокутну Зону А заданого розміру.
        /// </summary>
        public void CreateDefaultRect(float sizeX, float sizeZ)
        {
            float hx = sizeX * 0.5f;
            float hz = sizeZ * 0.5f;

            polygonVertices = new List<Vector3>
            {
                new Vector3(-hx, 0f, -hz),
                new Vector3(hx, 0f, -hz),
                new Vector3(hx, 0f, hz),
                new Vector3(-hx, 0f, hz)
            };
            RebuildEdgesFromPolygon(preserveTags: false);
        }

        #endregion

        #region Gizmos Візуалізація

        private void OnDrawGizmos()
        {
            if (!drawGizmos || polygonVertices == null || polygonVertices.Count < 3) return;

            // Малюємо контур Зони А
            Gizmos.color = zoneAColor;
            int count = polygonVertices.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 w1 = transform.TransformPoint(polygonVertices[i]);
                Vector3 w2 = transform.TransformPoint(polygonVertices[(i + 1) % count]);
                Gizmos.DrawLine(w1, w2);
                Gizmos.DrawSphere(w1, 0.2f);
            }

            // Малюємо ребра та нормалі
            if (localEdges != null)
            {
                for (int i = 0; i < localEdges.Count; i++)
                {
                    Edge edge = localEdges[i];
                    if (edge == null) continue;

                    Vector3 wStart = transform.TransformPoint(edge.startPoint);
                    Vector3 wEnd = transform.TransformPoint(edge.endPoint);
                    Vector3 wNormal = transform.TransformDirection(edge.normal).normalized;
                    Vector3 mid = (wStart + wEnd) * 0.5f;

                    // Колір за станом та тегом
                    if (edge.isConnected)
                    {
                        Gizmos.color = connectedEdgeColor;
                    }
                    else if (edge.edgeTag == "Terminal")
                    {
                        Gizmos.color = new Color(0.9f, 0.2f, 0.8f, 1f); // Фіолетовий для Terminal
                    }
                    else
                    {
                        Gizmos.color = openEdgeColor;
                    }

                    // Потовщена лінія ребра
                    Gizmos.DrawLine(wStart, wEnd);

                    // Стрілка нормалі
                    if (drawNormals)
                    {
                        Gizmos.color = edge.isConnected ? connectedEdgeColor : Color.yellow;
                        Gizmos.DrawRay(mid, wNormal * normalLength);
                    }
                }
            }
        }

        #endregion
    }
}
