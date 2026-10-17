using NaughtyAttributes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[DefaultExecutionOrder(-200)]
public class TrainEngine : MonoBehaviour
{
    [SerializeField] private TrainComposition trainComposition;
#pragma warning disable CS0414 // Used by NaughtyAttributes [ShowIf] via string reflection
    [SerializeField] private bool isEngine = false;
#pragma warning restore CS0414
    [ShowIf("isEngine")]
    [SerializeField] private float maxSpeed = 30f;       // м/с
    [Header("Параметри тяги")]

    [SerializeField] private float acceleration = 5f;    // м/с^2
    [SerializeField] private float brakeStrength = 10f;  // м/с^2

    private float throttleInput;
    [SerializeField] private float rollingResistance = 0.5f;

    public bool W = true, S = true;

    public float CurrentSpeed { get; private set; } // Поточна швидкість у м/с

    private bool isBraking { get; set; }

    private void Update()
    {
        if (trainComposition == null) return;

        // Блокуємо газ у забороненому напрямку
        if (!W && throttleInput > 0)
            throttleInput = 0;
        else if (!S && throttleInput < 0)
            throttleInput = 0;

        // Розблоковуємо протилежний напрямок при виборі газу в інший бік
        if (throttleInput < 0)
            W = true;
        else if (throttleInput > 0)
            S = true;

        if (isBraking)
        {
            // Активне гальмування до повної зупинки
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, brakeStrength * Time.deltaTime);
        }
        if (throttleInput != 0)
        {
            float targetSpeed = throttleInput * maxSpeed;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, rollingResistance * Time.deltaTime);
        }

        CurrentSpeed = Mathf.Clamp(CurrentSpeed, -maxSpeed * 0.4f, maxSpeed);

        float deltaDistance = CurrentSpeed * Time.deltaTime;

        trainComposition.MoveTrain(deltaDistance);
        Physics.SyncTransforms();
    }

    public void SetSpeed(float newSpeed)
    {
        CurrentSpeed = newSpeed;
    }
    public void SetBrake(bool brakeState)
    {
        isBraking = brakeState;
    }
    /// <summary>
    /// Блокує рух у заданому напрямку (уперед або назад) до моменту, поки потяг не поїде в інший бік
    /// </summary>
    public void BlockDirection(bool blockForward)
    {
        if (blockForward)
        {
            W = false;
            S = true;
            if (CurrentSpeed > 0f) CurrentSpeed = 0f;
            if (throttleInput > 0f) throttleInput = 0f;
        }
        else
        {
            S = false;
            W = true;
            if (CurrentSpeed < 0f) CurrentSpeed = 0f;
            if (throttleInput < 0f) throttleInput = 0f;
        }
    }

    public void SetInput(float newInput)
    {
        if (!W && newInput > 0)
        {
            throttleInput = 0;
            return;
        }
        if (!S && newInput < 0)
        {
            throttleInput = 0;
            return;
        }

        if (newInput < 0)
            W = true;
        else if (newInput > 0)
            S = true;

        throttleInput = newInput;
    }
}
