using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetPosition : MonoBehaviour
{
    public GameObject targetObject; // Assign the GameObject to reset in the Inspector
    public float resetInterval = 2f; // Time interval in seconds, editable in the Inspector

    private Vector3 originalPosition; // Stores the original position
    private Quaternion originalRotation; // Stores the original rotation

    void Start()
    {
        if (targetObject == null)
        {
            targetObject = gameObject; // Default to the GameObject this script is attached to
        }

        // Save the original position and rotation
        originalPosition = targetObject.transform.position;
        originalRotation = targetObject.transform.rotation;

        // Start the repeating reset function
        InvokeRepeating(nameof(ResetToOriginalPosition), resetInterval, resetInterval);
    }

    void ResetToOriginalPosition()
    {
        if (targetObject != null)
        {
            targetObject.transform.position = originalPosition;
            targetObject.transform.rotation = originalRotation;
        }
    }
}

