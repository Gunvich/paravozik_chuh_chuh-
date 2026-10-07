using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TrainBuffer : MonoBehaviour
{
    public enum BufferType { Bogie, Object }

    [Header("Тип буфера")]
    [SerializeField] private BufferType type = BufferType.Bogie;
    [SerializeField] private TrainCar ownerCar;

    [Header("Параметри зіткнення")]
    [Tooltip("Максимальна швидкість відносного руху для безпечної автозчіпки (м/с)")]
    [SerializeField] private float maxCouplingSpeed = 2.5f; // ~9 км/год
    [Tooltip("Коефіцієнт пружності удару (0 - пластилін, 1 - ідеально пружний відскок)")]
    [SerializeField][Range(0f, 1f)] private float bounciness = 0.2f;
    [SerializeField] private float bufferGap = 2.0f;

    public BufferType Type => type;
    public TrainCar OwnerCar => ownerCar;
    public bool IsConnected { get; set; }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (ownerCar == null && type == BufferType.Bogie)
            ownerCar = GetComponentInParent<TrainCar>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<TrainBuffer>(out var otherBuffer)) return;

        // Запобігаємо подвійному спрацьовуванню в одному кадрі:
        // розрахунок виконує лише об'єкт з меншим InstanceID
        if (GetEntityId() > otherBuffer.GetEntityId()) return;

        TrainCar carA = this.ownerCar;
        TrainCar carB = otherBuffer.ownerCar;

        // Ігноруємо зіткнення всередині одного вагона або вже зчеплені буфери
        if (carA != null && carA == carB) return;
        if (IsConnected || otherBuffer.IsConnected) return;

        // 1. Зіткнення зі статичною перешкодою (тупик, валун тощо)
        if (this.type == BufferType.Object || otherBuffer.type == BufferType.Object)
        {
            TrainBuffer movingBuffer = (this.type == BufferType.Bogie) ? this : otherBuffer;
            TrainBuffer staticBuffer = (this.type == BufferType.Object) ? this : otherBuffer;

            HandleStaticObstacleCollision(movingBuffer, staticBuffer);
            Debug.Log($"УДАР!");

            return;
        }
        Debug.Log($"УДАР ВАГОНІВ!");

        // 2. Зіткнення між двома рухомими вагонами
        HandleTrainCollision(this, otherBuffer);
    }

    private void HandleTrainCollision(TrainBuffer bufferA, TrainBuffer bufferB)
    {
        TrainCar carA = bufferA.OwnerCar;
        TrainCar carB = bufferB.OwnerCar;

        if (carA == null || carB == null) return;

        var compA = carA.GetComponentInParent<TrainComposition>();
        var engineA = compA != null ? compA.GetComponent<TrainEngine>() : null;

        var compB = carB.GetComponentInParent<TrainComposition>();
        var engineB = compB != null ? compB.GetComponent<TrainEngine>() : null;

        float speedA = engineA != null ? engineA.CurrentSpeed : carA.CurrentVelocity;
        float speedB = engineB != null ? engineB.CurrentSpeed : carB.CurrentVelocity;

        float relativeSpeed = Mathf.Abs(speedA - speedB);



        float m1 = carA.Mass;
        float m2 = carB.Mass;

        float combinedMass = m1 + m2;
        float newSpeedA = ((m1 - bounciness * m2) * speedA + (1f + bounciness) * m2 * speedB) / combinedMass;
        float newSpeedB = ((1f + bounciness) * m1 * speedA + (m2 - bounciness * m1) * speedB) / combinedMass;

        float impactForce = relativeSpeed * ((m1 * m2) / combinedMass);

        Debug.Log($"Відносна швидкість: {relativeSpeed:F2} м/с. Сила: {impactForce:F0} Н");

        if (relativeSpeed < maxCouplingSpeed)
        {
            Debug.Log($"Стикування");

            compA.AttachCarAtEnd(carB, bufferGap);
            ApplySpeedToCar(carA, engineA, relativeSpeed, impactForce);
            return;
        }

        ApplySpeedToCar(carA, engineA, newSpeedA, impactForce);
        ApplySpeedToCar(carB, engineB, newSpeedB, impactForce);
    }

    private void HandleStaticObstacleCollision(TrainBuffer movingBuffer, TrainBuffer staticBuffer)
    {
        TrainCar car = movingBuffer.OwnerCar;
        if (car == null) return;

        var comp = car.GetComponentInParent<TrainComposition>();
        var engine = comp != null ? comp.GetComponent<TrainEngine>() : null;

        float speed = engine != null ? engine.CurrentSpeed : car.CurrentVelocity;
        float relativeSpeed = Mathf.Abs(speed);

        float newSpeed = -bounciness * speed;
        float impactForce = relativeSpeed * car.Mass;

        Debug.LogWarning($"[Train] УДАР ОБ ПЕРЕШКОДУ! Швидкість: {relativeSpeed:F2} м/с. Сила: {impactForce:F0} Н");

        ApplySpeedToCar(car, engine, newSpeed, impactForce);
        if (speed < 0f)
            engine.S = false;
        if (speed > 0f)
            engine.W = false;
    }

    private void ApplySpeedToCar(TrainCar car, TrainEngine engine, float targetSpeed, float force)
    {
        if (engine != null)
        {
            engine.SetSpeed(targetSpeed);
        }
        else
        {
            car.ApplyImpact(targetSpeed, force);
        }
    }
}