using System.Collections.Generic;
using UnityEngine;

public class ChunkData : MonoBehaviour
{
    [Header("Ідентифікація")]
    public ChunkCategory category;
    public BiomeZone biome;

    [Header("Фізичні зони (XY площина)")]
    public PolygonCollider2D zoneA_Core; // Габарити (не можна перетинати)
    public PolygonCollider2D zoneB_Aura; // Аура (сюди спавняться сусіди)

    [Header("Правила")]
    public List<DirectionRule> rules = new List<DirectionRule>();

    public Vector2 GetRandomPointInAura()
    {
        if (zoneB_Aura == null) return new Vector2(transform.position.x, transform.position.z);
        return MathHelpers.GetRandomPointInPolygon(zoneB_Aura);
    }

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