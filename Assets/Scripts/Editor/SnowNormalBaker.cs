using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class SnowNormalBaker : EditorWindow
{
    private GameObject snowRoot;
    private string snowTag = "Untagged";
    private float gridResolution = 0.25f; // Розмір клітинки проксі-сітки в метрах
    private int blurPasses = 3;           // Кількість проходів згладжування стику
    private string saveFolder = "Assets/BakedSnowMeshes";

    [MenuItem("Tools/Snow/Bake Smooth Proxy Normals")]
    public static void ShowWindow()
    {
        GetWindow<SnowNormalBaker>("Snow Normal Baker");
    }

    private void OnGUI()
    {
        snowRoot = (GameObject)EditorGUILayout.ObjectField("Snow Parent Object", snowRoot, typeof(GameObject), true);
        snowTag = EditorGUILayout.TagField("Snow Tag", snowTag);
        gridResolution = EditorGUILayout.Slider("Proxy Grid Step (m)", gridResolution, 0.05f, 1.0f);
        blurPasses = EditorGUILayout.IntSlider("Smooth Iterations", blurPasses, 1, 10);
        saveFolder = EditorGUILayout.TextField("Output Folder", saveFolder);

        GUI.enabled = snowRoot != null;
        if (GUILayout.Button("Bake Normals to Separate Meshes", GUILayout.Height(32)))
        {
            BakeNormals();
        }
        GUI.enabled = true;
    }

    private void BakeNormals()
    {
        if (string.IsNullOrEmpty(snowTag) || System.Array.IndexOf(InternalEditorUtility.tags, snowTag) < 0)
        {
            EditorUtility.DisplayDialog("Snow Normal Baker", $"Тег '{snowTag}' не знайдено в Tag Manager проєкту.", "OK");
            return;
        }

        // Відбираємо лише об'єкти з потрібним тегом снігу
        MeshFilter[] allFilters = snowRoot.GetComponentsInChildren<MeshFilter>();
        List<MeshFilter> filters = new List<MeshFilter>();

        foreach (var mf in allFilters)
        {
            if (mf.sharedMesh != null && mf.gameObject.CompareTag(snowTag))
            {
                filters.Add(mf);
            }
        }

        if (filters.Count == 0)
        {
            EditorUtility.DisplayDialog("Snow Normal Baker", $"Усередині '{snowRoot.name}' не знайдено мешів із тегом '{snowTag}'.", "OK");
            return;
        }

        // 1. Визначаємо загальні межі локації тільки по об'єктах снігу
        Bounds totalBounds = new Bounds();
        bool hasBounds = false;
        foreach (var mf in filters)
        {
            var renderer = mf.GetComponent<Renderer>();
            if (renderer == null) continue;

            if (!hasBounds)
            {
                totalBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                totalBounds.Encapsulate(renderer.bounds);
            }
        }

        totalBounds.Expand(gridResolution * 4f);

        int sizeX = Mathf.CeilToInt(totalBounds.size.x / gridResolution) + 1;
        int sizeZ = Mathf.CeilToInt(totalBounds.size.z / gridResolution) + 1;
        float[,] heightMap = new float[sizeX, sizeZ];

        for (int x = 0; x < sizeX; x++)
            for (int z = 0; z < sizeZ; z++)
                heightMap[x, z] = float.MinValue;

        // 2. Растеризуємо трикутники снігу в єдину карту висот
        foreach (var mf in filters)
        {
            Mesh m = mf.sharedMesh;
            Vector3[] verts = m.vertices;
            int[] tris = m.triangles;
            Matrix4x4 l2w = mf.transform.localToWorldMatrix;

            Vector3[] worldVerts = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                worldVerts[i] = l2w.MultiplyPoint3x4(verts[i]);

            for (int i = 0; i < tris.Length; i += 3)
            {
                RasterizeTriangle(
                    worldVerts[tris[i]],
                    worldVerts[tris[i + 1]],
                    worldVerts[tris[i + 2]],
                    totalBounds.min, gridResolution, sizeX, sizeZ, heightMap
                );
            }
        }

        for (int x = 0; x < sizeX; x++)
            for (int z = 0; z < sizeZ; z++)
                if (heightMap[x, z] == float.MinValue)
                    heightMap[x, z] = totalBounds.min.y;

        // 3. Згладжуємо проксі-поверхню на місцях стиків
        for (int pass = 0; pass < blurPasses; pass++)
        {
            float[,] smoothed = new float[sizeX, sizeZ];
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int nx = Mathf.Clamp(x + dx, 0, sizeX - 1);
                            int nz = Mathf.Clamp(z + dz, 0, sizeZ - 1);
                            sum += heightMap[nx, nz];
                            count++;
                        }
                    }
                    smoothed[x, z] = sum / count;
                }
            }
            heightMap = smoothed;
        }

        // 4. Переносимо згладжені нормалі в кожен сніговий меш і зберігаємо на диск
        if (!AssetDatabase.IsValidFolder(saveFolder))
        {
            Directory.CreateDirectory(saveFolder);
            AssetDatabase.Refresh();
        }

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int idx = 0; idx < filters.Count; idx++)
            {
                MeshFilter mf = filters[idx];
                EditorUtility.DisplayProgressBar("Baking Snow Normals", mf.name, (float)idx / filters.Count);

                Mesh bakedMesh = Instantiate(mf.sharedMesh);
                bakedMesh.name = $"{mf.sharedMesh.name}_Baked_{idx}";

                Vector3[] verts = bakedMesh.vertices;
                Vector3[] newNormals = new Vector3[verts.Length];
                Matrix4x4 l2w = mf.transform.localToWorldMatrix;
                Matrix4x4 w2l = mf.transform.worldToLocalMatrix;

                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 worldPos = l2w.MultiplyPoint3x4(verts[v]);
                    Vector3 worldNormal = SampleSmoothNormal(worldPos, totalBounds.min, gridResolution, sizeX, sizeZ, heightMap);
                    newNormals[v] = w2l.MultiplyVector(worldNormal).normalized;
                }

                bakedMesh.normals = newNormals;
                bakedMesh.RecalculateTangents();

                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{saveFolder}/{bakedMesh.name}.asset");
                AssetDatabase.CreateAsset(bakedMesh, assetPath);
                mf.sharedMesh = bakedMesh;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();
        }
    }

    private static void RasterizeTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 minBounds, float step, int sizeX, int sizeZ, float[,] map)
    {
        float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
        float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z));
        float maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));

        int startX = Mathf.Clamp(Mathf.FloorToInt((minX - minBounds.x) / step), 0, sizeX - 1);
        int endX = Mathf.Clamp(Mathf.CeilToInt((maxX - minBounds.x) / step), 0, sizeX - 1);
        int startZ = Mathf.Clamp(Mathf.FloorToInt((minZ - minBounds.z) / step), 0, sizeZ - 1);
        int endZ = Mathf.Clamp(Mathf.CeilToInt((maxZ - minBounds.z) / step), 0, sizeZ - 1);

        float denom = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
        if (Mathf.Abs(denom) < 1e-6f) return;

        for (int gx = startX; gx <= endX; gx++)
        {
            float px = minBounds.x + gx * step;
            for (int gz = startZ; gz <= endZ; gz++)
            {
                float pz = minBounds.z + gz * step;
                float w1 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / denom;
                float w2 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / denom;
                float w3 = 1f - w1 - w2;

                if (w1 >= -0.01f && w2 >= -0.01f && w3 >= -0.01f)
                {
                    float y = w1 * a.y + w2 * b.y + w3 * c.y;
                    if (y > map[gx, gz])
                        map[gx, gz] = y;
                }
            }
        }
    }

    private static Vector3 SampleSmoothNormal(Vector3 worldPos, Vector3 minBounds, float step, int sizeX, int sizeZ, float[,] map)
    {
        float fx = (worldPos.x - minBounds.x) / step;
        float fz = (worldPos.z - minBounds.z) / step;

        float hL = SampleBilinear(fx - 1f, fz, sizeX, sizeZ, map);
        float hR = SampleBilinear(fx + 1f, fz, sizeX, sizeZ, map);
        float hD = SampleBilinear(fx, fz - 1f, sizeX, sizeZ, map);
        float hU = SampleBilinear(fx, fz + 1f, sizeX, sizeZ, map);

        return new Vector3(hL - hR, 2f * step, hD - hU).normalized;
    }

    private static float SampleBilinear(float fx, float fz, int sizeX, int sizeZ, float[,] map)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, sizeX - 1);
        int x1 = Mathf.Clamp(x0 + 1, 0, sizeX - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, sizeZ - 1);
        int z1 = Mathf.Clamp(z0 + 1, 0, sizeZ - 1);

        float tx = Mathf.Clamp01(fx - x0);
        float tz = Mathf.Clamp01(fz - z0);

        float a = Mathf.Lerp(map[x0, z0], map[x1, z0], tx);
        float b = Mathf.Lerp(map[x0, z1], map[x1, z1], tx);
        return Mathf.Lerp(a, b, tz);
    }
}