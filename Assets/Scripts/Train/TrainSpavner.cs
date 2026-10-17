using NaughtyAttributes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class TrainSpavner : MonoBehaviour
{
    [SerializeField] private TrainComposition trainComposition;
    [Header("Початкова позиція")]
    [SerializeField] private SplineContainer startingSpline;
    private TrackSegment startingSegment;
    private void Start()
    {
        startingSegment = startingSpline.GetComponent<TrackSegment>();

        var spline = startingSpline.Spline;
        float3 localTargetPos = startingSpline.transform.InverseTransformPoint(this.transform.position);

        SplineUtility.GetNearestPoint(
            spline, localTargetPos, out float3 nearestLocalPoint, out float t, resolution: 4, iterations: 2
        );

        var nearestWorldPoint = startingSpline.transform.TransformPoint(nearestLocalPoint);
        float splineLocalDistance = spline.ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Distance);

        if (trainComposition != null && startingSegment != null)
        {
            trainComposition.InitializeTrain(startingSegment, splineLocalDistance);
        }
    }
}