using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Connects to a Python superquadric fitter over TCP, sends a sampled point
// cloud as CSV, and reads back 11 superquadric parameters which are pushed
// into the ArmGraspAgent's observation vector.
[RequireComponent(typeof(MeshSampler))]
public class SuperquadricClient : MonoBehaviour
{
    [Header("Python Fitter Connection")]
    [Tooltip("Host of the Python superquadric fitter.")]
    public string host = "127.0.0.1";
    [Tooltip("Port of the Python superquadric fitter.")]
    public int port = 65432;

    private void Start()
    {
        StartCoroutine(FitShape());
    }

    private IEnumerator FitShape()
    {
        // 1) Sample a point cloud from the mesh on this GameObject.
        MeshSampler sampler = GetComponent<MeshSampler>();
        if (sampler == null)
        {
            Debug.LogError("SuperquadricClient: no MeshSampler component found.");
            yield break;
        }

        List<Vector3> points = sampler.Sample();
        if (points == null || points.Count == 0)
        {
            Debug.LogError("SuperquadricClient: MeshSampler returned no points.");
            yield break;
        }

        // 2) Build a CSV string, one "x,y,z" line per sampled point.
        var sb = new StringBuilder();
        foreach (var p in points)
            sb.Append(p.x).Append(',').Append(p.y).Append(',').Append(p.z).Append('\n');
        string csv = sb.ToString();

        // 3) Send the CSV to the Python fitter and read back the parameters.
        string response = null;
        try
        {
            using (TcpClient client = new TcpClient(host, port))
            using (NetworkStream stream = client.GetStream())
            {
                byte[] payload = Encoding.UTF8.GetBytes(csv);
                stream.Write(payload, 0, payload.Length);

                // Signal end-of-input so the fitter can start processing.
                client.Client.Shutdown(SocketShutdown.Send);

                // Read the full response (comma-separated 11 floats).
                var responseBuilder = new StringBuilder();
                byte[] buffer = new byte[4096];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    responseBuilder.Append(Encoding.UTF8.GetString(buffer, 0, read));

                response = responseBuilder.ToString().Trim();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SuperquadricClient: socket error talking to {host}:{port} — {e.Message}");
            yield break;
        }

        if (string.IsNullOrEmpty(response))
        {
            Debug.LogError("SuperquadricClient: empty response from Python fitter.");
            yield break;
        }

        // 4) Parse the response into 11 floats.
        string[] parts = response.Split(',');
        if (parts.Length < 11)
        {
            Debug.LogError($"SuperquadricClient: expected 11 params, got {parts.Length} — \"{response}\"");
            yield break;
        }

        Debug.Log($"SuperquadricClient: received params -> " +
            $"a1={parts[0]}, a2={parts[1]}, a3={parts[2]}, " +
            $"e1={parts[3]}, e2={parts[4]}, " +
            $"tx={parts[5]}, ty={parts[6]}, tz={parts[7]}, " +
            $"rx={parts[8]}, ry={parts[9]}, rz={parts[10]}");

        // 5) Push the params into the agent's observation vector.
        ArmGraspAgent agent = FindObjectOfType<ArmGraspAgent>();
        if (agent != null)
            agent.SetSuperquadricParams(
                float.Parse(parts[0]), float.Parse(parts[1]),
                float.Parse(parts[2]), float.Parse(parts[3]),
                float.Parse(parts[4]), float.Parse(parts[5]),
                float.Parse(parts[6]), float.Parse(parts[7]),
                float.Parse(parts[8]), float.Parse(parts[9]),
                float.Parse(parts[10]));
        else
            Debug.LogError("SuperquadricClient: no ArmGraspAgent found in scene.");
    }
}
