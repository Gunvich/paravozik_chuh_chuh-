using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to automatically generate ChunkEdge components for polygonal chunks
/// based on their Zone3D Core polygon points, or convert existing ChunkConnectors.
/// </summary>
public static class ChunkEdgeHelper
{
    private const string MenuGenerateFromCore = "Tools/Terrain Generator/Generate ChunkEdges From Zone3D Core";

    [MenuItem(MenuGenerateFromCore)]
    public static void GenerateEdgesFromCore()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("ChunkEdge Generator", "Будь ласка, виберіть GameObject або префаб із компонентом ChunkData у Project або Hierarchy.", "OK");
            return;
        }

        ChunkData chunk = selected.GetComponent<ChunkData>();
        if (chunk == null)
        {
            chunk = selected.GetComponentInChildren<ChunkData>();
        }

        if (chunk == null)
        {
            EditorUtility.DisplayDialog("ChunkEdge Generator", "На вибраному об'єкті не знайдено компонента ChunkData.", "OK");
            return;
        }

        if (chunk.zoneA_Core == null || chunk.zoneA_Core.localPoints == null || chunk.zoneA_Core.localPoints.Length < 3)
        {
            EditorUtility.DisplayDialog("ChunkEdge Generator", "ChunkData не має коректно налаштованого zoneA_Core з хоча б 3 вершинами.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(chunk.gameObject, "Generate ChunkEdges");

        // Remove existing ChunkEdge child objects if any
        Transform edgesContainer = chunk.transform.Find("GeneratedEdges");
        if (edgesContainer != null)
        {
            Undo.DestroyObjectImmediate(edgesContainer.gameObject);
        }

        GameObject containerGO = new GameObject("GeneratedEdges");
        Undo.RegisterCreatedObjectUndo(containerGO, "Create GeneratedEdges Container");
        containerGO.transform.SetParent(chunk.transform, false);

        Vector2[] pts = chunk.zoneA_Core.localPoints;
        int count = pts.Length;
        var createdEdges = new System.Collections.Generic.List<ChunkEdge>();

        // Estimate slope margin from zoneB_Aura if available
        float margin = 1.5f;
        if (chunk.zoneB_Aura != null && chunk.zoneB_Aura.localPoints != null && chunk.zoneB_Aura.localPoints.Length > 0)
        {
            Rect coreB = chunk.zoneA_Core.GetWorldBounds();
            Rect auraB = chunk.zoneB_Aura.GetWorldBounds();
            margin = Mathf.Max(0.5f, ((auraB.width - coreB.width) + (auraB.height - coreB.height)) * 0.25f);
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 pStart = pts[i];
            Vector2 pEnd = pts[(i + 1) % count];

            GameObject edgeGO = new GameObject($"Edge_{i}_{pStart:F1}_to_{pEnd:F1}");
            Undo.RegisterCreatedObjectUndo(edgeGO, "Create ChunkEdge");
            edgeGO.transform.SetParent(containerGO.transform, false);

            ChunkEdge edge = edgeGO.AddComponent<ChunkEdge>();
            edge.localStart = pStart;
            edge.localEnd = pEnd;
            edge.slopeMargin = margin;
            edge.maxJitterFactor = 0.5f;
            edge.edgeType = ConnectorType.Edge;

            createdEdges.Add(edge);
        }

        chunk.edges = createdEdges.ToArray();
        EditorUtility.SetDirty(chunk);

        EditorUtility.DisplayDialog("ChunkEdge Generator",
            $"Успішно створено {createdEdges.Count} ребер (ChunkEdge) на основі багатокутника Core з шириною спуску {margin:F2}м!",
            "OK");
    }

    [MenuItem(MenuGenerateFromCore, true)]
    public static bool ValidateGenerateEdgesFromCore()
    {
        return Selection.activeGameObject != null && !Application.isPlaying;
    }

    private const string MenuEnableReadWrite = "Tools/Terrain Generator/Enable Read-Write On All Terrain FBX";

    [MenuItem(MenuEnableReadWrite)]
    public static void EnableReadWriteOnAllTerrainFBX()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Mesh/Terrain" });
        int changed = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                changed++;
                Debug.Log($"[Terrain] Увімкнено Read/Write для: {path}");
            }
        }
        EditorUtility.DisplayDialog("Read/Write Helper", $"Оброблено {guids.Length} моделей у 'Assets/Mesh/Terrain'. Увімкнено Read/Write для {changed} моделей.", "OK");
    }
}
