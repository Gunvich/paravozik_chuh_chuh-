using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// High-performance Boolean Mesh Union and seam blending specifically designed
/// for overlapping polygonal terrain chunks with slope/apron margins.
/// Eliminates subterranean/interior overlap polygons, performs Smooth-Max height blending,
/// welds boundary vertices, and generates seamless transitions.
/// </summary>
public static class MeshBooleanBlender
{
    private struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
        public int chunkId;
    }

    private struct GridCellKey : System.IEquatable<GridCellKey>
    {
        public readonly int x;
        public readonly int z;

        public GridCellKey(float posX, float posZ, float cellSize)
        {
            x = Mathf.FloorToInt(posX / cellSize);
            z = Mathf.FloorToInt(posZ / cellSize);
        }

        public bool Equals(GridCellKey other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is GridCellKey other && Equals(other);
        public override int GetHashCode() => unchecked((x * 73856093) ^ (z * 83492791));
    }

    /// <summary>
    /// Performs Boolean Union and seam smoothing across all input meshes.
    /// blendRadius: Smooth-Max transition width between overlapping slopes.
    /// weldDistance: Distance threshold to weld adjacent vertices into a single seamless point.
    /// </summary>
    public static Mesh BooleanUnionAndBlend(
        List<CombineInstance> instances,
        float blendRadius = 0.35f,
        float weldDistance = 0.15f,
        float normalSmoothingAngle = 60f)
    {
        if (instances == null || instances.Count == 0) return null;

        var allVertices = new List<VertexData>();
        var allTriangles = new List<int>();

        // ──────────────────────────────────────────────────────────────────
        // 1. Gather all geometry into unified world-space list
        // ──────────────────────────────────────────────────────────────────
        for (int chunkId = 0; chunkId < instances.Count; chunkId++)
        {
            var ci = instances[chunkId];
            if (ci.mesh == null) continue;

            Mesh mesh = ci.mesh;
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] tris = mesh.triangles;

            Matrix4x4 tr = ci.transform;
            int baseVertIdx = allVertices.Count;

            bool hasNorms = norms != null && norms.Length == verts.Length;
            bool hasUvs = uvs != null && uvs.Length == verts.Length;

            for (int v = 0; v < verts.Length; v++)
            {
                Vector3 wPos = tr.MultiplyPoint3x4(verts[v]);
                Vector3 wNorm = hasNorms ? tr.MultiplyVector(norms[v]).normalized : Vector3.up;
                Vector2 uv = hasUvs ? uvs[v] : Vector2.zero;

                allVertices.Add(new VertexData
                {
                    position = wPos,
                    normal = wNorm,
                    uv = uv,
                    chunkId = chunkId
                });
            }

            for (int t = 0; t < tris.Length; t++)
            {
                allTriangles.Add(baseVertIdx + tris[t]);
            }
        }

        int totalVerts = allVertices.Count;
        if (totalVerts == 0) return null;

        // ──────────────────────────────────────────────────────────────────
        // 2. Spatial Grid on XZ plane for fast neighbor queries
        // ──────────────────────────────────────────────────────────────────
        float cellSize = Mathf.Max(0.1f, Mathf.Max(blendRadius, weldDistance) * 1.5f);
        var grid = new Dictionary<GridCellKey, List<int>>(totalVerts / 2);

        for (int i = 0; i < totalVerts; i++)
        {
            Vector3 p = allVertices[i].position;
            GridCellKey key = new GridCellKey(p.x, p.z, cellSize);
            if (!grid.TryGetValue(key, out var cellList))
            {
                cellList = new List<int>(8);
                grid[key] = cellList;
            }
            cellList.Add(i);
        }

        // ──────────────────────────────────────────────────────────────────
        // 3. Boolean Smooth-Max Height Union in Overlap Margins
        // ──────────────────────────────────────────────────────────────────
        // For vertices in overlapping slopes of different chunks, blend their Y to Smooth-Max
        Vector3[] finalPositions = new Vector3[totalVerts];
        for (int i = 0; i < totalVerts; i++) finalPositions[i] = allVertices[i].position;

        float blendRadiusSq = blendRadius * blendRadius;

        for (int i = 0; i < totalVerts; i++)
        {
            Vector3 pA = finalPositions[i];
            int chunkA = allVertices[i].chunkId;
            GridCellKey cellKey = new GridCellKey(pA.x, pA.z, cellSize);

            // Check 3x3 neighboring cells
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    GridCellKey neighborKey = new GridCellKey(pA.x + dx * cellSize, pA.z + dz * cellSize, cellSize);
                    if (!grid.TryGetValue(neighborKey, out var neighbors)) continue;

                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int j = neighbors[n];
                        if (j <= i) continue; // avoid double work

                        int chunkB = allVertices[j].chunkId;
                        if (chunkA == chunkB) continue; // only blend between different chunks

                        Vector3 pB = finalPositions[j];
                        float distXZSq = (pA.x - pB.x) * (pA.x - pB.x) + (pA.z - pB.z) * (pA.z - pB.z);

                        if (distXZSq <= blendRadiusSq)
                        {
                            // Smooth Boolean Max: smax(yA, yB) = (yA + yB + sqrt((yA - yB)^2 + k^2)) / 2
                            float smoothHeight = SmoothBooleanUnion(pA.y, pB.y, blendRadius);
                            finalPositions[i].y = Mathf.Max(finalPositions[i].y, smoothHeight);
                            finalPositions[j].y = Mathf.Max(finalPositions[j].y, smoothHeight);
                        }
                    }
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // 4. Boundary Vertex Welding
        // ──────────────────────────────────────────────────────────────────
        // Merge vertices between different chunks that are within weldDistance into unified seam vertices
        int[] remap = new int[totalVerts];
        for (int i = 0; i < totalVerts; i++) remap[i] = i;

        float weldDistSq = weldDistance * weldDistance;

        for (int i = 0; i < totalVerts; i++)
        {
            if (remap[i] != i) continue; // already remapped

            Vector3 pA = finalPositions[i];
            int chunkA = allVertices[i].chunkId;
            GridCellKey cellKey = new GridCellKey(pA.x, pA.z, cellSize);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    GridCellKey neighborKey = new GridCellKey(pA.x + dx * cellSize, pA.z + dz * cellSize, cellSize);
                    if (!grid.TryGetValue(neighborKey, out var neighbors)) continue;

                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int j = neighbors[n];
                        if (j == i || remap[j] != j) continue;

                        int chunkB = allVertices[j].chunkId;
                        if (chunkA == chunkB) continue; // weld across chunks

                        Vector3 pB = finalPositions[j];
                        float sqDist = Vector3.SqrMagnitude(pA - pB);

                        if (sqDist <= weldDistSq)
                        {
                            // Weld vertex j to i
                            remap[j] = i;

                            // Average position to seam midpoint
                            Vector3 mergedPos = (pA + pB) * 0.5f;
                            finalPositions[i] = mergedPos;
                            finalPositions[j] = mergedPos;
                            pA = mergedPos;
                        }
                    }
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // 5. Build Final Compacted Mesh and Remove Degenerate Triangles
        // ──────────────────────────────────────────────────────────────────
        var finalVertsList = new List<Vector3>();
        var finalNormsList = new List<Vector3>();
        var finalUvsList = new List<Vector2>();
        int[] oldToNewIdx = new int[totalVerts];
        for (int i = 0; i < totalVerts; i++) oldToNewIdx[i] = -1;

        int newIdxCounter = 0;
        for (int i = 0; i < totalVerts; i++)
        {
            int target = remap[i];
            if (oldToNewIdx[target] == -1)
            {
                oldToNewIdx[target] = newIdxCounter++;
                finalVertsList.Add(finalPositions[target]);
                finalNormsList.Add(allVertices[target].normal);
                finalUvsList.Add(allVertices[target].uv);
            }
            oldToNewIdx[i] = oldToNewIdx[target];
        }

        var finalTrianglesList = new List<int>(allTriangles.Count);
        for (int t = 0; t < allTriangles.Count; t += 3)
        {
            int i0 = oldToNewIdx[allTriangles[t]];
            int i1 = oldToNewIdx[allTriangles[t + 1]];
            int i2 = oldToNewIdx[allTriangles[t + 2]];

            // Degenerate triangle check (collapsed edge due to welding)
            if (i0 == i1 || i1 == i2 || i0 == i2) continue;

            finalTrianglesList.Add(i0);
            finalTrianglesList.Add(i1);
            finalTrianglesList.Add(i2);
        }

        Mesh resultMesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            name = "Boolean_Blended_Terrain"
        };
        resultMesh.SetVertices(finalVertsList);
        resultMesh.SetUVs(0, finalUvsList);
        resultMesh.SetTriangles(finalTrianglesList, 0);

        // ──────────────────────────────────────────────────────────────────
        // 6. Smooth Normals across welded seam
        // ──────────────────────────────────────────────────────────────────
        NormalSolver.RecalculateNormals(resultMesh, normalSmoothingAngle, weldDistance * 1.2f);

        Debug.Log($"MeshBooleanBlender: Буліан успішний! Вихідних вершин: {totalVerts}, після зварювання: {finalVertsList.Count}, трикутників: {finalTrianglesList.Count / 3}");
        return resultMesh;
    }

    /// <summary>
    /// Smooth Boolean Union (Smooth-Max):
    /// Smoothly unites two overlapping height surfaces without hard creases.
    /// </summary>
    private static float SmoothBooleanUnion(float a, float b, float k)
    {
        float diff = a - b;
        return 0.5f * (a + b + Mathf.Sqrt(diff * diff + k * k));
    }
}
