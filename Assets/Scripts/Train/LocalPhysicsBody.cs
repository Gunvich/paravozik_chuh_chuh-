using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CharacterController))]
public class TriggerLocalSpace : MonoBehaviour
{
    [Header("Налаштування локальної фізики")]
    public string localGridTag = "LocalGrid";

    [Tooltip("Затримка перед відв'язкою у секундах. Рятує від мікро-проміжків між тригерами на швидкості.")]
    public float detachDelay = 0.5f;

    private readonly List<Collider> _activeColliders = new List<Collider>();
    private Transform _currentGrid;
    private float _detachTimer = 0f;

    // Зберігаємо початкового батька на випадок, якщо гравець стартує в якомусь контейнері
    private Transform _originalParent;

    public UnityEvent Exit;

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
        Exit.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(localGridTag))
        {
            _activeColliders.Remove(other);
        }
        Exit.Invoke();
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
        var trainCar = col.GetComponentInParent<TrainCar>(); // Якщо TrainCar існує у вашому проєкті
        if (trainCar != null) return trainCar.transform;
        return col.transform.root;
    }

    void Update()
    {
        // 1. Очищення невалідних колайдерів
        _activeColliders.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

        // 2. Стабільний вибір цільового гріда
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

        // Якщо ми ще не на гріді або вийшли з нього
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

        // 3. Зміна гріда при ДІЙСНІЙ зміні вагона
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
            // Робимо гравця дочірнім об'єктом платформи/вагона. 
            // true - означає, що гравець збереже свою глобальну позицію при зміні батька.
            transform.SetParent(_currentGrid, true);
        }
        else
        {
            // Повертаємо гравця до початкового стану (зазвичай null - тобто в корінь сцени)
            transform.SetParent(_originalParent, true);
        }
    }
}