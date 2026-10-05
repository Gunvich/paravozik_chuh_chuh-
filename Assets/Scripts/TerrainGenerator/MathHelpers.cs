using System.Collections.Generic;
using UnityEngine;

public static class MathHelpers
{
    public static bool IsInSector(Vector3 fromPos, Vector3 toPos, Vector3 forwardDir, float minDot)
    {
        Vector3 dirToTarget = (toPos - fromPos).normalized;
        dirToTarget.y = 0; forwardDir.y = 0;
        return Vector3.Dot(forwardDir.normalized, dirToTarget.normalized) >= minDot;
    }

    public static bool IsZoneAClear(Collider2D zoneA, LayerMask zoneAMask)
    {
        if (zoneA == null) return true;
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = zoneAMask, useTriggers = true };
        List<Collider2D> results = new List<Collider2D>();

        zoneA.OverlapCollider(filter, results);
        foreach (var col in results) if (col != zoneA) return false;

        return true;
    }

    public static Vector2 GetRandomPointInPolygon(PolygonCollider2D poly, int maxAttempts = 50)
    {
        Bounds bounds = poly.bounds;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomPoint = new Vector2(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y));
            if (poly.OverlapPoint(randomPoint)) return randomPoint;
        }
        return (Vector2)poly.transform.position;
    }
}