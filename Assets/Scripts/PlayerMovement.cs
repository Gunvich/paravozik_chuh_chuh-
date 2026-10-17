using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private CharacterController controller;
    private TriggerLocalSpace localSpace;
    private Vector3 playerVelocity;
    private bool groundedPlayer;

    [Header("Настройки движения")]
    [SerializeField] private float walkSpeed = 5.0f;
    [SerializeField] private float runSpeed = 9.0f;
    [SerializeField] private float jumpHeight = 1.0f;
    [SerializeField] private float gravityValue = -9.81f;

    [Header("Ссылки")]
    [SerializeField] private Transform playerCamera;

    [Header("Анимация")]
    [SerializeField] private Animator animator;

    private float coyoteTimeCounter;
    [SerializeField] private float coyoteTime = 0.15f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        localSpace = GetComponent<TriggerLocalSpace>();
    }

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        groundedPlayer = controller.isGrounded;
        if (groundedPlayer)
        {
            coyoteTimeCounter = coyoteTime;
            if (playerVelocity.y < 0f)
            {
                playerVelocity.y = -2f; // Притискання до поверхні
            }
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // Ввід
        float moveX = 0f;
        float moveZ = 0f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveZ += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveZ -= 1f;

        // Напрямок камери
        Vector3 forward = playerCamera.forward;
        Vector3 right = playerCamera.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveInput = (forward * moveZ + right * moveX).normalized;

        bool isRunning = keyboard.leftShiftKey.isPressed;
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // Стрибок
        if (keyboard.spaceKey.wasPressedThisFrame && coyoteTimeCounter > 0f)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravityValue);
            coyoteTimeCounter = 0f;
            TriggerJumpAnimation();
        }

        // Гравітація
        playerVelocity.y += gravityValue * Time.deltaTime;

        Vector3 finalDisplacement = (moveInput * currentSpeed * Time.deltaTime)
                                  + (playerVelocity * Time.deltaTime);

        controller.Move(finalDisplacement);

        UpdateAnimations(moveInput.magnitude, isRunning);
    }

    private void UpdateAnimations(float moveMagnitude, bool isRunning) { }
    private void TriggerJumpAnimation() => Debug.Log("Прыжок!");
}
