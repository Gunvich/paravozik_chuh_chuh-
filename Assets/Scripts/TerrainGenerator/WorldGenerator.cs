using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TerrainStitcher))]
public class WorldGenerator : MonoBehaviour
{
    [Header("Налаштування")]
    public ChunkData defaultStartPrefab;
    public int maxChunks = 100;

    [Header("Межа світу (XZ)")]
    [Tooltip("Необов'язково: Zone3D, що обмежує весь світ.")]
    public Zone3D worldBoundaryZone;

    [Header("Попередньо розміщені чанки (замість FindObjectsOfType)")]
    public List<ChunkData> prePlacedChunks = new List<ChunkData>();

    [Header("Асинхронна генерація")]
    [Tooltip("Кількість чанків за один кадр перед yield")]
    public int chunksPerFrame = 10;

    [Header("Edge Snapping (1-to-N)")]
    [Tooltip("Якщо true — використовується нова система стикування ребро-в-ребро (1-to-N) зі спусками.")]
    public bool useEdgeSnapping = true;
    [Tooltip("Дозволити контрольований шум зсуву вздовж ребра у межах спуску (без дірок).")]
    public bool allowEdgeJitter = true;

    [Header("Legacy Connector Snapping")]
    [Tooltip("Якщо true — нові чанки прилягають по конекторах (якщо відсутні ChunkEdge).")]
    public bool useConnectorSnapping = true;
    [Tooltip("Максимальна відстань пошуку вільного конектора.")]
    public float connectorSearchRadius = 25f;

    private ChunkDatabase database;
    private TerrainStitcher stitcher;

    private Queue<ChunkData> openChunks = new Queue<ChunkData>();
    private List<ChunkData> allGeneratedChunks = new List<ChunkData>();

    // Registry of all placed core zones for SAT overlap checking
    private List<Zone3D> registeredCores = new List<Zone3D>();

    // Pool of free connectors across all placed chunks
    private List<ChunkConnector> allFreeConnectors = new List<ChunkConnector>();

    // Pool of free edges across all placed chunks
    private List<ChunkEdge> allFreeEdges = new List<ChunkEdge>();

    private int currentChunkCount = 0;

    public List<ChunkData> GetAllGeneratedChunks() => allGeneratedChunks;

    // ──────────────────────────────────────────────────────────────────────

    private void Start()
    {
        database = GetComponent<ChunkDatabase>();
        stitcher = GetComponent<TerrainStitcher>();
        StartCoroutine(GenerateWorldAsync());
    }

    private IEnumerator GenerateWorldAsync()
    {
        Phase0_PreGeneration();
        yield return null;

        yield return StartCoroutine(Phase1_Expansion());

        if (stitcher != null)
        {
            stitcher.StitchChunks(allGeneratedChunks);
        }
        Debug.Log($"Генерація завершена: {currentChunkCount} чанків");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Phase 0: seed chunks
    // ──────────────────────────────────────────────────────────────────────

    private void Phase0_PreGeneration()
    {
        if (prePlacedChunks.Count > 0)
        {
            foreach (var chunk in prePlacedChunks)
            {
                if (chunk == null) continue;
                RegisterChunk(chunk);
                openChunks.Enqueue(chunk);
                currentChunkCount++;
            }
            Debug.Log($"Phase0: знайдено {prePlacedChunks.Count} попередньо розміщених чанків");
        }
        else if (defaultStartPrefab != null)
        {
            ChunkData startChunk = Instantiate(defaultStartPrefab, Vector3.zero, Quaternion.identity);
            startChunk.transform.SetParent(transform);
            RegisterChunk(startChunk);
            openChunks.Enqueue(startChunk);
            currentChunkCount++;
        }

        Debug.Log($"Phase0 END: currentChunkCount={currentChunkCount}, freeConnectors={allFreeConnectors.Count}, freeEdges={allFreeEdges.Count}");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Phase 1: expansion
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator Phase1_Expansion()
    {
        int spawnedThisFrame = 0;

        Debug.Log($"Phase1_Expansion START: openChunks={openChunks.Count}, currentChunkCount={currentChunkCount}, maxChunks={maxChunks}");

        while (openChunks.Count > 0 && currentChunkCount < maxChunks)
        {
            ChunkData parent = openChunks.Dequeue();
            parent.EnsureEdgesCached();

            bool hasFreeEdges = parent.GetFreeEdges().Count > 0;
            bool hasFreeConnectors = parent.GetFreeConnectors().Count > 0;

            foreach (DirectionRule rule in parent.rules)
            {
                if (currentChunkCount >= maxChunks) break;
                if (!rule.isMandatory && Random.Range(0f, 100f) > rule.spawnProbability) continue;

                ChunkData newChunk = null;

                if (useEdgeSnapping && parent.edges != null && parent.edges.Length > 0)
                {
                    newChunk = TrySpawnChunkEdge(parent, rule);
                }
                
                if (newChunk == null && useConnectorSnapping && parent.connectors != null && parent.connectors.Length > 0)
                {
                    newChunk = TrySpawnChunkConnector(parent, rule);
                }

                if (newChunk == null && !useEdgeSnapping && !useConnectorSnapping)
                {
                    newChunk = TrySpawnChunkAura(parent, rule);
                }

                if (newChunk != null)
                {
                    openChunks.Enqueue(newChunk);
                    currentChunkCount++;
                    spawnedThisFrame++;

                    if (spawnedThisFrame >= chunksPerFrame)
                    {
                        spawnedThisFrame = 0;
                        yield return null;
                    }
                }
            }

            // Якщо у батьківського чанку все ще є вільні ребра (1-to-N можливість!), залишаємо його в черзі,
            // якщо ліміт ще не вичерпаний
            if (parent.GetFreeEdges().Count > 0 && currentChunkCount < maxChunks && openChunks.Count < 5)
            {
                openChunks.Enqueue(parent);
            }
        }

        Debug.Log($"Phase1_Expansion END: openChunks={openChunks.Count}, currentChunkCount={currentChunkCount}");
    }

    // ──────────────────────────────────────────────────────────────────────
    // 1-to-N Edge Snapping
    // ──────────────────────────────────────────────────────────────────────

    private ChunkData TrySpawnChunkEdge(ChunkData parent, DirectionRule rule)
    {
        Vector3 ruleWorldDir = parent.transform.TransformDirection(rule.localDirection).normalized;
        ruleWorldDir.y = 0f;

        // Знаходимо вільне ребро батька, напрямок якого найближчий до правила
        var freeParentEdges = parent.GetFreeEdges(0.5f);
        if (freeParentEdges.Count == 0) return null;

        ChunkEdge parentEdge = null;
        float bestDot = -2f;
        foreach (var edge in freeParentEdges)
        {
            float dot = Vector3.Dot(edge.WorldOutwardNormal, ruleWorldDir);
            if (dot > bestDot)
            {
                bestDot = dot;
                parentEdge = edge;
            }
        }

        if (parentEdge == null) return null;

        ChunkData prefab = database.GetRandomPrefab(rule.allowedCategory, rule.allowedBiome);
        if (prefab == null) return null;

        prefab.EnsureEdgesCached();
        if (prefab.edges == null || prefab.edges.Length == 0) return null;

        // Шукаємо підходяще ребро на префабі
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int inIdx = Random.Range(0, prefab.edges.Length);
            ChunkEdge inEdgeTemplate = prefab.edges[inIdx];

            // Перевіряємо сумісність типів ребер
            if (inEdgeTemplate.edgeType != ConnectorType.Any &&
                parentEdge.edgeType != ConnectorType.Any &&
                inEdgeTemplate.edgeType != parentEdge.edgeType)
            {
                continue;
            }

            // Пробуємо виділити інтервал на батьківському ребрі (з безпечним шумом)
            if (!parentEdge.TryAllocateInterval(inEdgeTemplate.Length, allowEdgeJitter, out float allocatedStart, out float jitter))
            {
                continue;
            }

            // Обчислюємо строгий 2D Yaw поворот і світову позицію
            GenMath.CalculateEdgeSnapTransform(
                parentEdge, allocatedStart, inEdgeTemplate, prefab,
                out Vector3 newPos, out Quaternion newRot);

            // Перевірка межі світу
            if (worldBoundaryZone != null && !worldBoundaryZone.Contains(newPos))
            {
                continue;
            }

            // Спавн кандидата
            ChunkData candidate = Instantiate(prefab, newPos, newRot);
            candidate.transform.SetParent(transform);

            // Перевірка перетину зон безпеки (Core)
            if (candidate.zoneA_Core != null)
            {
                bool free = GenMath.IsZone3DFree(candidate.zoneA_Core, registeredCores);
                if (!free)
                {
                    Destroy(candidate.gameObject);
                    continue;
                }
            }

            // Успіх! Позначаємо вхідне ребро на кандидаті
            candidate.EnsureEdgesCached();
            if (candidate.edges != null && candidate.edges.Length > inIdx)
            {
                candidate.edges[inIdx].ForceOccupy(0f, inEdgeTemplate.Length);
            }

            RegisterChunk(candidate);
            return candidate;
        }

        return null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Connector-based snapping (Legacy Fallback with strict 2D Yaw)
    // ──────────────────────────────────────────────────────────────────────

    private ChunkData TrySpawnChunkConnector(ChunkData parent, DirectionRule rule)
    {
        ChunkData prefab = database.GetRandomPrefab(rule.allowedCategory, rule.allowedBiome);
        if (prefab == null || prefab.connectors == null || prefab.connectors.Length == 0)
        {
            return null;
        }

        Vector3 ruleWorldDir = parent.transform.TransformDirection(rule.localDirection).normalized;
        ruleWorldDir.y = 0f;

        ChunkConnector parentConnector = null;
        float bestDot = -2f;

        foreach (var c in parent.connectors)
        {
            if (c == null || c.isOccupied) continue;
            Vector3 forwardXZ = c.transform.forward;
            forwardXZ.y = 0f;
            float dot = Vector3.Dot(forwardXZ.normalized, ruleWorldDir);
            if (dot > bestDot)
            {
                bestDot = dot;
                parentConnector = c;
            }
        }

        if (parentConnector == null) return null;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            int idx = Random.Range(0, prefab.connectors.Length);
            ChunkConnector inConnectorTemplate = prefab.connectors[idx];

            if (!parentConnector.IsCompatible(inConnectorTemplate.type))
                continue;

            GenMath.CalculateSnapTransform(
                parentConnector, inConnectorTemplate,
                out Vector3 newPos, out Quaternion newRot);

            if (worldBoundaryZone != null && !worldBoundaryZone.Contains(newPos))
                continue;

            ChunkData candidate = Instantiate(prefab, newPos, newRot);
            candidate.transform.SetParent(transform);

            if (candidate.zoneA_Core != null)
            {
                bool free = GenMath.IsZone3DFree(candidate.zoneA_Core, registeredCores);
                if (!free)
                {
                    Destroy(candidate.gameObject);
                    continue;
                }
            }

            parentConnector.isOccupied = true;
            allFreeConnectors.Remove(parentConnector);

            if (candidate.connectors != null && candidate.connectors.Length > idx)
            {
                candidate.connectors[idx].isOccupied = true;
            }

            RegisterChunk(candidate);
            return candidate;
        }

        return null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Aura-based placement (Fallback)
    // ──────────────────────────────────────────────────────────────────────

    private ChunkData TrySpawnChunkAura(ChunkData parent, DirectionRule rule)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector3 targetPos = parent.GetRandomPointInAura();

            if (worldBoundaryZone != null && !worldBoundaryZone.Contains(targetPos))
                continue;

            Vector3 targetDir = parent.transform.TransformDirection(rule.localDirection);
            targetDir.y = 0f;
            if (!GenMath.IsInSector(parent.transform.position, targetPos, targetDir, rule.minDotProduct))
                continue;

            ChunkData prefab = database.GetRandomPrefab(rule.allowedCategory, rule.allowedBiome);
            if (prefab == null) return null;

            ChunkData candidate = Instantiate(prefab, targetPos,
                Quaternion.Euler(0, Random.Range(0, 360), 0));
            candidate.transform.SetParent(transform);

            bool zoneFree = candidate.zoneA_Core == null ||
                            GenMath.IsZone3DFree(candidate.zoneA_Core, registeredCores);

            if (zoneFree)
            {
                RegisterChunk(candidate);
                return candidate;
            }
            else
            {
                Destroy(candidate.gameObject);
            }
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Registry helpers
    // ──────────────────────────────────────────────────────────────────────

    private void RegisterChunk(ChunkData chunk)
    {
        allGeneratedChunks.Add(chunk);

        if (chunk.zoneA_Core != null)
            registeredCores.Add(chunk.zoneA_Core);

        // Collect free edges
        chunk.EnsureEdgesCached();
        if (chunk.edges != null)
        {
            foreach (var e in chunk.edges)
            {
                if (e != null && e.HasFreeSpace(0.5f))
                    allFreeEdges.Add(e);
            }
        }

        // Collect free connectors
        if (chunk.connectors != null)
        {
            foreach (var c in chunk.connectors)
            {
                if (c != null && !c.isOccupied)
                    allFreeConnectors.Add(c);
            }
        }
    }
}