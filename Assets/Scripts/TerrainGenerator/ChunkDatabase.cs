using System.Collections.Generic;
using UnityEngine;

public class ChunkDatabase : MonoBehaviour
{
    public List<ChunkData> prefabs = new List<ChunkData>();

    // Cached list to avoid GC allocation on each call
    private List<ChunkData> validPrefabsCache = new List<ChunkData>();

    public ChunkData GetRandomPrefab(ChunkCategory category, BiomeZone biome = BiomeZone.Any)
    {
        validPrefabsCache.Clear();

        foreach (var prefab in prefabs)
        {
            if (prefab.category == category && (biome == BiomeZone.Any || prefab.biome == biome))
            {
                validPrefabsCache.Add(prefab);
            }
        }

        if (validPrefabsCache.Count > 0)
        {
            return validPrefabsCache[Random.Range(0, validPrefabsCache.Count)];
        }

        Debug.LogError($"No prefabs found for category {category}, biome {biome}");
        return null;
    }
}