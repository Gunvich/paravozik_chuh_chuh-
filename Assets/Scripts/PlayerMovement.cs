using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;
    private bool groundedPlayer;

    [Header("Настройки движения")]
    [SerializeField] private float walkSpeed = 5.0f;
    [SerializeField] private float runSpeed = 9.0f;
    [SerializeField] private float jumpHeight = 1.0f;
    [SerializeField] private float gravityValue = -9.81f;

    [Header("Ссылки")]
    [SerializeField] private Transform playerCamera;

    [Header("Анимация (для будущего подключения)")]
    [SerializeField] private Animator animator;

    private float coyoteTimeCounter;
    [SerializeField] private float coyoteTime = 0.15f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        
        groundedPlayer = controller.isGrounded;
        if (groundedPlayer)
        {
            coyoteTimeCounter = coyoteTime;
            if (playerVelocity.y < 0)
            {
                playerVelocity.y = -2f; // Прижим к полу
            }
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // Считываем ввод (WASD / Стрелочки)
        float moveX = 0f;
        float moveZ = 0f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveZ += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveZ -= 1f;

        // Вычисляем направление движения относительно КАМЕРЫ
        Vector3 forward = playerCamera.forward;
        Vector3 right = playerCamera.right;

        // Убираем составляющую по Y, чтобы игрок не летал вверх/вниз при взгляде камеры в небо/пол
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * moveZ + right * moveX).normalized;

        // Бег (Shift)
        bool isRunning = keyboard.leftShiftKey.isPressed;
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // Движение по горизонтали
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Логика прыжка
        if (keyboard.spaceKey.wasPressedThisFrame && coyoteTimeCounter > 0f)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravityValue);
            coyoteTimeCounter = 0f;
            TriggerJumpAnimation();
        }

        // Гравитация
        playerVelocity.y += gravityValue * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);

        // Анимации
        UpdateAnimations(move.magnitude, isRunning);
    }

    private void UpdateAnimations(float moveMagnitude, bool isRunning)
    {
        if (animator != null)
        {
            // Здесь будет логика параметров Animator (например, передача скорости)
        }
    }

    private void TriggerJumpAnimation()
    {
        Debug.Log("Прыжок!");
    }
}