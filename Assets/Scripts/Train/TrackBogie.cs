using UnityEngine;

public class TrackBogie : MonoBehaviour
{
    [Header("Поточний стан на колії")]
    public TrackSegment currentSegment;
    public float distanceOnSegment;

    public Vector3 CurrentPosition { get; private set; }
    public Vector3 CurrentTangent { get; private set; }
    public Vector3 CurrentUp { get; private set; }

    public void AdvanceDistance(float deltaDistance)
    {
        if (currentSegment == null) return;

        distanceOnSegment += deltaDistance;

        // Рух уперед: якщо виїхали за межі сегмента
        while (distanceOnSegment > currentSegment.Length)
        {
            float overflow = distanceOnSegment - currentSegment.Length;
            TrackSegment next = currentSegment.GetNextSegment();

            if (next != null)
            {
                currentSegment = next;
                distanceOnSegment = overflow;
            }
            else
            {
                // Кінець колії (тупик)
                distanceOnSegment = currentSegment.Length;
                break;
            }
        }

        // Рух назад (реверс): якщо виїхали за початок сегмента
        while (distanceOnSegment < 0f)
        {
            float underflow = -distanceOnSegment;
            TrackSegment prev = currentSegment.GetPreviousSegment();

            if (prev != null)
            {
                currentSegment = prev;
                distanceOnSegment = prev.Length - underflow;
            }
            else
            {
                // Початок колії (тупик)
                distanceOnSegment = 0f;
                break;
            }
        }

        UpdateTransform();
    }

    public void UpdateTransform()
    {
        if (currentSegment == null) return;

        currentSegment.EvaluateAtDistance(distanceOnSegment, out Vector3 pos, out Vector3 tangent, out Vector3 up);

        CurrentPosition = pos;
        CurrentTangent = tangent;
        CurrentUp = up;

        transform.position = pos;
        if (tangent != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(tangent, up);
        }
    }
}