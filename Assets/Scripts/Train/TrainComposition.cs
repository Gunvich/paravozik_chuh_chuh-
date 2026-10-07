using System.Collections.Generic;
using UnityEngine;

public class TrainComposition : MonoBehaviour
{
    [Header("Склад вагонів (від першого до останнього)")]
    [SerializeField] private List<TrainCar> cars = new List<TrainCar>();
    [SerializeField] private float defaultCouplerGap = 2.5f;
    public IReadOnlyList<TrainCar> Cars => cars;

    /// <summary>
    /// Рухає весь потяг на задану дистанцію
    /// </summary>
    public void MoveTrain(float deltaDistance)
    {
        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i] != null)
            {
                cars[i].MoveCar(deltaDistance);
            }
        }
    }

    /// <summary>
    /// Пристикувати новий вагон у хвіст
    /// </summary>
    public void AttachCarAtEnd(TrainCar newCar, float couplerDistance)
    {
        if (cars.Contains(newCar)) return;

        TrainCar lastCar = cars[cars.Count - 1];

        // Точне позиціонування нового вагона за останнім
        newCar.SnapBehindOtherCar(lastCar, couplerDistance);

        cars.Add(newCar);
        Debug.Log($"[Train] Вагон {newCar.name} успішно пристиковано!");
    }

    /// <summary>
    /// Розстикувати вагон (і всі вагони позаду нього)
    /// </summary>
    public void DetachCar(TrainCar carToDetach)
    {
        int index = cars.IndexOf(carToDetach);
        if (index <= 0) return; // Локомотив (перший) не розстиковуємо так просто

        // Забираємо від'єднаний вагон і хвіст
        List<TrainCar> detachedCars = new List<TrainCar>();
        for (int i = index; i < cars.Count; i++)
        {
            detachedCars.Add(cars[i]);
        }

        cars.RemoveRange(index, cars.Count - index);

        // Створюємо новий незалежний склад для від'єднаних вагонів (вони тепер стоять окремо)
        GameObject newTrainGO = new GameObject("Abandoned_Train_Cars");
        var newComp = newTrainGO.AddComponent<TrainComposition>();
        newComp.cars.AddRange(detachedCars);

        Debug.Log($"[Train] Вагони від індексу {index} відстиковано!");
    }
    public void InitializeTrain(TrackSegment startSegment, float startDistance)
    {
        if (cars.Count == 0 || startSegment == null) return;
        // 1. Ставимо перший вагон (локомотив) на стартову точку
        cars[0].InitializeCar(startSegment, startDistance);
        // 2. Усі наступні вагони автоматично шикуємо один за одним
        for (int i = 1; i < cars.Count; i++)
        {
            if (cars[i] != null && cars[i - 1] != null)
            {
                cars[i].SnapBehindOtherCar(cars[i - 1], defaultCouplerGap);
            }
        }
        Debug.Log($"[Train] Потяг успішно розставлено на колії. Кількість вагонів: {cars.Count}");
    }
}