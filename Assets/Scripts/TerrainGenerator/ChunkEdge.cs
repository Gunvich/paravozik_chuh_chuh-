using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represent a physical edge of a polygonal chunk on the XZ plane.
/// Supports 1-to-N connections (multiple smaller edges connected to one long edge),
/// edge sliding/tangent jitter within slope margins, and overlap tracking.
/// </summary>
[System.Serializable]
public struct EdgeInterval
{
    public float start;
    public float end;

    public float Length => end - start;

    public EdgeInterval(float start, float end)
    {
        this.start = start;
        this.end = end;
    }

    public bool Overlaps(float otherStart, float otherEnd)
    {
        return !(end <= otherStart || start >= otherEnd);
    }
}

public class ChunkEdge : MonoBehaviour
{
    [Header("Edge Geometry (Local XZ)")]
    [Tooltip("Start point of the edge relative to this object's transform (local XZ).")]
    public Vector2 localStart = new Vector2(-5f, 0f);

    [Tooltip("End point of the edge relative to this object's transform (local XZ).")]
    public Vector2 localEnd = new Vector2(5f, 0f);

    [Header("Edge Properties")]
    public ConnectorType edgeType = ConnectorType.Edge;

    [Tooltip("Width of the slope/apron margin on this edge (overlap zone with neighbors).")]
    [Range(0.1f, 10f)]
    public float slopeMargin = 1.5f;

    [Tooltip("Maximum allowed tangent slide jitter as a fraction of slopeMargin (0 = no jitter, 0.8 = safe max).")]
    [Range(0f, 0.9f)]
    public float maxJitterFactor = 0.5f;

    [Header("State (Runtime)")]
    [SerializeField]
    private List<EdgeInterval> occupiedIntervals = new List<EdgeInterval>();

    public ChunkData ParentChunk { get; private set; }

    private void Awake()
    {
        ParentChunk = GetComponentInParent<ChunkData>();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Mathematical Properties
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Length of the edge in meters.</summary>
    public float Length => Vector2.Distance(localStart, localEnd);

    /// <summary>Local unit tangent pointing from localStart to localEnd (in 3D XZ).</summary>
    public Vector3 LocalTangent
    {
        get
        {
            Vector2 diff = localEnd - localStart;
            float len = diff.magnitude;
            if (len < 0.0001f) return Vector3.forward;
            return new Vector3(diff.x / len, 0f, diff.y / len);
        }
    }

    /// <summary>Local outward normal perpendicular to tangent on XZ plane.</summary>
    public Vector3 LocalOutwardNormal
    {
        get
        {
            Vector3 t = LocalTangent;
            // Perpendicular in XZ plane pointing outward (assuming counter-clockwise winding: right side is out)
            return new Vector3(t.z, 0f, -t.x);
        }
    }

    /// <summary>World position of edge start point.</summary>
    public Vector3 WorldStart => transform.TransformPoint(new Vector3(localStart.x, 0f, localStart.y));

    /// <summary>World position of edge end point.</summary>
    public Vector3 WorldEnd => transform.TransformPoint(new Vector3(localEnd.x, 0f, localEnd.y));

    /// <summary>World outward normal in XZ plane (normalized).</summary>
    public Vector3 WorldOutwardNormal
    {
        get
        {
            Vector3 worldN = transform.TransformDirection(LocalOutwardNormal);
            worldN.y = 0f;
            return worldN.normalized;
        }
    }

    /// <summary>World tangent in XZ plane (normalized).</summary>
    public Vector3 WorldTangent
    {
        get
        {
            Vector3 worldT = transform.TransformDirection(LocalTangent);
            worldT.y = 0f;
            return worldT.normalized;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // 1-to-N Interval Allocation
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Check if this edge has any available space left.</summary>
    public bool HasFreeSpace(float minRequiredLength = 1f)
    {
        var freeList = GetFreeIntervals();
        foreach (var interval in freeList)
        {
            if (interval.Length >= minRequiredLength) return true;
        }
        return false;
    }

    /// <summary>Returns all currently unoccupied intervals along this edge.</summary>
    public List<EdgeInterval> GetFreeIntervals()
    {
        var freeIntervals = new List<EdgeInterval>();
        float totalLength = Length;

        if (occupiedIntervals == null || occupiedIntervals.Count == 0)
        {
            freeIntervals.Add(new EdgeInterval(0f, totalLength));
            return freeIntervals;
        }

        // Sort occupied intervals by start position
        var sorted = new List<EdgeInterval>(occupiedIntervals);
        sorted.Sort((a, b) => a.start.CompareTo(b.start));

        float current = 0f;
        foreach (var occ in sorted)
        {
            if (occ.start > current + 0.01f)
            {
                freeIntervals.Add(new EdgeInterval(current, Mathf.Min(occ.start, totalLength)));
            }
            current = Mathf.Max(current, occ.end);
        }

        if (current < totalLength - 0.01f)
        {
            freeIntervals.Add(new EdgeInterval(current, totalLength));
        }

        return freeIntervals;
    }

    /// <summary>
    /// Attempts to find a free sub-interval on this edge that can fit incoming edge of requiredLength.
    /// Calculates safe tangent jitter within slopeMargin without creating holes.
    /// </summary>
    public bool TryAllocateInterval(
        float requiredLength,
        bool applyJitter,
        out float allocatedStart,
        out float actualJitter)
    {
        allocatedStart = 0f;
        actualJitter = 0f;

        var freeList = GetFreeIntervals();
        foreach (var interval in freeList)
        {
            if (interval.Length >= requiredLength)
            {
                float excess = interval.Length - requiredLength;
                float maxSafeJitter = Mathf.Min(excess * 0.5f, slopeMargin * maxJitterFactor);

                if (applyJitter && maxSafeJitter > 0.05f)
                {
                    actualJitter = Random.Range(-maxSafeJitter, maxSafeJitter);
                }

                // Center the allocation within the free interval + jitter
                float centerOffset = (excess * 0.5f) + actualJitter;
                allocatedStart = Mathf.Clamp(interval.start + centerOffset, interval.start, interval.end - requiredLength);

                // Commit occupation
                occupiedIntervals.Add(new EdgeInterval(allocatedStart, allocatedStart + requiredLength));
                return true;
            }
        }

        return false;
    }

    /// <summary>Marks an interval as occupied manually.</summary>
    public void ForceOccupy(float start, float end)
    {
        occupiedIntervals.Add(new EdgeInterval(start, end));
    }

    /// <summary>Clears all occupied intervals (reset).</summary>
    public void ResetEdge()
    {
        occupiedIntervals.Clear();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Vector3 wStart = WorldStart;
        Vector3 wEnd = WorldEnd;
        Vector3 wNorm = WorldOutwardNormal;
        Vector3 wTan = WorldTangent;

        // Draw main edge line
        bool isFull = !HasFreeSpace(1f);
        Gizmos.color = isFull ? Color.red : Color.cyan;
        Gizmos.DrawLine(wStart, wEnd);

        // Draw start/end endpoints
        Gizmos.DrawSphere(wStart, 0.15f);
        Gizmos.DrawSphere(wEnd, 0.15f);

        // Draw outward normal arrow from center
        Vector3 center = (wStart + wEnd) * 0.5f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(center, wNorm * 1.5f);

        // Draw slope margin preview (apron)
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        Vector3 outerStart = wStart + wNorm * slopeMargin;
        Vector3 outerEnd = wEnd + wNorm * slopeMargin;
        Gizmos.DrawLine(wStart, outerStart);
        Gizmos.DrawLine(wEnd, outerEnd);
        Gizmos.DrawLine(outerStart, outerEnd);

        // Draw occupied segments
        if (occupiedIntervals != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
            foreach (var interval in occupiedIntervals)
            {
                Vector3 segStart = wStart + wTan * interval.start;
                Vector3 segEnd = wStart + wTan * interval.end;
                Gizmos.DrawLine(segStart + Vector3.up * 0.05f, segEnd + Vector3.up * 0.05f);
            }
        }
    }
}
