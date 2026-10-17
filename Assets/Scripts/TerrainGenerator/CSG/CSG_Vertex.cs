using System.Collections.Generic;
using UnityEngine;

namespace Parabox.CSG
{
    public struct CSG_Vertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;

        public CSG_Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            this.position = position;
            this.normal = normal;
            this.uv = uv;
        }

        public void Flip()
        {
            normal = -normal;
        }

        public static CSG_Vertex Lerp(CSG_Vertex a, CSG_Vertex b, float t)
        {
            return new CSG_Vertex(
                Vector3.Lerp(a.position, b.position, t),
                Vector3.Lerp(a.normal, b.normal, t),
                Vector2.Lerp(a.uv, b.uv, t)
            );
        }
    }
}
