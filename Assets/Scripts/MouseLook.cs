using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct LookProfile
{
    public float sensitivity;
    public float softZone;

    [Header("Pitch (Вверх/Вниз)")]
    public float minPitch;
    public float maxPitch;

    [Header("Yaw (Вліво/Вправо)")]
    public bool limitYaw;
    public float minYaw;
    public float maxYaw;
}

public class MouseLook : MonoBehaviour
{
    public LookProfile[] profiles;
    public int currentProfileIndex = 0;

    [Header("Посилання")]
    [SerializeField] private Transform playerBody;

    [Header("Система фокусування")]
    public Transform targetToTrack;
    public float focusSpeed = 8f;
    public bool allowMouseWhileTracking = false;

    private float xRotation = 0f;
    private float yRotation = 0f;

    public bool isRotation = true;

    private bool isFocusingOnAngle = false;
    private float targetFocusPitch = 0f;
    private float targetFocusYaw = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerBody == null)
        {
            playerBody = transform.parent;
        }

        if (playerBody != null)
        {
            yRotation = NormalizeAngle(playerBody.localEulerAngles.y);
        }
    }

    void LateUpdate()
    {
        if (profiles.Length == 0) return;
        if (!isRotation && targetToTrack == null && !isFocusingOnAngle) return;

        LookProfile activeProfile = profiles[currentProfileIndex];

        float mouseX = 0f;
        float mouseY = 0f;

        if (isRotation)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && (targetToTrack == null || allowMouseWhileTracking))
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                mouseX = mouseDelta.x * activeProfile.sensitivity * Time.deltaTime;
                mouseY = mouseDelta.y * activeProfile.sensitivity * Time.deltaTime;
            }
        }

        if (targetToTrack != null)
        {
            Vector3 direction = targetToTrack.position - transform.position;

            if (playerBody != null && playerBody.parent != null)
            {
                direction = playerBody.parent.InverseTransformDirection(direction);
            }

            float targetY = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            float distanceXZ = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
            float targetX = Mathf.Atan2(-direction.y, distanceXZ) * Mathf.Rad2Deg;

            xRotation = Mathf.Lerp(xRotation, targetX, focusSpeed * Time.deltaTime);
            yRotation = Mathf.LerpAngle(yRotation, targetY, focusSpeed * Time.deltaTime);

            isFocusingOnAngle = false;
        }
        else if (isFocusingOnAngle)
        {
            xRotation = Mathf.Lerp(xRotation, targetFocusPitch, focusSpeed * Time.deltaTime);
            yRotation = Mathf.LerpAngle(yRotation, targetFocusYaw, focusSpeed * Time.deltaTime);

            if (Mathf.Abs(xRotation - targetFocusPitch) < 0.1f && Mathf.Abs(Mathf.DeltaAngle(yRotation, targetFocusYaw)) < 0.1f)
            {
                isFocusingOnAngle = false;
            }
        }
        else
        {
            float resistanceY = CalculateResistance(xRotation, activeProfile.minPitch, activeProfile.maxPitch, -mouseY, activeProfile.softZone);
            xRotation -= mouseY * resistanceY;
            xRotation = Mathf.Clamp(xRotation, activeProfile.minPitch, activeProfile.maxPitch);

            if (activeProfile.limitYaw)
            {
                float resistanceX = CalculateResistance(yRotation, activeProfile.minYaw, activeProfile.maxYaw, mouseX, activeProfile.softZone);
                yRotation += mouseX * resistanceX;
                yRotation = Mathf.Clamp(yRotation, activeProfile.minYaw, activeProfile.maxYaw);
            }
            else
            {
                yRotation += mouseX;
            }
        }

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (playerBody != null)
        {
            if (activeProfile.limitYaw || targetToTrack != null || isFocusingOnAngle)
            {
                Vector3 currentEuler = playerBody.localEulerAngles;
                playerBody.localRotation = Quaternion.Euler(currentEuler.x, yRotation, currentEuler.z);
            }
            else
            {
                playerBody.Rotate(Vector3.up * mouseX);
                yRotation = NormalizeAngle(playerBody.localEulerAngles.y);
            }
        }
    }

    private float CalculateResistance(float currentAngle, float minLimit, float maxLimit, float deltaInput, float zone)
    {
        if (zone <= 0f) return 1f;

        float nextAngle = currentAngle + deltaInput;
        float resistance = 1f;

        if (nextAngle > maxLimit - zone && deltaInput > 0)
        {
            float t = Mathf.Clamp01((nextAngle - (maxLimit - zone)) / zone);
            resistance = 1f - t;
        }
        else if (nextAngle < minLimit + zone && deltaInput < 0)
        {
            float t = Mathf.Clamp01(((minLimit + zone) - nextAngle) / zone);
            resistance = 1f - t;
        }

        return resistance;
    }

    public void SetProfile(int profileIndex, float newCenterAngleYaw = 0f)
    {
        if (profileIndex >= 0 && profileIndex < profiles.Length)
        {
            currentProfileIndex = profileIndex;

            if (profiles[currentProfileIndex].limitYaw && playerBody != null)
            {
                yRotation = NormalizeAngle(playerBody.localEulerAngles.y);
            }
        }
    }

    public void TrackObject(Transform target)
    {
        targetToTrack = target;
        SyncRotations();
    }

    public void StopTracking()
    {
        targetToTrack = null;
        isRotation = true;
        SyncRotations();
    }

    public void FocusOnAngle(float pitch, float yaw)
    {
        targetFocusPitch = pitch;
        targetFocusYaw = yaw;
        isFocusingOnAngle = true;
        targetToTrack = null;
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) return angle - 360f;
        if (angle <= -180f) return angle + 360f;
        return angle;
    }

    public void SyncRotations()
    {
        // Нормалізуємо поточні кути, щоб уникнути стрибків через 360 градусів
        xRotation = NormalizeAngle(transform.localEulerAngles.x);

        if (playerBody != null)
        {
            yRotation = NormalizeAngle(playerBody.localEulerAngles.y);
        }
    }
}