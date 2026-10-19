using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace TerrainGenerator
{
    /// <summary>
    /// Допоміжний інструмент для швидкого створення тестових префабів чанків та налаштування сцени в 1 клік.
    /// </summary>
    public static class SampleChunkFactory
    {
        private const string PrefabsPath = "Assets/Prefabs/TerrainChunks";

        [MenuItem("Tools/Procedural World/Create Test Chunks and Setup Scene")]
        public static void CreateTestSetup()
        {
            if (!Directory.Exists(PrefabsPath))
            {
                Directory.CreateDirectory(PrefabsPath);
                AssetDatabase.Refresh();
            }

            // 1. Створюємо стандартний тестовий чанк 20x20
            ChunkObject straightPrefab = CreateOrUpdateChunkPrefab("Chunk_Square_20x20", 20f, 20f, isTerminal: false, "Default");

            // 2. Створюємо прямокутний чанк 30x20
            ChunkObject rectPrefab = CreateOrUpdateChunkPrefab("Chunk_Rect_30x20", 30f, 20f, isTerminal: false, "Default");

            // 3. Створюємо чанк-заглушку (Terminal)
            ChunkObject terminalPrefab = CreateOrUpdateChunkPrefab("Chunk_Terminal_Rocks", 20f, 15f, isTerminal: true, "Default");

            // 4. Створюємо генератор та обмежувач на поточній сцені
            SetupSceneGenerator(straightPrefab, rectPrefab, terminalPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Генератор готовий!",
                "Тестові префаби створено у папці 'Assets/Prefabs/TerrainChunks'.\n" +
                "На сцені створено об'єкт 'WorldGenerator' з прив'язаним обмежувачем 'SpawnAreaPolygon'.\n\n" +
                "Ви можете виділити WorldGenerator в ієрархії та натиснути кнопку 'Згенерувати світ' в інспекторі!",
                "Чудово");
        }

        private static ChunkObject CreateOrUpdateChunkPrefab(string name, float width, float depth, bool isTerminal, string baseTag)
        {
            string prefabPath = $"{PrefabsPath}/{name}.prefab";

            GameObject root = new GameObject(name);
            ChunkObject chunk = root.AddComponent<ChunkObject>();
            chunk.isTerminalChunk = isTerminal;
            chunk.isFillerChunk = isTerminal;

            // Зона А
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            chunk.polygonVertices = new List<Vector3>
            {
                new Vector3(-hw, 0f, -hd),
                new Vector3(hw, 0f, -hd),
                new Vector3(hw, 0f, hd),
                new Vector3(-hw, 0f, hd)
            };
            chunk.RebuildEdgesFromPolygon(preserveTags: false);

            if (isTerminal)
            {
                // Заглушка: перше ребро (вхід) залишаємо базовим, решту позначаємо як Terminal
                for (int i = 1; i < chunk.localEdges.Count; i++)
                {
                    chunk.localEdges[i].edgeTag = "Terminal";
                }
            }

            // Додаємо візуальний меш (Зона А + Зона Б / Спідниця зі скошеними краями)
            GameObject visual = CreateBeveledMesh(name + "_Mesh", width, depth, skirtExtra: 2f, skirtSlopeY: -1.5f, isTerminal: isTerminal);
            visual.transform.SetParent(root.transform, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            return prefab.GetComponent<ChunkObject>();
        }

        private static GameObject CreateBeveledMesh(string name, float w, float d, float skirtExtra, float skirtSlopeY, bool isTerminal)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            // Базовий матеріал (URP / Standard)
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = isTerminal ? new Color(0.35f, 0.3f, 0.3f) : new Color(0.35f, 0.55f, 0.35f);
            mr.sharedMaterial = mat;

            Mesh mesh = new Mesh();
            mesh.name = name;

            float hw = w * 0.5f;
            float hd = d * 0.5f;

            // Верхня поверхня (Зона А)
            float topY = isTerminal ? 4f : 0f;
            Vector3[] top = new Vector3[]
            {
                new Vector3(-hw, topY, -hd),
                new Vector3(hw, topY, -hd),
                new Vector3(hw, topY, hd),
                new Vector3(-hw, topY, hd)
            };

            // Нижня зовнішня спідниця (Зона Б, скошена вниз)
            float sw = hw + skirtExtra;
            float sd = hd + skirtExtra;
            float skirtY = topY + skirtSlopeY;
            Vector3[] skirt = new Vector3[]
            {
                new Vector3(-sw, skirtY, -sd),
                new Vector3(sw, skirtY, -sd),
                new Vector3(sw, skirtY, sd),
                new Vector3(-sw, skirtY, sd)
            };

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            // 1. Верхня границя (квадрат Зони А)
            int vStart = vertices.Count;
            vertices.AddRange(top);
            triangles.Add(vStart + 0); triangles.Add(vStart + 2); triangles.Add(vStart + 1);
            triangles.Add(vStart + 0); triangles.Add(vStart + 3); triangles.Add(vStart + 2);

            // 2. 4 скошені спідниці по боках
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                int baseIdx = vertices.Count;

                vertices.Add(top[i]);
                vertices.Add(top[next]);
                vertices.Add(skirt[next]);
                vertices.Add(skirt[i]);

                triangles.Add(baseIdx + 0); triangles.Add(baseIdx + 1); triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 0); triangles.Add(baseIdx + 2); triangles.Add(baseIdx + 3);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;
            return go;
        }

        private static void SetupSceneGenerator(ChunkObject straight, ChunkObject rect, ChunkObject terminal)
        {
            WorldGenerator existingGen = Object.FindAnyObjectByType<WorldGenerator>();
            GameObject genGo = existingGen != null ? existingGen.gameObject : new GameObject("WorldGenerator");

            WorldGenerator gen = genGo.GetComponent<WorldGenerator>();
            if (gen == null) gen = genGo.AddComponent<WorldGenerator>();

            // Контейнер чанків
            Transform container = genGo.transform.Find("ChunksContainer");
            if (container == null)
            {
                GameObject contGo = new GameObject("ChunksContainer");
                contGo.transform.SetParent(genGo.transform, false);
                container = contGo.transform;
            }
            gen.chunksContainer = container;

            // Обмежувач спавну
            SpawnAreaPolygon bounds = genGo.GetComponentInChildren<SpawnAreaPolygon>();
            if (bounds == null)
            {
                GameObject boundsGo = new GameObject("SpawnAreaPolygon");
                boundsGo.transform.SetParent(genGo.transform, false);
                bounds = boundsGo.AddComponent<SpawnAreaPolygon>();
                bounds.SetAsRectangle(120f, 120f);
            }
            gen.spawnBounds = bounds;

            // Префаби
            gen.standardPrefabs = new List<ChunkObject> { straight, rect };
            gen.terminalPrefabs = new List<ChunkObject> { terminal };
            gen.fillerPrefabs = new List<ChunkObject> { terminal };
            gen.maxChunks = 35;
            gen.enableSliding = true;

            Selection.activeGameObject = genGo;
        }
    }
}
