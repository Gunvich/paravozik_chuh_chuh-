using System.Collections.Generic;
using UnityEngine;

public class ChunkData : MonoBehaviour
{
    [Header("Класифікація")]
    public ChunkCategory category;
    public BiomeZone biome;

    [Header("Зони XZ (замість PolygonCollider2D)")]
    [Tooltip("Core zone — чанк не може перетинатися з іншими core-зонами.")]
    public Zone3D zoneA_Core;
    [Tooltip("Aura zone — звідси беруться точки для spawning наступних чанків.")]
    public Zone3D zoneB_Aura;

    [Header("Ребра (Edge-to-Edge 1-to-N)")]
    [Tooltip("Фізичні ребра багатокутного чанку для стикування 1-to-N.")]
    public ChunkEdge[] edges;

    [Header("Конектори (Legacy edge snapping)")]
    [Tooltip("Додай ChunkConnector-и як дочірні GameObject-и.")]
    public ChunkConnector[] connectors;

    [Header("Правила напряму")]
    public List<DirectionRule> rules = new List<DirectionRule>();

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureEdgesCached();
    }

    /// <summary>Ensures edges array is populated from children if not set in Inspector.</summary>
    public void EnsureEdgesCached()
    {
        if (edges == null || edges.Length == 0)
        {
            edges = GetComponentsInChildren<ChunkEdge>();
        }
    }

    /// <summary>Returns all edges that currently have at least minRequiredLength free space.</summary>
    public List<ChunkEdge> GetFreeEdges(float minRequiredLength = 1f)
    {
        EnsureEdgesCached();
        var result = new List<ChunkEdge>();
        if (edges == null) return result;

        foreach (var e in edges)
        {
            if (e != null && e.HasFreeSpace(minRequiredLength))
                result.Add(e);
        }
        return result;
    }

    /// <summary>
    /// Повертає випадкову точку в Aura-зоні (world space XZ).
    /// Fallback — позиція чанку.
    /// </summary>
    public Vector3 GetRandomPointInAura()
    {
        if (zoneB_Aura != null)
            return GenMath.GetRandomPointInZone3D(zoneB_Aura);

        // Fallback: random point in a small circle around the chunk
        Vector2 rand = Random.insideUnitCircle * 5f;
        return transform.position + new Vector3(rand.x, 0f, rand.y);
    }

    /// <summary>Returns all connectors that are currently free.</summary>
    public List<ChunkConnector> GetFreeConnectors()
    {
        var result = new List<ChunkConnector>();
        if (connectors == null) return result;
        foreach (var c in connectors)
        {
            if (c != null && !c.isOccupied)
                result.Add(c);
        }
        return result;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (rules == null) return;
        foreach (var rule in rules)
        {
            Vector3 worldDir = transform.TransformDirection(rule.localDirection).normalized;
            Gizmos.color = rule.isMandatory ? Color.red : Color.green;
            Vector3 startPos = transform.position + Vector3.up * 2f;
            Gizmos.DrawLine(startPos, startPos + worldDir * 4f);
            Gizmos.DrawWireSphere(startPos + worldDir * 4f, 0.5f);
        }
    }
}