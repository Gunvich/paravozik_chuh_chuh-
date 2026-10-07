using TMPro;
using UnityEngine;

public interface IInteractable
{
    public void Interact();
    public void StopInteract();
    KeyCode GetKey();
    string GetInteractPrompt();
}

public class PlayerInteractor : MonoBehaviour
{
    [Header("Налаштування")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactLayer;
    [SerializeField] private TMPro.TMP_Text interactTextInfo;

    private void Update()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        {
            if (hit.collider.TryGetComponent(out IInteractable interactableObject))
            {
                if (interactTextInfo != null)
                {
                    interactTextInfo.text = interactableObject.GetInteractPrompt();
                }

                if (Input.GetKeyDown(interactableObject.GetKey()))
                {
                    interactableObject.Interact();
                    playerCamera.GetComponent<MouseLook>().isRotation = false;
                }

                if (Input.GetKeyUp(interactableObject.GetKey()))
                {
                    interactableObject.StopInteract();
                    playerCamera.GetComponent<MouseLook>().isRotation = true;
                }
            }
        }
        else
        {
            if (interactTextInfo != null)
            interactTextInfo.text = "";
        }
    }
}