using System.Collections.Generic;
using UnityEngine;

public class TrainComposition : MonoBehaviour
{
    [Header("Налаштування складу")]
    [Tooltip("Чи є цей склад головним (потяг гравця)")]
    public bool isMainComposition = false;

    [Tooltip("Максимальна відстань між вагонами для успішного зчеплення під час гри")]
    [SerializeField] private float maxCouplingDistance = 4.0f;

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
    /// Об'єднує цей склад з іншим (наприклад, коли локомотив під'їхав до відчеплених вагонів).
    /// Викликай це при колізії (тригері) зчеплень.
    /// </summary>
    public bool MergeWithComposition(TrainComposition otherComposition)
    {
        if (otherComposition == this || otherComposition.Cars.Count == 0) return false;

        TrainCar ourLast = cars[cars.Count - 1];
        TrainCar theirFirst = otherComposition.Cars[0];
        TrainCar theirLast = otherComposition.Cars[otherComposition.Cars.Count - 1];

        // Перевіряємо, чи ми близько до початку їхнього складу
        float distToTheirFirst = Vector3.Distance(ourLast.transform.position, theirFirst.transform.position);

        // Перевіряємо, чи ми не врізалися в них з іншого боку (в хвіст)
        float distToTheirLast = Vector3.Distance(ourLast.transform.position, theirLast.transform.position);

        if (distToTheirFirst <= maxCouplingDistance)
        {
            // Правильний порядок: наш хвіст торкнувся їхньої голови
            // Логічно додаємо вагони без телепортації
            cars.AddRange(otherComposition.Cars);

            Debug.Log($"[Train] Склад успішно приєднано! Нова кількість вагонів: {cars.Count}");

            // Знищуємо компонент іншого складу (він тепер частина нашого)
            Destroy(otherComposition.gameObject);
            return true;
        }
        else if (distToTheirLast <= maxCouplingDistance)
        {
            // Неправильний порядок: наш хвіст торкнувся їхнього хвоста.
            // Тут можна додати логіку реверсу вагонів, але поки просто блокуємо зчеплення.
            Debug.LogWarning("[Train] Спроба зчеплення з неправильного боку (хвіст до хвоста). Зчеплення відхилено.");
            return false;
        }

        Debug.LogWarning("[Train] Вагони занадто далеко для зчеплення!");
        return false;
    }

    public bool MergeWithComposition(TrainComposition otherComposition, TrainBuffer ourBuffer, TrainBuffer theirBuffer)
    {
        if (otherComposition == this || otherComposition.Cars.Count == 0) return false;

        TrainCar ourCar = ourBuffer.OwnerCar;
        TrainCar theirCar = theirBuffer.OwnerCar;

        bool isOurRear = ourCar == cars[cars.Count - 1] && ourBuffer.Position == TrainBuffer.BufferPosition.Rear;
        bool isOurFront = ourCar == cars[0] && ourBuffer.Position == TrainBuffer.BufferPosition.Front;

        bool isTheirFront = theirCar == otherComposition.Cars[0] && theirBuffer.Position == TrainBuffer.BufferPosition.Front;
        bool isTheirRear = theirCar == otherComposition.Cars[otherComposition.Cars.Count - 1] && theirBuffer.Position == TrainBuffer.BufferPosition.Rear;

        List<TrainCar> newCarsToAdd = new List<TrainCar>(otherComposition.Cars);

        // Сценарій 1: Наш ХВІСТ підчепив їхню ГОЛОВУ (стандартно під'їхали локомотивом вперед до вагонів)
        if (isOurRear && isTheirFront)
        {
            cars.AddRange(newCarsToAdd);
        }
        // Сценарій 2: Наш ХВІСТ підчепив їхній ХВІСТ (вагони стояли задом наперед)
        else if (isOurRear && isTheirRear)
        {
            newCarsToAdd.Reverse(); // Розвертаємо список іншого складу
            // TODO: Якщо у тебе вагони мають напрямок (Front/Rear visuals), тут треба викликати:
            // foreach(var c in newCarsToAdd) c.ReverseDirection();
            cars.AddRange(newCarsToAdd);
        }
        // Сценарій 3: Наша ГОЛОВА (локомотив передом) підчепила їхній ХВІСТ
        else if (isOurFront && isTheirRear)
        {
            cars.InsertRange(0, newCarsToAdd); // Вставляємо вагони ПЕРЕД нашим локомотивом
        }
        // Сценарій 4: Наша ГОЛОВА підчепила їхню ГОЛОВУ
        else if (isOurFront && isTheirFront)
        {
            newCarsToAdd.Reverse();
            cars.InsertRange(0, newCarsToAdd);
        }
        else
        {
            Debug.LogWarning("[Train] Спроба зчеплення всередині складу (не перший і не останній вагон). Відхилено.");
            return false;
        }

        // Присвоюємо новим вагонам цей склад
        foreach (var car in newCarsToAdd)
        {
            car.transform.SetParent(this.transform);
        }

        // Тут ми викликаємо функцію з TrainCar, щоб вагони вирівнялися ПО КОЛІЇ (а не по вектору)
        AlignCarsOnTrack();

        Debug.Log($"[Train] Склад успішно об'єднано! Всього вагонів: {cars.Count}");
        Destroy(otherComposition);
        return true;
    }

    private void AlignCarsOnTrack()
    {
        // Припускаємо, що перший вагон (індекс 0) стоїть правильно.
        // Усі наступні вагони підтягуються до нього.
        for (int i = 1; i < cars.Count; i++)
        {
            // Якщо у TrainCar є метод SnapBehindOtherCar, використовуємо його:
            // Він має розраховувати дистанцію вздовж сплайну/колії!
            cars[i].SnapBehindOtherCar(cars[i - 1], defaultCouplerGap);
        }
    }

    public void DetachCar(TrainCar firstCarToDetach)
    {
        int index = cars.IndexOf(firstCarToDetach);

        // Не можна відчепити локомотив або вагон, якого немає в списку
        if (index <= 0 || index >= cars.Count)
        {
            Debug.LogWarning("[Train] Неможливо відчепити цей вагон.");
            return;
        }

        // Вагон, який залишається останнім у нашому складі
        TrainCar lastCarOfMain = cars[index - 1];

        // Збираємо всі вагони, що відриваються (від index і до кінця)
        List<TrainCar> detachedCars = cars.GetRange(index, cars.Count - index);
        cars.RemoveRange(index, cars.Count - index);

        // 1. Створюємо новий склад для відчеплених вагонів
        GameObject newTrainGO = new GameObject($"Abandoned_Train_{System.Guid.NewGuid().ToString().Substring(0, 5)}");
        var newComp = newTrainGO.AddComponent<TrainComposition>();
        newComp.isMainComposition = false;

        // 2. Переносимо об'єкти в ієрархії
        foreach (var car in detachedCars)
        {
            car.transform.SetParent(newComp.transform, true); // true зберігає координати
        }
        newComp.cars.AddRange(detachedCars);

        // 3. Скидаємо статус буферів між розірваними вагонами
        ResetBuffersBetween(lastCarOfMain, firstCarToDetach);

        // 4. Даємо легкий поштовх, щоб візуально показати розрив
        PushCarsApart(lastCarOfMain, firstCarToDetach);

        Debug.Log($"[Train] Відстиковано {detachedCars.Count} вагонів. Створено новий склад.");
    }

    private void ResetBuffersBetween(TrainCar carA, TrainCar carB)
    {
        // Знаходимо буфери на стику між двома вагонами і запускаємо їм розчеплення
        foreach (var buffer in carA.GetComponentsInChildren<TrainBuffer>())
        {
            if (buffer.Position == TrainBuffer.BufferPosition.Rear)
                buffer.Disconnect();
        }
        foreach (var buffer in carB.GetComponentsInChildren<TrainBuffer>())
        {
            if (buffer.Position == TrainBuffer.BufferPosition.Front)
                buffer.Disconnect();
        }
    }

    private void PushCarsApart(TrainCar carA, TrainCar carB)
    {
        // Даємо легке початкове кочення відчепленому вагону назад
        carB.CurrentVelocity = -0.8f;
        carB.MoveCar(-0.1f);
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Ініціалізація ТІЛЬКИ НА СТАРТІ рівня. Тут Snap виправданий, бо фізика ще не працює.
    /// </summary>
    public void InitializeTrain(TrackSegment startSegment, float startDistance)
    {
        if (cars.Count == 0 || startSegment == null) return;

        cars[0].InitializeCar(startSegment, startDistance);

        for (int i = 1; i < cars.Count; i++)
        {
            if (cars[i] != null && cars[i - 1] != null)
            {
                // Тут залишаємо Snap, бо ми спавнимо потяг "з нуля"
                cars[i].SnapBehindOtherCar(cars[i - 1], defaultCouplerGap);
            }
        }
        Debug.Log($"[Train] Потяг розставлено. Кількість: {cars.Count}. Головний склад: {isMainComposition}");
    }

    public float GetTotalMass()
    {
        float totalMass = 0f;
        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i] != null)
                totalMass += cars[i].Mass; // Використовуємо масу з TrainCar
        }
        // Захист від ділення на нуль
        return totalMass > 0f ? totalMass : 1000f;
    }

    public int GetCarIndex(TrainCar car)
{
    return cars.IndexOf(car);
}
}