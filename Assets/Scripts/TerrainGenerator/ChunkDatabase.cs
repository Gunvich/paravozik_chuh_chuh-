using System.Collections.Generic;
using UnityEngine;

public class ChunkDatabase : MonoBehaviour
{
    public List<ChunkData> prefabs = new List<ChunkData>();

    public ChunkData GetRandomPrefab(ChunkCategory category, BiomeZone biome = BiomeZone.Any)
    {
        List<ChunkData> validPrefabs = new List<ChunkData>();

        foreach (var prefab in prefabs)
        {
            if (prefab.category == category && (biome == BiomeZone.Any || prefab.biome == biome))
            {
                validPrefabs.Add(prefab);
            }
        }

        if (validPrefabs.Count > 0)
        {
            return validPrefabs[Random.Range(0, validPrefabs.Count)];
        }

        Debug.LogError($"Не знайдено префаба для категорії {category} та біома {biome}");
        return null;
    }
}