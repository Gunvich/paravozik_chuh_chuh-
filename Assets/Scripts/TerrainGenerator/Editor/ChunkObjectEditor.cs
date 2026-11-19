using UnityEngine;
using UnityEditor;

namespace TerrainGenerator
{
    [CustomEditor(typeof(ChunkObject))]
    public class ChunkObjectEditor : Editor
    {
        private ChunkObject chunk;

        private void OnEnable()
        {
            chunk = (ChunkObject)target;
        }

        public override void OnInspectorGUI()
        {
            if (chunk.isBaked)
            {
                EditorGUILayout.HelpBox("⭐ ЦЕЙ ЧАНК ЗАПЕЧЕНИЙ (Baked)!\nВін не видаляється при генерації/очищенні та враховується системою як фіксована частина світу.", MessageType.Warning);
            }

            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Запікання та Фіксація", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = chunk.isBaked ? new Color(1f, 0.4f, 0.4f) : new Color(1f, 0.8f, 0.2f);
            if (GUILayout.Button(chunk.isBaked ? "Розпекти чанк (Unbake)" : "Запекти чанк (Bake)", GUILayout.Height(28)))
            {
                Undo.RecordObject(chunk, "Toggle Bake Chunk");
                chunk.isBaked = !chunk.isBaked;
                EditorUtility.SetDirty(chunk);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Утиліти Зони А", EditorStyles.boldLabel);

            // Кнопка автоматичної підгонки під Mesh/Bounds
            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Button("Підігнати Зону А під Mesh / Bounds", GUILayout.Height(30)))
            {
                Undo.RecordObject(chunk, "Fit Zone A to Bounds");
                chunk.FitZoneToMeshBounds(margin: 0.1f);
                EditorUtility.SetDirty(chunk);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Перебудувати ребра"))
            {
                Undo.RecordObject(chunk, "Rebuild Edges");
                chunk.RebuildEdgesFromPolygon(preserveTags: true);
                EditorUtility.SetDirty(chunk);
            }

            if (GUILayout.Button("Вирівняти CCW"))
            {
                Undo.RecordObject(chunk, "Ensure CCW");
                chunk.EnsureCCW();
                chunk.RebuildEdgesFromPolygon(preserveTags: true);
                EditorUtility.SetDirty(chunk);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Створити прямокутник 20x20"))
            {
                Undo.RecordObject(chunk, "Create Rect 20x20");
                chunk.CreateDefaultRect(20f, 20f);
                EditorUtility.SetDirty(chunk);
            }

            if (GUILayout.Button("Створити прямокутник 30x30"))
            {
                Undo.RecordObject(chunk, "Create Rect 30x30");
                chunk.CreateDefaultRect(30f, 30f);
                EditorUtility.SetDirty(chunk);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Підказки:\n" +
                "• 'Підігнати Зону А під Mesh / Bounds' миттєво створює контур навколо вашої будівлі чи дороги.\n" +
                "• Запечені об'єкти (isBaked = true) не видаляються при перезапуску генератора.\n" +
                "• 'allowGrowthFromEdges' визначає, чи будуть нові чанки пристиковуватися до країв цього об'єкта (дороги/двері), чи він діє лише як перешкода (будинок).",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            if (chunk == null || chunk.polygonVertices == null) return;

            Transform t = chunk.transform;
            int count = chunk.polygonVertices.Count;

            for (int i = 0; i < count; i++)
            {
                Vector3 worldPos = t.TransformPoint(chunk.polygonVertices[i]);
                EditorGUI.BeginChangeCheck();

                Vector3 newWorldPos = Handles.PositionHandle(worldPos, t.rotation);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(chunk, "Move Chunk Vertex");
                    chunk.polygonVertices[i] = t.InverseTransformPoint(newWorldPos);
                    // Вирівнюємо локальний Y на 0
                    Vector3 local = chunk.polygonVertices[i];
                    local.y = 0f;
                    chunk.polygonVertices[i] = local;

                    chunk.UpdateEdgeCoordinates();
                    EditorUtility.SetDirty(chunk);
                }

                Handles.Label(worldPos + Vector3.up * 0.5f, $"P{i}");
            }
        }
    }
}
