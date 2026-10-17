using System.Collections.Generic;
using UnityEngine;

namespace Parabox.CSG
{
    public class CSG_Node
    {
        public List<CSG_Polygon> polygons;
        public CSG_Plane plane;
        public CSG_Node front;
        public CSG_Node back;

        private const int MAX_DEPTH = 22;

        public CSG_Node()
        {
            polygons = new List<CSG_Polygon>();
        }

        public CSG_Node(List<CSG_Polygon> list)
        {
            polygons = new List<CSG_Polygon>();
            Build(list, 0);
        }

        public CSG_Node Clone()
        {
            CSG_Node clone = new CSG_Node();
            clone.plane = plane;
            clone.front = front != null ? front.Clone() : null;
            clone.back = back != null ? back.Clone() : null;
            clone.polygons = new List<CSG_Polygon>(polygons);
            return clone;
        }

        public void Invert()
        {
            for (int i = 0; i < polygons.Count; i++)
                polygons[i].Flip();

            plane.Flip();

            if (front != null) front.Invert();
            if (back != null) back.Invert();

            CSG_Node temp = front;
            front = back;
            back = temp;
        }

        public List<CSG_Polygon> ClipPolygons(List<CSG_Polygon> list)
        {
            if (polygons.Count == 0) return new List<CSG_Polygon>(list);

            var cpFront = new List<CSG_Polygon>();
            var cpBack = new List<CSG_Polygon>();
            var f = new List<CSG_Polygon>();
            var b = new List<CSG_Polygon>();

            for (int i = 0; i < list.Count; i++)
            {
                plane.SplitPolygon(list[i], cpFront, cpBack, f, b);
            }

            if (front != null) f = front.ClipPolygons(f);
            if (back != null) b = back.ClipPolygons(b);
            else b.Clear();

            f.AddRange(cpFront);
            f.AddRange(b);
            return f;
        }

        public void ClipTo(CSG_Node bsp)
        {
            polygons = bsp.ClipPolygons(polygons);
            if (front != null) front.ClipTo(bsp);
            if (back != null) back.ClipTo(bsp);
        }

        public List<CSG_Polygon> AllPolygons()
        {
            var list = new List<CSG_Polygon>(polygons);
            if (front != null) list.AddRange(front.AllPolygons());
            if (back != null) list.AddRange(back.AllPolygons());
            return list;
        }

        public void Build(List<CSG_Polygon> list, int depth = 0)
        {
            if (list == null || list.Count == 0) return;

            if (depth >= MAX_DEPTH)
            {
                polygons.AddRange(list);
                return;
            }

            if (polygons.Count == 0)
            {
                plane = list[0].plane;
            }

            var cpFront = new List<CSG_Polygon>();
            var cpBack = new List<CSG_Polygon>();
            var f = new List<CSG_Polygon>();
            var b = new List<CSG_Polygon>();

            for (int i = 0; i < list.Count; i++)
            {
                plane.SplitPolygon(list[i], cpFront, cpBack, f, b);
            }

            polygons.AddRange(cpFront);
            polygons.AddRange(cpBack);

            if (f.Count > 0)
            {
                if (front == null) front = new CSG_Node();
                front.Build(f, depth + 1);
            }

            if (b.Count > 0)
            {
                if (back == null) back = new CSG_Node();
                back.Build(b, depth + 1);
            }
        }
    }
}
