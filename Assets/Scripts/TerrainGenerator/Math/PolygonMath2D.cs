using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
    /// <summary>
    /// Математична утиліта для 2D геометрії у площині XZ (Unity X -> 2D X, Unity Z -> 2D Y).
    /// Реалізує SAT, перевірку перетинів довільних багатокутників, входження точок та променеві розрахунки.
    /// </summary>
    public static class PolygonMath2D
    {
        public const float Epsilon = 1e-4f;

        #region Конвертація координат

        public static Vector2 To2D(Vector3 v) => new Vector2(v.x, v.z);
        public static Vector3 To3D(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);

        public static List<Vector2> To2DList(IList<Vector3> list)
        {
            if (list == null) return new List<Vector2>();
            List<Vector2> result = new List<Vector2>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                result.Add(new Vector2(list[i].x, list[i].z));
            }
            return result;
        }

        public static List<Vector3> To3DList(IList<Vector2> list, float y = 0f)
        {
            if (list == null) return new List<Vector3>();
            List<Vector3> result = new List<Vector3>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                result.Add(new Vector3(list[i].x, y, list[i].y));
            }
            return result;
        }

        #endregion

        #region Орієнтація та площа

        /// <summary>
        /// Повертає площу зі знаком (позитивна, якщо проти годинникової стрілки CCW).
        /// </summary>
        public static float SignedArea(IList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0f;
            float sum = 0f;
            int count = polygon.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % count];
                sum += (a.x * b.y - b.x * a.y);
            }
            return sum * 0.5f;
        }

        public static bool IsCounterClockwise(IList<Vector2> polygon) => SignedArea(polygon) > 0f;

        /// <summary>
        /// Зовнішня нормаль для сегмента [a -> b] при обході CCW.
        /// </summary>
        public static Vector2 GetNormalCCW(Vector2 a, Vector2 b)
        {
            Vector2 dir = (b - a).normalized;
            return new Vector2(dir.y, -dir.x);
        }

        #endregion

        #region Перевірка AABB

        public static Rect GetBoundingRect(IList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count == 0) return Rect.zero;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 p = polygon[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public static bool DoBoundingRectsOverlap(Rect a, Rect b, float margin = 0f)
        {
            return (a.xMax >= b.xMin + margin) && (a.xMin <= b.xMax - margin) &&
                   (a.yMax >= b.yMin + margin) && (a.yMin <= b.yMax - margin);
        }

        #endregion

        #region Перевірка перетину відрізків

        /// <summary>
        /// Строга перевірка перетину двох відрізків [a1 -> a2] та [b1 -> b2].
        /// Якщо вони перетинаються всередині (а не просто торкаються кінчиками), повертає true.
        /// </summary>
        public static bool DoSegmentsIntersectStrict(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, float margin = 0.01f)
        {
            Vector2 da = a2 - a1;
            Vector2 db = b2 - b1;

            float cross = da.x * db.y - da.y * db.x;
            if (Mathf.Abs(cross) < Epsilon)
            {
                // Паралельні або колінеарні відрізки
                return false;
            }

            Vector2 d = b1 - a1;
            float t = (d.x * db.y - d.y * db.x) / cross;
            float u = (d.x * da.y - d.y * da.x) / cross;

            // t та u повинні бути строго між margin та (1 - margin), щоб уникнути помилкового детекту на дотичних стиках
            float eps = Mathf.Clamp(margin / Mathf.Max(da.magnitude, db.magnitude, 1f), 0.001f, 0.49f);
            return (t > eps && t < (1f - eps) && u > eps && u < (1f - eps));
        }

        #endregion

        #region Точка всередині багатокутника

        /// <summary>
        /// Перевіряє, чи знаходиться точка всередині багатокутника (Ray-casting / Even-odd).
        /// </summary>
        public static bool IsPointInPolygon(Vector2 point, IList<Vector2> polygon, bool countBoundaryAsInside = true)
        {
            if (polygon == null || polygon.Count < 3) return false;

            bool inside = false;
            int count = polygon.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];

                // Перевірка на співпадіння з ребром
                if (DistancePointToSegment(point, pi, pj, out _) < Epsilon)
                {
                    return countBoundaryAsInside;
                }

                if (((pi.y > point.y) != (pj.y > point.y)) &&
                    (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y + Epsilon) + pi.x))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Перевіряє, чи знаходиться точка строго всередині багатокутника з відступом margin всередину.
        /// </summary>
        public static bool IsPointStrictlyInside(Vector2 point, IList<Vector2> polygon, float minDistanceToEdge = 0.02f)
        {
            if (!IsPointInPolygon(point, polygon, countBoundaryAsInside: false))
                return false;

            float dist = GetMinDistanceToPolygonEdges(point, polygon);
            return dist > minDistanceToEdge;
        }

        #endregion

        #region Перевірка перетину двох багатокутників (SAT + Robust Poly-Poly)

        /// <summary>
        /// Перевіряє, чи перетинаються два багатокутники PolyA та PolyB.
        /// Підтримує як опуклі, так і увігнуті багатокутники.
        /// Враховує 'contactSkinMargin', щоб суміжні чанки, які торкаються ребрами (Zone A стик в стик), НЕ вважалися такими, що перетинаються!
        /// </summary>
        public static bool DoPolygonsOverlap(IList<Vector2> polyA, IList<Vector2> polyB, float contactSkinMargin = 0.02f)
        {
            if (polyA == null || polyA.Count < 3 || polyB == null || polyB.Count < 3)
                return false;

            // 1. Швидкий AABB тест
            Rect boxA = GetBoundingRect(polyA);
            Rect boxB = GetBoundingRect(polyB);
            if (!DoBoundingRectsOverlap(boxA, boxB, contactSkinMargin))
                return false;

            // 2. Перевірка перетину ребер
            int countA = polyA.Count;
            int countB = polyB.Count;

            for (int i = 0; i < countA; i++)
            {
                Vector2 a1 = polyA[i];
                Vector2 a2 = polyA[(i + 1) % countA];

                for (int j = 0; j < countB; j++)
                {
                    Vector2 b1 = polyB[j];
                    Vector2 b2 = polyB[(j + 1) % countB];

                    if (DoSegmentsIntersectStrict(a1, a2, b1, b2, contactSkinMargin))
                    {
                        return true;
                    }
                }
            }

            // 3. Перевірка, чи одна фігура не опинилась строго всередині іншої
            for (int i = 0; i < countA; i++)
            {
                if (IsPointStrictlyInside(polyA[i], polyB, contactSkinMargin))
                    return true;
            }

            for (int j = 0; j < countB; j++)
            {
                if (IsPointStrictlyInside(polyB[j], polyA, contactSkinMargin))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Перевірка перетину за теоремою про розділяючу вісь (SAT) для опуклих багатокутників.
        /// </summary>
        public static bool OverlapConvexSAT(IList<Vector2> polyA, IList<Vector2> polyB, float skinMargin = 0.02f)
        {
            if (!SATAxisTest(polyA, polyB, skinMargin)) return false;
            if (!SATAxisTest(polyB, polyA, skinMargin)) return false;
            return true;
        }

        private static bool SATAxisTest(IList<Vector2> polyA, IList<Vector2> polyB, float skinMargin)
        {
            int countA = polyA.Count;
            for (int i = 0; i < countA; i++)
            {
                Vector2 p1 = polyA[i];
                Vector2 p2 = polyA[(i + 1) % countA];
                Vector2 edge = p2 - p1;
                Vector2 normal = new Vector2(-edge.y, edge.x).normalized;

                ProjectPolygon(polyA, normal, out float minA, out float maxA);
                ProjectPolygon(polyB, normal, out float minB, out float maxB);

                // Якщо є розділення з урахуванням запасу стикування
                if (maxA <= minB + skinMargin || maxB <= minA + skinMargin)
                {
                    return false; // Знайдено розділяючу вісь -> не перетинаються
                }
            }
            return true;
        }

        private static void ProjectPolygon(IList<Vector2> poly, Vector2 axis, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (int i = 0; i < poly.Count; i++)
            {
                float dot = Vector2.Dot(poly[i], axis);
                if (dot < min) min = dot;
                if (dot > max) max = dot;
            }
        }

        #endregion

        #region Перевірка входження в межі (Bounds Containment)

        /// <summary>
        /// Перевіряє, чи багатокутник innerPoly повністю знаходиться всередині outerPoly (наприклад, полігону SpawnArea).
        /// </summary>
        public static bool IsPolygonFullyInside(IList<Vector2> innerPoly, IList<Vector2> outerPoly, float tolerance = 0.01f)
        {
            if (innerPoly == null || innerPoly.Count < 3 || outerPoly == null || outerPoly.Count < 3)
                return false;

            // 1. Усі вершини innerPoly мають бути всередині outerPoly
            for (int i = 0; i < innerPoly.Count; i++)
            {
                if (!IsPointInPolygon(innerPoly[i], outerPoly, countBoundaryAsInside: true))
                {
                    return false;
                }
            }

            // 2. Жодне ребро innerPoly не повинно строго перетинати ребра outerPoly
            int countInner = innerPoly.Count;
            int countOuter = outerPoly.Count;

            for (int i = 0; i < countInner; i++)
            {
                Vector2 in1 = innerPoly[i];
                Vector2 in2 = innerPoly[(i + 1) % countInner];

                for (int j = 0; j < countOuter; j++)
                {
                    Vector2 out1 = outerPoly[j];
                    Vector2 out2 = outerPoly[(j + 1) % countOuter];

                    if (DoSegmentsIntersectStrict(in1, in2, out1, out2, tolerance))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region Дистанція та Промені (Raycast)

        public static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b, out Vector2 closestPoint)
        {
            Vector2 ab = b - a;
            float mag2 = ab.sqrMagnitude;
            if (mag2 < Epsilon)
            {
                closestPoint = a;
                return Vector2.Distance(p, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / mag2);
            closestPoint = a + t * ab;
            return Vector2.Distance(p, closestPoint);
        }

        public static float GetMinDistanceToPolygonEdges(Vector2 point, IList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 2) return float.MaxValue;
            float minDist = float.MaxValue;
            int count = polygon.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % count];
                float d = DistancePointToSegment(point, a, b, out _);
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        /// <summary>
        /// Випускає 2D промінь з точки origin у напрямку direction (має бути нормалізований) до ребер багатокутника.
        /// Повертає відстань до найближчого перетину або float.MaxValue, якщо перетину немає.
        /// </summary>
        public static float RaycastToPolygon(Vector2 origin, Vector2 direction, IList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return float.MaxValue;

            float minDistance = float.MaxValue;
            int count = polygon.Count;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % count];
                Vector2 edge = b - a;

                float cross = direction.x * edge.y - direction.y * edge.x;
                if (Mathf.Abs(cross) < Epsilon) continue; // Паралельні

                Vector2 d = a - origin;
                float t = (d.x * edge.y - d.y * edge.x) / cross; // Відстань вздовж direction
                float u = (d.x * direction.y - d.y * direction.x) / cross; // Параметр вздовж edge [0..1]

                if (t > Epsilon && u >= 0f && u <= 1f)
                {
                    if (t < minDistance)
                    {
                        minDistance = t;
                    }
                }
            }

            return minDistance;
        }

        #endregion
    }
}
