using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TrainCarVolume : MonoBehaviour
{
    [SerializeField] private Rigidbody carRigidbody;

    private Vector3 _lastPosition;
    private Quaternion _lastRotation;

    public Vector3 FrameDeltaPosition { get; private set; }
    public Quaternion FrameDeltaRotation { get; private set; }
    public Vector3 WorldVelocity { get; private set; }

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;

        if (carRigidbody == null)
            carRigidbody = GetComponentInParent<Rigidbody>();
    }

    private void Start()
    {
        if (carRigidbody != null)
        {
            _lastPosition = carRigidbody.position;
            _lastRotation = carRigidbody.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (carRigidbody == null) return;

        // Точне зміщення вагона за поточний FixedUpdate
        FrameDeltaPosition = carRigidbody.position - _lastPosition;
        FrameDeltaRotation = carRigidbody.rotation * Quaternion.Inverse(_lastRotation);

        WorldVelocity = FrameDeltaPosition / Time.fixedDeltaTime;

        _lastPosition = carRigidbody.position;
        _lastRotation = carRigidbody.rotation;
    }
}