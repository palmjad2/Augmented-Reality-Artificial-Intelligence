using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class ArmGraspAgent : Agent
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees of rotation per action unit.")]
    public float rotationSpeed = 90f;
    public GameObject arm;

    //–– Specific finger joint groups ––
    private Transform[] indexBaseJoints, indexMiddleJoints, indexEndJoints;
    private Transform[] middleBaseJoints, middleMiddleJoints, middleEndJoints;
    private Transform[] ringBaseJoints, ringMiddleJoints, ringEndJoints;
    private Transform[] pinkyBaseJoints, pinkyMiddleJoints, pinkyEndJoints;
    private Transform[] thumbBaseJoints, thumbEndJoints;
    private Transform[] palmJoints;

    //–– Cylinder references & reward bookkeeping ––
    private Transform cylinderTransform;
    private Collider cylinderCollider;
    private List<Collider> segmentColliders = new List<Collider>();
    private Dictionary<Collider, float> previousDistances = new Dictionary<Collider, float>();
    private const float distanceRewardScale = 1.0f;

    public override void Initialize()
    {
        // Find each finger’s segments by tag
        indexBaseJoints = FindTransformsWithTags("indexBase");
        indexMiddleJoints = FindTransformsWithTags("indexMiddle");
        indexEndJoints = FindTransformsWithTags("indexEnd");

        middleBaseJoints = FindTransformsWithTags("middleBase");
        middleMiddleJoints = FindTransformsWithTags("middleMiddle");
        middleEndJoints = FindTransformsWithTags("middleEnd");

        ringBaseJoints = FindTransformsWithTags("ringBase");
        ringMiddleJoints = FindTransformsWithTags("ringMiddle");
        ringEndJoints = FindTransformsWithTags("ringEnd");

        pinkyBaseJoints = FindTransformsWithTags("pinkyBase");
        pinkyMiddleJoints = FindTransformsWithTags("pinkyMiddle");
        pinkyEndJoints = FindTransformsWithTags("pinkyEnd");

        thumbBaseJoints = FindTransformsWithTags("thumbBase");
        thumbEndJoints = FindTransformsWithTags("thumbEnd");

        palmJoints = FindTransformsWithTags("Palm");

        // Cache cylinder Transform & Collider
        var cylObj = GameObject.FindGameObjectWithTag("Cylinder");
        if (cylObj != null)
        {
            cylinderTransform = cylObj.transform;
            cylinderCollider = cylObj.GetComponent<Collider>();
        }

        // Collect all segment colliders for nearest‑point queries
        segmentColliders.Clear();
        string[] tags = {
            "indexBase","indexMiddle","indexEnd",
            "middleBase","middleMiddle","middleEnd",
            "ringBase","ringMiddle","ringEnd",
            "pinkyBase","pinkyMiddle","pinkyEnd",
            "thumbBase","thumbEnd"
        };
        foreach (var tag in tags)
            foreach (var go in GameObject.FindGameObjectsWithTag(tag))
                if (go.TryGetComponent<Collider>(out var col))
                    segmentColliders.Add(col);
    }

    private Transform[] FindTransformsWithTags(params string[] tags)
    {
        var list = new List<Transform>();
        foreach (var t in tags)
            foreach (var go in GameObject.FindGameObjectsWithTag(t))
                list.Add(go.transform);
        return list.ToArray();
    }

    public override void OnEpisodeBegin()
    {
        // Reset rigidbody orientation
        Rigidbody rb = arm.GetComponent<Rigidbody>();
        rb.centerOfMass = Vector3.zero;
        rb.inertiaTensorRotation = Quaternion.identity;

        // Zero out Z‑rotation on all joints
        void ZeroZ(Transform[] group)
        {
            if (group == null) return;
            foreach (var t in group)
            {
                var e = t.localEulerAngles;
                t.localRotation = Quaternion.Euler(e.x, e.y, 0f);
            }
        }

        ZeroZ(indexBaseJoints);
        ZeroZ(indexMiddleJoints);
        ZeroZ(indexEndJoints);
        ZeroZ(middleBaseJoints);
        ZeroZ(middleMiddleJoints);
        ZeroZ(middleEndJoints);
        ZeroZ(ringBaseJoints);
        ZeroZ(ringMiddleJoints);
        ZeroZ(ringEndJoints);
        ZeroZ(pinkyBaseJoints);
        ZeroZ(pinkyMiddleJoints);
        ZeroZ(pinkyEndJoints);
        ZeroZ(thumbBaseJoints);
        ZeroZ(thumbEndJoints);
        ZeroZ(palmJoints);

        // Initialize previousDistances for each segment
        previousDistances.Clear();
        foreach (var col in segmentColliders)
        {
            float d = Vector3.Distance(
                col.ClosestPoint(cylinderTransform.position),
                cylinderCollider.ClosestPoint(col.transform.position)
            );
            previousDistances[col] = d;
        }
    }

    private void FixedUpdate()
    {
        // (no per-step guards needed with distance reward)
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) Contact flags (left in place, but always false)
        //    (sensor.AddObservation(indexBaseInContact ? 1f : 0f); etc.)

        // 2) Joint Z‑rotations
        void ObsRot(Transform[] group)
        {
            foreach (var t in group)
                sensor.AddObservation(t.localEulerAngles.z);
        }
        ObsRot(indexBaseJoints);
        ObsRot(indexMiddleJoints);
        ObsRot(indexEndJoints);
        ObsRot(middleBaseJoints);
        ObsRot(middleMiddleJoints);
        ObsRot(middleEndJoints);
        ObsRot(ringBaseJoints);
        ObsRot(ringMiddleJoints);
        ObsRot(ringEndJoints);
        ObsRot(pinkyBaseJoints);
        ObsRot(pinkyMiddleJoints);
        ObsRot(pinkyEndJoints);
        ObsRot(thumbBaseJoints);
        ObsRot(thumbEndJoints);

        // 3) Distances-to-cylinder (center‑to‑center, as before)
        void ObsDist(Transform[] group)
        {
            foreach (var t in group)
                sensor.AddObservation(Vector3.Distance(t.position, cylinderTransform.position));
        }
        ObsDist(indexBaseJoints);
        ObsDist(indexMiddleJoints);
        ObsDist(indexEndJoints);
        ObsDist(middleBaseJoints);
        ObsDist(middleMiddleJoints);
        ObsDist(middleEndJoints);
        ObsDist(ringBaseJoints);
        ObsDist(ringMiddleJoints);
        ObsDist(ringEndJoints);
        ObsDist(pinkyBaseJoints);
        ObsDist(pinkyMiddleJoints);
        ObsDist(pinkyEndJoints);
        ObsDist(thumbBaseJoints);
        ObsDist(thumbEndJoints);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        float dt = Time.deltaTime;

        // Rotate each joint group
        RotateGroup(indexBaseJoints, a[0], dt);
        RotateGroup(indexMiddleJoints, a[1], dt);
        RotateGroup(indexEndJoints, a[2], dt);

        RotateGroup(middleBaseJoints, a[3], dt);
        RotateGroup(middleMiddleJoints, a[4], dt);
        RotateGroup(middleEndJoints, a[5], dt);

        RotateGroup(ringBaseJoints, a[6], dt);
        RotateGroup(ringMiddleJoints, a[7], dt);
        RotateGroup(ringEndJoints, a[8], dt);

        RotateGroup(pinkyBaseJoints, a[9], dt);
        RotateGroup(pinkyMiddleJoints, a[10], dt);
        RotateGroup(pinkyEndJoints, a[11], dt);

        RotateGroup(thumbBaseJoints, a[12], dt);
        RotateGroup(thumbEndJoints, a[13], dt);

        // —— Distance‑based incremental reward —— 
        float totalReward = 0f;
        foreach (var col in segmentColliders)
        {
            float currDist = Vector3.Distance(
                col.ClosestPoint(cylinderTransform.position),
                cylinderCollider.ClosestPoint(col.transform.position)
            );
            float delta = previousDistances[col] - currDist;
            totalReward += delta * distanceRewardScale;
            previousDistances[col] = currDist;
        }
        AddReward(totalReward);
    }

    private void RotateGroup(Transform[] group, float action, float dt)
    {
        float angle = action * rotationSpeed * dt;
        foreach (var t in group)
            t.localRotation *= Quaternion.Euler(0f, 0f, angle);
    }
}
