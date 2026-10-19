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
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Утиліти Зони А", EditorStyles.boldLabel);

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
                "• Зона А визначає строгу логічну площу чанка, яка не повинна перетинатися з іншими.\n" +
                "• Вершини можна перетягувати маркерами прямо у Scene View.\n" +
                "• Ребра з тегом 'Terminal' не генерують нових шляхів росту і зупиняють генерацію.",
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
