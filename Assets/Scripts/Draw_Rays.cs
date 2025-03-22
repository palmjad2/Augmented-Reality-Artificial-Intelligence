using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Debug.DrawRay will only work if Gizmos is enabled
    // This means it is not guaranteed to show on the Hololens, and the scene gets messy with other gizmos enabled.
    // To get around this the LineRenderer class is used instead
public class Draw_Rays : MonoBehaviour
{
    // Find the tranform positions of GameObjects that rays will be drawn through
    private Transform rPoint1;
    private Transform rPoint2;
    private Transform bPoint1;
    private Transform bPoint2;
    RayDrawer rayDrawerRed;
    RayDrawer rayDrawerBlue;
    // Start is called before the first frame update

    public struct RayDrawer // Using struct to create class that can draw lines in scene, so that instances of the stuct can be called as many times as needed
      {
          private LineRenderer lineRenderer;
          private float lineSize;

          public RayDrawer(float lineSize = 0.001f)
          {
              // Initiate ray object into Unity scene
              GameObject rayObj = new GameObject("RayObj");
              // Add the class that renders lines
              lineRenderer = rayObj.AddComponent<LineRenderer>();
              // Initial material for line
              lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));

              this.lineSize = lineSize; // Sets lineSize parameter equal to private float initiated at start of struct
          }

          private void init(float lineSize = 0.001f)
          {
              if (lineRenderer == null) // Called upon if ray has not been initiated yet
              {

                  GameObject rayObj = new GameObject("RayObj");
                  lineRenderer = rayObj.AddComponent<LineRenderer>();
                  lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));

                  this.lineSize = lineSize;
              } 
          }

          public void InitiateRay(Vector3 start, Vector3 end, Color RayColor)
          {
              if (lineRenderer == null) // Called upon if ray has not been initiated yet
              {
                  init(0.001f); // Set ray size

              }

              // Set rays color
              lineRenderer.startColor = RayColor;
              lineRenderer.endColor = RayColor;

              // Set the width of the ray
              lineRenderer.startWidth = lineSize; 
              lineRenderer.endWidth = lineSize;

              // Set the positions for where the line is connecting
                  // Given that it is just one line in between 2 GameObject this will be 2
              lineRenderer.positionCount= 2;

              // Set where in the scene the 1st and 2nd positions start
              lineRenderer.SetPosition(0, start);
              lineRenderer.SetPosition(1, end);

              lineRenderer.textureMode = LineTextureMode.RepeatPerSegment;
          }
        public void Destroy() // Destroy so that infinite rays are not created
        {
            if (lineRenderer != null)
            {
                UnityEngine.Object.Destroy(lineRenderer.gameObject);
            }
        }
    }
    

   
    void Start()
    {
        rPoint1 = GameObject.Find("rPoint_1").transform;
        rPoint2 = GameObject.Find("rPoint_2").transform;
        bPoint1 = GameObject.Find("bPoint_1").transform;
        bPoint2 = GameObject.Find("bPoint_2").transform;
        rayDrawerRed = new RayDrawer();
        rayDrawerBlue = new RayDrawer();
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.DrawRay(rPoint1.transform.position, rPoint2.transform.position- rPoint1.transform.position, Color.red);
        rayDrawerRed.InitiateRay(rPoint1.transform.position, rPoint2.transform.position, Color.red);
        rayDrawerBlue.InitiateRay(bPoint1.transform.position, bPoint2.transform.position, Color.blue);
    }
}
