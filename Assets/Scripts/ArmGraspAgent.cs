using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class ArmGraspAgent : Agent
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees of rotation per action unit.")]
    public float rotationSpeed = 90f;

    // Private arrays of Transforms, filled at runtime by tag
    private Transform[] fingerBaseJoints;
    private Transform[] fingerMidJoints;
    private Transform[] fingerEndJoints;
    private Transform[] thumbBaseJoints;
    private Transform[] thumbEndJoints;
    private Transform[] palmJoints;

    // Contact state flags
    private bool fingerBaseInContact, fingerMidInContact, fingerEndInContact;
    private bool thumbBaseInContact, thumbEndInContact, palmInContact;

    // “Had contact” flags to gate punishment
    private bool fingerBaseHad, fingerMidHad, fingerEndHad;
    private bool thumbBaseHad, thumbEndHad, palmHad;

    // Per–fixedUpdate reward guards
    private bool fbRewarded, fmRewarded, feRewarded;
    private bool tbRewarded, teRewarded, pRewarded;

    // Immediate Reward constants
    const float punishValue = -0.07f;
    const float baseImmReward = 0.15f;
    const float midImmReward = 0.10f;
    const float endImmReward = 0.05f;
    const float thumbImmReward = 0.20f;
    const float palmImmReward = 0.25f;

    // Constant contact rewards
    const float baseContRate = 0.002f / 5f;
    const float midContRate = 0.0015f / 5f;
    const float endContRate = 0.001f / 5f;
    const float thumbContRate = 0.0025f / 5f;
    const float palmContRate = 0.003f / 5f;

    public override void Initialize()
    {
        // Find all joints by tag
        fingerBaseJoints = FindTransformsWithTag("FingerBase");
        fingerMidJoints = FindTransformsWithTag("FingerMiddle");
        fingerEndJoints = FindTransformsWithTag("FingerEnd");
        thumbBaseJoints = FindTransformsWithTag("ThumbBase");
        thumbEndJoints = FindTransformsWithTag("ThumbEnd");
        palmJoints = FindTransformsWithTag("Palm");
    }

    // Helper to find all Transforms with a given tag
    private Transform[] FindTransformsWithTag(string tag)
    {
        var gos = GameObject.FindGameObjectsWithTag(tag);
        var ts = new Transform[gos.Length];
        for (int i = 0; i < gos.Length; i++)
            ts[i] = gos[i].transform;
        return ts;
    }

    public override void OnEpisodeBegin()
    {
        // Reset contact flags
        fingerBaseInContact = fingerMidInContact = fingerEndInContact = false;
        thumbBaseInContact = thumbEndInContact = palmInContact = false;

        fingerBaseHad = fingerMidHad = fingerEndHad = false;
        thumbBaseHad = thumbEndHad = palmHad = false;
    }

    private void FixedUpdate()
    {
        // Clear per‑step reward guards
        fbRewarded = fmRewarded = feRewarded =
        tbRewarded = teRewarded = pRewarded = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 6 booleans, one per tag
        sensor.AddObservation(fingerBaseInContact ? 1f : 0f);
        sensor.AddObservation(fingerMidInContact ? 1f : 0f);
        sensor.AddObservation(fingerEndInContact ? 1f : 0f);
        sensor.AddObservation(thumbBaseInContact ? 1f : 0f);
        sensor.AddObservation(thumbEndInContact ? 1f : 0f);
        sensor.AddObservation(palmInContact ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 6 continuous actions: index 0→FingerBase, 1→FingerMiddle, 2→FingerEnd,
        // 3→ThumbBase, 4→ThumbEnd, 5→Palm
        var a = actions.ContinuousActions;
        float dt = Time.deltaTime;

        RotateGroup(fingerBaseJoints, a[0], dt);
        RotateGroup(fingerMidJoints, a[1], dt);
        RotateGroup(fingerEndJoints, a[2], dt);
        RotateGroup(thumbBaseJoints, a[3], dt);
        RotateGroup(thumbEndJoints, a[4], dt);
        RotateGroup(palmJoints, a[5], dt);
    }

    // Rotate every transform in the group around its X‑axis
    private void RotateGroup(Transform[] group, float action, float dt)
    {
        float angle = action * rotationSpeed * dt;
        for (int i = 0; i < group.Length; i++)
            group[i].localRotation *= Quaternion.Euler(angle, 0f, 0f);
    }
    
    // Checks if it is in constant contact a rewards based on that.

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Cylinder")) return;

        float dt = Time.fixedDeltaTime;
        float rb = baseContRate * dt;
        float rm = midContRate * dt;
        float re = endContRate * dt;
        float rt = thumbContRate * dt;
        float rp = palmContRate * dt;

        foreach (var cp in collision.contacts)
        {
            if (cp.thisCollider == null) continue;
            switch (cp.thisCollider.tag)
            {
                case "FingerBase":
                    ProcessContact(ref fingerBaseInContact, ref fingerBaseHad, ref fbRewarded,
                                   baseImmReward, rb);
                    break;
                case "FingerMiddle":
                    ProcessContact(ref fingerMidInContact, ref fingerMidHad, ref fmRewarded,
                                   midImmReward, rm);
                    break;
                case "FingerEnd":
                    ProcessContact(ref fingerEndInContact, ref fingerEndHad, ref feRewarded,
                                   endImmReward, re);
                    break;
                case "ThumbBase":
                    ProcessContact(ref thumbBaseInContact, ref thumbBaseHad, ref tbRewarded,
                                   thumbImmReward, rt);
                    break;
                case "ThumbEnd":
                    ProcessContact(ref thumbEndInContact, ref thumbEndHad, ref teRewarded,
                                   thumbImmReward, rt);
                    break;
                case "Palm":
                    ProcessContact(ref palmInContact, ref palmHad, ref pRewarded,
                                   palmImmReward, rp);
                    break;
            }
        }
    }

    // A function that checks collision is with cylinder and checks each collider on each part of the finger and gives a punishement.
    private void OnCollisionExit(Collision collision) 
    {
        if (!collision.gameObject.CompareTag("Cylinder")) return;

        foreach (var cp in collision.contacts)
        {
            if (cp.thisCollider == null) continue;
            switch (cp.thisCollider.tag)
            {
                case "FingerBase":
                    ProcessRelease(ref fingerBaseInContact, ref fingerBaseHad);
                    break;
                case "FingerMiddle":
                    ProcessRelease(ref fingerMidInContact, ref fingerMidHad);
                    break;
                case "FingerEnd":
                    ProcessRelease(ref fingerEndInContact, ref fingerEndHad);
                    break;
                case "ThumbBase":
                    ProcessRelease(ref thumbBaseInContact, ref thumbBaseHad);
                    break;
                case "ThumbEnd":
                    ProcessRelease(ref thumbEndInContact, ref thumbEndHad);
                    break;
                case "Palm":
                    ProcessRelease(ref palmInContact, ref palmHad);
                    break;
            }
        }
    }

    // This funcntion differentiates punishment of removal from intial non-contact.
    void ProcessContact(
        ref bool inContact,
        ref bool hadContact,
        ref bool rewardedThisStep,
        float immediateReward,
        float continuousReward
    )
    {
        if (!inContact) // Sets the default state of contact to true after initla contact
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

    void ProcessRelease(ref bool inContact, ref bool hadContact) // Punishement for release
    {
        if (inContact && hadContact)
            AddReward(punishValue);
        inContact = false;
    }
}
