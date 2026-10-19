using UnityEngine;
using UnityEditor;

namespace TerrainGenerator
{
    [CustomEditor(typeof(WorldGenerator))]
    public class WorldGeneratorEditor : Editor
    {
        private WorldGenerator generator;

        private void OnEnable()
        {
            generator = (WorldGenerator)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Панель керування генерацією", EditorStyles.boldLabel);

            // Інформаційний блок про стан
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Поточний Seed: {generator.seed}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Згенеровано чанків: {generator.spawnedChunks.Count} / {generator.maxChunks}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Активних відкритих ребер (1-й прохід): {generator.activeOpenEdges.Count}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Тріщин для 2-го проходу: {generator.unplacedCrackEdges.Count}", EditorStyles.boldLabel);

            if (generator.spawnBounds != null)
            {
                EditorGUILayout.LabelField($"Межа спавну: {generator.spawnBounds.name} ({generator.spawnBounds.localVertices.Count} вершин)", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Межа спавну: Не призначена (світ без полігонального обмеження)", EditorStyles.miniBoldLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Кнопка повної генерації
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button("Згенерувати світ (Generate World)", GUILayout.Height(35)))
            {
                Undo.RecordObject(generator, "Generate Procedural World");
                generator.GenerateWorld();
                EditorUtility.SetDirty(generator);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            // Крок генерації
            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Один крок (Step)", GUILayout.Height(28)))
            {
                if (generator.spawnedChunks.Count == 0 && generator.activeOpenEdges.Count == 0)
                {
                    generator.InitializeWorld();
                }
                else
                {
                    generator.GenerateStep();
                }
                EditorUtility.SetDirty(generator);
            }

            // Очищення світу
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("Очистити світ (Clear)", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Очищення", "Ви дійсно бажаєте видалити всі згенеровані чанки?", "Так", "Ні"))
                {
                    Undo.RecordObject(generator, "Clear Generated World");
                    generator.ClearGeneratedWorld();
                    EditorUtility.SetDirty(generator);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            // Кнопка 2-го проходу (заповнення тріщин)
            GUI.backgroundColor = new Color(0.7f, 0.45f, 1f);
            if (GUILayout.Button("2-й прохід: Заповнити тріщини (Run 2nd Pass)", GUILayout.Height(30)))
            {
                Undo.RecordObject(generator, "Run 2nd Pass Crack Filling");
                int filled = generator.RunSecondPass();
                EditorUtility.SetDirty(generator);
                Debug.Log($"[WorldGenerator] 2-й прохід завершено: заповнено {filled} тріщин.");
            }

            // Кнопка Leak Test
            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("Тест на замкненість (Leak Test)", GUILayout.Height(26)))
            {
                generator.RunLeakTest();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(10);

            // Швидке створення SpawnAreaPolygon якщо ще немає
            if (generator.spawnBounds == null)
            {
                if (GUILayout.Button("Створити обмежувач спавну (SpawnAreaPolygon)"))
                {
                    GameObject boundsGo = new GameObject("SpawnAreaBounds");
                    boundsGo.transform.SetParent(generator.transform);
                    boundsGo.transform.localPosition = Vector3.zero;

                    SpawnAreaPolygon polyComp = boundsGo.AddComponent<SpawnAreaPolygon>();
                    polyComp.SetAsRectangle(120f, 120f);

                    generator.spawnBounds = polyComp;
                    Undo.RegisterCreatedObjectUndo(boundsGo, "Create SpawnAreaBounds");
                    EditorUtility.SetDirty(generator);
                }
            }
        }
    }
}
