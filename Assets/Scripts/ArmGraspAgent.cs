using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class ArmGraspAgent : Agent
{
    [Header("References")]
    [Tooltip("Finger segments with colliders that can rotate.")]
    public Transform pointerBase;
    public Transform pointerEnd;
    public Transform middleBase;
    public Transform middleEnd;
    public Transform ringBase;
    public Transform ringEnd;
    public Transform pinkyBase;
    public Transform pinkyEnd;
    public Transform thumb;

    [Header("Finger Rotation Settings")]
    [Tooltip("How many degrees per step the agent can rotate each finger joint.")]
    public float rotationSpeed = 90f;

    // Internal contact booleans for each collider
    private bool pointerBaseInContact = false;
    private bool pointerEndInContact = false;
    private bool middleBaseInContact = false;
    private bool middleEndInContact = false;
    private bool ringBaseInContact = false;
    private bool ringEndInContact = false;
    private bool pinkyBaseInContact = false;
    private bool pinkyEndInContact = false;
    private bool thumbInContact = false;

    // Called once at the beginning
    public override void Initialize()
    {
        // Any additional initialization if needed
    }

    // Reset logic when each new episode begins
    public override void OnEpisodeBegin()
    {
        // Reset rotations for each joint
        pointerBase.localRotation = Quaternion.identity;
        pointerEnd.localRotation = Quaternion.identity;
        middleBase.localRotation = Quaternion.identity;
        middleEnd.localRotation = Quaternion.identity;
        ringBase.localRotation = Quaternion.identity;
        ringEnd.localRotation = Quaternion.identity;
        pinkyBase.localRotation = Quaternion.identity;
        pinkyEnd.localRotation = Quaternion.identity;
        thumb.localRotation = Quaternion.identity;

        // Reset contact booleans
        pointerBaseInContact = false;
        pointerEndInContact = false;
        middleBaseInContact = false;
        middleEndInContact = false;
        ringBaseInContact = false;
        ringEndInContact = false;
        pinkyBaseInContact = false;
        pinkyEndInContact = false;
        thumbInContact = false;
    }

    // Collect observations: each collider's contact state (9 total observations)
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(pointerBaseInContact ? 1f : 0f);
        sensor.AddObservation(pointerEndInContact ? 1f : 0f);
        sensor.AddObservation(middleBaseInContact ? 1f : 0f);
        sensor.AddObservation(middleEndInContact ? 1f : 0f);
        sensor.AddObservation(ringBaseInContact ? 1f : 0f);
        sensor.AddObservation(ringEndInContact ? 1f : 0f);
        sensor.AddObservation(pinkyBaseInContact ? 1f : 0f);
        sensor.AddObservation(pinkyEndInContact ? 1f : 0f);
        sensor.AddObservation(thumbInContact ? 1f : 0f);
    }

    // Define actions: We assume 27 continuous actions (3 per joint for 9 joints)
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Finger 1: Pointer Base (3 actions)
        float rotatePointerBaseX = actions.ContinuousActions[0];
        float rotatePointerBaseY = actions.ContinuousActions[1];
        float rotatePointerBaseZ = actions.ContinuousActions[2];

        // Finger 2: Pointer End (3 actions)
        float rotatePointerEndX = actions.ContinuousActions[3];
        float rotatePointerEndY = actions.ContinuousActions[4];
        float rotatePointerEndZ = actions.ContinuousActions[5];

        // Finger 3: Middle Base (3 actions)
        float rotateMiddleBaseX = actions.ContinuousActions[6];
        float rotateMiddleBaseY = actions.ContinuousActions[7];
        float rotateMiddleBaseZ = actions.ContinuousActions[8];

        // Finger 4: Middle End (3 actions)
        float rotateMiddleEndX = actions.ContinuousActions[9];
        float rotateMiddleEndY = actions.ContinuousActions[10];
        float rotateMiddleEndZ = actions.ContinuousActions[11];

        // Finger 5: Ring Base (3 actions)
        float rotateRingBaseX = actions.ContinuousActions[12];
        float rotateRingBaseY = actions.ContinuousActions[13];
        float rotateRingBaseZ = actions.ContinuousActions[14];

        // Finger 6: Ring End (3 actions)
        float rotateRingEndX = actions.ContinuousActions[15];
        float rotateRingEndY = actions.ContinuousActions[16];
        float rotateRingEndZ = actions.ContinuousActions[17];

        // Finger 7: Pinky Base (3 actions)
        float rotatePinkyBaseX = actions.ContinuousActions[18];
        float rotatePinkyBaseY = actions.ContinuousActions[19];
        float rotatePinkyBaseZ = actions.ContinuousActions[20];

        // Finger 8: Pinky End (3 actions)
        float rotatePinkyEndX = actions.ContinuousActions[21];
        float rotatePinkyEndY = actions.ContinuousActions[22];
        float rotatePinkyEndZ = actions.ContinuousActions[23];

        // Finger 9: Thumb (3 actions)
        float rotateThumbX = actions.ContinuousActions[24];
        float rotateThumbY = actions.ContinuousActions[25];
        float rotateThumbZ = actions.ContinuousActions[26];

        // Apply rotations with the new 3-axis values for each transform
        pointerBase.localRotation *= Quaternion.Euler(
            rotatePointerBaseX * rotationSpeed * Time.deltaTime,
            rotatePointerBaseY * rotationSpeed * Time.deltaTime,
            rotatePointerBaseZ * rotationSpeed * Time.deltaTime);

        pointerEnd.localRotation *= Quaternion.Euler(
            rotatePointerEndX * rotationSpeed * Time.deltaTime,
            rotatePointerEndY * rotationSpeed * Time.deltaTime,
            rotatePointerEndZ * rotationSpeed * Time.deltaTime);

        middleBase.localRotation *= Quaternion.Euler(
            rotateMiddleBaseX * rotationSpeed * Time.deltaTime,
            rotateMiddleBaseY * rotationSpeed * Time.deltaTime,
            rotateMiddleBaseZ * rotationSpeed * Time.deltaTime);

        middleEnd.localRotation *= Quaternion.Euler(
            rotateMiddleEndX * rotationSpeed * Time.deltaTime,
            rotateMiddleEndY * rotationSpeed * Time.deltaTime,
            rotateMiddleEndZ * rotationSpeed * Time.deltaTime);

        ringBase.localRotation *= Quaternion.Euler(
            rotateRingBaseX * rotationSpeed * Time.deltaTime,
            rotateRingBaseY * rotationSpeed * Time.deltaTime,
            rotateRingBaseZ * rotationSpeed * Time.deltaTime);

        ringEnd.localRotation *= Quaternion.Euler(
            rotateRingEndX * rotationSpeed * Time.deltaTime,
            rotateRingEndY * rotationSpeed * Time.deltaTime,
            rotateRingEndZ * rotationSpeed * Time.deltaTime);

        pinkyBase.localRotation *= Quaternion.Euler(
            rotatePinkyBaseX * rotationSpeed * Time.deltaTime,
            rotatePinkyBaseY * rotationSpeed * Time.deltaTime,
            rotatePinkyBaseZ * rotationSpeed * Time.deltaTime);

        pinkyEnd.localRotation *= Quaternion.Euler(
            rotatePinkyEndX * rotationSpeed * Time.deltaTime,
            rotatePinkyEndY * rotationSpeed * Time.deltaTime,
            rotatePinkyEndZ * rotationSpeed * Time.deltaTime);

        thumb.localRotation *= Quaternion.Euler(
            rotateThumbX * rotationSpeed * Time.deltaTime,
            rotateThumbY * rotationSpeed * Time.deltaTime,
            rotateThumbZ * rotationSpeed * Time.deltaTime);
    }

    // Collision detection: reward immediately when a collider touches the cylinder
    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Cylinder"))
        {
            foreach (ContactPoint contact in collision.contacts)
            {
                if (contact.thisCollider != null)
                {
                    if (contact.thisCollider.gameObject == pointerBase.gameObject && !pointerBaseInContact)
                    {
                        pointerBaseInContact = true;
                        AddReward(0.1f);
                    }
                    else if (contact.thisCollider.gameObject == pointerEnd.gameObject && !pointerEndInContact)
                    {
                        pointerEndInContact = true;
                        AddReward(0.1f);
                    }
                    else if (contact.thisCollider.gameObject == middleBase.gameObject && !middleBaseInContact)
                    {
                        middleBaseInContact = true;
                        AddReward(0.1f);
                    }
                    else if (contact.thisCollider.gameObject == middleEnd.gameObject && !middleEndInContact)
                    {
                        Debug.Log("Touched");
                        middleEndInContact = true;
                        AddReward(0.1f);
                    }
                    else if (contact.thisCollider.gameObject == ringBase.gameObject && !ringBaseInContact)
                    {
                        ringBaseInContact = true;
                        AddReward(0.1f);
                    }
                    else if (contact.thisCollider.gameObject == ringEnd.gameObject && !ringEndInContact)
                    {
                        ringEndInContact = true;
                        AddReward(0.1f);
                    }
                    else if (contact.thisCollider.gameObject == pinkyBase.gameObject && !pinkyBaseInContact)
                    {
                        pinkyBaseInContact = true;
                        AddReward(0.1f);
                    }
                    else if (contact.thisCollider.gameObject == pinkyEnd.gameObject && !pinkyEndInContact)
                    {
                        Debug.Log("Touched");
                        pinkyEndInContact = true;
                        AddReward(0.1f);
                    }
                    else if (contact.thisCollider.gameObject == thumb.gameObject && !thumbInContact)
                    {
                        thumbInContact = true;
                        AddReward(0.1f);
                    }
                }
            }
        }
    }

    // Reset contact booleans when a collider stops touching the cylinder
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Cylinder"))
        {
            foreach (ContactPoint contact in collision.contacts)
            {
                if (contact.thisCollider != null)
                {
                    if (contact.thisCollider.gameObject == pointerBase.gameObject)
                    {
                        pointerBaseInContact = false;
                    }
                    else if (contact.thisCollider.gameObject == pointerEnd.gameObject)
                    {
                        pointerEndInContact = false;
                    }
                    else if (contact.thisCollider.gameObject == middleBase.gameObject)
                    {
                        middleBaseInContact = false;
                    }
                    else if (contact.thisCollider.gameObject == middleEnd.gameObject)
                    {
                        middleEndInContact = false;
                    }
                    else if (contact.thisCollider.gameObject == ringBase.gameObject)
                    {
                        ringBaseInContact = false;
                    }
                    else if (contact.thisCollider.gameObject == ringEnd.gameObject)
                    {
                        ringEndInContact = false;
                    }
                    else if (contact.thisCollider.gameObject == pinkyBase.gameObject)
                    {
                        pinkyBaseInContact = false;
                    }
                    else if (contact.thisCollider.gameObject == pinkyEnd.gameObject)
                    {
                        pinkyEndInContact = false;
                    }
                    else if (contact.thisCollider.gameObject == thumb.gameObject)
                    {
                        thumbInContact = false;
                    }
                }
            }
        }
    }
}
