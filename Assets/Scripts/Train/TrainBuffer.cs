using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TrainBuffer : MonoBehaviour
{
    public enum BufferType { Bogie, Object }
    // ДОДАНО: Визначаємо позицію буфера на вагоні
    public enum BufferPosition { Front, Rear }

    [Header("Тип буфера")]
    [SerializeField] private BufferType type = BufferType.Bogie;
    [SerializeField] private BufferPosition position = BufferPosition.Front; // Вкажи в інспекторі!
    [SerializeField] private GameObject train;
    private TrainCar ownerCar;
    private TrainEngine engine;

    [Header("Параметри зіткнення")]
    [Tooltip("Максимальна швидкість для автозчіпки (м/с)")]
    [SerializeField] private float maxCouplingSpeed = 2.5f;
    [SerializeField][Range(0f, 1f)] private float bounciness = 0.2f;

    public BufferType Type => type;
    public BufferPosition Position => position;
    public TrainCar OwnerCar => ownerCar;
    public bool IsConnected { get; set; }

    private float uncoupleCooldown = 0f;
    private float bounceCooldown = 0f;

    public bool CanConnected { get; set; } = false;

    private void Update()
    {
        if (uncoupleCooldown > 0f)
        {
            uncoupleCooldown -= Time.deltaTime;
        }

        if (bounceCooldown > 0f)
        {
            bounceCooldown -= Time.deltaTime;
        }
    }

    private void Awake()
    {
        if (train != null)
        {
            ownerCar = train.GetComponent<TrainCar>();
            engine = train.GetComponent<TrainEngine>();
        }
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (ownerCar == null && type == BufferType.Bogie)
            ownerCar = GetComponentInParent<TrainCar>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<TrainBuffer>(out var otherBuffer)) return;
        if (this.bounceCooldown > 0f || otherBuffer.bounceCooldown > 0f) return;
        // Уникаємо подвійного спрацьовування тригера
        if (this.gameObject.GetEntityId() > otherBuffer.gameObject.GetEntityId()) return;

        TrainCar carA = this.ownerCar;
        TrainCar carB = otherBuffer.ownerCar;

        if (carA != null && carA == carB) return; // Це один і той самий вагон
        if (IsConnected || otherBuffer.IsConnected) return; // Вже зчеплені

        if (this.type == BufferType.Object || otherBuffer.type == BufferType.Object)
        {
            HandleStaticObstacleCollision(this, otherBuffer);
            return;
        }
        HandleTrainCollision(this, otherBuffer);
    }

    private void HandleTrainCollision(TrainBuffer bufferA, TrainBuffer bufferB)
    {
        TrainCar carA = bufferA.OwnerCar;
        TrainCar carB = bufferB.OwnerCar;

        if (carA == null || carB == null) return;

        var compA = carA.GetComponentInParent<TrainComposition>();
        var compB = carB.GetComponentInParent<TrainComposition>();

        // Отримуємо дійсні швидкості (враховуючи TrainEngine або вільне кочення)
        float speedA = carA.GetEffectiveVelocity();
        float speedB = carB.GetEffectiveVelocity();

        // Проекція швидкості кожного вагона на вісь удару (напрямок від bufferA до bufferB)
        float dirA = (bufferA.Position == BufferPosition.Front) ? 1f : -1f;
        float dirB = (bufferB.Position == BufferPosition.Front) ? 1f : -1f;

        float vA_col = speedA * dirA;
        float vB_col = -(speedB * dirB);
        float relativeSpeed = vA_col - vB_col;

        // Перевіряємо умови автозчіпки
        bool canCouple = (bufferA.CanConnected || bufferB.CanConnected)
                         && bufferA.uncoupleCooldown <= 0f && bufferB.uncoupleCooldown <= 0f;

        if (canCouple && relativeSpeed > 0f && relativeSpeed <= maxCouplingSpeed)
        {
            // Намагаємося зчепити
            bool mergeSuccess = false;

            if (compA != null && compB != null)
            {
                // Завжди зливаємо другорядний склад у головний
                if (compA.isMainComposition && !compB.isMainComposition)
                {
                    mergeSuccess = compA.MergeWithComposition(compB, bufferA, bufferB);
                }
                else if (compB.isMainComposition && !compA.isMainComposition)
                {
                    mergeSuccess = compB.MergeWithComposition(compA, bufferB, bufferA);
                }
                // Якщо обидва головні або обидва не головні
                else
                {
                    mergeSuccess = compA.MergeWithComposition(compB, bufferA, bufferB);
                }
            }

            if (mergeSuccess)
            {
                // Позначаємо буфери як зчеплені, щоб не було спаму колізій
                bufferA.IsConnected = true;
                bufferB.IsConnected = true;
                Debug.Log($"[Buffer] Зчеплення успішне: Вагон {carA.name} ({bufferA.Position}) + Вагон {carB.name} ({bufferB.Position})");
                return;
            }
        }

        // Якщо швидкість завелика або зчеплення вимкнено — пружний відскок
        ApplyBouncePhysics(bufferA, bufferB, carA, carB, compA, compB, vA_col, vB_col, dirA, dirB, relativeSpeed);
    }

    private void ApplyBouncePhysics(
        TrainBuffer bufferA, TrainBuffer bufferB,
        TrainCar carA, TrainCar carB,
        TrainComposition compA, TrainComposition compB,
        float vA_col, float vB_col,
        float dirA, float dirB,
        float relativeSpeed)
    {
        float m1 = compA != null ? compA.GetTotalMass() : carA.Mass;
        float m2 = compB != null ? compB.GetTotalMass() : carB.Mass;
        float combinedMass = Mathf.Max(m1 + m2, 1f);

        float effectiveRelSpeed = Mathf.Max(relativeSpeed, 0.4f);

        float newVa_col;
        float newVb_col;

        if (relativeSpeed > 0.05f)
        {
            newVa_col = ((m1 - bounciness * m2) * vA_col + (1f + bounciness) * m2 * vB_col) / combinedMass;
            newVb_col = ((1f + bounciness) * m1 * vA_col + (m2 - bounciness * m1) * vB_col) / combinedMass;
        }
        else
        {
            // Якщо вагони торкнулися з майже нульовою швидкістю — даємо легкий імпульс розведення
            newVa_col = -0.3f;
            newVb_col = +0.3f;
        }

        // Перетворюємо назад у локальну швидкість кожного вагона вздовж колії
        float newSpeedA = newVa_col * dirA;
        float newSpeedB = -newVb_col * dirB;
        float impactForce = effectiveRelSpeed * ((m1 * m2) / combinedMass);

        // Блокуємо рух у бік зіткнення, поки потяг не поїде в інший напрямок
        carA.BlockDirection(bufferA.Position == BufferPosition.Front);
        carB.BlockDirection(bufferB.Position == BufferPosition.Front);

        carA.ApplyImpact(newSpeedA, impactForce);
        carB.ApplyImpact(newSpeedB, impactForce);

        // Фізичне розведення (Depenetration), щоб тригери не застрягали один в одному
        float separationDist = 0.06f;
        if (compA != null)
            compA.MoveTrain(-dirA * separationDist);
        else
            carA.MoveCar(-dirA * separationDist);

        if (compB != null)
            compB.MoveTrain(-dirB * separationDist);
        else
            carB.MoveCar(-dirB * separationDist);

        Physics.SyncTransforms();

        bufferA.bounceCooldown = 0.2f;
        bufferB.bounceCooldown = 0.2f;
    }

    private void HandleStaticObstacleCollision(TrainBuffer bufA, TrainBuffer bufB)
    {
        TrainBuffer trainBuf = bufA.type == BufferType.Bogie ? bufA : bufB;
        if (trainBuf == null || trainBuf.ownerCar == null) return;

        TrainCar car = trainBuf.ownerCar;
        var comp = car.GetComponentInParent<TrainComposition>();

        float speed = car.GetEffectiveVelocity();
        float dir = (trainBuf.Position == BufferPosition.Front) ? 1f : -1f;

        float approachSpeed = speed * dir;
        if (approachSpeed > 0f)
        {
            float bounceSpeed = -approachSpeed * trainBuf.bounciness;
            if (Mathf.Abs(bounceSpeed) < 0.3f) bounceSpeed = -0.3f;

            float newSpeed = bounceSpeed * dir;
            float mass = comp != null ? comp.GetTotalMass() : car.Mass;
            float impactForce = approachSpeed * mass;

            car.BlockDirection(trainBuf.Position == BufferPosition.Front);
            car.ApplyImpact(newSpeed, impactForce);

            float separationDist = 0.08f;
            if (comp != null)
                comp.MoveTrain(-dir * separationDist);
            else
                car.MoveCar(-dir * separationDist);

            Physics.SyncTransforms();
            trainBuf.bounceCooldown = 0.2f;
        }
    }

    public void Disconnect()
    {
        IsConnected = false;
        CanConnected = false;
        uncoupleCooldown = 3.0f; // 3 секунди ігноруємо будь-які зіткнення
    }

    public void InteractToUncouple()
    {
        if (!IsConnected)
        {
            Debug.Log("[Buffer] Ці вагони і так не зчеплені!");
            return;
        }

        TrainComposition comp = ownerCar.GetComponentInParent<TrainComposition>();
        if (comp == null) return;

        int myIndex = comp.GetCarIndex(ownerCar);
        TrainCar carToDetach = null;

        // Логіка:
        // Якщо гравець взаємодіє з ПЕРЕДНІМ буфером вагона №3, значить ми відриваємо вагон №3 від вагона №2.
        // Якщо гравець взаємодіє із ЗАДНІМ буфером вагона №2, значить ми ТАКОЖ відриваємо вагон №3 від вагона №2.

        if (position == BufferPosition.Front)
        {
            carToDetach = ownerCar; // Відриваємо цей вагон і все, що за ним
        }
        else if (position == BufferPosition.Rear)
        {
            // Перевіряємо, чи є за нами наступний вагон, який можна відірвати
            if (myIndex + 1 < comp.Cars.Count)
            {
                carToDetach = comp.Cars[myIndex + 1]; // Відриваємо наступний вагон
            }
            else
            {
                Debug.Log("[Buffer] За цим вагоном нічого не причеплено.");
                return;
            }
        }

        // Якщо визначили, який вагон треба відірвати — передаємо команду складу
        if (carToDetach != null)
        {
            comp.DetachCar(carToDetach);
        }
    }
}