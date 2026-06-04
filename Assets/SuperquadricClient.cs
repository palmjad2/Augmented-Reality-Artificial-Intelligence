using System.Collections;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class SuperquadricClient : MonoBehaviour
{
    private MeshSampler meshSampler;

    void Start()
    {
        meshSampler = GetComponent<MeshSampler>();
        StartCoroutine(FitShape());
    }

    IEnumerator FitShape()
    {
        // Wait one frame so MeshSampler.Start() runs first
        yield return null;

        var points = meshSampler.Sample();

        // Build CSV string of points
        StringBuilder sb = new StringBuilder();
        foreach (var p in points)
            sb.AppendLine($"{p.x},{p.y},{p.z}");

        string data = sb.ToString();

        // Send to Python and get params back
        string response = SendToPython(data);

        if (response != null)
        {
            string[] parts = response.Split(',');
            Debug.Log($"Superquadric params received: {response}");
            Debug.Log($"a1={parts[0]} a2={parts[1]} a3={parts[2]}");
            Debug.Log($"e1={parts[3]} e2={parts[4]}");
            Debug.Log($"tx={parts[5]} ty={parts[6]} tz={parts[7]}");
            Debug.Log($"rx={parts[8]} ry={parts[9]} rz={parts[10]}");
        }
        else
        {
            Debug.LogError("No response from Python fitter.");
        }
    }

    string SendToPython(string data)
    {
        try
        {
            using TcpClient client = new TcpClient("127.0.0.1", 65432);
            NetworkStream stream = client.GetStream();

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            stream.Write(bytes, 0, bytes.Length);
            client.Client.Shutdown(SocketShutdown.Send);

            // Read response
            StringBuilder response = new StringBuilder();
            byte[] buffer = new byte[1024];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                response.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

            return response.ToString();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Socket error: {e.Message}");
            return null;
        }
    }
}