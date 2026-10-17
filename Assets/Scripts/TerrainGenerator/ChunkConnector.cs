using UnityEngine;

public enum ConnectorType
{
    Edge,       // Generic terrain edge
    Road,       // Road connection
    Trail,      // Trail/path connection
    Any         // Matches any type
}

/// <summary>
/// Connection point on a chunk edge. Place as a child GameObject on each face.
/// transform.position = center of the face (world space).
/// transform.forward  = outward normal of the face (pointing AWAY from the chunk center).
/// </summary>
public class ChunkConnector : MonoBehaviour
{
    public ConnectorType type = ConnectorType.Edge;

    [HideInInspector]
    public bool isOccupied = false;

    /// <summary>
    /// Check if this connector can connect to another connector by type.
    /// </summary>
    public bool IsCompatible(ConnectorType otherType)
    {
        if (isOccupied) return false;
        if (type == ConnectorType.Any || otherType == ConnectorType.Any) return true;
        return type == otherType;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOccupied ? Color.red : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.25f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
}
