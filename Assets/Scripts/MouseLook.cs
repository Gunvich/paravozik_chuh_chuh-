using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    [Header("Настройки чувствительности")]
    [SerializeField] private float mouseSensitivity = 15.0f;

    [Header("Ссылки")]
    [SerializeField] private Transform playerBody;

    private float xRotation = 0f;

    public bool isRotation = true;

    void Start()
    {

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerBody == null)
        {

            playerBody = transform.parent;
        }
    }

    void LateUpdate()
    {
        if (isRotation)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mouseDelta = mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
            float mouseY = mouseDelta.y * mouseSensitivity * Time.deltaTime;


            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);


            if (playerBody != null)
            {
                playerBody.Rotate(Vector3.up * mouseX);
            }
        }
    }
}