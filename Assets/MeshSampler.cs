using System.Collections.Generic;
using UnityEngine;

public class MeshSampler : MonoBehaviour
{
    [Header("Sampling")]
    public int numPoints = 1000;
    public bool visualize = true;

    private List<Vector3> sampledPoints = new List<Vector3>();

    void Start()
    {
        Sample();
    }

    public List<Vector3> Sample()
    {
        sampledPoints.Clear();

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null)
        {
            Debug.LogError("MeshSampler: no MeshFilter found on this GameObject.");
            return sampledPoints;
        }

        Mesh mesh = mf.sharedMesh;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;

        List<(int i0, int i1, int i2, float area)> tris = new();
        float totalArea = 0f;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 v1 = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 2]]);

            float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            tris.Add((triangles[i], triangles[i + 1], triangles[i + 2], area));
            totalArea += area;
        }

        for (int i = 0; i < numPoints; i++)
        {
            float r = Random.value * totalArea;
            float cumulative = 0f;

            foreach (var tri in tris)
            {
                cumulative += tri.area;
                if (cumulative >= r)
                {
                    Vector3 v0 = transform.TransformPoint(vertices[tri.i0]);
                    Vector3 v1 = transform.TransformPoint(vertices[tri.i1]);
                    Vector3 v2 = transform.TransformPoint(vertices[tri.i2]);

                    sampledPoints.Add(RandomPointOnTriangle(v0, v1, v2));
                    break;
                }
            }
        }

        Debug.Log($"MeshSampler: sampled {sampledPoints.Count} points.");
        return sampledPoints;
    }

    Vector3 RandomPointOnTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        float u = Random.value;
        float v = Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        return a + u * (b - a) + v * (c - a);
    }

    void OnDrawGizmos()
    {
        if (!visualize || sampledPoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (var p in sampledPoints)
            Gizmos.DrawSphere(p, 0.005f);
    }
}