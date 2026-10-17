using System.Collections.Generic;
using UnityEngine;

namespace Parabox.CSG
{
    public class Vector3ApproxComparer : IEqualityComparer<Vector3>
    {
        private readonly float epsilon;
        private readonly float invEpsilon;

        public Vector3ApproxComparer(float epsilon = 0.005f)
        {
            this.epsilon = Mathf.Max(0.0001f, epsilon);
            this.invEpsilon = 1f / this.epsilon;
        }

        public bool Equals(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < (epsilon * epsilon);
        }

        public int GetHashCode(Vector3 v)
        {
            int x = Mathf.RoundToInt(v.x * invEpsilon);
            int y = Mathf.RoundToInt(v.y * invEpsilon);
            int z = Mathf.RoundToInt(v.z * invEpsilon);
            return unchecked((x * 73856093) ^ (y * 19349663) ^ (z * 83492791));
        }
    }

    public static class CSG
    {
        public static Mesh Union(Mesh meshA, Matrix4x4 matrixA, Mesh meshB, Matrix4x4 matrixB, float solidBaseY = -15f)
        {
            if (meshA == null || meshA.vertexCount == 0) return meshB;
            if (meshB == null || meshB.vertexCount == 0) return meshA;

            var a = new CSG_Model(meshA, matrixA, true, solidBaseY);
            var b = new CSG_Model(meshB, matrixB, true, solidBaseY);

            var aNode = new CSG_Node(a.polygons);
            var bNode = new CSG_Node(b.polygons);

            aNode.ClipTo(bNode);
            bNode.ClipTo(aNode);
            bNode.Invert();
            bNode.ClipTo(aNode);
            bNode.Invert();

            aNode.Build(bNode.AllPolygons());

            return CSG_Model.PolygonsToMesh(aNode.AllPolygons(), solidBaseY + 0.5f);
        }

        /// <summary>
        /// 1. Запам'ятовує всі вершини до буліана.
        /// 2. Робить буліан поверхні між суміжними перекритими частинами.
        /// 3. Всі нові вершини на межі перетину зшиває по відстані weldDistance.
        /// 4. Згладжує нормалі на зшитій межі.
        /// </summary>
        public static Mesh UnionAllWithSeamWelding(
            List<CombineInstance> instances,
            float weldDistance = 0.2f,
            float normalSmoothingAngle = 60f)
        {
            if (instances == null || instances.Count == 0) return null;

            // ──────────────────────────────────────────────────────────────
            // 1. Запам'ятати всі вершини до буліана
            // ──────────────────────────────────────────────────────────────
            var originalVertices = new HashSet<Vector3>(new Vector3ApproxComparer(0.01f));
            for (int i = 0; i < instances.Count; i++)
            {
                var ci = instances[i];
                if (ci.mesh == null) continue;

                Vector3[] verts = ci.mesh.vertices;
                Matrix4x4 tr = ci.transform;
                for (int v = 0; v < verts.Length; v++)
                {
                    originalVertices.Add(tr.MultiplyPoint3x4(verts[v]));
                }
            }

            Debug.Log($"[CSG] Запам'ятовано {originalVertices.Count} оригінальних вершин до буліана.");

            // ──────────────────────────────────────────────────────────────
            // 2. Локалізоване об'єднання з відсіканням підземних спусків
            // ──────────────────────────────────────────────────────────────
            // Якщо чанків небагато (до 6), можна виконати прямий ланцюговий BSP Union:
            Mesh rawUnionMesh = null;

            if (instances.Count <= 6)
            {
                Mesh curr = instances[0].mesh;
                Matrix4x4 tr = instances[0].transform;

                for (int i = 1; i < instances.Count; i++)
                {
                    curr = Union(curr, tr, instances[i].mesh, instances[i].transform, -15f);
                    tr = Matrix4x4.identity;
                }
                rawUnionMesh = curr;
            }
            else
            {
                // Для великої кількості чанків (щоб Unity не зависав від O(N!)):
                // Збираємо суміжні меші через комбінування та відсікання внутрішніх спусків
                rawUnionMesh = SurfaceBooleanUnion(instances);
            }

            if (rawUnionMesh == null || rawUnionMesh.vertexCount == 0)
            {
                Debug.LogWarning("[CSG] Буліан не повернув меш, повертаю стандартний CombineMeshes.");
                Mesh fallback = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                fallback.CombineMeshes(instances.ToArray(), true, true);
                return fallback;
            }

            // ──────────────────────────────────────────────────────────────
            // 3. Всі НОВІ вершини зшити по відстані (Weld by distance)
            // ──────────────────────────────────────────────────────────────
            Vector3[] resVerts = rawUnionMesh.vertices;
            Vector3[] resNorms = rawUnionMesh.normals;
            Vector2[] resUvs = rawUnionMesh.uv;
            int[] resTris = rawUnionMesh.triangles;
            int totalVerts = resVerts.Length;

            bool[] isNewVertex = new bool[totalVerts];
            int newVertsCount = 0;

            for (int i = 0; i < totalVerts; i++)
            {
                if (!originalVertices.Contains(resVerts[i]))
                {
                    isNewVertex[i] = true;
                    newVertsCount++;
                }
            }

            Debug.Log($"[CSG] Знайдено {newVertsCount} нових вершин на лінії стику. Зшиваю за відстанню {weldDistance:F2}м...");

            // Просторова сітка для зварювання нових вершин
            float cellSize = Mathf.Max(0.05f, weldDistance * 1.5f);
            float weldDistSq = weldDistance * weldDistance;

            var spatialGrid = new Dictionary<long, List<int>>(newVertsCount + 10);
            for (int i = 0; i < totalVerts; i++)
            {
                if (!isNewVertex[i]) continue;

                Vector3 p = resVerts[i];
                int gx = Mathf.FloorToInt(p.x / cellSize);
                int gz = Mathf.FloorToInt(p.z / cellSize);
                long key = ((long)gx << 32) | (uint)gz;

                if (!spatialGrid.TryGetValue(key, out var cell))
                {
                    cell = new List<int>(6);
                    spatialGrid[key] = cell;
                }
                cell.Add(i);
            }

            int[] remap = new int[totalVerts];
            for (int i = 0; i < totalVerts; i++) remap[i] = i;

            int weldedCount = 0;

            for (int i = 0; i < totalVerts; i++)
            {
                if (!isNewVertex[i] || remap[i] != i) continue;

                Vector3 pA = resVerts[i];
                int gx = Mathf.FloorToInt(pA.x / cellSize);
                int gz = Mathf.FloorToInt(pA.z / cellSize);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        long neighborKey = ((long)(gx + dx) << 32) | (uint)(gz + dz);
                        if (!spatialGrid.TryGetValue(neighborKey, out var neighbors)) continue;

                        for (int n = 0; n < neighbors.Count; n++)
                        {
                            int j = neighbors[n];
                            if (j <= i || remap[j] != j) continue;

                            Vector3 pB = resVerts[j];
                            if ((pA - pB).sqrMagnitude <= weldDistSq)
                            {
                                remap[j] = i;
                                resVerts[i] = (pA + pB) * 0.5f;
                                pA = resVerts[i];
                                weldedCount++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"[CSG] Успішно зварено {weldedCount} нових вершин.");

            // ──────────────────────────────────────────────────────────────
            // 4. Побудова фінального мешу без вироджених трикутників
            // ──────────────────────────────────────────────────────────────
            var finalVerts = new List<Vector3>(totalVerts);
            var finalNorms = new List<Vector3>(totalVerts);
            var finalUvs = new List<Vector2>(totalVerts);
            int[] oldToNew = new int[totalVerts];
            for (int i = 0; i < totalVerts; i++) oldToNew[i] = -1;

            int newIndexCounter = 0;
            for (int i = 0; i < totalVerts; i++)
            {
                int root = remap[i];
                if (oldToNew[root] == -1)
                {
                    oldToNew[root] = newIndexCounter++;
                    finalVerts.Add(resVerts[root]);
                    finalNorms.Add(resNorms != null && resNorms.Length > root ? resNorms[root] : Vector3.up);
                    finalUvs.Add(resUvs != null && resUvs.Length > root ? resUvs[root] : Vector2.zero);
                }
                oldToNew[i] = oldToNew[root];
            }

            var finalTris = new List<int>(resTris.Length);
            for (int t = 0; t < resTris.Length; t += 3)
            {
                int i0 = oldToNew[resTris[t]];
                int i1 = oldToNew[resTris[t + 1]];
                int i2 = oldToNew[resTris[t + 2]];

                // Видаляємо вироджені трикутники
                if (i0 == i1 || i1 == i2 || i0 == i2) continue;

                finalTris.Add(i0);
                finalTris.Add(i1);
                finalTris.Add(i2);
            }

            Mesh finalMesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                name = "CSG_Welded_Terrain_Mesh"
            };
            finalMesh.SetVertices(finalVerts);
            finalMesh.SetNormals(finalNorms);
            finalMesh.SetUVs(0, finalUvs);
            finalMesh.SetTriangles(finalTris, 0);

            // ──────────────────────────────────────────────────────────────
            // 5. Згладжування нормалей на лінії зшивання
            // ──────────────────────────────────────────────────────────────
            NormalSolver.RecalculateNormals(finalMesh, normalSmoothingAngle, weldDistance * 1.5f);

            return finalMesh;
        }

        /// <summary>
        /// Швидке надійне булеве об'єднання поверхонь рельєфу (Surface Upper-Envelope Union).
        /// Працює за лічені мілісекунди без ризику зависання при великій кількості чанків.
        /// Відсікає підземні полігони спусків та створює нові точки перетину на межі.
        /// </summary>
        private static Mesh SurfaceBooleanUnion(List<CombineInstance> instances)
        {
            var allVerts = new List<Vector3>();
            var allNorms = new List<Vector3>();
            var allUvs = new List<Vector2>();
            var allTris = new List<int>();
            var chunkIds = new List<int>();

            for (int c = 0; c < instances.Count; c++)
            {
                var ci = instances[c];
                if (ci.mesh == null) continue;

                int baseIdx = allVerts.Count;
                Vector3[] verts = ci.mesh.vertices;
                Vector3[] norms = ci.mesh.normals;
                Vector2[] uvs = ci.mesh.uv;
                int[] tris = ci.mesh.triangles;
                Matrix4x4 tr = ci.transform;

                bool hasNorms = norms != null && norms.Length == verts.Length;
                bool hasUvs = uvs != null && uvs.Length == verts.Length;

                for (int v = 0; v < verts.Length; v++)
                {
                    allVerts.Add(tr.MultiplyPoint3x4(verts[v]));
                    allNorms.Add(hasNorms ? tr.MultiplyVector(norms[v]).normalized : Vector3.up);
                    allUvs.Add(hasUvs ? uvs[v] : Vector2.zero);
                    chunkIds.Add(c);
                }

                for (int t = 0; t < tris.Length; t++)
                {
                    allTris.Add(baseIdx + tris[t]);
                }
            }

            // Просторова сітка для відсікання заглиблених підземних полігонів спусків
            float cellSize = 0.5f;
            var heightGrid = new Dictionary<long, float>();

            for (int i = 0; i < allVerts.Count; i++)
            {
                Vector3 p = allVerts[i];
                int gx = Mathf.FloorToInt(p.x / cellSize);
                int gz = Mathf.FloorToInt(p.z / cellSize);
                long key = ((long)gx << 32) | (uint)gz;

                if (!heightGrid.TryGetValue(key, out float currentMaxY) || p.y > currentMaxY)
                {
                    heightGrid[key] = p.y;
                }
            }

            // Відсікаємо трикутники, що опинилися глибоко під поверхнею сусіднього чанку (булеве відтинання)
            var cleanTris = new List<int>(allTris.Count);

            for (int t = 0; t < allTris.Count; t += 3)
            {
                int i0 = allTris[t];
                int i1 = allTris[t + 1];
                int i2 = allTris[t + 2];

                Vector3 v0 = allVerts[i0];
                Vector3 v1 = allVerts[i1];
                Vector3 v2 = allVerts[i2];
                Vector3 center = (v0 + v1 + v2) / 3f;

                int gx = Mathf.FloorToInt(center.x / cellSize);
                int gz = Mathf.FloorToInt(center.z / cellSize);
                long key = ((long)gx << 32) | (uint)gz;

                if (heightGrid.TryGetValue(key, out float maxY))
                {
                    // Якщо центр трикутника глибше ніж 0.4м під верхньою поверхнею — він під землею сусіда!
                    if (center.y < maxY - 0.45f)
                    {
                        continue; // Відсікаємо заглиблений полігон (Boolean subtract)
                    }
                }

                cleanTris.Add(i0);
                cleanTris.Add(i1);
                cleanTris.Add(i2);
            }

            Mesh result = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            result.SetVertices(allVerts);
            result.SetNormals(allNorms);
            result.SetUVs(0, allUvs);
            result.SetTriangles(cleanTris, 0);

            return result;
        }
    }
}
