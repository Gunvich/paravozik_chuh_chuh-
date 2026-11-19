using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
    /// <summary>
    /// Визначає довільну 2D-полігональну межу (у площині XZ) для зони генерації/спавну світу.
    /// Працює за аналогією до Зони А чанків.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    public class SpawnAreaPolygon : MonoBehaviour
    {
        [Header("Вершини багатокутника (XZ)")]
        [Tooltip("Список локальних вершин контуру межі генерації")]
        public List<Vector3> localVertices = new List<Vector3>();

        [Header("Візуалізація (Gizmos)")]
        public Color boundaryColor = new Color(0.1f, 0.9f, 0.8f, 1f);
        public Color normalColor = new Color(1f, 0.6f, 0.1f, 0.8f);
        public bool showNormals = true;
        public float normalLength = 3f;
        public bool showFilledArea = true;
        public Color fillColor = new Color(0.1f, 0.9f, 0.8f, 0.05f);

        private void Reset()
        {
            if (localVertices == null || localVertices.Count == 0)
            {
                SetAsRectangle(100f, 100f);
            }
        }

        #region Світові координати

        public List<Vector3> GetWorldVertices()
        {
            List<Vector3> worldList = new List<Vector3>(localVertices.Count);
            for (int i = 0; i < localVertices.Count; i++)
            {
                worldList.Add(transform.TransformPoint(localVertices[i]));
            }
            return worldList;
        }

        public List<Vector2> GetWorld2DPolygon()
        {
            List<Vector2> list = new List<Vector2>(localVertices.Count);
            for (int i = 0; i < localVertices.Count; i++)
            {
                Vector3 w = transform.TransformPoint(localVertices[i]);
                list.Add(new Vector2(w.x, w.z));
            }
            return list;
        }

        #endregion

        #region Перевірки геометрії

        /// <summary>
        /// Перевіряє, чи точка лежить всередині зони спавну.
        /// </summary>
        public bool IsPointInside(Vector3 worldPoint)
        {
            List<Vector2> poly = GetWorld2DPolygon();
            return PolygonMath2D.IsPointInPolygon(PolygonMath2D.To2D(worldPoint), poly);
        }

        /// <summary>
        /// Перевіряє, чи багатокутник (наприклад, Зона А чанка) повністю поміщається всередині цієї зони спавну.
        /// </summary>
        public bool IsPolygonInside(IList<Vector3> worldPolygon, float tolerance = 0.01f)
        {
            if (worldPolygon == null || worldPolygon.Count == 0) return true;
            List<Vector2> outer = GetWorld2DPolygon();
            List<Vector2> inner = PolygonMath2D.To2DList(worldPolygon);
            return PolygonMath2D.IsPolygonFullyInside(inner, outer, tolerance);
        }

        /// <summary>
        /// Повертає відстань від точки у напрямку вектора (промінь) до межі зони спавну.
        /// </summary>
        public float GetDistanceToBoundary(Vector3 worldPoint, Vector3 worldDirection)
        {
            List<Vector2> poly = GetWorld2DPolygon();
            Vector2 origin = PolygonMath2D.To2D(worldPoint);
            Vector2 dir = PolygonMath2D.To2D(worldDirection).normalized;
            return PolygonMath2D.RaycastToPolygon(origin, dir, poly);
        }

        /// <summary>
        /// Мінімальна відстань від точки до будь-якого сегмента межі.
        /// </summary>
        public float GetMinDistanceToBoundary(Vector3 worldPoint)
        {
            List<Vector2> poly = GetWorld2DPolygon();
            return PolygonMath2D.GetMinDistanceToPolygonEdges(PolygonMath2D.To2D(worldPoint), poly);
        }

        #endregion

        #region Шаблони для швидкого створення

        public void SetAsRectangle(float width, float height)
        {
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            localVertices = new List<Vector3>
            {
                new Vector3(-halfW, 0f, -halfH),
                new Vector3(halfW, 0f, -halfH),
                new Vector3(halfW, 0f, halfH),
                new Vector3(-halfW, 0f, halfH)
            };
            EnsureCCW();
        }

        public void SetAsRegularPolygon(int sides, float radius)
        {
            sides = Mathf.Max(3, sides);
            localVertices = new List<Vector3>(sides);
            float angleStep = 360f / sides;

            for (int i = 0; i < sides; i++)
            {
                float rad = Mathf.Deg2Rad * (i * angleStep);
                localVertices.Add(new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius));
            }
            EnsureCCW();
        }

        public void EnsureCCW()
        {
            List<Vector2> poly2D = new List<Vector2>();
            for (int i = 0; i < localVertices.Count; i++)
            {
                poly2D.Add(new Vector2(localVertices[i].x, localVertices[i].z));
            }

            if (!PolygonMath2D.IsCounterClockwise(poly2D))
            {
                localVertices.Reverse();
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (localVertices == null || localVertices.Count < 3) return;

            List<Vector3> wVerts = GetWorldVertices();
            int count = wVerts.Count;

            Gizmos.color = boundaryColor;
            for (int i = 0; i < count; i++)
            {
                Vector3 p1 = wVerts[i];
                Vector3 p2 = wVerts[(i + 1) % count];
                Gizmos.DrawLine(p1, p2);
                Gizmos.DrawSphere(p1, 0.4f);

                if (showNormals)
                {
                    Vector3 mid = (p1 + p2) * 0.5f;
                    Vector2 p1_2 = new Vector2(p1.x, p1.z);
                    Vector2 p2_2 = new Vector2(p2.x, p2.z);
                    Vector2 n2 = PolygonMath2D.GetNormalCCW(p1_2, p2_2);
                    Vector3 n3 = new Vector3(n2.x, 0f, n2.y);

                    Gizmos.color = normalColor;
                    Gizmos.DrawLine(mid, mid + n3 * normalLength);
                    Gizmos.color = boundaryColor;
                }
            }
        }

        #endregion
    }
}
