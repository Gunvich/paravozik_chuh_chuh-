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
    private bool isMoving = false;

    public bool isDown = false;
    public bool isUp = false;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            mouseLook = player.GetComponentInChildren<MouseLook>();
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
                StartCoroutine(MovePlayerAlongCurve(targetObject2.transform.position, targetObject1.transform.position)); 
                isDown = true;
                isUp = false;
            }
            else if (isDown)
            {
                StartCoroutine(MovePlayerAlongCurve(targetObject1.transform.position, targetObject2.transform.position));
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

    private IEnumerator MovePlayerAlongCurve(Vector3 startPos, Vector3 endPos)
    {
        isMoving = true;
        float elapsedTime = 0f;
        player.transform.position = startPos;

        try
        {
            while (elapsedTime < moveDuration)
            {
                if (player == null)
                {
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                float t = elapsedTime / moveDuration;

                Vector3 linearPosition = Vector3.Lerp(startPos, endPos, t);
                float heightOffset = Mathf.Sin(t * Mathf.PI) * curveHeight;

                player.transform.position = linearPosition + new Vector3(0f, heightOffset, 0f);

                yield return null;

            }

            player.transform.position = endPos;
        }
        finally
        {
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

    public void InDown ()
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