using System.Collections.Generic;
using UnityEngine;

// Samples random points from a mesh surface, area-weighted so that larger
// triangles receive proportionally more samples. The resulting point cloud is
// consumed by SuperquadricClient.cs and shipped to the Python fitter.
[RequireComponent(typeof(MeshFilter))]
public class MeshSampler : MonoBehaviour
{
    [Tooltip("Number of surface points to sample.")]
    public int numPoints = 512;

    // Cached most-recent sample, used for Gizmo visualization.
    private List<Vector3> lastSampled = new List<Vector3>();

    /// <summary>
    /// Sample <see cref="numPoints"/> points from the MeshFilter.sharedMesh,
    /// area-weighted across triangles. Points are returned in world space.
    /// </summary>
    public List<Vector3> Sample()
    {
        var result = new List<Vector3>();

        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("MeshSampler: no MeshFilter.sharedMesh found.");
            lastSampled = result;
            return result;
        }

        Mesh mesh = mf.sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        int triCount = tris.Length / 3;
        if (triCount == 0)
        {
            lastSampled = result;
            return result;
        }

        // Build a cumulative-area table so triangles can be picked area-weighted.
        float[] cumulative = new float[triCount];
        float totalArea = 0f;
        for (int i = 0; i < triCount; i++)
        {
            Vector3 a = verts[tris[i * 3 + 0]];
            Vector3 b = verts[tris[i * 3 + 1]];
            Vector3 c = verts[tris[i * 3 + 2]];
            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            totalArea += area;
            cumulative[i] = totalArea;
        }

        if (totalArea <= 0f)
        {
            lastSampled = result;
            return result;
        }

        for (int p = 0; p < numPoints; p++)
        {
            // Pick a triangle proportional to its area.
            float r = Random.value * totalArea;
            int tri = System.Array.BinarySearch(cumulative, r);
            if (tri < 0) tri = ~tri;
            if (tri >= triCount) tri = triCount - 1;

            Vector3 a = verts[tris[tri * 3 + 0]];
            Vector3 b = verts[tris[tri * 3 + 1]];
            Vector3 c = verts[tris[tri * 3 + 2]];

            // Uniform random barycentric point within the triangle.
            float u = Random.value;
            float v = Random.value;
            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }
            Vector3 local = a + u * (b - a) + v * (c - a);

            // Convert to world space so the fitted shape lives in scene coords.
            result.Add(transform.TransformPoint(local));
        }

        lastSampled = result;
        return result;
    }

    private void OnDrawGizmos()
    {
        if (lastSampled == null) return;
        Gizmos.color = Color.cyan;
        foreach (var pt in lastSampled)
            Gizmos.DrawSphere(pt, 0.005f);
    }
}
