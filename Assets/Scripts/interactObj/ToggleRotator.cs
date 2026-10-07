using UnityEngine;
using System.Collections;

public class ToggleRotator : MonoBehaviour
{
    [Header("Налаштування")]
    [SerializeField] private Vector3 offAnimate = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 onAnimate = new Vector3(0f, 90f, 0f);
    [SerializeField] private Transform targetTransform;

    [Header("Налаштування анімації")]
    [SerializeField] private float animationDuration = 1f;
    [SerializeField] private AnimationCurve offAnimationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve onAnimationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool isOn = false;
    private Coroutine animationRotationCoroutine;
    private Coroutine animationMoveCoroutine;

    [SerializeField] private bool rotation = false;
    [SerializeField] private bool move = false;

    public void Toggle()
    {
        isOn = !isOn;

        Vector3 targetRot = isOn ? onAnimate : offAnimate;
        Vector3 targetPos = isOn ? onAnimate : offAnimate;

        SetTargetState(targetRot, targetPos, isOn ? onAnimationCurve : offAnimationCurve);
    }

    public void TurnOn()
    {
        if (!isOn) Toggle();
    }

    public void TurnOff()
    {
        if (isOn) Toggle();
    }

    public void SetTargetState(Vector3 targetRotation, Vector3 targetPosition, AnimationCurve curve = null)
    {
        if (curve == null) curve = onAnimationCurve;

        if (animationRotationCoroutine != null) StopCoroutine(animationRotationCoroutine);
        if (animationMoveCoroutine != null) StopCoroutine(animationMoveCoroutine);

        if (rotation)
            animationRotationCoroutine = StartCoroutine(AnimateRotation(Quaternion.Euler(targetRotation), curve));
        
        if (move)
            animationMoveCoroutine = StartCoroutine(AnimateMove(targetPosition, curve));
    }

    private IEnumerator AnimateRotation(Quaternion targetRotation, AnimationCurve curve)
    {
        Vector3 startAngles = targetTransform.localEulerAngles;
        Vector3 targetAngles = targetRotation.eulerAngles;

        targetAngles.x = startAngles.x + Mathf.DeltaAngle(startAngles.x, targetAngles.x);
        targetAngles.y = startAngles.y + Mathf.DeltaAngle(startAngles.y, targetAngles.y);
        targetAngles.z = startAngles.z + Mathf.DeltaAngle(startAngles.z, targetAngles.z);

        float timePassed = 0f;

        while (timePassed < animationDuration)
        {
            timePassed += Time.deltaTime;
            float t = timePassed / animationDuration;
            float curveEvaluation = curve.Evaluate(t);

            targetTransform.localEulerAngles = Vector3.LerpUnclamped(startAngles, targetAngles, curveEvaluation);
            yield return null;
        }

        targetTransform.localRotation = targetRotation;
    }

    private IEnumerator AnimateMove(Vector3 targetPosition, AnimationCurve curve)
    {
        Vector3 startPosition = targetTransform.localPosition;
        float timePassed = 0f;

        while (timePassed < animationDuration)
        {
            timePassed += Time.deltaTime;
            float t = timePassed / animationDuration;
            float curveEvaluation = curve.Evaluate(t);

            targetTransform.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, curveEvaluation);
            yield return null;
        }

        targetTransform.localPosition = targetPosition;
    }
}