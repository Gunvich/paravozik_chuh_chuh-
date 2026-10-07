using System;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[RequireComponent(typeof(Rigidbody))]
public class TrainCar : MonoBehaviour
{
    [Header("Візки вагона")]
    [SerializeField] private TrackBogie frontBogie;
    [SerializeField] private TrackBogie rearBogie;

    [Header("Геометрія та фізика")]
    private float wheelbase = 12f;
    [SerializeField] private float mass = 15000f; // Маса вагона в кг (15 тонн)
    [SerializeField] private float rollingFriction = 0.8f; // Тертя кочення для відчепленого вагона

    public TrackBogie FrontBogie => frontBogie;
    public TrackBogie RearBogie => rearBogie;
    public float Wheelbase => wheelbase;
    public float Mass => mass;

    /// <summary>
    /// Вільна швидкість вагона (коли він відчеплений і котиться сам)
    /// </summary>
    public float CurrentVelocity { get; set; }

    /// <summary>
    /// Чи знаходиться вагон у складі активного потяга під тягою
    /// </summary>
    public bool IsInActiveTrain { get; set; }

    public event Action<float> OnCarImpact; // Подія удару (передає силу удару)

    private Rigidbody _rb;

    private void Awake()
    {
        wheelbase = Vector3.Distance(frontBogie.transform.position, rearBogie.transform.position);

        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;

        _rb.interpolation = RigidbodyInterpolation.None;
    }

    private void FixedUpdate()
    {
        // Якщо вагон відчеплений і отримав поштовх від удару — він котиться сам
        if (!IsInActiveTrain && Mathf.Abs(CurrentVelocity) > 0.01f)
        {
            float deltaDistance = CurrentVelocity * Time.fixedDeltaTime;
            MoveCar(deltaDistance);

            // Плавне сповільнення від тертя рейок
            CurrentVelocity = Mathf.MoveTowards(CurrentVelocity, 0f, rollingFriction * Time.fixedDeltaTime);
        }
    }

    public void InitializeCar(TrackSegment startSegment, float startDistance)
    {
        frontBogie.currentSegment = startSegment;
        frontBogie.distanceOnSegment = startDistance;
        frontBogie.AdvanceDistance(+wheelbase);

        rearBogie.currentSegment = startSegment;
        rearBogie.distanceOnSegment = startDistance;
        rearBogie.UpdateTransform();

        UpdateCarPositionAndRotation(true);
    }

    public void MoveCar(float deltaDistance)
    {
        frontBogie.AdvanceDistance(deltaDistance);
        rearBogie.AdvanceDistance(deltaDistance);
        UpdateCarPositionAndRotation(false);
    }

    public void SnapBehindOtherCar(TrainCar leadingCar, float couplerGap)
    {
        frontBogie.currentSegment = leadingCar.RearBogie.currentSegment;
        frontBogie.distanceOnSegment = leadingCar.RearBogie.distanceOnSegment;
        frontBogie.AdvanceDistance(-couplerGap);

        rearBogie.currentSegment = frontBogie.currentSegment;
        rearBogie.distanceOnSegment = frontBogie.distanceOnSegment;
        rearBogie.AdvanceDistance(-wheelbase);

        UpdateCarPositionAndRotation(true);
    }

    /// <summary>
    /// Отримання імпульсу від зіткнення
    /// </summary>
    public void ApplyImpact(float impulseVelocity, float impactForce)
    {
        CurrentVelocity = impulseVelocity;
        OnCarImpact?.Invoke(impactForce);
    }

    private void UpdateCarPositionAndRotation(bool teleport)
    {
        Vector3 frontPos = frontBogie.CurrentPosition;
        Vector3 rearPos = rearBogie.CurrentPosition;

        Vector3 carCenter = (frontPos + rearPos) * 0.5f;
        Vector3 carForward = (frontPos - rearPos).normalized;
        Vector3 averageUp = (frontBogie.CurrentUp + rearBogie.CurrentUp).normalized;

        Quaternion carRotation = Quaternion.LookRotation(carForward, averageUp);

        if (teleport)
        {
            _rb.position = carCenter;
            _rb.rotation = carRotation;
        }
        else
        {
            _rb.MovePosition(carCenter);
            _rb.MoveRotation(carRotation);
        }
    }
}