using UnityEngine;
[ExecuteAlways]
public class GlobalWindManager : MonoBehaviour
{
    private static readonly int GlobalWindDirID = Shader.PropertyToID("_GlobalWindDirection");

    void Update()
    {
        if (transform.hasChanged)
        {
            // Передаємо напрямок (вектор), куди дивиться цей об'єкт
            Shader.SetGlobalVector(GlobalWindDirID, transform.forward);
            transform.hasChanged = false;
        }
    }
}