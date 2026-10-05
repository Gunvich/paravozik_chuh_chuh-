using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TerrainStitcher))]
public class WorldGenerator : MonoBehaviour
{
    [Header("Налаштування Світу")]
    public ChunkData defaultStartPrefab;
    public PolygonCollider2D worldBoundary; // Кордон карти (форма лісу)
    public LayerMask zoneALayer;
    public int maxChunks = 100;

    private ChunkDatabase database;
    private TerrainStitcher stitcher;

    private Queue<ChunkData> openChunks = new Queue<ChunkData>();
    private List<ChunkData> allGeneratedChunks = new List<ChunkData>();
    private int currentChunkCount = 0;

    private void Start()
    {
        database = GetComponent<ChunkDatabase>();
        stitcher = GetComponent<TerrainStitcher>();
        GenerateWorld();
    }

    private void GenerateWorld()
    {
        Phase0_PreGeneration();
        Phase1_Expansion();
        // Тут можна додати Phase 2 (Декорації)
        stitcher.BuildIntersections(); // Фінал
    }

    private void Phase0_PreGeneration()
    {
        // Знаходимо всі чанки (села, дороги), які ти розставив руками до запуску
        ChunkData[] prePlaced = FindObjectsOfType<ChunkData>();

        if (prePlaced.Length > 0)
        {
            foreach (var chunk in prePlaced)
            {
                openChunks.Enqueue(chunk);
                allGeneratedChunks.Add(chunk);
            }
            Debug.Log($"Пре-генерація: знайдено {prePlaced.Length} ручних об'єктів. Ростемо від них!");
        }
        else if (defaultStartPrefab != null)
        {
            ChunkData startChunk = Instantiate(defaultStartPrefab, Vector3.zero, Quaternion.identity);
            openChunks.Enqueue(startChunk);
            allGeneratedChunks.Add(startChunk);
        }
    }

    private void Phase1_Expansion()
    {
        while (openChunks.Count > 0 && currentChunkCount < maxChunks)
        {
            ChunkData parent = openChunks.Dequeue();

            foreach (DirectionRule rule in parent.rules)
            {
                // Якщо правило необов'язкове і ми не пройшли шанс - пропускаємо
                if (!rule.isMandatory && Random.Range(0f, 100f) > rule.spawnProbability) continue;

                TrySpawnChunk(parent, rule);
            }
        }
    }

    private void TrySpawnChunk(ChunkData parent, DirectionRule rule)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 randomPoint = parent.GetRandomPointInAura();

            // ПЕРЕВІРКА КОРДОНУ КАРТИ: Якщо точка за межами нашого полігону лісу - скасовуємо!
            if (worldBoundary != null && !worldBoundary.OverlapPoint(randomPoint)) continue;

            Vector3 targetPos = new Vector3(randomPoint.x, 0, randomPoint.y);
            Vector3 targetDir = parent.transform.TransformDirection(rule.localDirection);

            if (!MathHelpers.IsInSector(parent.transform.position, targetPos, targetDir, rule.minDotProduct)) continue;

            ChunkData prefab = database.GetRandomPrefab(rule.allowedCategory, rule.allowedBiome);
            if (prefab == null) return;

            // Рандомний поворот навколо осі Y
            ChunkData newChunk = Instantiate(prefab, targetPos, Quaternion.Euler(0, Random.Range(0, 360), 0));
            newChunk.transform.SetParent(this.transform); // Для порядку в ієрархії

            // Мікро-зміщення по висоті для уникнення Z-Fighting
            newChunk.transform.position += new Vector3(0, Random.Range(-0.02f, 0.02f), 0);

            if (MathHelpers.IsZoneAClear(newChunk.zoneA_Core, zoneALayer))
            {
                openChunks.Enqueue(newChunk);
                allGeneratedChunks.Add(newChunk);
                currentChunkCount++;
                return; // Успіх!
            }
            else
            {
                Destroy(newChunk.gameObject); // Місце зайняте, видаляємо
            }
        }
    }
}