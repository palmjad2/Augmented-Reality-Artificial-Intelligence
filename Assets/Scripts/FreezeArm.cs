using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreezeArm: MonoBehaviour
{
    // Assign the target GameObject in the Inspector.
    public GameObject targetObject;

    // Used for objects without a Rigidbody.
    private Vector3 initialPosition;

    // Reference to the Rigidbody if available.
    private Rigidbody targetRigidbody;

    void Start()
    {
        if (targetObject != null)
        {
            // Try to get the Rigidbody component
            targetRigidbody = targetObject.GetComponent<Rigidbody>();

            if (targetRigidbody != null)
            {
                // Freeze the position using Rigidbody constraints.
                targetRigidbody.constraints = RigidbodyConstraints.FreezePosition;
            }
            else
            {
                // Save the starting position to reset it in Update.
                initialPosition = targetObject.transform.position;
            }
        }
        else
        {
            Debug.LogWarning("No target GameObject assigned!");
        }
    }

    void Update()
    {
        // If there's no Rigidbody, reset the position every frame.
        if (targetObject != null && targetRigidbody == null)
        {
            targetObject.transform.position = initialPosition;
        }
    }
}


