using System.Collections;
using UnityEngine;

public class ToggleSteps : MonoBehaviour
{
    public GameObject targetObject1;
    public GameObject targetObject2;
    public GameObject targetLook;

    [Header("Налаштування переміщення")]
    public float moveDuration = 1.5f;
    public float curveHeight = 3f;

    private GameObject player;
    private MouseLook mouseLook;
    private CharacterController characterController; // Додано посилання на CC
    private bool isMoving = false;

    public bool isDown = false;
    public bool isUp = false;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            mouseLook = player.GetComponentInChildren<MouseLook>();
            characterController = player.GetComponent<CharacterController>();
        }
    }

    private void OnDisable()
    {
        if (isMoving)
        {
            ForceStopTracking();
        }
    }

    public void Teleport()
    {
        if (!isMoving && player != null && targetObject1 != null && targetObject2 != null)
        {
            if (isUp)
            {
                // Передаємо Transform замість Vector3
                StartCoroutine(MovePlayerAlongCurve(targetObject2.transform, targetObject1.transform));
                isDown = true;
                isUp = false;
            }
            else if (isDown)
            {
                StartCoroutine(MovePlayerAlongCurve(targetObject1.transform, targetObject2.transform));
                isUp = true;
                isDown = false;
            }
            else return;

            if (targetLook != null && mouseLook != null)
            {
                mouseLook.TrackObject(targetLook.transform);
            }
        }
    }

    // Змінено параметри на Transform, щоб відстежувати рухомі цілі
    private IEnumerator MovePlayerAlongCurve(Transform startTransform, Transform endTransform)
    {
        isMoving = true;
        float elapsedTime = 0f;

        // 1. ВИМИКАЄМО CharacterController перед рухом
        if (characterController != null)
            characterController.enabled = false;

        try
        {
            while (elapsedTime < moveDuration)
            {
                if (player == null || startTransform == null || endTransform == null)
                {
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                float t = elapsedTime / moveDuration;

                // 2. Динамічний Lerp. Зчитуємо position ЩОКАДРУ, щоб встигати за потягом
                Vector3 linearPosition = Vector3.Lerp(startTransform.position, endTransform.position, t);
                float heightOffset = Mathf.Sin(t * Mathf.PI) * curveHeight;

                player.transform.position = linearPosition + new Vector3(0f, heightOffset, 0f);

                yield return null;
            }

            // Гарантуємо точне приземлення у фінальну точку
            player.transform.position = endTransform.position;
        }
        finally
        {
            // 3. ВМИКАЄМО CharacterController назад
            if (characterController != null)
                characterController.enabled = true;

            ForceStopTracking();
        }
    }

    private void ForceStopTracking()
    {
        if (mouseLook != null)
        {
            mouseLook.StopTracking();
        }
        isMoving = false;
    }

    public void InDown()
    {
        isDown = true;
        isUp = false;
    }

    public void InUp()
    {
        isDown = false;
        isUp = true;
    }

    public void InNull()
    {
        isDown = false;
        isUp = false;
    }
}