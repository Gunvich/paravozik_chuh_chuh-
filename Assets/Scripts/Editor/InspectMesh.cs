using UnityEditor;
using UnityEngine;

public static class InspectMesh
{
    [MenuItem("Tools/Inspect Terrain Mesh 1")]
    public static void Inspect()
    {
        string path = "Assets/Mesh/Terrain/1.fbx";
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null)
        {
            Debug.LogError("Не знайдено " + path);
            return;
        }

        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("Не знайдено MeshFilter у " + path);
            return;
        }

        Mesh mesh = mf.sharedMesh;
        Vector3[] v = mesh.vertices;
        int[] t = mesh.triangles;
        Bounds b = mesh.bounds;

        // Пошук граничних ребер (ребро, яке належить тільки 1 трикутнику)
        var edgeCount = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < t.Length; i += 3)
        {
            AddEdge(edgeCount, t[i], t[i + 1]);
            AddEdge(edgeCount, t[i + 1], t[i + 2]);
            AddEdge(edgeCount, t[i + 2], t[i]);
        }

        int boundaryEdges = 0;
        foreach (var pair in edgeCount)
        {
            if (pair.Value == 1) boundaryEdges++;
        }

        Debug.Log($"[InspectMesh] {path}: Вершин={v.Length}, Трикутників={t.Length / 3}, Межі={b.size}, Граничних (відкритих) ребер={boundaryEdges} ({(boundaryEdges > 0 ? "ВІДКРИТИЙ ЛИСТ/SHEET" : "ЗАМКНЕНЕ ТІЛО/CLOSED SOLID")})");
    }

    private static void AddEdge(System.Collections.Generic.Dictionary<string, int> dict, int a, int b)
    {
        int min = Mathf.Min(a, b);
        int max = Mathf.Max(a, b);
        string key = $"{min}_{max}";
        dict.TryGetValue(key, out int count);
        dict[key] = count + 1;
    }
}
