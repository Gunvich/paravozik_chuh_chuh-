using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class TriggerLocalSpace : MonoBehaviour
{
    private CharacterController _controller;

    [Header("Налаштування локальної фізики")]
    public string localGridTag = "LocalGrid";

    private List<Transform> _activeGrids = new List<Transform>();
    private Transform _currentGrid;

    private Transform _anchor;

    // Зберігаємо координати якоря з попереднього кадру
    private Vector3 _previousAnchorPosition;
    private Quaternion _previousAnchorRotation;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _anchor = new GameObject("PlayerLocalAnchor").transform;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(localGridTag))
        {
            if (!_activeGrids.Contains(other.transform))
            {
                _activeGrids.Add(other.transform);
                UpdateCurrentGrid();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(localGridTag))
        {
            if (_activeGrids.Contains(other.transform))
            {
                _activeGrids.Remove(other.transform);
                UpdateCurrentGrid();
            }
        }
    }

    private void UpdateCurrentGrid()
    {
        if (_activeGrids.Count > 0)
        {
            Transform newGrid = _activeGrids[_activeGrids.Count - 1];

            if (_currentGrid != newGrid)
            {
                _currentGrid = newGrid;
                _anchor.SetParent(_currentGrid, true);
                SyncAnchorWithPlayer();
            }
        }
        else
        {
            _currentGrid = null;
            _anchor.SetParent(null);
        }
    }

    void LateUpdate()
    {
        if (_currentGrid != null)
        {
            ApplyLocalPhysics();
        }
    }

    private void ApplyLocalPhysics()
    {
        // 1. Рахуємо рух ПЛАТФОРМИ (різниця між поточною та минулою позицією якоря)
        Vector3 platformPositionDelta = _anchor.position - _previousAnchorPosition;
        Quaternion platformRotationDelta = _anchor.rotation * Quaternion.Inverse(_previousAnchorRotation);

        // 2. Додаємо рух платформи до персонажа
        if (platformPositionDelta.magnitude > 0.0001f)
        {
            _controller.Move(platformPositionDelta);
        }

        // 3. Застосовуємо обертання платформи
        if (platformRotationDelta != Quaternion.identity)
        {
            Vector3 eulerDelta = platformRotationDelta.eulerAngles;
            float yRot = eulerDelta.y;
            if (yRot > 180) yRot -= 360;

            transform.Rotate(0, yRot, 0);
        }

        // 4. Синхронізуємо якір з НОВОЮ позицією гравця (разом зі стрибком) для наступного кадру
        SyncAnchorWithPlayer();
    }

    private void SyncAnchorWithPlayer()
    {
        _anchor.position = transform.position;
        _anchor.rotation = transform.rotation;

        // Оновлюємо координати для розрахунків у наступному кадрі
        _previousAnchorPosition = _anchor.position;
        _previousAnchorRotation = _anchor.rotation;
    }
}