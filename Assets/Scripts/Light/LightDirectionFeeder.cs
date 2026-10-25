using UnityEngine;
[ExecuteAlways]
public class LightDirectionFeeder : MonoBehaviour
{
    void Update()
    {
        // Передаємо інвертований вектор напрямку світла у глобальну змінну
        Vector3 lightDir = -transform.forward;
        Shader.SetGlobalVector("_WorldSpaceLightPos0", new Vector4(lightDir.x, lightDir.y, lightDir.z, 0.0f));
    }
}