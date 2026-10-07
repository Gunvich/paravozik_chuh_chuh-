using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class TrainEngine : MonoBehaviour
{
    [SerializeField] private TrainComposition trainComposition;

    [Header("Параметри тяги")]
    [SerializeField] private float acceleration = 5f;    // м/с^2
    [SerializeField] private float brakeStrength = 10f;  // м/с^2
    [SerializeField] private float maxSpeed = 30f;       // м/с
    [SerializeField] private float throttleInput;
    [SerializeField] private float rollingResistance = 0.5f;

    [Header("Початкова позиція")]
    [SerializeField] private SplineContainer startingSpline;
    [SerializeField] private TrackSegment startingSegment;
    private float startingDistance = 20f;

    public bool W = true, S = true;

    public float CurrentSpeed { get; private set; } // Поточна швидкість у м/с

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

    private void Update()
    {
        if (trainComposition == null) return;

        // В Update залишаємо ТІЛЬКИ обробку інпутів (логіку W/S)
        if (!W && throttleInput > 0)
            throttleInput = 0;
        else if (!S && throttleInput < 0)
            throttleInput = 0;

        if (throttleInput > 0)
            S = true;
        else if (throttleInput < 0)
            W = true;
    }

    private void FixedUpdate()
    {
        if (trainComposition == null) return;

        // 1. Розрахунок тяги перенесено сюди! І використовуємо Time.fixedDeltaTime
        if (throttleInput != 0)
        {
            float targetSpeed = throttleInput * maxSpeed;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, rollingResistance * Time.fixedDeltaTime);
        }

        CurrentSpeed = Mathf.Clamp(CurrentSpeed, -maxSpeed * 0.4f, maxSpeed);

        // 2. Пройдений шлях за фізичний кадр
        float deltaDistance = CurrentSpeed * Time.fixedDeltaTime;

        // 3. Передаємо зміщення вагону саме у FixedUpdate
        trainComposition.MoveTrain(deltaDistance);
    }

    public void SetSpeed(float newSpeed)
    {
        CurrentSpeed = newSpeed;
    }

    public void SetInput(float newInput)
    {
        throttleInput = newInput;
    }
}