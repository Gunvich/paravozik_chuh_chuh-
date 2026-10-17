using System.Collections.Generic;
using UnityEngine;

public class TerrainStitcher : MonoBehaviour
{
    [Header("Налаштування зшивання")]
    [Tooltip("Матеріал зшитого рельєфу. Якщо порожньо — автоматично береться матеріал першого чанку.")]
    public Material terrainMaterial;

    [Tooltip("Кутовий поріг для згладжування нормалей (у градусах).")]
    public float smoothingAngle = 60f;

    [Tooltip("Радіус допуску для зшивання нормалей на стиках спусків (у метрах).")]
    [Range(0.01f, 1f)]
    public float stitchTolerance = 0.25f;

    [Header("Світовий шум рельєфу")]
    [Tooltip("Застосовувати детермінований світовий шум висоти до вершин рельєфу (гарантує відсутність вертикальних розривів).")]
    public bool applyWorldHeightNoise = false;
    public float noiseScale = 0.05f;
    public float noiseAmplitude = 0.5f;
    public int noiseSeed = 1337;

    [Header("Булеве об'єднання та згладжування межі")]
    [Tooltip("Використовувати буліан (Boolean Union) для перекритих спусків зі Smooth-Max висотами та зварюванням вершин межі.")]
    public bool useBooleanUnion = true;

    [Tooltip("Радіус плавного переходу буліану (Smooth-Max) між спусками (у метрах).")]
    public float booleanBlendRadius = 0.4f;

    [Tooltip("Відстань зварювання вершин на межі перетину (у метрах).")]
    public float vertexWeldDistance = 0.2f;

    [Header("Legacy fallback")]
    public string terrainTag = "TerrainChunk";

    private GameObject mergedTerrainObject;

    /// <summary>
    /// Головний метод зшивання списку згенерованих чанків.
    /// Працює напряму зі списком чанків (без повільного FindGameObjectsWithTag).
    /// </summary>
    public GameObject StitchChunks(List<ChunkData> chunks)
    {
        if (chunks == null || chunks.Count == 0)
        {
            Debug.LogWarning("TerrainStitcher.StitchChunks: список чанків порожній.");
            return null;
        }

        List<CombineInstance> validCombines = new List<CombineInstance>(chunks.Count);
        List<MeshRenderer> renderersToDisable = new List<MeshRenderer>();
        Material resolvedMaterial = terrainMaterial;

        // Створюємо або очищуємо контейнер для зшитого мешу
        if (mergedTerrainObject != null)
        {
            Destroy(mergedTerrainObject);
        }

        mergedTerrainObject = new GameObject("Procedural_Terrain_Merged");
        mergedTerrainObject.transform.SetParent(transform);
        mergedTerrainObject.transform.localPosition = Vector3.zero;
        mergedTerrainObject.transform.localRotation = Quaternion.identity;
        mergedTerrainObject.transform.localScale = Vector3.one;

        Matrix4x4 containerWorldToLocal = mergedTerrainObject.transform.worldToLocalMatrix;

        foreach (var chunk in chunks)
        {
            if (chunk == null) continue;

            // Find all MeshFilters in the chunk (including child objects)
            MeshFilter[] filters = chunk.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                if (filter == null || filter.sharedMesh == null) continue;

                if (!filter.sharedMesh.isReadable)
                {
                    Debug.LogError($"TerrainStitcher: Меш '{filter.sharedMesh.name}' на '{filter.gameObject.name}' НЕ має увімкненого 'Read/Write'! Увімкніть 'Read/Write' у налаштуваннях імпорту FBX моделі.");
                    continue;
                }

                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    if (resolvedMaterial == null && renderer.sharedMaterial != null)
                    {
                        resolvedMaterial = renderer.sharedMaterial;
                    }
                    renderersToDisable.Add(renderer);
                }

                CombineInstance ci = new CombineInstance();
                ci.mesh = filter.sharedMesh;
                // Матриця відносно контейнера Procedural_Terrain_Merged
                ci.transform = containerWorldToLocal * filter.transform.localToWorldMatrix;
                validCombines.Add(ci);
            }
        }

        if (validCombines.Count == 0)
        {
            Debug.LogWarning("TerrainStitcher: не знайдено жодного валідного або читабельного (isReadable) мешу для зшивання.");
            Destroy(mergedTerrainObject);
            return null;
        }

        Mesh combinedMesh = null;

        // ── 1. Спроба виконати CSG Boolean Union та зшивання нових вершин по відстані ──
        if (useBooleanUnion)
        {
            try
            {
                combinedMesh = Parabox.CSG.CSG.UnionAllWithSeamWelding(
                    validCombines, vertexWeldDistance, smoothingAngle);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"TerrainStitcher: Помилка CSG буліану ({ex.Message}), перемикаюся на стандартний CombineMeshes.");
            }
        }

        // ── 2. Fallback на стандартний CombineMeshes якщо буліан вимкнено або не повернув меш ──
        if (combinedMesh == null)
        {
            combinedMesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                name = "Stitched_Terrain_Mesh"
            };
            combinedMesh.CombineMeshes(validCombines.ToArray(), true, true);
            NormalSolver.RecalculateNormals(combinedMesh, smoothingAngle, stitchTolerance);
        }

        if (combinedMesh.vertexCount == 0)
        {
            Debug.LogError("TerrainStitcher: CombineMeshes повернув 0 вершин! Залишаю окремі меші видимими.");
            Destroy(mergedTerrainObject);
            return null;
        }

        // Тільки коли об'єднання гарантовано успішне — приховуємо окремі рендерери
        foreach (var r in renderersToDisable)
        {
            if (r != null) r.enabled = false;
        }

        // Застосування світового висотного шуму (якщо увімкнено)
        if (applyWorldHeightNoise)
        {
            ApplyWorldHeightNoise(combinedMesh);
        }

        MeshFilter mf = mergedTerrainObject.AddComponent<MeshFilter>();
        mf.mesh = combinedMesh;

        MeshRenderer mr = mergedTerrainObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = resolvedMaterial != null ? resolvedMaterial : GetDefaultFallbackMaterial();

        MeshCollider mc = mergedTerrainObject.AddComponent<MeshCollider>();
        mc.sharedMesh = combinedMesh;

        Debug.Log($"TerrainStitcher: успішно зшито {validCombines.Count} мешів у єдиний ландшафт (вершин: {combinedMesh.vertexCount}, трикутників: {combinedMesh.triangles.Length / 3}).");
        return mergedTerrainObject;
    }

    /// <summary>
    /// Legacy метод для зворотної сумісності.
    /// </summary>
    public void BuildIntersections()
    {
        // Спробуємо спочатку взяти чанки з WorldGenerator на тому ж об'єкті
        WorldGenerator wg = GetComponent<WorldGenerator>();
        if (wg != null)
        {
            var chunks = wg.GetAllGeneratedChunks();
            if (chunks != null && chunks.Count > 0)
            {
                StitchChunks(chunks);
                return;
            }
        }

        // Fallback за тегом
        GameObject[] terrainObjects = GameObject.FindGameObjectsWithTag(terrainTag);
        if (terrainObjects.Length == 0)
        {
            Debug.LogWarning($"TerrainStitcher: не знайдено об'єктів з тегом '{terrainTag}' та відсутні чанки у WorldGenerator.");
            return;
        }

        var chunkList = new List<ChunkData>();
        foreach (var go in terrainObjects)
        {
            ChunkData cd = go.GetComponent<ChunkData>();
            if (cd != null) chunkList.Add(cd);
        }

        if (chunkList.Count > 0)
        {
            StitchChunks(chunkList);
        }
    }

    private void ApplyWorldHeightNoise(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            // Since combinedMesh is in world-space already (via localToWorldMatrix)
            Vector3 v = vertices[i];
            float noiseY = GenMath.GetWorldHeightNoise(v, noiseScale, noiseAmplitude, noiseSeed);
            v.y += noiseY;
            vertices[i] = v;
        }
        mesh.vertices = vertices;
    }

    private Material GetDefaultFallbackMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { color = new Color(0.45f, 0.6f, 0.35f) };
    }
}