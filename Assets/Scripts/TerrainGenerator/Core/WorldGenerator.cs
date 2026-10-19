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
    /// Реалізує алгоритм кристалічного росту з ковзним стикуванням (Sliding Alignment),
    /// перевірку перетинів (SAT/Polygon collision), підтримку меж SpawnAreaPolygon та заглушок.
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

        [Tooltip("Максимальна кількість чанків для генерації")]
        [Range(1, 10000)]
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

        [Tooltip("Префаби-заповнювачі для 2-го проходу (заповнення тріщин та щілин)")]
        public List<ChunkObject> fillerPrefabs = new List<ChunkObject>();

        [Header("Другий прохід (Заповнення тріщин)")]
        [Tooltip("Чи запускати 2-й прохід заповнення тріщин автоматично після завершення основної генерації")]
        public bool runSecondPassOnFinish = true;

        [Tooltip("Чи масштабувати префаби-заповнювачі під розмір квадратів сітки для строгого замкнення без щілин")]
        public bool scaleFillersToCell = true;

        [Tooltip("Максимальна дальність пошуку протилежної стінки тріщини")]
        public float maxCrackSearchDepth = 40f;

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
        [HideInInspector] public List<ChunkObject> spawnedChunks = new List<ChunkObject>();
        [HideInInspector] public List<WorldEdge> activeOpenEdges = new List<WorldEdge>();
        [HideInInspector] public List<WorldEdge> unplacedCrackEdges = new List<WorldEdge>();

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
        /// Повністю генерує світ: ініціалізує початковий стан і виконує кроки росту до ліміту.
        /// </summary>
        public void GenerateWorld()
        {
            InitializeWorld();

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

            // 2-й прохід: Заповнення тріщин та щілин сіткою квадратів/комірок
            if (runSecondPassOnFinish)
            {
                RunSecondPass();
            }

            RunLeakTest();
        }

        /// <summary>
        /// Ініціалізує генератор: сканує сцену на наявність ручних чанків або створює стартовий чанк.
        /// </summary>
        public void InitializeWorld()
        {
            if (useRandomSeed)
            {
                seed = UnityEngine.Random.Range(1000, 999999);
            }
            rng = new System.Random(seed);

            ClearGeneratedWorld();

            if (chunksContainer == null)
            {
                chunksContainer = transform;
            }

            spawnedChunks.Clear();
            activeOpenEdges.Clear();
            unplacedCrackEdges.Clear();

            // 1. Шукаємо вже розставлені вручну чанки (Гібридний режим)
            ChunkObject[] existing = chunksContainer.GetComponentsInChildren<ChunkObject>();
            if (existing != null && existing.Length > 0)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    RegisterExistingChunk(existing[i]);
                }
                if (logDebugInfo)
                    Debug.Log($"[WorldGenerator] Знайдено {existing.Length} ручних чанків. Відкритих ребер: {activeOpenEdges.Count}");
            }
            else
            {
                // 2. Якщо сцена порожня, спавнимо стартовий чанк
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

            // Обираємо ребро зі списку відкритих (наприклад, випадкове або перше)
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

            // Формуємо список префабів: якщо близько до межі, шукаємо спочатку заглушки
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
        /// Очищає згенерований світ.
        /// </summary>
        public void ClearGeneratedWorld()
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

            spawnedChunks.Clear();
            activeOpenEdges.Clear();
            unplacedCrackEdges.Clear();
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

            // 2. Перевірка колізії Зони А з уже розміщеними чанками
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

        private void SpawnChunkInstance(ChunkObject prefab, Vector3 pos, Quaternion rot, WorldEdge connectedWorldEdge, int connectedPrefabEdgeIndex)
        {
            if (prefab == null || prefab.gameObject == null)
            {
                Debug.LogError("[WorldGenerator] Спроба створити чанк із порожнього префаба (null)!");
                return;
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
                return;
            }

            go.transform.position = pos;
            go.transform.rotation = rot;

            ChunkObject chunkComp = go.GetComponent<ChunkObject>();
            if (chunkComp == null)
            {
                Debug.LogError($"[WorldGenerator] На створеному об'єкті '{go.name}' відсутній компонент ChunkObject!");
                return;
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

            // 2. Додаємо всі інші незакриті ребра нового чанка до activeOpenEdges
            if (chunkComp.localEdges != null)
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

            SpawnChunkInstance(startingPrefab, spawnPos, spawnRot, null, -1);
        }

        private void RegisterExistingChunk(ChunkObject chunk)
        {
            if (chunk == null) return;
            spawnedChunks.Add(chunk);

            for (int i = 0; i < chunk.localEdges.Count; i++)
            {
                Edge edge = chunk.localEdges[i];
                if (!edge.isConnected && edge.edgeTag != "Terminal")
                {
                    activeOpenEdges.Add(new WorldEdge(chunk, i, edge));
                }
            }
        }

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

        #region Другий прохід: Заповнення тріщин (Second Pass: Crack Filling)

        /// <summary>
        /// Виконує другий прохід генерації: знаходить усі незаповнені тріщини та щілини між мешами
        /// і заповнює їх сіткою квадратів/комірок префабів-заповнювачів для строгого замикання локації.
        /// </summary>
        public int RunSecondPass()
        {
            List<ChunkObject> pool = GetFillerPool();
            if (pool == null || pool.Count == 0)
            {
                Debug.LogWarning("[WorldGenerator: 2-й прохід] Пули fillerPrefabs та terminalPrefabs порожні! Будь ласка, призначте префаби-заповнювачі.");
                return 0;
            }

            // Збираємо всі незакриті ребра (тріщини)
            List<WorldEdge> allCracks = new List<WorldEdge>();
            for (int i = 0; i < activeOpenEdges.Count; i++)
            {
                if (activeOpenEdges[i] != null && !activeOpenEdges[i].isConnected)
                {
                    allCracks.Add(activeOpenEdges[i]);
                }
            }
            for (int i = 0; i < unplacedCrackEdges.Count; i++)
            {
                if (unplacedCrackEdges[i] != null && !unplacedCrackEdges[i].isConnected)
                {
                    allCracks.Add(unplacedCrackEdges[i]);
                }
            }

            if (allCracks.Count == 0)
            {
                if (logDebugInfo) Debug.Log("[WorldGenerator: 2-й прохід] Тріщин для заповнення не знайдено, світ вже замкнений.");
                return 0;
            }

            int filledCount = 0;

            for (int i = 0; i < allCracks.Count; i++)
            {
                WorldEdge edge = allCracks[i];
                if (edge == null || edge.isConnected) continue;

                // 1. Обираємо префаб-заповнювач
                int pIdx = (rng != null) ? rng.Next(0, pool.Count) : UnityEngine.Random.Range(0, pool.Count);
                ChunkObject fillerPrefab = pool[pIdx];
                if (fillerPrefab == null) continue;

                // 2. Визначаємо розміри комірки на основі компонента ChunkObject
                Vector2 fillerSize = fillerPrefab.GetFootprintSize();
                float fw = Mathf.Max(1f, fillerSize.x);
                float fd = Mathf.Max(1f, fillerSize.y);

                // 3. Знаходимо глибину тріщини попереду та зустрічне ребро
                float crackDepth = CalculateCrackDepth(edge, maxCrackSearchDepth, out WorldEdge facingEdge);
                if (crackDepth <= 0.05f || crackDepth >= maxCrackSearchDepth)
                {
                    crackDepth = fd;
                }

                // 4. Розбиваємо тріщину на площу з квадратів/комірок
                int cols = Mathf.Max(1, Mathf.RoundToInt(edge.length / fw));
                float cellW = edge.length / cols;

                int rows = Mathf.Max(1, Mathf.RoundToInt(crackDepth / fd));
                float cellD = crackDepth / rows;

                Quaternion baseRot = Quaternion.LookRotation(edge.worldNormal, Vector3.up);
                Vector3 cellScale = scaleFillersToCell ? new Vector3(cellW / fw, 1f, cellD / fd) : Vector3.one;

                // 5. Заповнюємо площу комірками, що прилягають ребро до ребра
                for (int c = 0; c < cols; c++)
                {
                    float u = (c + 0.5f) * cellW;

                    for (int r = 0; r < rows; r++)
                    {
                        float v = (r + 0.5f) * cellD;

                        Vector3 spawnPos = edge.worldStart + (edge.Direction * u) + (edge.worldNormal * v);

                        SpawnFillerInstance(fillerPrefab, spawnPos, baseRot, cellScale);
                    }
                }

                // 6. Позначаємо ребра як з'єднані (строго закриті)
                if (edge.chunk != null && edge.localEdgeIndex >= 0 && edge.chunk.localEdges != null && edge.localEdgeIndex < edge.chunk.localEdges.Count)
                {
                    edge.chunk.localEdges[edge.localEdgeIndex].isConnected = true;
                }
                edge.isConnected = true;

                if (facingEdge != null)
                {
                    if (facingEdge.chunk != null && facingEdge.localEdgeIndex >= 0 && facingEdge.chunk.localEdges != null && facingEdge.localEdgeIndex < facingEdge.chunk.localEdges.Count)
                    {
                        facingEdge.chunk.localEdges[facingEdge.localEdgeIndex].isConnected = true;
                    }
                    facingEdge.isConnected = true;
                }

                filledCount++;
            }

            activeOpenEdges.Clear();
            unplacedCrackEdges.Clear();

            if (logDebugInfo)
            {
                Debug.Log($"<color=cyan>[WorldGenerator: 2-й прохід]</color> Заповнено {filledCount} тріщин. Усі щілини замкнено.");
            }

            return filledCount;
        }

        private float CalculateCrackDepth(WorldEdge edge, float maxDepth, out WorldEdge facingEdge)
        {
            facingEdge = null;
            float closestDist = maxDepth;

            Vector2 origin2D = PolygonMath2D.To2D(edge.Midpoint);
            Vector2 dir2D = PolygonMath2D.To2D(edge.worldNormal).normalized;

            // Шукаємо перешкоди (інші чанки попереду)
            for (int i = 0; i < spawnedChunks.Count; i++)
            {
                ChunkObject other = spawnedChunks[i];
                if (other == null || other == edge.chunk) continue;

                float hitDist = PolygonMath2D.RaycastToPolygon(origin2D, dir2D, other.GetWorldPolygon2D());
                if (hitDist > 0.05f && hitDist < closestDist)
                {
                    closestDist = hitDist;
                }
            }

            // Шукаємо перешкоди з межею SpawnAreaPolygon
            if (spawnBounds != null)
            {
                float boundDist = spawnBounds.GetDistanceToBoundary(edge.Midpoint, edge.worldNormal);
                if (boundDist > 0.05f && boundDist < closestDist)
                {
                    closestDist = boundDist;
                }
            }

            // Шукаємо зустрічне ребро на іншому боці тріщини
            Vector3 targetPoint = edge.Midpoint + edge.worldNormal * closestDist;
            for (int i = 0; i < unplacedCrackEdges.Count; i++)
            {
                WorldEdge candidate = unplacedCrackEdges[i];
                if (candidate == edge || candidate.isConnected) continue;

                if (Vector3.Dot(edge.worldNormal, candidate.worldNormal) < -0.5f)
                {
                    float distToTarget = Vector3.Distance(candidate.Midpoint, targetPoint);
                    if (distToTarget < edge.length + 1f)
                    {
                        facingEdge = candidate;
                        break;
                    }
                }
            }

            return closestDist;
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

        private ChunkObject SpawnFillerInstance(ChunkObject prefab, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            if (prefab == null || prefab.gameObject == null) return null;
            if (chunksContainer == null) chunksContainer = transform;

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
                    Undo.RegisterCreatedObjectUndo(go, "Spawn Second Pass Filler");
                }
            }
            else
            {
                go = Instantiate(prefab.gameObject, chunksContainer);
            }
#else
            go = Instantiate(prefab.gameObject, chunksContainer);
#endif

            if (go == null) return null;

            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;

            ChunkObject chunkComp = go.GetComponent<ChunkObject>();
            if (chunkComp != null)
            {
                if (chunkComp.localEdges != null)
                {
                    for (int i = 0; i < chunkComp.localEdges.Count; i++)
                    {
                        chunkComp.localEdges[i].isConnected = true;
                    }
                }
                spawnedChunks.Add(chunkComp);
            }

            return chunkComp;
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
