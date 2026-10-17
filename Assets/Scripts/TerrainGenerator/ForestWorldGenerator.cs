using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForestWorldGenerator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("World boundary in XZ — Zone3D component.")]
    public Zone3D worldBoundaryZone;

    public ChunkData bakedForestHub;
    public int maxTrails = 40;

    [Header("Prefabs")]
    public List<ChunkData> allPrefabs;

    [Header("Async Generation")]
    [Tooltip("Chunks to spawn per frame before yield")]
    public int chunksPerFrame = 10;

    [Header("Connector Snapping")]
    [Tooltip("Enable face-to-face edge snapping instead of aura-based random placement.")]
    public bool useConnectorSnapping = true;
    public float connectorSearchRadius = 25f;

    private Queue<ChunkData> openTrails = new Queue<ChunkData>();
    private List<ChunkData> generatedChunks = new List<ChunkData>();
    private List<Zone3D> registeredCores = new List<Zone3D>();
    private List<ChunkConnector> allFreeConnectors = new List<ChunkConnector>();
    private int currentTrailCount = 0;

    // Cached list for prefab filtering (reduces GC)
    private List<ChunkData> filteredPrefabsCache = new List<ChunkData>();

    // ──────────────────────────────────────────────────────────────────────

    private void Start()
    {
        StartCoroutine(StartForestGenerationAsync());
    }

    public IEnumerator StartForestGenerationAsync()
    {
        // 1. Place baked hub
        ChunkData hub = Instantiate(bakedForestHub, Vector3.zero, Quaternion.identity);
        RegisterChunk(hub);
        openTrails.Enqueue(hub);
        yield return null;

        // 2. Trail phase
        yield return StartCoroutine(GeneratePhase1_5_Trails());

        // 3. POI phase
        yield return StartCoroutine(GeneratePhase2_POI());

        Debug.Log($"Forest generation complete: {currentTrailCount} trails, {generatedChunks.Count} chunks total");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Phase 1.5: Trails
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator GeneratePhase1_5_Trails()
    {
        int spawnedThisFrame = 0;

        while (openTrails.Count > 0 && currentTrailCount < maxTrails)
        {
            ChunkData currentChunk = openTrails.Dequeue();

            foreach (DirectionRule rule in currentChunk.rules)
            {
                if (rule.allowedCategory != ChunkCategory.Trail) continue;
                if (Random.Range(0f, 100f) > rule.spawnProbability) continue;

                ChunkData newTrail = useConnectorSnapping
                    ? TrySpawnConnector(currentChunk, rule)
                    : TrySpawnAura(currentChunk, rule);

                if (newTrail != null)
                {
                    openTrails.Enqueue(newTrail);
                    currentTrailCount++;
                    spawnedThisFrame++;

                    if (spawnedThisFrame >= chunksPerFrame)
                    {
                        spawnedThisFrame = 0;
                        yield return null;
                    }
                }
            }
        }
        Debug.Log($"Trails generated: {currentTrailCount}");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Phase 2: POI
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator GeneratePhase2_POI()
    {
        int initialCount = generatedChunks.Count;
        for (int i = 0; i < initialCount; i++)
        {
            ChunkData currentChunk = generatedChunks[i];

            foreach (DirectionRule rule in currentChunk.rules)
            {
                if (rule.allowedCategory != ChunkCategory.POI) continue;
                if (Random.Range(0f, 100f) > rule.spawnProbability) continue;

                ChunkData newPOI = useConnectorSnapping
                    ? TrySpawnConnector(currentChunk, rule)
                    : TrySpawnAura(currentChunk, rule);

                if (newPOI != null)
                    Debug.Log($"POI spawned, biome: {newPOI.biome}");
            }

            if (i % chunksPerFrame == 0 && i > 0)
                yield return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Connector-based snapping
    // ──────────────────────────────────────────────────────────────────────

    private ChunkData TrySpawnConnector(ChunkData parent, DirectionRule rule)
    {
        GetFilteredPrefabs(rule);
        if (filteredPrefabsCache.Count == 0) return null;

        // Find open connector in the desired direction
        Vector3 searchFrom = parent.transform.position +
                             parent.transform.TransformDirection(rule.localDirection) * 2f;

        ChunkConnector openConnector = GenMath.FindNearestFreeConnector(
            allFreeConnectors, searchFrom, ConnectorType.Any, connectorSearchRadius);

        if (openConnector == null) return TrySpawnAura(parent, rule); // fallback

        ChunkData prefab = filteredPrefabsCache[Random.Range(0, filteredPrefabsCache.Count)];
        if (prefab.connectors == null || prefab.connectors.Length == 0)
            return TrySpawnAura(parent, rule);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            int idx = Random.Range(0, prefab.connectors.Length);
            ChunkConnector inTemplate = prefab.connectors[idx];

            GenMath.CalculateSnapTransform(openConnector, inTemplate,
                out Vector3 newPos, out Quaternion newRot);

            if (worldBoundaryZone != null && !worldBoundaryZone.Contains(newPos))
                continue;

            ChunkData candidate = Instantiate(prefab, newPos, newRot);

            bool free = candidate.zoneA_Core == null ||
                        GenMath.IsZone3DFree(candidate.zoneA_Core, registeredCores);

            if (!free)
            {
                Destroy(candidate.gameObject);
                continue;
            }

            openConnector.isOccupied = true;
            allFreeConnectors.Remove(openConnector);
            candidate.connectors[idx].isOccupied = true;

            RegisterChunk(candidate);
            return candidate;
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Aura-based placement (fallback / legacy)
    // ──────────────────────────────────────────────────────────────────────

    private ChunkData TrySpawnAura(ChunkData parentChunk, DirectionRule rule)
    {
        GetFilteredPrefabs(rule);
        if (filteredPrefabsCache.Count == 0) return null;

        int maxAttempts = 15;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 targetPos = parentChunk.GetRandomPointInAura();

            if (worldBoundaryZone != null && !worldBoundaryZone.Contains(targetPos))
                continue;

            Vector3 forwardWorld = parentChunk.transform.TransformDirection(rule.localDirection);
            if (!GenMath.IsInSector(parentChunk.transform.position, targetPos, forwardWorld, rule.minDotProduct))
                continue;

            ChunkData prefab = filteredPrefabsCache[Random.Range(0, filteredPrefabsCache.Count)];
            Quaternion randomRot = Quaternion.Euler(0, Random.Range(0, 360), 0);
            ChunkData candidate = Instantiate(prefab, targetPos, randomRot);

            bool free = candidate.zoneA_Core == null ||
                        GenMath.IsZone3DFree(candidate.zoneA_Core, registeredCores);

            if (free)
            {
                RegisterChunk(candidate);
                return candidate;
            }
            Destroy(candidate.gameObject);
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private void GetFilteredPrefabs(DirectionRule rule)
    {
        filteredPrefabsCache.Clear();
        foreach (var p in allPrefabs)
        {
            if (p == null) continue;
            if (p.category == rule.allowedCategory &&
                (rule.allowedBiome == BiomeZone.Any || p.biome == rule.allowedBiome))
            {
                filteredPrefabsCache.Add(p);
            }
        }
    }

    private void RegisterChunk(ChunkData chunk)
    {
        generatedChunks.Add(chunk);

        if (chunk.zoneA_Core != null)
            registeredCores.Add(chunk.zoneA_Core);

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