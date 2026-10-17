using System.Collections.Generic;
using UnityEngine;

namespace Parabox.CSG
{
    public class CSG_Polygon
    {
        public List<CSG_Vertex> vertices;
        public CSG_Plane plane;

        public CSG_Polygon(List<CSG_Vertex> list)
        {
            vertices = list;
            plane = new CSG_Plane(list[0].position, list[1].position, list[2].position);
        }

        public CSG_Polygon(List<CSG_Vertex> list, CSG_Plane p)
        {
            vertices = list;
            plane = p;
        }

        public void Flip()
        {
            vertices.Reverse();
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                v.Flip();
                vertices[i] = v;
            }
            plane.Flip();
        }
    }

    public struct CSG_Plane
    {
        public const float EPSILON = 0.0001f;

        public Vector3 normal;
        public float w;

        public CSG_Plane(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            float mag = cross.magnitude;
            if (mag > 1e-6f)
            {
                normal = cross / mag;
            }
            else
            {
                normal = Vector3.up;
            }
            w = Vector3.Dot(normal, a);
        }

        public CSG_Plane(Vector3 n, float w)
        {
            normal = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.up;
            this.w = w;
        }

        public void Flip()
        {
            normal = -normal;
            w = -w;
        }

        public void SplitPolygon(
            CSG_Polygon polygon,
            List<CSG_Polygon> coplanarFront,
            List<CSG_Polygon> coplanarBack,
            List<CSG_Polygon> front,
            List<CSG_Polygon> back)
        {
            const int COPLANAR = 0;
            const int FRONT = 1;
            const int BACK = 2;
            const int SPANNING = 3;

            int polygonType = 0;
            int vertCount = polygon.vertices.Count;
            var types = new List<int>(vertCount);

            for (int i = 0; i < vertCount; i++)
            {
                float t = Vector3.Dot(normal, polygon.vertices[i].position) - w;
                int type = (t < -EPSILON) ? BACK : (t > EPSILON) ? FRONT : COPLANAR;
                polygonType |= type;
                types.Add(type);
            }

            switch (polygonType)
            {
                case COPLANAR:
                    (Vector3.Dot(normal, polygon.plane.normal) > 0 ? coplanarFront : coplanarBack).Add(polygon);
                    break;

                case FRONT:
                    front.Add(polygon);
                    break;

                case BACK:
                    back.Add(polygon);
                    break;

                case SPANNING:
                    var f = new List<CSG_Vertex>();
                    var b = new List<CSG_Vertex>();

                    for (int i = 0; i < vertCount; i++)
                    {
                        int j = (i + 1) % vertCount;
                        int ti = types[i];
                        int tj = types[j];
                        CSG_Vertex vi = polygon.vertices[i];
                        CSG_Vertex vj = polygon.vertices[j];

                        if (ti != BACK) f.Add(vi);
                        if (ti != FRONT) b.Add(vi);

                        if ((ti | tj) == SPANNING)
                        {
                            Vector3 dir = vj.position - vi.position;
                            float denom = Vector3.Dot(normal, dir);

                            if (Mathf.Abs(denom) > 1e-6f)
                            {
                                float t = (w - Vector3.Dot(normal, vi.position)) / denom;
                                if (!float.IsNaN(t) && !float.IsInfinity(t))
                                {
                                    t = Mathf.Clamp(t, 0.0001f, 0.9999f);
                                    CSG_Vertex v = CSG_Vertex.Lerp(vi, vj, t);
                                    f.Add(v);
                                    b.Add(v);
                                }
                            }
                        }
                    }

                    if (f.Count >= 3) front.Add(new CSG_Polygon(f, polygon.plane));
                    if (b.Count >= 3) back.Add(new CSG_Polygon(b, polygon.plane));
                    break;
            }
        }
    }
}
