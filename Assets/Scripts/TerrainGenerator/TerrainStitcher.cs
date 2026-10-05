using UnityEngine;

public class TerrainStitcher : MonoBehaviour
{
    public string terrainTag = "TerrainChunk";
    public Material terrainMaterial;
    public float smoothingAngle = 60f;

    public void BuildIntersections()
    {
        GameObject[] terrainObjects = GameObject.FindGameObjectsWithTag(terrainTag);
        if (terrainObjects.Length == 0) return;

        CombineInstance[] combineInstances = new CombineInstance[terrainObjects.Length];
        for (int i = 0; i < terrainObjects.Length; i++)
        {
            MeshFilter filter = terrainObjects[i].GetComponent<MeshFilter>();
            if (filter != null)
            {
                combineInstances[i].mesh = filter.sharedMesh;
                combineInstances[i].transform = filter.transform.localToWorldMatrix;
            }
            terrainObjects[i].GetComponent<MeshRenderer>().enabled = false;
        }

        Mesh combinedMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        combinedMesh.CombineMeshes(combineInstances, true, true);

        NormalSolver.RecalculateNormals(combinedMesh, smoothingAngle);

        GameObject finalTerrain = new GameObject("Procedural_Terrain_Merged");
        finalTerrain.AddComponent<MeshFilter>().mesh = combinedMesh;
        finalTerrain.AddComponent<MeshRenderer>().material = terrainMaterial;
        finalTerrain.AddComponent<MeshCollider>().sharedMesh = combinedMesh;
    }
}