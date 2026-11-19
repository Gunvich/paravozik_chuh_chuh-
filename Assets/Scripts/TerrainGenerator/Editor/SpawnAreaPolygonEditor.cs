using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TerrainGenerator
{
    [CustomEditor(typeof(SpawnAreaPolygon))]
    public class SpawnAreaPolygonEditor : Editor
    {
        private SpawnAreaPolygon polygon;

        private void OnEnable()
        {
            polygon = (SpawnAreaPolygon)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Швидке налаштування меж", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Прямокутник 100x100"))
            {
                Undo.RecordObject(polygon, "Set Rectangle 100x100");
                polygon.SetAsRectangle(100f, 100f);
                EditorUtility.SetDirty(polygon);
            }
            if (GUILayout.Button("Прямокутник 200x200"))
            {
                Undo.RecordObject(polygon, "Set Rectangle 200x200");
                polygon.SetAsRectangle(200f, 200f);
                EditorUtility.SetDirty(polygon);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Шестикутник (R=80)"))
            {
                Undo.RecordObject(polygon, "Set Hexagon");
                polygon.SetAsRegularPolygon(6, 80f);
                EditorUtility.SetDirty(polygon);
            }
            if (GUILayout.Button("Восьмикутник (R=100)"))
            {
                Undo.RecordObject(polygon, "Set Octagon");
                polygon.SetAsRegularPolygon(8, 100f);
                EditorUtility.SetDirty(polygon);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Вирівняти напрямок (CCW)"))
            {
                Undo.RecordObject(polygon, "Ensure CCW");
                polygon.EnsureCCW();
                EditorUtility.SetDirty(polygon);
            }

            EditorGUILayout.HelpBox(
                "Підказка для Scene View:\n" +
                "• Перетягуйте білі маркери для зміни форми межі.\n" +
                "• Shift + Клік на сцені додає нову вершину.\n" +
                "• Стрілки вказують зовнішні нормалі ребер.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            if (polygon == null || polygon.localVertices == null) return;

            Transform t = polygon.transform;
            int count = polygon.localVertices.Count;

            // 1. Малюємо маркери переміщення для кожної вершини
            for (int i = 0; i < count; i++)
            {
                Vector3 worldPos = t.TransformPoint(polygon.localVertices[i]);
                EditorGUI.BeginChangeCheck();

                Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(polygon, "Move Spawn Boundary Vertex");
                    polygon.localVertices[i] = t.InverseTransformPoint(newWorldPos);
                    // Тримаємо координату Y на нулі локального простору для XZ-площини
                    Vector3 local = polygon.localVertices[i];
                    local.y = 0f;
                    polygon.localVertices[i] = local;
                    EditorUtility.SetDirty(polygon);
                }

                // Підпис номеру вершини
                Handles.Label(worldPos + Vector3.up * 1f, $"V{i}", EditorStyles.boldLabel);
            }

            // 2. Додавання нової точки через Shift + лівий клік
            Event e = Event.current;
            if (e.shift && e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Plane xzPlane = new Plane(Vector3.up, t.position);

                if (xzPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitWorld = ray.GetPoint(enter);
                    Vector3 localPoint = t.InverseTransformPoint(hitWorld);
                    localPoint.y = 0f;

                    Undo.RecordObject(polygon, "Add Spawn Boundary Vertex");
                    polygon.localVertices.Add(localPoint);
                    polygon.EnsureCCW();
                    EditorUtility.SetDirty(polygon);

                    e.Use();
                }
            }
        }
    }
}
