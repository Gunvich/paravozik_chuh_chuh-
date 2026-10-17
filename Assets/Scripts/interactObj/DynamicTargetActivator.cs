using UnityEngine;

public class DynamicTargetActivator : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private string parentNameToFind = "MyTargetObject";
    [SerializeField] private string childNameToFind = "Phone";

    private Transform parentObject;
    private GameObject targetObject;

    private void TryFindTarget()
    {
        if (targetObject != null) return;

        if (parentObject == null)
        {
            GameObject foundParent = GameObject.Find(parentNameToFind);
            if (foundParent != null)
            {
                parentObject = foundParent.transform;
            }
            else
            {
                return;
            }
        }

        if (parentObject != null)
        {
            Transform hiddenChild = parentObject.Find(childNameToFind);

            if (hiddenChild != null)
            {
                targetObject = hiddenChild.gameObject;
            }
            else
            {
                Debug.LogWarning($"Об'єкт з назвою '{childNameToFind}' не знайдено всередині '{parentObject.name}'");
            }
        }
    }

    public void TurnOnTarget()
    {
        TryFindTarget();
        if (targetObject != null)
        {
            targetObject.SetActive(true);
        }
    }

    public void TurnOffTarget()
    {
        TryFindTarget();
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }

    public void SetTargetActive(bool state)
    {
        TryFindTarget();
        if (targetObject != null)
        {
            targetObject.SetActive(state);
        }
    }
}