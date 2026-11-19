using UnityEngine;

public class FreeFlyCamera : MonoBehaviour
{
    [Header("Налаштування")]
    public float moveSpeed = 10f;
    public float fastMultiplier = 2.5f;
    public float mouseSensitivity = 2f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // Зберігаємо початковий кут камери
        Vector3 rot = transform.localEulerAngles;
        rotationX = rot.y;
        rotationY = -rot.x;
    }

    void Update()
    {
        // Обертання камерою при затиснутій правій кнопці миші (ПКМ)
        if (Input.GetMouseButton(1))
        {
            rotationX += Input.GetAxis("Mouse X") * mouseSensitivity;
            rotationY += Input.GetAxis("Mouse Y") * mouseSensitivity;
            rotationY = Mathf.Clamp(rotationY, -89f, 89f); // Обмеження, щоб камера не переверталася

            transform.localRotation = Quaternion.Euler(-rotationY, rotationX, 0f);
        }

        // Розрахунок швидкості
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);

        // Рух вперед/назад та вліво/вправо (WASD)
        float moveForward = Input.GetAxis("Vertical");
        float moveRight = Input.GetAxis("Horizontal");

        // Рух вгору/вниз (E / Q)
        float moveUp = 0f;
        if (Input.GetKey(KeyCode.E)) moveUp += 1f;
        if (Input.GetKey(KeyCode.Q)) moveUp -= 1f;

        Vector3 move = transform.forward * moveForward + transform.right * moveRight + Vector3.up * moveUp;
        transform.position += move * speed * Time.deltaTime;
    }
}