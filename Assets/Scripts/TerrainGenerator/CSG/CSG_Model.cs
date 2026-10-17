using System.Collections.Generic;
using UnityEngine;

namespace Parabox.CSG
{
    public class CSG_Model
    {
        public List<CSG_Polygon> polygons;

        public CSG_Model()
        {
            polygons = new List<CSG_Polygon>();
        }

        public CSG_Model(Mesh mesh, Matrix4x4 matrix, bool makeSolid = true, float solidBaseY = -15f)
        {
            polygons = new List<CSG_Polygon>();
            if (mesh == null) return;

            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] tris = mesh.triangles;

            bool hasNorms = norms != null && norms.Length == verts.Length;
            bool hasUvs = uvs != null && uvs.Length == verts.Length;

            // Transform vertices to world space
            CSG_Vertex[] worldVerts = new CSG_Vertex[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                worldVerts[i] = new CSG_Vertex(
                    matrix.MultiplyPoint3x4(verts[i]),
                    hasNorms ? matrix.MultiplyVector(norms[i]).normalized : Vector3.up,
                    hasUvs ? uvs[i] : Vector2.zero
                );
            }

            // Convert triangles to polygons
            for (int i = 0; i < tris.Length; i += 3)
            {
                var pVerts = new List<CSG_Vertex>(3)
                {
                    worldVerts[tris[i]],
                    worldVerts[tris[i + 1]],
                    worldVerts[tris[i + 2]]
                };

                // Guard against degenerate input triangles
                Vector3 cross = Vector3.Cross(pVerts[1].position - pVerts[0].position, pVerts[2].position - pVerts[0].position);
                if (cross.sqrMagnitude > 1e-8f)
                {
                    polygons.Add(new CSG_Polygon(pVerts));
                }
            }

            // If requested, add a downward skirt to form a watertight solid for BSP CSG
            if (makeSolid)
            {
                AddSolidSkirt(worldVerts, tris, solidBaseY);
            }
        }

        private void AddSolidSkirt(CSG_Vertex[] worldVerts, int[] tris, float solidBaseY)
        {
            // Find boundary edges
            var edgeCount = new Dictionary<long, EdgeInfo>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                RegisterEdge(edgeCount, tris[i], tris[i + 1]);
                RegisterEdge(edgeCount, tris[i + 1], tris[i + 2]);
                RegisterEdge(edgeCount, tris[i + 2], tris[i]);
            }

            var baseVertices = new List<Vector3>();

            foreach (var kvp in edgeCount)
            {
                if (kvp.Value.count == 1) // boundary edge!
                {
                    int v0Idx = kvp.Value.v0;
                    int v1Idx = kvp.Value.v1;

                    CSG_Vertex top0 = worldVerts[v0Idx];
                    CSG_Vertex top1 = worldVerts[v1Idx];

                    CSG_Vertex bot0 = new CSG_Vertex(new Vector3(top0.position.x, solidBaseY, top0.position.z), Vector3.down, top0.uv);
                    CSG_Vertex bot1 = new CSG_Vertex(new Vector3(top1.position.x, solidBaseY, top1.position.z), Vector3.down, top1.uv);

                    // Wall quad = 2 triangles
                    polygons.Add(new CSG_Polygon(new List<CSG_Vertex> { top0, top1, bot1 }));
                    polygons.Add(new CSG_Polygon(new List<CSG_Vertex> { top0, bot1, bot0 }));

                    baseVertices.Add(bot0.position);
                    baseVertices.Add(bot1.position);
                }
            }
        }

        private struct EdgeInfo
        {
            public int v0;
            public int v1;
            public int count;
        }

        private void RegisterEdge(Dictionary<long, EdgeInfo> dict, int a, int b)
        {
            int min = Mathf.Min(a, b);
            int max = Mathf.Max(a, b);
            long key = ((long)min << 32) | (uint)max;

            if (dict.TryGetValue(key, out var info))
            {
                info.count++;
                dict[key] = info;
            }
            else
            {
                dict[key] = new EdgeInfo { v0 = a, v1 = b, count = 1 };
            }
        }

        public static Mesh PolygonsToMesh(List<CSG_Polygon> polys, float filterBelowY = -14.5f)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < polys.Count; i++)
            {
                var p = polys[i];
                if (p.vertices.Count < 3) continue;

                // Filter out the artificial solid base floor
                bool isFloor = true;
                for (int v = 0; v < p.vertices.Count; v++)
                {
                    if (p.vertices[v].position.y > filterBelowY)
                    {
                        isFloor = false;
                        break;
                    }
                }
                if (isFloor) continue;

                // Triangulate polygon via triangle fan
                int baseIdx = vertices.Count;
                vertices.Add(p.vertices[0].position);
                normals.Add(p.vertices[0].normal);
                uvs.Add(p.vertices[0].uv);

                for (int v = 1; v < p.vertices.Count - 1; v++)
                {
                    int idx1 = vertices.Count;
                    vertices.Add(p.vertices[v].position);
                    normals.Add(p.vertices[v].normal);
                    uvs.Add(p.vertices[v].uv);

                    int idx2 = vertices.Count;
                    vertices.Add(p.vertices[v + 1].position);
                    normals.Add(p.vertices[v + 1].normal);
                    uvs.Add(p.vertices[v + 1].uv);

                    triangles.Add(baseIdx);
                    triangles.Add(idx1);
                    triangles.Add(idx2);
                }
            }

            Mesh m = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            m.SetVertices(vertices);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.SetTriangles(triangles, 0);
            return m;
        }
    }
}
