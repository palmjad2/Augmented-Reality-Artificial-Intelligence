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

    //–– Joint groups for rotation ––
/*    private Transform[] fingerBaseJoints;
    private Transform[] fingerMidJoints;
    private Transform[] fingerEndJoints;*/

    //–– Per‐finger contact flags ––
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

    //–– Per‐step reward guards ––
    private bool iBRewarded, iMRewarded, iERewarded;
    private bool mBRewarded, mMRewarded, mERewarded;
    private bool rBRewarded, rMRewarded, rERewarded;
    private bool pBRewarded, pMRewarded, pERewarded;
    private bool tBRewarded, tERewarded;

    //–– Reward constants ––
    const float punishValue = -0.25f;
    const float baseImmReward = 0.15f;
    const float midImmReward = 0.10f;
    const float endImmReward = 0.05f;
    const float thumbImmReward = 0.20f;
    const float baseContRate = 0.04f / 5f;
    const float midContRate = 0.03f / 5f;
    const float endContRate = 0.02f / 5f;
    const float thumbContRate = 0.05f / 5f;


    //–– Specific finger joint groups ––
    private Transform[] indexBaseJoints, indexMiddleJoints, indexEndJoints;
    private Transform[] middleBaseJoints, middleMiddleJoints, middleEndJoints;
    private Transform[] ringBaseJoints, ringMiddleJoints, ringEndJoints;
    private Transform[] pinkyBaseJoints, pinkyMiddleJoints, pinkyEndJoints;
    private Transform[] thumbBaseJoints, thumbEndJoints;
    private Transform[] palmJoints;

    //–– Cylinder reference for distance observations ––
    private Transform cylinderTransform;

    //–– (all your contact flags and reward guards unchanged) ––

    public override void Initialize()
    {
        // Find each finger’s segments separately
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

        // Cache cylinder for distance calcs
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
        // Reset all flags
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

        // Zero out every joint’s Z-rotation
        void ZeroZ(Transform[] group)
        {
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

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) Contact flags 
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


        // 2) Joint rotations z-axis or each finger segment
        void ObsRot(Transform[] group)
        {
            foreach (var t in group)
            {
                var e = t.localEulerAngles;
                sensor.AddObservation(e.z);
            }
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

        // 3) Distances from each segment to the cylinder

        void ObsDist(Transform[] group)
        {
            foreach (var t in group)
                sensor.AddObservation(Vector3.Distance(t.position, cylinderTransform.position));
        }

        ObsDist(indexBaseJoints);
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

        // all four finger‐base joints
        RotateGroup(indexBaseJoints, a[0], dt);
        RotateGroup(middleBaseJoints, a[0], dt);
        RotateGroup(ringBaseJoints, a[0], dt);
        RotateGroup(pinkyBaseJoints, a[0], dt);

        // all four finger‐middle joints
        RotateGroup(indexMiddleJoints, a[1], dt);
        RotateGroup(middleMiddleJoints, a[1], dt);
        RotateGroup(ringMiddleJoints, a[1], dt);
        RotateGroup(pinkyMiddleJoints, a[1], dt);

        // all four finger‐end joints
        RotateGroup(indexEndJoints, a[2], dt);
        RotateGroup(middleEndJoints, a[2], dt);
        RotateGroup(ringEndJoints, a[2], dt);
        RotateGroup(pinkyEndJoints, a[2], dt);

        //thumb base
        RotateGroup(thumbBaseJoints, a[3], dt);

        //thumb end
        RotateGroup(thumbEndJoints, a[4], dt);
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
        float rb = baseContRate  * dt;
        float rm = midContRate   * dt;
        float re = endContRate   * dt;
        float rt = thumbContRate * dt;

        foreach (var cp in collision.contacts)
        {
            if (cp.thisCollider == null) continue;
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
        foreach (var cp in collision.contacts)
        {
            if (cp.thisCollider == null) continue;
            switch (cp.thisCollider.tag)
            {
                case "indexBase":
                    ProcessRelease(ref indexBaseInContact, ref indexBaseHad);
                    break;
                case "indexMiddle":
                    ProcessRelease(ref indexMiddleInContact, ref indexMiddleHad);
                    break;
                case "indexEnd":
                    ProcessRelease(ref indexEndInContact, ref indexEndHad);
                    break;

                case "middleBase":
                    ProcessRelease(ref middleBaseInContact, ref middleBaseHad);
                    break;
                case "middleMiddle":
                    ProcessRelease(ref middleMiddleInContact, ref middleMiddleHad);
                    break;
                case "middleEnd":
                    ProcessRelease(ref middleEndInContact, ref middleEndHad);
                    break;

                case "ringBase":
                    ProcessRelease(ref ringBaseInContact, ref ringBaseHad);
                    break;
                case "ringMiddle":
                    ProcessRelease(ref ringMiddleInContact, ref ringMiddleHad);
                    break;
                case "ringEnd":
                    ProcessRelease(ref ringEndInContact, ref ringEndHad);
                    break;

                case "pinkyBase":
                    ProcessRelease(ref pinkyBaseInContact, ref pinkyBaseHad);
                    break;
                case "pinkyMiddle":
                    ProcessRelease(ref pinkyMiddleInContact, ref pinkyMiddleHad);
                    break;
                case "pinkyEnd":
                    ProcessRelease(ref pinkyEndInContact, ref pinkyEndHad);
                    break;

                case "thumbBase":
                    ProcessRelease(ref thumbBaseInContact, ref thumbBaseHad);
                    break;
                case "thumbEnd":
                    ProcessRelease(ref thumbEndInContact, ref thumbEndHad);
                    break;
            }
        }
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
}
