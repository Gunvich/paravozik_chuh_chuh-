using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TrainCoupler : MonoBehaviour
{
    public enum CouplerType { Front, Rear }

    [SerializeField] private CouplerType type;
    [SerializeField] private TrainCar ownerCar;
    [SerializeField] private float couplerOffset = 1.2f; // Довжина самої зчіпки

    public CouplerType Type => type;
    public TrainCar OwnerCar => ownerCar;
    public float CouplerOffset => couplerOffset;

    public bool IsConnected { get; set; }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // Обов'язково тригер!

        if (ownerCar == null)
            ownerCar = GetComponentInParent<TrainCar>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Перевіряємо, чи зіткнулися з іншою зчіпкою
        if (!other.TryGetComponent<TrainCoupler>(out var otherCoupler)) return;

        // Не стикуємося самі з собою або якщо вже підключені
        if (otherCoupler.OwnerCar == ownerCar || IsConnected || otherCoupler.IsConnected) return;

        // Стикуємося тільки тоді, коли зад стикається з передом (Rear + Front)
        if (this.type == CouplerType.Rear && otherCoupler.type == CouplerType.Front)
        {
            // Знаходимо склад локомотива
            var comp = GetComponentInParent<TrainComposition>();
            if (comp != null)
            {
                float totalCouplerDistance = this.couplerOffset + otherCoupler.couplerOffset;
                comp.AttachCarAtEnd(otherCoupler.OwnerCar, totalCouplerDistance);

                this.IsConnected = true;
                otherCoupler.IsConnected = true;
            }
        }
    }
}