using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Єдиний утилітний клас для математики генерації світу.
/// Замінює дублікати MathHelpers та GenMath.
/// </summary>
public static class GenMath
{
    /// <summary>
    /// Перевіряє, чи знаходиться точка toPos у секторі відносно fromPos
    /// з напрямком forwardDir та мінімальним dot product.
    /// </summary>
    public static bool IsInSector(Vector3 fromPos, Vector3 toPos, Vector3 forwardDir, float minDot)
    {
        Vector3 dirToTarget = toPos - fromPos;
        dirToTarget.y = 0;
        forwardDir.y = 0;
        dirToTarget.Normalize();
        forwardDir.Normalize();

        return Vector3.Dot(forwardDir, dirToTarget) >= minDot;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Zone3D helpers (replaces Physics2D collider checks)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Перевіряє, чи Zone3D (core) не перетинається з жодною іншою
    /// зареєстрованою Zone3D у наданому списку.
    /// </summary>
    public static bool IsZone3DFree(Zone3D core, List<Zone3D> registeredCores)
    {
        if (core == null) return true;
        foreach (var other in registeredCores)
        {
            if (other == null || other == core) continue;
            if (core.Overlaps(other)) return false;
        }
        return true;
    }

    /// <summary>
    /// Повертає випадкову точку всередині Zone3D (world space).
    /// </summary>
    public static Vector3 GetRandomPointInZone3D(Zone3D zone, int maxAttempts = 60)
    {
        if (zone == null) return Vector3.zero;
        return zone.GetRandomWorldPoint(maxAttempts);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Legacy PolygonCollider2D helpers (kept for backward compatibility)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// [Legacy] Перевіряє, чи зона (Zone A) не перетинається з іншими колайдерами.
    /// Використовуй IsZone3DFree для нових чанків.
    /// </summary>
    public static bool IsZoneFree(Collider2D zoneA, LayerMask coreLayer)
    {
        if (zoneA == null) return true;

        Physics2D.SyncTransforms();

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(coreLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        List<Collider2D> results = new List<Collider2D>();
        zoneA.Overlap(filter, results);

        foreach (var col in results)
        {
            if (col != zoneA) return false;
        }
        return true;
    }

    /// <summary>
    /// [Legacy] Повертає випадкову точку всередині PolygonCollider2D методом rejection sampling.
    /// Використовуй GetRandomPointInZone3D для нових чанків.
    /// </summary>
    public static Vector2 GetRandomPointInPolygon(PolygonCollider2D poly, int maxAttempts = 50)
    {
        Bounds bounds = poly.bounds;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomPoint = new Vector2(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y));
            if (poly.OverlapPoint(randomPoint)) return randomPoint;
        }
        return (Vector2)poly.transform.position;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Edge snapping helpers (1-to-N and strict 2D Yaw)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates strict 2D Yaw rotation and world position to snap an incoming chunk edge (inEdge)
    /// into a specific allocated interval [allocatedStart, allocatedStart + inEdge.Length] on parentEdge.
    /// Strictly guarantees 0 pitch/roll (prevents vertical gaps and tilt).
    /// </summary>
    public static void CalculateEdgeSnapTransform(
        ChunkEdge parentEdge,
        float allocatedStart,
        ChunkEdge inEdge,
        ChunkData inChunkRoot,
        out Vector3 newPosition,
        out Quaternion newRotation)
    {
        Vector3 parentNormal = parentEdge.WorldOutwardNormal;
        Vector3 targetInNormal = -parentNormal;

        Transform rootTransform = inChunkRoot.transform;

        Vector3 inEdgeLocalStart = rootTransform.InverseTransformPoint(inEdge.WorldStart);
        Vector3 inEdgeLocalEnd = rootTransform.InverseTransformPoint(inEdge.WorldEnd);
        Vector3 inEdgeLocalCenter = (inEdgeLocalStart + inEdgeLocalEnd) * 0.5f;

        Vector3 inEdgeLocalNormal = rootTransform.InverseTransformDirection(inEdge.WorldOutwardNormal);
        inEdgeLocalNormal.y = 0f;
        inEdgeLocalNormal.Normalize();

        float targetAngle = Mathf.Atan2(targetInNormal.x, targetInNormal.z) * Mathf.Rad2Deg;
        float inAngle = Mathf.Atan2(inEdgeLocalNormal.x, inEdgeLocalNormal.z) * Mathf.Rad2Deg;
        float yawDiff = Mathf.DeltaAngle(inAngle, targetAngle);

        newRotation = Quaternion.Euler(0f, yawDiff, 0f);

        float inLength = inEdge.Length;
        Vector3 parentTangent = parentEdge.WorldTangent;
        Vector3 contactWorldPos = parentEdge.WorldStart + parentTangent * (allocatedStart + inLength * 0.5f);

        Vector3 rotatedOffset = newRotation * inEdgeLocalCenter;
        newPosition = contactWorldPos - rotatedOffset;
        newPosition.y = parentEdge.transform.position.y;
    }

    /// <summary>
    /// Computes deterministic world-space Perlin height noise.
    /// Because it only depends on world XZ, two neighboring chunks evaluate to the exact same Y height.
    /// </summary>
    public static float GetWorldHeightNoise(Vector3 worldPos, float scale = 0.05f, float amplitude = 1.0f, int seed = 0)
    {
        float x = (worldPos.x + seed * 123.456f) * scale;
        float z = (worldPos.z + seed * 123.456f) * scale;
        return Mathf.PerlinNoise(x, z) * amplitude;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Connector snapping helpers (upgraded to strict 2D Yaw)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Знаходить найближчий вільний ChunkConnector, сумісний з targetType,
    /// з переліку connectors у заданому радіусі від позиції.
    /// </summary>
    public static ChunkConnector FindNearestFreeConnector(
        List<ChunkConnector> connectors,
        Vector3 fromPosition,
        ConnectorType targetType,
        float maxDistance = 20f)
    {
        ChunkConnector best = null;
        float bestDist = float.MaxValue;

        foreach (var c in connectors)
        {
            if (c == null || !c.IsCompatible(targetType)) continue;
            float dist = Vector3.Distance(fromPosition, c.transform.position);
            if (dist < maxDistance && dist < bestDist)
            {
                bestDist = dist;
                best = c;
            }
        }
        return best;
    }

    /// <summary>
    /// Обчислює позицію і поворот нового чанку з суворим 2D Yaw (без 3D нахилів).
    /// </summary>
    public static void CalculateSnapTransform(
        ChunkConnector openConnector,
        ChunkConnector inConnector,
        out Vector3 newPosition,
        out Quaternion newRotation)
    {
        Vector3 targetForward = -openConnector.transform.forward;
        targetForward.y = 0f;
        targetForward.Normalize();

        Vector3 inForward = inConnector.transform.forward;
        inForward.y = 0f;
        inForward.Normalize();

        float targetAngle = Mathf.Atan2(targetForward.x, targetForward.z) * Mathf.Rad2Deg;
        float inAngle = Mathf.Atan2(inForward.x, inForward.z) * Mathf.Rad2Deg;
        float yawDiff = Mathf.DeltaAngle(inAngle, targetAngle);

        newRotation = Quaternion.Euler(0f, yawDiff, 0f);

        Transform inRoot = inConnector.GetComponentInParent<ChunkData>()?.transform ?? inConnector.transform.parent;
        Vector3 localOffset = inRoot.InverseTransformPoint(inConnector.transform.position);
        newPosition = openConnector.transform.position - (newRotation * localOffset);
        newPosition.y = openConnector.transform.position.y;
    }
}
