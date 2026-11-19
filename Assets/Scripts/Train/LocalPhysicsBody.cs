using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CharacterController))]
public class TriggerLocalSpace : MonoBehaviour
{
    [Header("Налаштування локальної фізики")]
    public string localGridTag = "LocalGrid";
    public float detachDelay = 0.5f;

    // Додаємо публічну змінну, щоб блокувати логіку під час анімації
    public bool isClimbingOrInteracting = false;

    private readonly List<Collider> _activeColliders = new List<Collider>();
    private Transform _currentGrid;
    private float _detachTimer = 0f;
    private Transform _originalParent;

    [Tooltip("Викликається лише тоді, коли гравець ЗМІНЮЄ вагон або виходить на землю")]
    public UnityEvent OnGridChanged;

    void Awake()
    {
        _originalParent = transform.parent;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(localGridTag) && !_activeColliders.Contains(other))
        {
            _activeColliders.Add(other);
            ExpandTriggerIfNeeded(other);
        }
        // ПРИБРАНО: Exit.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(localGridTag))
        {
            _activeColliders.Remove(other);
        }
        // ПРИБРАНО: Exit.Invoke();
    }

    private void ExpandTriggerIfNeeded(Collider col)
    {
        if (col is BoxCollider box && !col.gameObject.name.EndsWith("_Padded"))
        {
            box.size = new Vector3(box.size.x + 1.0f, box.size.y + 1.0f, box.size.z + 2.0f);
            col.gameObject.name += "_Padded";
        }
    }

    private Transform GetGridTransform(Collider col)
    {
        if (col == null) return null;
        if (col.attachedRigidbody != null) return col.attachedRigidbody.transform;

        // Замініть на вашу логіку TrainCar, якщо вона відрізняється
        var trainCar = col.GetComponentInParent<Transform>();
        if (trainCar != null && trainCar.name.Contains("Train")) return trainCar; // Приклад

        return col.transform.root;
    }

    void LateUpdate()
    {
        // Якщо гравець зараз в анімації залазання - ігноруємо зміну тригерів
        if (isClimbingOrInteracting) return;

        _activeColliders.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

        Transform desiredGrid = null;

        if (_currentGrid != null)
        {
            foreach (var col in _activeColliders)
            {
                if (GetGridTransform(col) == _currentGrid)
                {
                    desiredGrid = _currentGrid;
                    _detachTimer = 0f;
                    break;
                }
            }
        }

        if (desiredGrid == null && _activeColliders.Count > 0)
        {
            desiredGrid = GetGridTransform(_activeColliders[_activeColliders.Count - 1]);
            _detachTimer = 0f;
        }
        else if (desiredGrid == null && _currentGrid != null)
        {
            _detachTimer += Time.deltaTime;
            if (_detachTimer < detachDelay)
            {
                desiredGrid = _currentGrid;
            }
        }

        // 3. Зміна гріда при ДІЙСНІЙ зміні
        if (_currentGrid != desiredGrid)
        {
            ChangeGrid(desiredGrid);
        }
    }

    private void ChangeGrid(Transform newGrid)
    {
        _currentGrid = newGrid;

        if (_currentGrid != null)
        {
            transform.SetParent(_currentGrid, true);
        }
        else
        {
            transform.SetParent(_originalParent, true);
        }

        // Викликаємо синхронізацію камери ТІЛЬКИ тут, коли гріди дійсно змінилися
        OnGridChanged.Invoke();
    }
}