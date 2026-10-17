using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TrainCar : MonoBehaviour
{
    [Header("Візки")]
    [SerializeField] private TrackBogie frontBogie;
    [SerializeField] private TrackBogie rearBogie;

    [Header("Параметри вагона")]
    private float wheelbase = 12f;
    [SerializeField] private float mass = 15000f; // Маса у кг (15 тонн)
    [SerializeField] private float rollingFriction = 0.8f; // Опір коченню у м/с^2

    public TrackBogie FrontBogie => frontBogie;
    public TrackBogie RearBogie => rearBogie;
    public float Wheelbase => wheelbase;
    public float Mass => mass;

    /// <summary>
    /// Локальна швидкість (для відчепленого вагона)
    /// </summary>
    public float CurrentVelocity { get; set; }

    /// <summary>
    /// Чи знаходиться у складі активного тягача
    /// </summary>
    public bool IsInActiveTrain
    {
        get
        {
            if (_isInActiveTrain) return true;
            var comp = GetComponentInParent<TrainComposition>();
            if (comp != null)
            {
                var engine = comp.GetComponentInChildren<TrainEngine>();
                if (engine != null && engine.isActiveAndEnabled)
                    return true;
            }
            return false;
        }
        set => _isInActiveTrain = value;
    }
    private bool _isInActiveTrain;

    public event Action<float> OnCarImpact; // Подія удару (передає силу удару)

    private Rigidbody _rb;

    private void Awake()
    {
        wheelbase = Vector3.Distance(frontBogie.transform.position, rearBogie.transform.position);

        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.None;
    }

    private void Update()
    {
        // Потяг не в активному складі і має швидкість - вільне кочення
        if (!IsInActiveTrain && Mathf.Abs(CurrentVelocity) > 0.01f)
        {
            float deltaDistance = CurrentVelocity * Time.deltaTime;
            MoveCar(deltaDistance);

            // Уповільнення від тертя
            CurrentVelocity = Mathf.MoveTowards(CurrentVelocity, 0f, rollingFriction * Time.deltaTime);
            Physics.SyncTransforms();
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
    /// Повертає діючу швидкість вагона (з урахуванням TrainEngine, якщо вагон у складі)
    /// </summary>
    public float GetEffectiveVelocity()
    {
        var comp = GetComponentInParent<TrainComposition>();
        if (comp != null)
        {
            var engine = comp.GetComponentInChildren<TrainEngine>();
            if (engine != null && engine.isActiveAndEnabled)
                return engine.CurrentSpeed;
        }
        return CurrentVelocity;
    }

    /// <summary>
    /// Прийняти імпульс від зіткнення
    /// </summary>
    public void ApplyImpact(float impulseVelocity, float impactForce)
    {
        var comp = GetComponentInParent<TrainComposition>();
        if (comp != null)
        {
            var engine = comp.GetComponentInChildren<TrainEngine>();
            if (engine != null && engine.isActiveAndEnabled)
            {
                engine.SetSpeed(impulseVelocity);
            }
            else
            {
                // Для відчепленого складу передаємо швидкість усім вагонам
                foreach (var car in comp.Cars)
                {
                    if (car != null)
                        car.CurrentVelocity = impulseVelocity;
                }
            }
        }
        else
        {
            CurrentVelocity = impulseVelocity;
        }

        OnCarImpact?.Invoke(impactForce);
    }

    /// <summary>
    /// Блокує рух потяга у напрямку зіткнення, поки гравець не поїде в інший бік
    /// </summary>
    public void BlockDirection(bool blockForward)
    {
        var comp = GetComponentInParent<TrainComposition>();
        if (comp != null)
        {
            var engine = comp.GetComponentInChildren<TrainEngine>();
            if (engine != null)
            {
                engine.BlockDirection(blockForward);
            }
        }
    }

    private void UpdateCarPositionAndRotation(bool teleport)
    {
        Vector3 frontPos = frontBogie.CurrentPosition;
        Vector3 rearPos = rearBogie.CurrentPosition;

        Vector3 carCenter = (frontPos + rearPos) * 0.5f;
        Vector3 carForward = (frontPos - rearPos).normalized;
        Vector3 averageUp = (frontBogie.CurrentUp + rearBogie.CurrentUp).normalized;

        Quaternion carRotation = Quaternion.LookRotation(carForward, averageUp);

        _rb.position = carCenter;
        _rb.rotation = carRotation;
        transform.position = carCenter;
        transform.rotation = carRotation;
    }
}
