using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class MultiSwitch
{
    public Vector3 Angle;
    public Vector3 Move;
    public UnityEvent onSwitchSelected;
}

public class InteractableTrigger : MonoBehaviour, IInteractable
{
    public KeyCode key = KeyCode.E;

    public bool isMultiSwitch = false;

    [ShowIf("isMultiSwitch")]
    public ToggleRotator targetRotator;

    [HideIf("isMultiSwitch")]
    public bool isToggled = false;

    [Tooltip("Основний текст")]
    [SerializeField] private string promptTextFalse = "Відчинити";
    [Tooltip("Додатковий текст")]
    [HideIf("isMultiSwitch")]
    [SerializeField] private string promptTextTrue = "Зачинити";

    [HideIf("isMultiSwitch")]
    public UnityEvent onInteract;
    [HideIf("isMultiSwitch")]
    public UnityEvent onInteractTrue;
    [HideIf("isMultiSwitch")]
    public UnityEvent onInteractFalse;

    [ShowIf("isMultiSwitch")]
    public List<MultiSwitch> positionSwitch;

    [ShowIf("isMultiSwitch")]
    public int currentSwitch = 0;

    [ShowIf("isMultiSwitch")]
    public float sensitivity = 1f;

    [ShowIf("isMultiSwitch")]
    [Tooltip("Наскільки далеко треба потягнути мишку, щоб перемкнути передачу")]
    public float switchThreshold = 1.5f;

    [ShowIf("isMultiSwitch")]
    public UnityEvent onShiftUp;

    [ShowIf("isMultiSwitch")]
    public UnityEvent onShiftDown;


    private float totalMovementY = 0f;
    private bool isTracking = false;

    public void Interact()
    {
        if (!isMultiSwitch)
        {
            isToggled = !isToggled;
            onInteract?.Invoke();

            if (isToggled)
            {
                onInteractTrue?.Invoke();
            }
            else
            {
                onInteractFalse?.Invoke();
            }
        }
        else
        {
            isTracking = true;
            totalMovementY = 0f;
        }
    }

    public void StopInteract()
    {
        if (isMultiSwitch && isTracking)
        {
            isTracking = false;
            totalMovementY = 0f;
        }
    }

    public string GetInteractPrompt()
    {
        if (!isMultiSwitch)
            return isToggled ? promptTextTrue : promptTextFalse;
        else return promptTextFalse;
    }

    public KeyCode GetKey()
    {
        return key;
    }

    private void Update()
    {
        if (isMultiSwitch && isTracking)
        {
            totalMovementY += Input.GetAxis("Mouse Y") * sensitivity;

            if (totalMovementY >= switchThreshold)
            {
                Shift(1);
                totalMovementY = 0f;
            }
            else if (totalMovementY <= -switchThreshold)
            {
                Shift(-1);
                totalMovementY = 0f;
            }
        }
    }

    private void Shift(int direction)
    {
        if (positionSwitch == null || positionSwitch.Count == 0) return;

        int previousSwitch = currentSwitch;

        currentSwitch = Mathf.Clamp(currentSwitch + direction, 0, positionSwitch.Count - 1);
        positionSwitch[currentSwitch].onSwitchSelected?.Invoke();

        if (currentSwitch != previousSwitch)
        {
            if (direction > 0)
            {
                onShiftUp?.Invoke();
            }
            else
            {
                onShiftDown?.Invoke();
            }
            

            if (targetRotator != null)
            {
                targetRotator.SetTargetState(
                    positionSwitch[currentSwitch].Angle, 
                    positionSwitch[currentSwitch].Move
                );
            }
        }
    }

    private void Start()
    {
        targetRotator.SetTargetState(
        positionSwitch[currentSwitch].Angle,
        positionSwitch[currentSwitch].Move
        );
    }
}