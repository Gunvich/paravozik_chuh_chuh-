using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TerrainGenerator
{
    /// <summary>
    /// Головний менеджер генерації процедурного світу на основі полігональних чанків (Зона А + Зона Б).
    /// Реалізує двоетапну генерацію:
    /// 1-й прохід: Алгоритм кристалічного росту з ковзним стикуванням (Sliding Alignment) та перевіркою SAT.
    /// 2-й прохід: Заповнення тріщин та щілин квадратами/заповнювачами без накладання (SAT перевірка) та без масштабування (1:1 scale).
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    public class WorldGenerator : MonoBehaviour
    {
        [Header("Налаштування генерації")]
        [Tooltip("Фіксоване зерно генерації")]
        public int seed = 12345;

        [Tooltip("Чи генерувати нове випадкове зерно при кожному запуску")]
        public bool useRandomSeed = false;

        [Tooltip("Максимальна кількість чанків для генерації 1-го проходу")]
        [Range(1, 1000)]
        public int maxChunks = 40;

        [Tooltip("Максимальна кількість спроб знайти підходящий чанк для одного ребра")]
        [Range(1, 50)]
        public int maxTriesPerEdge = 15;

        [Header("Обмеження світу (Полігональні межі)")]
        [Tooltip("Полігон меж спавну (як Зона А для всієї карти). Якщо призначено, чанки не вийдуть за його межі.")]
        public SpawnAreaPolygon spawnBounds;

        [Tooltip("Поріг відстані до межі, ближче якої алгоритм обирає чанки-заглушки")]
        public float terminalDistanceThreshold = 20f;

        [Header("Пули префабів")]
        [Tooltip("Стандартні чанки для заповнення світу")]
        public List<ChunkObject> standardPrefabs = new List<ChunkObject>();

        [Tooltip("Чанки-заглушки (скелі, непрохідні межі з ребрами 'Terminal')")]
        public List<ChunkObject> terminalPrefabs = new List<ChunkObject>();

        [Tooltip("Префаби-заповнювачі для 2-го проходу (наприклад, квадрати 10x10, 5x5, які закривають тріщини)")]
        public List<ChunkObject> fillerPrefabs = new List<ChunkObject>();

        [Header("Другий прохід (Заповнення тріщин)")]
        [Tooltip("Чи запускати 2-й прохід заповнення тріщин автоматично після завершення основної генерації")]
        public bool runSecondPassOnFinish = true;

        [Tooltip("Максимальна кількість кроків/шарів заповнення тріщин квадратами у глибину")]
        [Range(1, 10)]
        public int maxFillerPasses = 5;

        [Header("Налаштування ковзання (Sliding)")]
        [Tooltip("Чи дозволяти чанкам ковзати вздовж ребер різної довжини")]
        public bool enableSliding = true;

        [Tooltip("Кількість випадкових позицій ковзання, які тестуються для кожного кандидата")]
        [Range(1, 10)]
        public int slideSamplesCount = 4;

        [Header("Контейнер ієрархії")]
        [Tooltip("Батьківський Transform, куди спавняться згенеровані чанки")]
        public Transform chunksContainer;

        [Header("Дебаг та статистика")]
        public bool logDebugInfo = false;
        public bool drawOpenEdgesGizmos = true;

        // Поточний стан генерації
        [System.NonSerialized] public List<ChunkObject> spawnedChunks = new List<ChunkObject>();
        [System.NonSerialized] public List<WorldEdge> activeOpenEdges = new List<WorldEdge>();
        [System.NonSerialized] public List<WorldEdge> unplacedCrackEdges = new List<WorldEdge>();

        private System.Random rng;

        private void Awake()
        {
            if (chunksContainer == null)
            {
                chunksContainer = transform;
            }
        }

        #region Публічні методи керування

        /// <summary>
        /// Повністю генерує світ: очищує процедурні чанки (зберігаючи запечені дороги/будинки),
        /// виконує 1-й прохід росту навколо запечених об'єктів та 2-й прохід заповнення тріщин.
        /// </summary>
        public void GenerateWorld(bool keepBaked = true)
        {
            InitializeWorld(keepBaked: keepBaked);

            int step = 0;
            int maxTotalSteps = maxChunks * 5; // Захист від нескінченного циклу

            // 1-й прохід: Основна генерація чанків
            while (activeOpenEdges.Count > 0 && spawnedChunks.Count < maxChunks && step < maxTotalSteps)
            {
                step++;
                bool success = GenerateStep();
                if (!success && activeOpenEdges.Count == 0)
                {
                    break;
                }
            }

            // 2-й прохід: Заповнення тріщин та щілин квадратами/заповнювачами без масштабування
            if (runSecondPassOnFinish)
            {
                RunSecondPass();
            }

            RunLeakTest();
        }

        /// <summary>
        /// Догенерація світу (Incremental / In-fill):
        /// Добудовує процедурний світ навколо існуючих та запечених чанків БЕЗ видалення вже згенерованого.
        /// </summary>
        public void GenerateIncremental()
        {
            if (chunksContainer == null)
            {
                chunksContainer = transform;
            }

            if (rng == null)
            {
                if (useRandomSeed) seed = UnityEngine.Random.Range(1000, 999999);
                rng = new System.Random(seed);
            }

            // 1. Синхронізуємо список spawnedChunks з усіма чанками на сцені (запеченими та процедурними)
            SyncSceneChunks();

            // 2. Якщо відкритих ребер немає, збираємо всі незакриті ребра
            if (activeOpenEdges.Count == 0)
            {
                CollectOpenEdgesFromSpawnedChunks();
            }

            // 3. Якщо і після цього немає ребер і світ абсолютно порожній - спавнимо стартовий
            if (activeOpenEdges.Count == 0 && spawnedChunks.Count == 0)
            {
                SpawnStartingChunk();
            }

            int step = 0;
            int maxTotalSteps = maxChunks * 5;

            while (activeOpenEdges.Count > 0 && spawnedChunks.Count < maxChunks && step < maxTotalSteps)
            {
                step++;
                bool success = GenerateStep();
                if (!success && activeOpenEdges.Count == 0)
                {
                    break;
                }
            }

            if (runSecondPassOnFinish)
            {
                RunSecondPass();
            }

            RunLeakTest();
        }

        /// <summary>
        /// Ініціалізує генератор: очищує процедурні чанки, сканує сцену на запечені об'єкти (дороги, будинки),
        /// відновлює їхні стики та підготовлює відкриті ребра до росту світу.
        /// </summary>
        public void InitializeWorld(bool keepBaked = true)
        {
            if (useRandomSeed)
            {
                seed = UnityEngine.Random.Range(1000, 999999);
            }
            rng = new System.Random(seed);

            ClearGeneratedWorld(keepBaked: keepBaked);

            if (chunksContainer == null)
            {
                chunksContainer = transform;
            }

            spawnedChunks.Clear();
            activeOpenEdges.Clear();
            unplacedCrackEdges.Clear();

            // 1. Знаходимо всі запечені об'єкти на сцені (в контейнері та за його межами)
            List<ChunkObject> bakedChunks = FindAllBakedChunksInScene();
            for (int i = 0; i < bakedChunks.Count; i++)
            {
                if (!spawnedChunks.Contains(bakedChunks[i]))
                {
                    spawnedChunks.Add(bakedChunks[i]);
                }
            }

            // 2. Відновлюємо стикування між запеченими об'єктами (щоб не проростати всередину запечених доріг)
            ReconnectBakedChunks(bakedChunks);

            // 3. Додаємо вільні ребра запечених об'єктів для догенерації (якщо allowGrowthFromEdges == true)
            for (int i = 0; i < bakedChunks.Count; i++)
            {
                ChunkObject chunk = bakedChunks[i];
                if (chunk == null || !chunk.allowGrowthFromEdges || chunk.localEdges == null) continue;

                for (int e = 0; e < chunk.localEdges.Count; e++)
                {
                    Edge edge = chunk.localEdges[e];
                    if (edge != null && !edge.isConnected && edge.edgeTag != "Terminal")
                    {
                        activeOpenEdges.Add(new WorldEdge(chunk, e, edge));
                    }
                }
            }

            if (bakedChunks.Count > 0 && logDebugInfo)
            {
                Debug.Log($"[WorldGenerator] Знайдено {bakedChunks.Count} запечених чанків (доріг/будівель). Відкритих ребер для догенерації: {activeOpenEdges.Count}");
            }

            // 4. Якщо відкритих ребер немає (або немає запечених об'єктів), спавнимо стартовий чанк
            if (activeOpenEdges.Count == 0)
            {
                SpawnStartingChunk();
            }
        }

        /// <summary>
        /// Виконує один крок росту: бере відкрите ребро, підбирає сумісний префаб і пристиковує його.
        /// </summary>
        public bool GenerateStep()
        {
            if (activeOpenEdges == null || activeOpenEdges.Count == 0)
            {
                if (logDebugInfo) Debug.Log("[WorldGenerator] Немає відкритих ребер для продовження генерації.");
                return false;
            }

            if (spawnedChunks.Count >= maxChunks)
            {
                if (logDebugInfo) Debug.Log($"[WorldGenerator] Досягнуто максимальної кількості чанків ({maxChunks}).");
                return false;
            }

            // Обираємо ребро зі списку відкритих
            int edgeIndex = rng.Next(0, activeOpenEdges.Count);
            WorldEdge currentEdge = activeOpenEdges[edgeIndex];

            // Перевіряємо відстань до межі світу
            bool nearBoundary = false;
            if (spawnBounds != null)
            {
                float distToBound = spawnBounds.GetDistanceToBoundary(currentEdge.Midpoint, currentEdge.worldNormal);
                if (distToBound < terminalDistanceThreshold)
                {
                    nearBoundary = true;
                }
            }

            // Формуємо список префабів
            List<ChunkObject> candidatePrefabs = new List<ChunkObject>();
            if (nearBoundary && terminalPrefabs != null && terminalPrefabs.Count > 0)
            {
                candidatePrefabs.AddRange(terminalPrefabs);
                candidatePrefabs.AddRange(standardPrefabs);
            }
            else
            {
                candidatePrefabs.AddRange(standardPrefabs);
                if (terminalPrefabs != null) candidatePrefabs.AddRange(terminalPrefabs);
            }

            if (candidatePrefabs.Count == 0)
            {
                Debug.LogWarning("[WorldGenerator] Пули префабів порожні!");
                return false;
            }

            // Шукаємо підходящий префаб та варіант стикування
            bool placed = TryPlaceChunkAtEdge(currentEdge, candidatePrefabs);

            if (!placed)
            {
                // Зберігаємо відкрите ребро для 2-го проходу (заповнення тріщин)
                if (!unplacedCrackEdges.Contains(currentEdge))
                {
                    unplacedCrackEdges.Add(currentEdge);
                }

                // Видаляємо це ребро з активних 1-го проходу, щоб не застрягати у нескінченному циклі
                activeOpenEdges.RemoveAt(edgeIndex);
                if (logDebugInfo)
                    Debug.Log($"[WorldGenerator] Ребро #{edgeIndex} додано до черги 2-го проходу (тріщини).");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Очищає згенерований світ. Якщо keepBaked == true, запечені об'єкти (дороги, будинки) зберігаються.
        /// </summary>
        public void ClearGeneratedWorld(bool keepBaked = true)
        {
            if (chunksContainer != null)
            {
                List<GameObject> toDestroy = new List<GameObject>();
                for (int i = 0; i < chunksContainer.childCount; i++)
                {
                    Transform child = chunksContainer.GetChild(i);
                    ChunkObject chunk = child.GetComponent<ChunkObject>();
                    if (chunk != null)
                    {
                        if (keepBaked && chunk.isBaked)
                        {
                            continue; // Захищаємо запечений чанк!
                        }
                        toDestroy.Add(child.gameObject);
                    }
                }

                for (int i = toDestroy.Count - 1; i >= 0; i--)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        Undo.DestroyObjectImmediate(toDestroy[i]);
                    }
                    else
                    {
                        Destroy(toDestroy[i]);
                    }
#else
                    Destroy(toDestroy[i]);
#endif
                }
            }

            if (keepBaked)
            {
                spawnedChunks.RemoveAll(c => c == null || !c.isBaked);
            }
            else
            {
                spawnedChunks.Clear();
            }

            activeOpenEdges.Clear();
            unplacedCrackEdges.Clear();
        }

        /// <summary>
        /// Очищає виключно процедурні чанки, залишаючи всі запечені дороги та будівлі недоторканими.
        /// </summary>
        public void ClearOnlyProceduralChunks()
        {
            ClearGeneratedWorld(keepBaked: true);
        }

        /// <summary>
        /// Повне очищення світу: видаляє абсолютно всі чанки з контейнера (включно із запеченими).
        /// </summary>
        public void ClearAllChunks()
        {
            ClearGeneratedWorld(keepBaked: false);
        }

        /// <summary>
        /// Тест на замкненість світу (Leak Test).
        /// Перевіряє кількість незакритих ребер.
        /// </summary>
        public int RunLeakTest()
        {
            int unclosedCount = 0;
            for (int i = 0; i < activeOpenEdges.Count; i++)
            {
                if (activeOpenEdges[i] != null && activeOpenEdges[i].edgeTag != "Terminal" && !activeOpenEdges[i].isConnected)
                {
                    unclosedCount++;
                }
            }
            for (int i = 0; i < unplacedCrackEdges.Count; i++)
            {
                if (unplacedCrackEdges[i] != null && unplacedCrackEdges[i].edgeTag != "Terminal" && !unplacedCrackEdges[i].isConnected)
                {
                    unclosedCount++;
                }
            }

            if (unclosedCount == 0)
            {
                Debug.Log($"<color=green>[WorldGenerator: Leak Test PASSED]</color> Світ повністю замкнений! Чанків: {spawnedChunks.Count}. Відкритих дірок: 0.");
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[WorldGenerator: Leak Test WARNING]</color> Залишилося {unclosedCount} незакритих відкритих ребер/тріщин. Всього чанків: {spawnedChunks.Count}.");
            }

            return unclosedCount;
        }

        #endregion

        #region Логіка розміщення та стикування (Alignment & Overlap)

        private bool TryPlaceChunkAtEdge(WorldEdge worldEdge, List<ChunkObject> prefabsPool)
        {
            // Перемішуємо префаби для варіативності
            List<ChunkObject> shuffled = new List<ChunkObject>(prefabsPool);
            ShuffleList(shuffled);

            for (int p = 0; p < shuffled.Count; p++)
            {
                ChunkObject prefab = shuffled[p];
                if (prefab == null || prefab.localEdges == null) continue;

                // Знаходимо всі ребра префаба, які збігаються за тегом із worldEdge
                List<int> matchingEdgeIndices = new List<int>();
                for (int e = 0; e < prefab.localEdges.Count; e++)
                {
                    if (IsTagCompatible(worldEdge.edgeTag, prefab.localEdges[e].edgeTag))
                    {
                        matchingEdgeIndices.Add(e);
                    }
                }

                if (matchingEdgeIndices.Count == 0) continue;
                ShuffleList(matchingEdgeIndices);

                for (int m = 0; m < matchingEdgeIndices.Count; m++)
                {
                    int prefabEdgeIndex = matchingEdgeIndices[m];
                    Edge candidateEdge = prefab.localEdges[prefabEdgeIndex];

                    // Генеруємо варіанти зміщення (Sliding offsets)
                    List<float> slideOffsets = CalculateSlideOffsets(worldEdge.length, candidateEdge.length);

                    for (int s = 0; s < slideOffsets.Count; s++)
                    {
                        float slideOffset = slideOffsets[s];

                        // Обчислюємо позицію та поворот для стикування
                        AlignChunk(worldEdge, candidateEdge, slideOffset, out Vector3 targetPos, out Quaternion targetRot);

                        // Перевіряємо валідність розміщення (Зона А в межах Bounds та без перетинів з існуючими)
                        if (ValidatePlacement(prefab, targetPos, targetRot))
                        {
                            // Успішно! Спавнимо чанк
                            SpawnChunkInstance(prefab, targetPos, targetRot, worldEdge, prefabEdgeIndex);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Обчислює необхідний поворот та позицію нового чанка для стикування candidateEdge з worldEdge.
        /// </summary>
        public void AlignChunk(WorldEdge worldEdge, Edge candidateEdge, float slideOffset, out Vector3 targetPos, out Quaternion targetRot)
        {
            // 1. Поворот: нормаль нового чанка повинна бути спрямована строго назустріч світовому ребру (-worldNormal)
            Vector3 desiredNormal = -worldEdge.worldNormal;
            float rotAngle = Vector3.SignedAngle(candidateEdge.normal, desiredNormal, Vector3.up);
            targetRot = Quaternion.Euler(0f, rotAngle, 0f);

            // 2. Позиціонування з урахуванням ковзання:
            // При протилежних нормалях контури йдуть антипаралельно:
            // worldStart стикується з candidateEnd, worldEnd стикується з candidateStart.
            Vector3 rotatedEnd = targetRot * candidateEdge.endPoint;
            Vector3 worldSlideBase = worldEdge.worldStart + worldEdge.Direction * slideOffset;

            // P_chunk + rotatedEnd = worldSlideBase => P_chunk = worldSlideBase - rotatedEnd
            targetPos = worldSlideBase - rotatedEnd;
        }

        private List<float> CalculateSlideOffsets(float worldEdgeLen, float candidateEdgeLen)
        {
            List<float> offsets = new List<float>();

            if (!enableSliding || Mathf.Abs(worldEdgeLen - candidateEdgeLen) < 0.1f)
            {
                // Якщо довжини однакові або ковзання вимкнене, центруємо
                float centerOffset = Mathf.Max(0f, (worldEdgeLen - candidateEdgeLen) * 0.5f);
                offsets.Add(centerOffset);
                return offsets;
            }

            if (worldEdgeLen >= candidateEdgeLen)
            {
                float maxOffset = worldEdgeLen - candidateEdgeLen;
                offsets.Add(0f); // На початку
                offsets.Add(maxOffset); // В кінці
                offsets.Add(maxOffset * 0.5f); // По центру

                for (int i = 0; i < slideSamplesCount; i++)
                {
                    float r = (float)rng.NextDouble();
                    offsets.Add(r * maxOffset);
                }
            }
            else
            {
                // Якщо нове ребро довше, зсуваємо його так, щоб світове ребро було всередині
                float maxOffset = candidateEdgeLen - worldEdgeLen;
                offsets.Add(0f);
                offsets.Add(-maxOffset);
                offsets.Add(-maxOffset * 0.5f);

                for (int i = 0; i < slideSamplesCount; i++)
                {
                    float r = (float)rng.NextDouble();
                    offsets.Add(-r * maxOffset);
                }
            }

            return offsets;
        }

        /// <summary>
        /// Перевіряє, чи не перетинається Зона А нового чанка з існуючими чанками та чи входить у SpawnAreaPolygon.
        /// </summary>
        private bool ValidatePlacement(ChunkObject prefab, Vector3 targetPos, Quaternion targetRot)
        {
            if (prefab == null || prefab.polygonVertices == null || prefab.polygonVertices.Count < 3)
                return false;

            // Трансформуємо вершини Зони А префаба у світовий простір без створення GameObject
            List<Vector2> candidatePoly2D = new List<Vector2>(prefab.polygonVertices.Count);
            List<Vector3> candidatePoly3D = new List<Vector3>(prefab.polygonVertices.Count);

            for (int i = 0; i < prefab.polygonVertices.Count; i++)
            {
                Vector3 w = targetPos + (targetRot * prefab.polygonVertices[i]);
                candidatePoly3D.Add(w);
                candidatePoly2D.Add(new Vector2(w.x, w.z));
            }

            // 1. Перевірка обмеження зони спавну (SpawnAreaPolygon)
            if (spawnBounds != null)
            {
                if (!spawnBounds.IsPolygonInside(candidatePoly3D, tolerance: 0.02f))
                {
                    return false; // Виходить за межі карти!
                }
            }

            // 2. Перевірка колізії Зони А з уже розміщеними чанками (включно з уже розміщеними заповнювачами)
            for (int i = 0; i < spawnedChunks.Count; i++)
            {
                ChunkObject existing = spawnedChunks[i];
                if (existing == null) continue;

                List<Vector2> existingPoly2D = existing.GetWorldPolygon2D();
                if (PolygonMath2D.DoPolygonsOverlap(candidatePoly2D, existingPoly2D, contactSkinMargin: 0.03f))
                {
                    return false; // Налазить на інший чанк!
                }
            }

            return true;
        }

        private ChunkObject SpawnChunkInstance(ChunkObject prefab, Vector3 pos, Quaternion rot, WorldEdge connectedWorldEdge, int connectedPrefabEdgeIndex, bool addToActiveOpenEdges = true)
        {
            if (prefab == null || prefab.gameObject == null)
            {
                Debug.LogError("[WorldGenerator] Спроба створити чанк із порожнього префаба (null)!");
                return null;
            }

            if (chunksContainer == null)
            {
                chunksContainer = transform;
            }

            GameObject go = null;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(prefab.gameObject))
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, chunksContainer);
                }

                if (go == null)
                {
                    go = Instantiate(prefab.gameObject, chunksContainer);
                }

                if (go != null)
                {
                    Undo.RegisterCreatedObjectUndo(go, "Generate Chunk");
                }
            }
            else
            {
                go = Instantiate(prefab.gameObject, chunksContainer);
            }
#else
            go = Instantiate(prefab.gameObject, chunksContainer);
#endif

            if (go == null)
            {
                Debug.LogError($"[WorldGenerator] Не вдалося створити екземпляр об'єкта {prefab.name}!");
                return null;
            }

            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one; // Строго 1:1, жодного масштабування/викривлення!

            ChunkObject chunkComp = go.GetComponent<ChunkObject>();
            if (chunkComp == null)
            {
                Debug.LogError($"[WorldGenerator] На створеному об'єкті '{go.name}' відсутній компонент ChunkObject!");
                return null;
            }

            spawnedChunks.Add(chunkComp);

            // 1. Позначаємо з'єднані ребра
            if (connectedWorldEdge != null && connectedWorldEdge.chunk != null)
            {
                if (connectedWorldEdge.localEdgeIndex >= 0 && connectedWorldEdge.localEdgeIndex < connectedWorldEdge.chunk.localEdges.Count)
                {
                    connectedWorldEdge.chunk.localEdges[connectedWorldEdge.localEdgeIndex].isConnected = true;
                }
                activeOpenEdges.Remove(connectedWorldEdge);
            }

            // Безпечна перевірка індексу для підключеного ребра (для стартового чанка передається -1)
            if (connectedPrefabEdgeIndex >= 0 && chunkComp.localEdges != null && connectedPrefabEdgeIndex < chunkComp.localEdges.Count)
            {
                chunkComp.localEdges[connectedPrefabEdgeIndex].isConnected = true;
            }

            // Автоматично закриваємо дотичні ребра з сусідніми або запеченими чанками
            CloseCoincidentOpenEdges(chunkComp);

            // 2. Додаємо всі інші незакриті ребра нового чанка до activeOpenEdges (якщо дозволено)
            if (addToActiveOpenEdges && chunkComp.localEdges != null)
            {
                for (int i = 0; i < chunkComp.localEdges.Count; i++)
                {
                    if (i == connectedPrefabEdgeIndex) continue;

                    Edge edge = chunkComp.localEdges[i];
                    if (edge == null) continue;

                    // Ребра з тегом Terminal не додаються до відкритих, щоб зупинити генерацію
                    if (edge.edgeTag != "Terminal" && !edge.isConnected)
                    {
                        activeOpenEdges.Add(new WorldEdge(chunkComp, i, edge));
                    }
                }
            }

            if (logDebugInfo)
                Debug.Log($"[WorldGenerator] Успішно створено чанк '{prefab.name}' на позиції {pos}. Залишилось відкритих ребер: {activeOpenEdges.Count}");

            return chunkComp;
        }

        private void CloseCoincidentOpenEdges(ChunkObject newChunk)
        {
            if (newChunk == null || newChunk.localEdges == null) return;

            for (int i = 0; i < newChunk.localEdges.Count; i++)
            {
                Edge localEdge = newChunk.localEdges[i];
                if (localEdge == null || localEdge.isConnected) continue;

                Vector3 wStart = newChunk.transform.TransformPoint(localEdge.startPoint);
                Vector3 wEnd = newChunk.transform.TransformPoint(localEdge.endPoint);
                Vector2 s1 = PolygonMath2D.To2D(wStart);
                Vector2 e1 = PolygonMath2D.To2D(wEnd);

                for (int k = activeOpenEdges.Count - 1; k >= 0; k--)
                {
                    WorldEdge openEdge = activeOpenEdges[k];
                    if (openEdge == null || openEdge.chunk == newChunk) continue;

                    Vector2 s2 = PolygonMath2D.To2D(openEdge.worldStart);
                    Vector2 e2 = PolygonMath2D.To2D(openEdge.worldEnd);

                    // У полігонах CCW протилежні дотичні ребра йдуть назустріч одне одному
                    if (Vector2.Distance(s1, e2) < 0.15f && Vector2.Distance(e1, s2) < 0.15f)
                    {
                        localEdge.isConnected = true;
                        if (openEdge.chunk != null && openEdge.localEdgeIndex >= 0 && openEdge.localEdgeIndex < openEdge.chunk.localEdges.Count)
                        {
                            openEdge.chunk.localEdges[openEdge.localEdgeIndex].isConnected = true;
                        }
                        activeOpenEdges.RemoveAt(k);
                        break;
                    }
                }
            }
        }

        private void SpawnStartingChunk()
        {
            if (standardPrefabs == null || standardPrefabs.Count == 0)
            {
                Debug.LogError("[WorldGenerator] Список стандартних префабів порожній! Додайте префаби у standardPrefabs.");
                return;
            }

            // Фільтруємо null елементи
            List<ChunkObject> validPrefabs = new List<ChunkObject>();
            for (int i = 0; i < standardPrefabs.Count; i++)
            {
                if (standardPrefabs[i] != null)
                {
                    validPrefabs.Add(standardPrefabs[i]);
                }
            }

            if (validPrefabs.Count == 0)
            {
                Debug.LogError("[WorldGenerator] Усі елементи у standardPrefabs мають значення null!");
                return;
            }

            int idx = rng.Next(0, validPrefabs.Count);
            ChunkObject startingPrefab = validPrefabs[idx];

            Vector3 spawnPos = transform.position;
            if (spawnBounds != null && spawnBounds.localVertices != null && spawnBounds.localVertices.Count > 0)
            {
                List<Vector3> worldVerts = spawnBounds.GetWorldVertices();
                Vector3 center = Vector3.zero;
                for (int i = 0; i < worldVerts.Count; i++)
                {
                    center += worldVerts[i];
                }
                center /= worldVerts.Count;
                spawnPos = center;
            }

            Quaternion spawnRot = Quaternion.identity;

            // Якщо на початковій позиції вже є запечені об'єкти, шукаємо вільне місце поруч
            if (spawnedChunks.Count > 0 && !ValidatePlacement(startingPrefab, spawnPos, spawnRot))
            {
                bool found = false;
                for (float radius = 10f; radius <= 150f; radius += 10f)
                {
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        float rad = angle * Mathf.Deg2Rad;
                        Vector3 testPos = spawnPos + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
                        if (ValidatePlacement(startingPrefab, testPos, spawnRot))
                        {
                            spawnPos = testPos;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                if (!found)
                {
                    Debug.LogWarning("[WorldGenerator] Не вдалося знайти вільну позицію для стартового чанка навколо запечених частин.");
                    return;
                }
            }

            SpawnChunkInstance(startingPrefab, spawnPos, spawnRot, null, -1);
        }

        private void RegisterExistingChunk(ChunkObject chunk)
        {
            if (chunk == null) return;
            if (!spawnedChunks.Contains(chunk))
            {
                spawnedChunks.Add(chunk);
            }

            if (chunk.isBaked && !chunk.allowGrowthFromEdges)
            {
                return; // Не додаємо ребра до росту, якщо заборонено
            }

            for (int i = 0; i < chunk.localEdges.Count; i++)
            {
                Edge edge = chunk.localEdges[i];
                if (!edge.isConnected && edge.edgeTag != "Terminal")
                {
                    activeOpenEdges.Add(new WorldEdge(chunk, i, edge));
                }
            }
        }

        /// <summary>
        /// Пошук усіх запечених об'єктів (будинків, доріг тощо) на всій сцені.
        /// </summary>
        public List<ChunkObject> FindAllBakedChunksInScene()
        {
            List<ChunkObject> baked = new List<ChunkObject>();
            ChunkObject[] all = FindObjectsByType<ChunkObject>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                ChunkObject c = all[i];
                if (c != null && c.isBaked && c.gameObject.scene.IsValid())
                {
                    baked.Add(c);
                }
            }
            return baked;
        }

        /// <summary>
        /// Відновлює стикування між запеченими об'єктами:
        /// Скидає старі тимчасові з'єднання та знаходить дійсні спільні ребра між запеченими частинами (наприклад, стики доріг).
        /// </summary>
        public void ReconnectBakedChunks(List<ChunkObject> bakedChunks)
        {
            if (bakedChunks == null || bakedChunks.Count == 0) return;

            // 1. Скидаємо прапорці
            for (int i = 0; i < bakedChunks.Count; i++)
            {
                ChunkObject chunk = bakedChunks[i];
                if (chunk == null || chunk.localEdges == null) continue;
                for (int e = 0; e < chunk.localEdges.Count; e++)
                {
                    if (chunk.localEdges[e] != null)
                    {
                        chunk.localEdges[e].isConnected = false;
                    }
                }
            }

            // 2. Знаходимо спільні ребра
            for (int i = 0; i < bakedChunks.Count; i++)
            {
                ChunkObject chunkA = bakedChunks[i];
                if (chunkA == null || chunkA.localEdges == null) continue;

                for (int j = i + 1; j < bakedChunks.Count; j++)
                {
                    ChunkObject chunkB = bakedChunks[j];
                    if (chunkB == null || chunkB.localEdges == null) continue;

                    for (int ea = 0; ea < chunkA.localEdges.Count; ea++)
                    {
                        Edge edgeA = chunkA.localEdges[ea];
                        if (edgeA == null || edgeA.isConnected) continue;

                        Vector3 aStart = chunkA.transform.TransformPoint(edgeA.startPoint);
                        Vector3 aEnd = chunkA.transform.TransformPoint(edgeA.endPoint);
                        Vector2 aS = PolygonMath2D.To2D(aStart);
                        Vector2 aE = PolygonMath2D.To2D(aEnd);

                        for (int eb = 0; eb < chunkB.localEdges.Count; eb++)
                        {
                            Edge edgeB = chunkB.localEdges[eb];
                            if (edgeB == null || edgeB.isConnected) continue;

                            Vector3 bStart = chunkB.transform.TransformPoint(edgeB.startPoint);
                            Vector3 bEnd = chunkB.transform.TransformPoint(edgeB.endPoint);
                            Vector2 bS = PolygonMath2D.To2D(bStart);
                            Vector2 bE = PolygonMath2D.To2D(bEnd);

                            if (Vector2.Distance(aS, bE) < 0.15f && Vector2.Distance(aE, bS) < 0.15f)
                            {
                                edgeA.isConnected = true;
                                edgeB.isConnected = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Синхронізує внутрішній список spawnedChunks з усіма ChunkObject, які наразі існують на сцені.
        /// </summary>
        public void SyncSceneChunks()
        {
            ChunkObject[] all = FindObjectsByType<ChunkObject>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                ChunkObject c = all[i];
                if (c != null && c.gameObject.scene.IsValid() && !spawnedChunks.Contains(c))
                {
                    spawnedChunks.Add(c);
                }
            }
            spawnedChunks.RemoveAll(c => c == null);
        }

        /// <summary>
        /// Збирає всі незакриті відкриті ребра з існуючих чанків, які дозволяють ріст.
        /// </summary>
        public void CollectOpenEdgesFromSpawnedChunks()
        {
            activeOpenEdges.Clear();
            for (int i = 0; i < spawnedChunks.Count; i++)
            {
                ChunkObject chunk = spawnedChunks[i];
                if (chunk == null) continue;
                if (chunk.isBaked && !chunk.allowGrowthFromEdges) continue;
                if (chunk.localEdges == null) continue;

                for (int e = 0; e < chunk.localEdges.Count; e++)
                {
                    Edge edge = chunk.localEdges[e];
                    if (edge != null && !edge.isConnected && edge.edgeTag != "Terminal")
                    {
                        activeOpenEdges.Add(new WorldEdge(chunk, e, edge));
                    }
                }
            }
        }

        #region Управління Запіканням (Baking API)

        /// <summary>
        /// Позначає всі чанки серед переданих об'єктів як запечені (isBaked = true).
        /// </summary>
        public void BakeObjects(GameObject[] objects)
        {
            if (objects == null || objects.Length == 0) return;
            int count = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null) continue;
                ChunkObject[] chunks = objects[i].GetComponentsInChildren<ChunkObject>(true);
                for (int c = 0; c < chunks.Length; c++)
                {
                    chunks[c].isBaked = true;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(chunks[c]);
#endif
                    count++;
                }
            }
            Debug.Log($"[WorldGenerator] Запечено {count} чанків.");
        }

        /// <summary>
        /// Знімає статус запікання з переданих об'єктів (isBaked = false).
        /// </summary>
        public void UnbakeObjects(GameObject[] objects)
        {
            if (objects == null || objects.Length == 0) return;
            int count = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null) continue;
                ChunkObject[] chunks = objects[i].GetComponentsInChildren<ChunkObject>(true);
                for (int c = 0; c < chunks.Length; c++)
                {
                    chunks[c].isBaked = false;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(chunks[c]);
#endif
                    count++;
                }
            }
            Debug.Log($"[WorldGenerator] Розпечено {count} чанків.");
        }

        /// <summary>
        /// Запікає абсолютно всі чанки, які зараз створені на сцені.
        /// </summary>
        public void BakeAllCurrentChunks()
        {
            SyncSceneChunks();
            for (int i = 0; i < spawnedChunks.Count; i++)
            {
                if (spawnedChunks[i] != null)
                {
                    spawnedChunks[i].isBaked = true;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(spawnedChunks[i]);
#endif
                }
            }
            Debug.Log($"[WorldGenerator] Всі поточні чанки ({spawnedChunks.Count} шт.) успішно запечено.");
        }

        /// <summary>
        /// Знімає статус запікання з усіх чанків у сцені.
        /// </summary>
        public void UnbakeAllChunks()
        {
            ChunkObject[] all = FindObjectsByType<ChunkObject>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    all[i].isBaked = false;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(all[i]);
#endif
                }
            }
            Debug.Log($"[WorldGenerator] Знято запікання з усіх {all.Length} чанків у сцені.");
        }

        #endregion

        private bool IsTagCompatible(string tagA, string tagB)
        {
            if (tagA == tagB) return true;
            if (tagA == "Default" || tagB == "Default") return true;
            return false;
        }

        private void ShuffleList<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        #endregion

        #region Другий прохід: Заповнення тріщин квадратами (Second Pass: Crack Filling)

        /// <summary>
        /// Виконує 2-й прохід генерації:
        /// Бере префаби-заповнювачі (наприклад, квадрати 10x10, 5x5) і розставляє їх уздовж та вглиб тріщин,
        /// закриваючи частинами площу тріщини.
        /// Суворо дотримується:
        /// 1. НІЯКИХ накладань 50 шт в одному місці (перевірка SAT ValidatePlacement для кожного заповнювача).
        /// 2. НІЯКОГО масштабування (масштаб 1:1 scale).
        /// </summary>
        public int RunSecondPass()
        {
            List<ChunkObject> fillers = GetFillerPool();
            if (fillers == null || fillers.Count == 0)
            {
                Debug.LogWarning("[WorldGenerator: 2-й прохід] Пули fillerPrefabs та terminalPrefabs порожні! Призначте префаби-заповнювачі.");
                return 0;
            }

            // Сортуємо префаби за розміром (від більших до менших), щоб спочатку закривати великі порожнечі, а потім дрібні
            fillers.Sort((a, b) => b.GetFootprintSize().sqrMagnitude.CompareTo(a.GetFootprintSize().sqrMagnitude));

            // Збираємо всі незакриті ребра тріщин
            List<WorldEdge> crackQueue = new List<WorldEdge>();
            for (int i = 0; i < activeOpenEdges.Count; i++)
            {
                if (activeOpenEdges[i] != null && !activeOpenEdges[i].isConnected)
                {
                    crackQueue.Add(activeOpenEdges[i]);
                }
            }
            for (int i = 0; i < unplacedCrackEdges.Count; i++)
            {
                if (unplacedCrackEdges[i] != null && !unplacedCrackEdges[i].isConnected)
                {
                    crackQueue.Add(unplacedCrackEdges[i]);
                }
            }

            if (crackQueue.Count == 0)
            {
                if (logDebugInfo) Debug.Log("[WorldGenerator: 2-й прохід] Тріщин для заповнення не знайдено, світ вже замкнений.");
                return 0;
            }

            // 1. Попереднє зшивання зустрічних ребер, які вже впритул торкаються одне одного
            StitchCloseEdges(crackQueue, maxDistance: 1.0f);

            int totalFillersPlaced = 0;

            // 2. Ітеративне нарощування заповнювачів шарами вглиб тріщин (до maxFillerPasses кроків)
            for (int layer = 0; layer < maxFillerPasses; layer++)
            {
                List<WorldEdge> nextLayerEdges = new List<WorldEdge>();
                bool placedAnyInLayer = false;

                for (int i = 0; i < crackQueue.Count; i++)
                {
                    WorldEdge edge = crackQueue[i];
                    if (edge == null || edge.isConnected) continue;

                    bool edgeCovered = false;

                    // Пробуємо доступні префаби-заповнювачі
                    for (int p = 0; p < fillers.Count; p++)
                    {
                        ChunkObject prefab = fillers[p];
                        if (prefab == null || prefab.localEdges == null || prefab.localEdges.Count == 0) continue;

                        for (int eIdx = 0; eIdx < prefab.localEdges.Count; eIdx++)
                        {
                            Edge candEdge = prefab.localEdges[eIdx];
                            if (candEdge == null || candEdge.length < 0.1f) continue;

                            // Розраховуємо кроки вздовж ребра для розміщення квадратів стик-у-стик
                            List<float> offsets = GetTilingOffsets(edge.length, candEdge.length);

                            for (int o = 0; o < offsets.Count; o++)
                            {
                                float offset = offsets[o];
                                AlignChunk(edge, candEdge, offset, out Vector3 targetPos, out Quaternion targetRot);

                                // СУВОРЕ ПРАВИЛО: Перевірка колізії SAT проти ВСІХ існуючих чанків і вже поставлених квадратів!
                                // Якщо накладається — ніколи не ставиться!
                                if (ValidatePlacement(prefab, targetPos, targetRot))
                                {
                                    // СУВОРЕ ПРАВИЛО: Масштаб строго 1:1, без розтягування/масштабування
                                    ChunkObject newFiller = SpawnChunkInstance(prefab, targetPos, targetRot, null, -1, addToActiveOpenEdges: false);
                                    if (newFiller != null)
                                    {
                                        newFiller.transform.localScale = Vector3.one;
                                        placedAnyInLayer = true;
                                        edgeCovered = true;
                                        totalFillersPlaced++;

                                        // Підключаємо ребро нового квадрата, яким він приєднався
                                        for (int k = 0; k < newFiller.localEdges.Count; k++)
                                        {
                                            if (k == eIdx)
                                            {
                                                newFiller.localEdges[k].isConnected = true;
                                            }
                                            else if (!newFiller.localEdges[k].isConnected && newFiller.localEdges[k].edgeTag != "Terminal")
                                            {
                                                // Інші відкриті ребра квадрата йдуть у наступний шар для заповнення тріщини вглиб
                                                nextLayerEdges.Add(new WorldEdge(newFiller, k, newFiller.localEdges[k]));
                                            }
                                        }
                                    }
                                }
                            }

                            if (edgeCovered) break;
                        }

                        if (edgeCovered) break;
                    }

                    if (edgeCovered)
                    {
                        MarkEdgeConnected(edge);
                    }
                }

                // Зшиваємо краї, які зімкнулися між собою або торкнулися стінок тріщини
                StitchCloseEdges(nextLayerEdges, maxDistance: 1.2f);

                // Оновлюємо чергу ребер на наступний шар
                crackQueue.Clear();
                for (int k = 0; k < nextLayerEdges.Count; k++)
                {
                    if (nextLayerEdges[k] != null && !nextLayerEdges[k].isConnected)
                    {
                        crackQueue.Add(nextLayerEdges[k]);
                    }
                }

                if (!placedAnyInLayer || crackQueue.Count == 0)
                {
                    break;
                }
            }

            // 3. Фінальне зшивання та закриття швів
            StitchCloseEdges(crackQueue, maxDistance: 1.5f);

            // Закриваємо залишені дрібні щілини (менші за розмір квадрата), щоб не було витоків
            for (int i = 0; i < crackQueue.Count; i++)
            {
                if (crackQueue[i] != null && !crackQueue[i].isConnected)
                {
                    MarkEdgeConnected(crackQueue[i]);
                }
            }

            activeOpenEdges.Clear();
            unplacedCrackEdges.Clear();

            Debug.Log($"<color=cyan>[WorldGenerator: 2-й прохід]</color> Успішно розставлено {totalFillersPlaced} заповнювачів. Жодного накладання та масштабування. Тріщини замкнено!");

            return totalFillersPlaced;
        }

        /// <summary>
        /// Генерує дискретні зміщення вздовж довжини ребра для розміщення заповнювачів стик-у-стик (як плитка).
        /// </summary>
        private List<float> GetTilingOffsets(float worldEdgeLen, float candLen)
        {
            List<float> offsets = new List<float>();
            if (candLen <= 0.001f) return offsets;

            if (worldEdgeLen >= candLen)
            {
                // Крокуємо вздовж ребра з кроком ширини квадрата
                float u = 0f;
                while (u + candLen <= worldEdgeLen + 0.01f)
                {
                    offsets.Add(u);
                    u += candLen;
                }

                // Якщо залишився хвостик, додаємо варіант з вирівнюванням по кінцю
                if (worldEdgeLen - u > 0.5f)
                {
                    float endOffset = worldEdgeLen - candLen;
                    if (!offsets.Contains(endOffset) && endOffset >= 0f)
                    {
                        offsets.Add(endOffset);
                    }
                }
            }
            else
            {
                // Ребро коротше за квадрат: пробуємо по центру, на початку та в кінці
                offsets.Add((worldEdgeLen - candLen) * 0.5f);
                offsets.Add(0f);
                offsets.Add(worldEdgeLen - candLen);
            }

            return offsets;
        }

        /// <summary>
        /// Знаходить зустрічні ребра, що знаходяться близько одне до одного, та позначає їх як з'єднані.
        /// </summary>
        private void StitchCloseEdges(List<WorldEdge> edges, float maxDistance = 1.0f)
        {
            if (edges == null || edges.Count == 0) return;

            // 1. Перевіряємо ребра всередині переданого списку
            for (int i = 0; i < edges.Count; i++)
            {
                WorldEdge e1 = edges[i];
                if (e1 == null || e1.isConnected) continue;

                for (int j = i + 1; j < edges.Count; j++)
                {
                    WorldEdge e2 = edges[j];
                    if (e2 == null || e2.isConnected) continue;
                    if (e1.chunk == e2.chunk) continue;

                    if (AreEdgesOpposingAndClose(e1, e2, maxDistance))
                    {
                        MarkEdgeConnected(e1);
                        MarkEdgeConnected(e2);
                        break;
                    }
                }
            }

            // 2. Перевіряємо ребра проти відкритих ребер усіх розміщених чанків на сцені
            for (int i = 0; i < edges.Count; i++)
            {
                WorldEdge e1 = edges[i];
                if (e1 == null || e1.isConnected) continue;

                for (int c = 0; c < spawnedChunks.Count; c++)
                {
                    ChunkObject chunk = spawnedChunks[c];
                    if (chunk == null || chunk == e1.chunk || chunk.localEdges == null) continue;

                    for (int le = 0; le < chunk.localEdges.Count; le++)
                    {
                        Edge localEdge = chunk.localEdges[le];
                        if (localEdge == null || localEdge.isConnected) continue;

                        WorldEdge e2 = new WorldEdge(chunk, le, localEdge);
                        if (AreEdgesOpposingAndClose(e1, e2, maxDistance))
                        {
                            MarkEdgeConnected(e1);
                            localEdge.isConnected = true;
                            break;
                        }
                    }

                    if (e1.isConnected) break;
                }
            }
        }

        private bool AreEdgesOpposingAndClose(WorldEdge e1, WorldEdge e2, float maxDistance)
        {
            float dot = Vector3.Dot(e1.worldNormal, e2.worldNormal);
            if (dot > -0.6f) return false;

            float dist = Vector3.Distance(e1.Midpoint, e2.Midpoint);
            float allowedDist = maxDistance + Mathf.Abs(e1.length - e2.length) * 0.5f;
            return dist <= allowedDist;
        }

        private void MarkEdgeConnected(WorldEdge edge)
        {
            if (edge == null) return;
            edge.isConnected = true;
            if (edge.chunk != null && edge.localEdgeIndex >= 0 && edge.chunk.localEdges != null && edge.localEdgeIndex < edge.chunk.localEdges.Count)
            {
                edge.chunk.localEdges[edge.localEdgeIndex].isConnected = true;
            }
        }

        private List<ChunkObject> GetFillerPool()
        {
            List<ChunkObject> pool = new List<ChunkObject>();
            if (fillerPrefabs != null)
            {
                for (int i = 0; i < fillerPrefabs.Count; i++)
                {
                    if (fillerPrefabs[i] != null) pool.Add(fillerPrefabs[i]);
                }
            }

            // Якщо fillerPrefabs порожній, використовуємо terminalPrefabs як заглушки
            if (pool.Count == 0 && terminalPrefabs != null)
            {
                for (int i = 0; i < terminalPrefabs.Count; i++)
                {
                    if (terminalPrefabs[i] != null) pool.Add(terminalPrefabs[i]);
                }
            }

            return pool;
        }

        #endregion

        #region Візуалізація Gizmos

        private void OnDrawGizmos()
        {
            if (!drawOpenEdgesGizmos || activeOpenEdges == null) return;

            // Малюємо всі поточні відкриті ребра зеленими лініями зі стрілками
            Gizmos.color = Color.green;
            for (int i = 0; i < activeOpenEdges.Count; i++)
            {
                WorldEdge e = activeOpenEdges[i];
                if (e == null) continue;

                Gizmos.DrawLine(e.worldStart, e.worldEnd);
                Gizmos.DrawRay(e.Midpoint, e.worldNormal * 2f);
            }
        }

        #endregion
    }
}
