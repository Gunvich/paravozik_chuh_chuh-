// Assets/Scripts/Editor/ChunkZoneMigration.cs
// Run via: Tools → Migrate ChunkData → Convert PolygonCollider2D → Zone3D
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot migration tool.
/// Scans every prefab in the project, and for each ChunkData component
/// that still has a PolygonCollider2D stored in zoneA_Core / zoneB_Aura:
///   1. Reads the collider's world-space path (converted to local XZ).
///   2. Adds a Zone3D MonoBehaviour on the same GameObject.
///   3. Copies the polygon points (as local XZ Vector2[]).
///   4. Assigns the new Zone3D to zoneA_Core / zoneB_Aura.
///   5. Removes the now-redundant PolygonCollider2D.
/// </summary>
public static class ChunkZoneMigration
{
    private const string MenuPath = "Tools/Migrate ChunkData/Convert PolygonCollider2D → Zone3D";

    [MenuItem(MenuPath)]
    public static void RunMigration()
    {
        if (!EditorUtility.DisplayDialog(
            "Migrate ChunkData zones",
            "This will modify ALL prefabs that contain ChunkData.\n\n" +
            "Make sure you have a Git backup before proceeding!\n\n" +
            "Continue?",
            "Yes, Migrate", "Cancel"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int total = 0, migrated = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                ChunkData[] chunkDatas = prefab.GetComponentsInChildren<ChunkData>(true);
                if (chunkDatas.Length == 0) continue;

                total++;
                bool changed = false;

                using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    GameObject root = editScope.prefabContentsRoot;

                    foreach (ChunkData cd in root.GetComponentsInChildren<ChunkData>(true))
                    {
                        // ─── zoneA_Core ───────────────────────────────────
                        var collA = cd.gameObject.GetComponent<PolygonCollider2D>();
                        if (collA != null && cd.zoneA_Core == null)
                        {
                            Zone3D z = GetOrAddZone3D(cd.gameObject, "Zone3D_Core",
                                new Color(1f, 0.3f, 0.3f, 0.4f));
                            z.localPoints = ConvertPath(collA.GetPath(0));
                            cd.zoneA_Core = z;
                            Object.DestroyImmediate(collA);
                            changed = true;
                            Debug.Log($"[Migration] {path}: created Zone3D_Core on '{cd.name}'");
                        }

                        // ─── zoneB_Aura ───────────────────────────────────
                        // Aura is often the second PolygonCollider2D on the same GO,
                        // or on a child named "Aura"
                        PolygonCollider2D collB = FindAuraCollider(cd);
                        if (collB != null && cd.zoneB_Aura == null)
                        {
                            Zone3D z = GetOrAddZone3D(cd.gameObject, "Zone3D_Aura",
                                new Color(0.2f, 0.8f, 1f, 0.25f));
                            z.localPoints = ConvertPath(collB.GetPath(0));
                            cd.zoneB_Aura = z;
                            Object.DestroyImmediate(collB);
                            changed = true;
                            Debug.Log($"[Migration] {path}: created Zone3D_Aura on '{cd.name}'");
                        }
                    }
                }

                if (changed)
                {
                    migrated++;
                    EditorUtility.SetDirty(prefab);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Migration complete",
            $"Scanned {total} prefab(s) with ChunkData.\n" +
            $"Migrated {migrated} prefab(s).\n\n" +
            "Check the Console for per-prefab details.",
            "OK");
    }

    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the second PolygonCollider2D on the ChunkData GameObject
    /// (or the one on a child named "Aura" / "ZoneAura").
    /// </summary>
    private static PolygonCollider2D FindAuraCollider(ChunkData cd)
    {
        // Try a child GO named "Aura" or "ZoneB" first
        foreach (Transform child in cd.transform)
        {
            string n = child.name.ToLowerInvariant();
            if (n.Contains("aura") || n.Contains("zoneb"))
            {
                var c = child.GetComponent<PolygonCollider2D>();
                if (c != null) return c;
            }
        }

        // Fallback: get all PolygonCollider2D on the same GO — pick the second one
        var all = cd.gameObject.GetComponents<PolygonCollider2D>();
        return all.Length >= 2 ? all[1] : null;
    }

    /// <summary>Gets an existing Zone3D with the given name (child) or adds one.</summary>
    private static Zone3D GetOrAddZone3D(GameObject parent, string childName, Color color)
    {
        // Try to find an existing child with that name
        Transform existing = parent.transform.Find(childName);
        if (existing != null)
        {
            var ez = existing.GetComponent<Zone3D>();
            if (ez != null) return ez;
        }

        // Create a child GameObject for the zone
        var child = new GameObject(childName);
        child.transform.SetParent(parent.transform, false);
        var zone = child.AddComponent<Zone3D>();
        zone.gizmoColor = color;
        return zone;
    }

    /// <summary>
    /// Converts a PolygonCollider2D path (local XY Vector2[]) to Zone3D localPoints (local XZ Vector2[]).
    /// PolygonCollider2D works in XY; Zone3D works in XZ — so we just pass X through
    /// and swap Y → Z (map poly.Y to zone.Y which represents world Z).
    /// </summary>
    private static Vector2[] ConvertPath(Vector2[] polyPath)
    {
        // PolygonCollider2D local points are in the object's local XY plane.
        // Zone3D.localPoints are in local XZ plane.
        // Since we're on a flat-ground chunk, the mapping is: (x, y_2d) → (x, y_2d as z)
        var result = new Vector2[polyPath.Length];
        for (int i = 0; i < polyPath.Length; i++)
            result[i] = new Vector2(polyPath[i].x, polyPath[i].y);
        return result;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Validate menu item availability

    [MenuItem(MenuPath, true)]
    public static bool ValidateMigration() => !Application.isPlaying;
}
