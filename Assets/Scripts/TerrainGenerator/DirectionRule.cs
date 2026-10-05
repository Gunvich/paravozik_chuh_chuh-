using UnityEngine;

// Категорії чанків
public enum ChunkCategory
{
    Road,
    Trail,
    Building,
    WildNature,
    POI // Point of Interest (Закинутий дім, табір)
}

// Зони (Біоми) для лісу
public enum BiomeZone
{
    Any,
    PineForest,
    Swamp,
    DeepForest
}

[System.Serializable]
public class DirectionRule
{
    [Header("Що шукаємо?")]
    public ChunkCategory allowedCategory;
    public BiomeZone allowedBiome = BiomeZone.Any;

    [Header("Де шукаємо?")]
    public Vector3 localDirection = Vector3.forward;
    [Range(-1f, 1f)] public float minDotProduct = 0.5f;

    [Header("Умови спавну")]
    public bool isMandatory = false; // Для доріг - true, для стежок лісу - false
    [Range(0f, 100f)] public float spawnProbability = 70f;
}