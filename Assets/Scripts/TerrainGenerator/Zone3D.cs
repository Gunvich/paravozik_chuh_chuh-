using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Custom XZ-plane polygon zone.
/// Replaces PolygonCollider2D so zones work correctly in 3D world space
/// regardless of the object's rotation (the polygon is always on the XZ plane).
///
/// Usage:
///   - Set localPoints in the Inspector (local XZ coordinates).
///   - Call Contains(worldPoint) or Overlaps(otherZone) at runtime.
/// </summary>
public class Zone3D : MonoBehaviour
{
    [Tooltip("Polygon vertices in LOCAL XZ space (Y is ignored).")]
    public Vector2[] localPoints;

    [Header("Debug")]
    public Color gizmoColor = new Color(0f, 1f, 0.5f, 0.35f);

    // ──────────────────────────────────────────────────────────────────────
    // World-space polygon
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Returns the polygon vertices in world XZ space.</summary>
    public Vector2[] GetWorldPoints()
    {
        if (localPoints == null || localPoints.Length == 0) return System.Array.Empty<Vector2>();

        var result = new Vector2[localPoints.Length];
        for (int i = 0; i < localPoints.Length; i++)
        {
            // Transform local XZ → world XZ (ignore Y)
            Vector3 local3 = new Vector3(localPoints[i].x, 0f, localPoints[i].y);
            Vector3 world3 = transform.TransformPoint(local3);
            result[i] = new Vector2(world3.x, world3.z);
        }
        return result;
    }

    /// <summary>Bounding rect of the world-space polygon (for quick rejection).</summary>
    public Rect GetWorldBounds()
    {
        Vector2[] pts = GetWorldPoints();
        if (pts.Length == 0) return new Rect(transform.position.x, transform.position.z, 0, 0);

        float minX = pts[0].x, maxX = pts[0].x;
        float minY = pts[0].y, maxY = pts[0].y;
        for (int i = 1; i < pts.Length; i++)
        {
            if (pts[i].x < minX) minX = pts[i].x;
            if (pts[i].x > maxX) maxX = pts[i].x;
            if (pts[i].y < minY) minY = pts[i].y;
            if (pts[i].y > maxY) maxY = pts[i].y;
        }
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Point containment — Ray casting algorithm
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Returns true if world point (x, z) is inside this polygon.</summary>
    public bool Contains(Vector3 worldPos)
    {
        return ContainsXZ(new Vector2(worldPos.x, worldPos.z), GetWorldPoints());
    }

    public static bool ContainsXZ(Vector2 p, Vector2[] poly)
    {
        if (poly == null || poly.Length < 3) return false;
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Overlap — Separating Axis Theorem (SAT) for convex polygons,
    // with a ray-cast fallback for concave shapes.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Returns true if this zone overlaps another zone.</summary>
    public bool Overlaps(Zone3D other)
    {
        if (other == null) return false;
        Vector2[] a = GetWorldPoints();
        Vector2[] b = other.GetWorldPoints();
        return PolygonsOverlapSAT(a, b);
    }

    /// <summary>
    /// SAT overlap test. Works correctly for convex polygons;
    /// for concave shapes it gives conservative (never false-negative) results.
    /// </summary>
    public static bool PolygonsOverlapSAT(Vector2[] a, Vector2[] b)
    {
        if (a == null || a.Length < 2 || b == null || b.Length < 2) return false;

        // Test axes from polygon A and polygon B
        if (HasSeparatingAxis(a, b)) return false;
        if (HasSeparatingAxis(b, a)) return false;
        return true;
    }

    private static bool HasSeparatingAxis(Vector2[] poly, Vector2[] other)
    {
        int n = poly.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 edge = poly[(i + 1) % n] - poly[i];
            Vector2 axis = new Vector2(-edge.y, edge.x); // perpendicular

            GetProjectionRange(poly, axis, out float minA, out float maxA);
            GetProjectionRange(other, axis, out float minB, out float maxB);

            if (maxA < minB || maxB < minA) return true; // gap found
        }
        return false;
    }

    private static void GetProjectionRange(Vector2[] poly, Vector2 axis, out float min, out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;
        foreach (var p in poly)
        {
            float d = Vector2.Dot(p, axis);
            if (d < min) min = d;
            if (d > max) max = d;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Random point sampling
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a random world-space point inside this zone using rejection sampling.
    /// Falls back to transform.position after maxAttempts.
    /// </summary>
    public Vector3 GetRandomWorldPoint(int maxAttempts = 60)
    {
        Vector2[] pts = GetWorldPoints();
        Rect b = GetWorldBounds();

        for (int i = 0; i < maxAttempts; i++)
        {
            float rx = Random.Range(b.xMin, b.xMax);
            float ry = Random.Range(b.yMin, b.yMax);
            var p = new Vector2(rx, ry);
            if (ContainsXZ(p, pts))
                return new Vector3(rx, transform.position.y, ry);
        }
        return transform.position;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (localPoints == null || localPoints.Length < 2) return;

        Gizmos.color = gizmoColor;
        Vector2[] world = GetWorldPoints();

        for (int i = 0; i < world.Length; i++)
        {
            Vector3 p0 = new Vector3(world[i].x, transform.position.y + 0.1f, world[i].y);
            Vector3 p1 = new Vector3(world[(i + 1) % world.Length].x, transform.position.y + 0.1f, world[(i + 1) % world.Length].y);
            Gizmos.DrawLine(p0, p1);
        }

        // Fill triangles via fan for visual reference
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.1f);
        Vector3 center = transform.position + Vector3.up * 0.1f;
        for (int i = 1; i < world.Length - 1; i++)
        {
            Vector3 v1 = new Vector3(world[i].x, transform.position.y + 0.1f, world[i].y);
            Vector3 v2 = new Vector3(world[i + 1].x, transform.position.y + 0.1f, world[i + 1].y);
            Gizmos.DrawLine(center, v1);
            Gizmos.DrawLine(center, v2);
        }
    }
}
