using System.Collections.Generic;
using UnityEngine;

public class ForestWorldGenerator : MonoBehaviour
{
    [Header("Налаштування")]
    [Tooltip("Головний кордон світу. За його межі генерація не вийде.")]
    public PolygonCollider2D worldBoundary;

    public ChunkData bakedForestHub; // Наш "запечений" стартовий шматок
    public LayerMask zoneALayer;
    public int maxTrails = 40;

    [Header("Префаби (Спрощена база)")]
    public List<ChunkData> allPrefabs;

    private Queue<ChunkData> openTrails = new Queue<ChunkData>();
    private List<ChunkData> generatedChunks = new List<ChunkData>();
    private int currentTrailCount = 0;

    private void Start()
    {
        StartForestGeneration();
    }

    public void StartForestGeneration()
    {
        // 1. Ставимо Запечений Хаб
        ChunkData hub = Instantiate(bakedForestHub, Vector3.zero, Quaternion.identity);
        generatedChunks.Add(hub);

        // Хаб має правила для стежок на своїх краях
        openTrails.Enqueue(hub);

        // 2. Запускаємо фазу Стежок (Phase 1.5)
        GeneratePhase1_5_Trails();

        // 3. Запускаємо фазу Точок Інтересу (POI)
        GeneratePhase2_POI();
    }

    private void GeneratePhase1_5_Trails()
    {
        while (openTrails.Count > 0 && currentTrailCount < maxTrails)
        {
            ChunkData currentChunk = openTrails.Dequeue();

            foreach (DirectionRule rule in currentChunk.rules)
            {
                // Нас цікавлять тільки стежки
                if (rule.allowedCategory != ChunkCategory.Trail) continue;

                // Оскільки стежки НЕ обов'язкові, перевіряємо шанс
                if (Random.Range(0f, 100f) > rule.spawnProbability)
                {
                    continue; // Не пощастило, стежка тут обривається
                }

                TrySpawnChunk(currentChunk, rule, out ChunkData newTrail);

                if (newTrail != null)
                {
                    openTrails.Enqueue(newTrail);
                    generatedChunks.Add(newTrail);
                    currentTrailCount++;
                }
            }
        }
        Debug.Log($"Стежок згенеровано: {currentTrailCount}");
    }

    private void GeneratePhase2_POI()
    {
        // Проходимо по всіх згенерованих об'єктах (хаб, стежки)
        int initialCount = generatedChunks.Count;
        for (int i = 0; i < initialCount; i++)
        {
            ChunkData currentChunk = generatedChunks[i];

            foreach (DirectionRule rule in currentChunk.rules)
            {
                if (rule.allowedCategory != ChunkCategory.POI) continue;
                if (Random.Range(0f, 100f) > rule.spawnProbability) continue;

                // Шукаємо будинок лісника або руїни
                TrySpawnChunk(currentChunk, rule, out ChunkData newPOI);

                if (newPOI != null)
                {
                    generatedChunks.Add(newPOI);
                    Debug.Log($"Згенеровано POI у біомі: {newPOI.biome}");
                }
            }
        }
    }

    private bool TrySpawnChunk(ChunkData parentChunk, DirectionRule rule, out ChunkData spawnedChunk)
    {
        spawnedChunk = null;
        int maxAttempts = 15;

        // Шукаємо відповідний префаб у базі (фільтруємо за категорією ТА біомом)
        List<ChunkData> validPrefabs = allPrefabs.FindAll(p =>
            p.category == rule.allowedCategory &&
            (rule.allowedBiome == BiomeZone.Any || p.biome == rule.allowedBiome));

        if (validPrefabs.Count == 0) return false;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomPoint2D = parentChunk.GetRandomPointInAura();

            // НОВЕ: Перевіряємо, чи ця точка не вилізла за межі нашого світу!
            if (worldBoundary != null && !worldBoundary.OverlapPoint(randomPoint2D))
            {
                continue; // Точка за межами карти, пропускаємо цю спробу
            }

            Vector3 targetPos = new Vector3(randomPoint2D.x, 0, randomPoint2D.y);

            // Перевірка сектору
            Vector3 forwardWorld = parentChunk.transform.TransformDirection(rule.localDirection);
            if (!GenMath.IsInSector(parentChunk.transform.position, targetPos, forwardWorld, rule.minDotProduct))
            {
                continue;
            }

            ChunkData prefabToSpawn = validPrefabs[Random.Range(0, validPrefabs.Count)];

            // Стежки можуть крутитися як завгодно
            Quaternion randomRot = Quaternion.Euler(0, Random.Range(0, 360), 0);
            ChunkData newChunk = Instantiate(prefabToSpawn, targetPos, randomRot);

            // Перевіряємо, чи є місце для об'єкта
            if (GenMath.IsZoneFree(newChunk.zoneA_Core, zoneALayer))
            {
                spawnedChunk = newChunk;
                return true;
            }
            else
            {
                Destroy(newChunk.gameObject);
            }
        }
        return false;
    }
}