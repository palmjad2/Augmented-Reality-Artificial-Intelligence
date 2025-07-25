using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class ArmGraspAgentCopy : Agent
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees of rotation per action unit.")]
    public float rotationSpeed = 90f;
    public GameObject arm;

    //–– Per-finger contact flags ––
    private bool indexBaseInContact, indexMiddleInContact, indexEndInContact;
    private bool middleBaseInContact, middleMiddleInContact, middleEndInContact;
    private bool ringBaseInContact, ringMiddleInContact, ringEndInContact;
    private bool pinkyBaseInContact, pinkyMiddleInContact, pinkyEndInContact;
    private bool thumbBaseInContact, thumbEndInContact;

    //–– “Had contact” flags ––
    private bool indexBaseHad, indexMiddleHad, indexEndHad;
    private bool middleBaseHad, middleMiddleHad, middleEndHad;
    private bool ringBaseHad, ringMiddleHad, ringEndHad;
    private bool pinkyBaseHad, pinkyMiddleHad, pinkyEndHad;
    private bool thumbBaseHad, thumbEndHad;

    //–– Per-step reward guards ––
    private bool iBRewarded, iMRewarded, iERewarded;
    private bool mBRewarded, mMRewarded, mERewarded;
    private bool rBRewarded, rMRewarded, rERewarded;
    private bool pBRewarded, pMRewarded, pERewarded;
    private bool tBRewarded, tERewarded;

    //–– Reward constants ––
    const float punishValue = -0.25f;
    const float baseImmReward = 0.00015f;
    const float midImmReward = 0.00010f;
    const float endImmReward = 0.00005f;
    const float thumbImmReward = 0.00020f;
    const float baseContRate = 0.0000005f / 5f;
    const float midContRate = 0.0000004f / 5f;
    const float endContRate = 0.0000003f / 5f;
    const float thumbContRate = 0.0000007f / 5f;

    //–– Specific finger joint groups ––
    private Transform[] indexBaseJoints, indexMiddleJoints, indexEndJoints;
    private Transform[] middleBaseJoints, middleMiddleJoints, middleEndJoints;
    private Transform[] ringBaseJoints, ringMiddleJoints, ringEndJoints;
    private Transform[] pinkyBaseJoints, pinkyMiddleJoints, pinkyEndJoints;
    private Transform[] thumbBaseJoints, thumbEndJoints;
    private Transform[] palmJoints;

    //–– Cylinder reference ––
    private Transform cylinderTransform;

    public override void Initialize()
    {
        // Find each finger’s segments
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

        // Cache cylinder
        var cylObj = GameObject.FindGameObjectWithTag("Cylinder");
        if (cylObj != null) cylinderTransform = cylObj.transform;
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
        // Clear all contact & had flags
        indexBaseInContact = indexMiddleInContact = indexEndInContact =
        middleBaseInContact = middleMiddleInContact = middleEndInContact =
        ringBaseInContact = ringMiddleInContact = ringEndInContact =
        pinkyBaseInContact = pinkyMiddleInContact = pinkyEndInContact =
        thumbBaseInContact = thumbEndInContact = false;

        indexBaseHad = indexMiddleHad = indexEndHad =
        middleBaseHad = middleMiddleHad = middleEndHad =
        ringBaseHad = ringMiddleHad = ringEndHad =
        pinkyBaseHad = pinkyMiddleHad = pinkyEndHad =
        thumbBaseHad = thumbEndHad = false;

        // \ (•◡•) /
        Rigidbody rb = arm.GetComponent<Rigidbody>();
        rb.centerOfMass = Vector3.zero;
        rb.inertiaTensorRotation = Quaternion.identity;

        // Zero out Z-rotation
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
    }

    private void FixedUpdate()
    {
        // Clear per-step continuous‐reward guards
        iBRewarded = iMRewarded = iERewarded =
        mBRewarded = mMRewarded = mERewarded =
        rBRewarded = rMRewarded = rERewarded =
        pBRewarded = pMRewarded = pERewarded =
        tBRewarded = tERewarded = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) Contact flags (14)
        sensor.AddObservation(indexBaseInContact ? 1f : 0f);
        sensor.AddObservation(indexMiddleInContact ? 1f : 0f);
        sensor.AddObservation(indexEndInContact ? 1f : 0f);
        sensor.AddObservation(middleBaseInContact ? 1f : 0f);
        sensor.AddObservation(middleMiddleInContact ? 1f : 0f);
        sensor.AddObservation(middleEndInContact ? 1f : 0f);
        sensor.AddObservation(ringBaseInContact ? 1f : 0f);
        sensor.AddObservation(ringMiddleInContact ? 1f : 0f);
        sensor.AddObservation(ringEndInContact ? 1f : 0f);
        sensor.AddObservation(pinkyBaseInContact ? 1f : 0f);
        sensor.AddObservation(pinkyMiddleInContact ? 1f : 0f);
        sensor.AddObservation(pinkyEndInContact ? 1f : 0f);
        sensor.AddObservation(thumbBaseInContact ? 1f : 0f);
        sensor.AddObservation(thumbEndInContact ? 1f : 0f);

        // 2) Joint Z-rotations
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




        // 3) Distances to cylinder
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

        // Index finger
        RotateGroup(indexBaseJoints, a[0], dt);
        RotateGroup(indexMiddleJoints, a[1], dt);
        RotateGroup(indexEndJoints, a[2], dt);

        // Middle finger
        RotateGroup(middleBaseJoints, a[3], dt);
        RotateGroup(middleMiddleJoints, a[4], dt);
        RotateGroup(middleEndJoints, a[5], dt);

        // Ring finger
        RotateGroup(ringBaseJoints, a[6], dt);
        RotateGroup(ringMiddleJoints, a[7], dt);
        RotateGroup(ringEndJoints, a[8], dt);

        // Pinky finger
        RotateGroup(pinkyBaseJoints, a[9], dt);
        RotateGroup(pinkyMiddleJoints, a[10], dt);
        RotateGroup(pinkyEndJoints, a[11], dt);

        // Thumb
        RotateGroup(thumbBaseJoints, a[12], dt);
        RotateGroup(thumbEndJoints, a[13], dt);
    }

    private void RotateGroup(Transform[] group, float action, float dt)
    {
        float angle = action * rotationSpeed * dt;
        foreach (var t in group)
            t.localRotation *= Quaternion.Euler(0f, 0f, angle);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Cylinder")) return;

        float dt = Time.fixedDeltaTime;
        float rb = baseContRate * dt;
        float rm = midContRate * dt;
        float re = endContRate * dt;
        float rt = thumbContRate * dt;

        foreach (var cp in collision.contacts)
        {
            if (cp.thisCollider == null) continue;
            Debug.Log($"CollisionStay on {cp.thisCollider.tag} | inContact? {GetInContactFlag(cp.thisCollider.tag)}");
            switch (cp.thisCollider.tag)
            {
                case "indexBase":
                    ProcessContact(ref indexBaseInContact, ref indexBaseHad, ref iBRewarded, baseImmReward, rb);
                    break;
                case "indexMiddle":
                    ProcessContact(ref indexMiddleInContact, ref indexMiddleHad, ref iMRewarded, midImmReward, rm);
                    break;
                case "indexEnd":
                    ProcessContact(ref indexEndInContact, ref indexEndHad, ref iERewarded, endImmReward, re);
                    break;
                case "middleBase":
                    ProcessContact(ref middleBaseInContact, ref middleBaseHad, ref mBRewarded, baseImmReward, rb);
                    break;
                case "middleMiddle":
                    ProcessContact(ref middleMiddleInContact, ref middleMiddleHad, ref mMRewarded, midImmReward, rm);
                    break;
                case "middleEnd":
                    ProcessContact(ref middleEndInContact, ref middleEndHad, ref mERewarded, endImmReward, re);
                    break;
                case "ringBase":
                    ProcessContact(ref ringBaseInContact, ref ringBaseHad, ref rBRewarded, baseImmReward, rb);
                    break;
                case "ringMiddle":
                    ProcessContact(ref ringMiddleInContact, ref ringMiddleHad, ref rMRewarded, midImmReward, rm);
                    break;
                case "ringEnd":
                    ProcessContact(ref ringEndInContact, ref ringEndHad, ref rERewarded, endImmReward, re);
                    break;
                case "pinkyBase":
                    ProcessContact(ref pinkyBaseInContact, ref pinkyBaseHad, ref pBRewarded, baseImmReward, rb);
                    break;
                case "pinkyMiddle":
                    ProcessContact(ref pinkyMiddleInContact, ref pinkyMiddleHad, ref pMRewarded, midImmReward, rm);
                    break;
                case "pinkyEnd":
                    ProcessContact(ref pinkyEndInContact, ref pinkyEndHad, ref pERewarded, endImmReward, re);
                    break;
                case "thumbBase":
                    ProcessContact(ref thumbBaseInContact, ref thumbBaseHad, ref tBRewarded, thumbImmReward, rt);
                    break;
                case "thumbEnd":
                    ProcessContact(ref thumbEndInContact, ref thumbEndHad, ref tERewarded, thumbImmReward, rt);
                    break;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Cylinder")) return;
        // clear all inContact flags so next contact re-triggers reward
        indexBaseInContact = indexMiddleInContact = indexEndInContact =
        middleBaseInContact = middleMiddleInContact = middleEndInContact =
        ringBaseInContact = ringMiddleInContact = ringEndInContact =
        pinkyBaseInContact = pinkyMiddleInContact = pinkyEndInContact =
        thumbBaseInContact = thumbEndInContact = false;
    }

    void ProcessContact(
        ref bool inContact,
        ref bool hadContact,
        ref bool rewardedThisStep,
        float immediateReward,
        float continuousReward
    )
    {
        if (!inContact)
        {
            inContact = true;
            hadContact = true;
            AddReward(immediateReward);
            Debug.Log("Contact Made");
        }
        if (!rewardedThisStep)
        {
            AddReward(continuousReward);
            rewardedThisStep = true;
        }
    }

    void ProcessRelease(ref bool inContact, ref bool hadContact)
    {
        if (inContact && hadContact)
            AddReward(punishValue);
        inContact = false;
    }

    // utility to print current inContact flag in the log
    private bool GetInContactFlag(string tag)
    {
        return tag switch
        {
            "indexBase" => indexBaseInContact,
            "indexMiddle" => indexMiddleInContact,
            "indexEnd" => indexEndInContact,
            "middleBase" => middleBaseInContact,
            "middleMiddle" => middleMiddleInContact,
            "middleEnd" => middleEndInContact,
            "ringBase" => ringBaseInContact,
            "ringMiddle" => ringMiddleInContact,
            "ringEnd" => ringEndInContact,
            "pinkyBase" => pinkyBaseInContact,
            "pinkyMiddle" => pinkyMiddleInContact,
            "pinkyEnd" => pinkyEndInContact,
            "thumbBase" => thumbBaseInContact,
            "thumbEnd" => thumbEndInContact,
            _ => false
        };
    }
}
