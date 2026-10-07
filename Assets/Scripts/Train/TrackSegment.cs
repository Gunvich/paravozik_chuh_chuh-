using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(SplineContainer))]
public class TrackSegment : MonoBehaviour
{
    [Header("З'єднання колії")]
    [Tooltip("Наступний сегмент (куди веде кінець цього сплайну)")]
    public TrackSegment nextSegment;
    [Tooltip("Попередній сегмент (звідки приходить початок цього сплайну)")]
    public TrackSegment previousSegment;

    private SplineContainer _splineContainer;

    public float Length { get; private set; }

    private void Awake()
    {
        _splineContainer = GetComponent<SplineContainer>();
        UpdateLength();
    }

    /// <summary>
    /// Оновлює розрахункову довжину сегмента в метрах
    /// </summary>
    public void UpdateLength()
    {
        if (_splineContainer == null)
            _splineContainer = GetComponent<SplineContainer>();

        // Рахуємо чесну фізичну довжину першого сплайну в контейнері
        Length = _splineContainer.CalculateLength();
    }

    /// <summary>
    /// Отримує світову позицію, напрямок (тангенс) та нормаль за дистанцією в метрах
    /// </summary>
    public void EvaluateAtDistance(float distance, out Vector3 worldPos, out Vector3 worldTangent, out Vector3 worldUp)
    {
        if (Length <= 0.0001f) UpdateLength();

        // Перетворюємо дистанцію (метри) у нормалізований параметр t [0; 1] без спотворень
        float t = _splineContainer.Spline.ConvertIndexUnit(distance, PathIndexUnit.Distance, PathIndexUnit.Normalized);

        _splineContainer.Evaluate(t, out float3 localPos, out float3 localTangent, out float3 localUp);

        worldPos = transform.TransformPoint((Vector3)localPos);
        worldTangent = transform.TransformDirection((Vector3)localTangent).normalized;
        worldUp = transform.TransformDirection((Vector3)localUp).normalized;
    }

    // Допоміжні методи для майбутніх стрілок/розвилок
    public virtual TrackSegment GetNextSegment() => nextSegment;
    public virtual TrackSegment GetPreviousSegment() => previousSegment;

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateLength();
    }
#endif
}