using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class ArmGraspAgent : Agent
{
    [Header("References")]
    [Tooltip("Any animated finger/joint transforms—it doesn’t matter which, tags drive logic.")]
    public Transform[] joints; // you can leave this empty; we rotate by tag, not by array.

    [Header("Rotation Settings")]
    public float rotationSpeed = 90f;

    // Contact state flags
    private bool fingerBaseInContact, fingerMidInContact, fingerEndInContact;
    private bool thumbBaseInContact, thumbEndInContact, palmInContact;

    // “Had contact” flags to gate punishments
    private bool fingerBaseHad, fingerMidHad, fingerEndHad;
    private bool thumbBaseHad, thumbEndHad, palmHad;

    // Per–fixedUpdate reward guards
    private bool fbRewarded, fmRewarded, feRewarded;
    private bool tbRewarded, teRewarded, pRewarded;

    // Reward & punishment constants
    const float punishValue = -0.02f;

    // Immediate rewards
    const float baseImmReward = 0.15f;   // Finger base
    const float midImmReward = 0.10f;   // Finger middle
    const float endImmReward = 0.05f;   // Finger end
    const float thumbImmReward = 0.20f;   // Thumb base & end
    const float palmImmReward = 0.25f;   // Palm highest

    // Continuous (time‑gradient) rates per second
    const float baseContRate = 0.002f / 5f;
    const float midContRate = 0.0015f / 5f;
    const float endContRate = 0.001f / 5f;
    const float thumbContRate = 0.0025f / 5f;
    const float palmContRate = 0.003f / 5f;

    public override void Initialize() { }

    public override void OnEpisodeBegin()
    {
        // Reset all contact & hadContact flags
        fingerBaseInContact = fingerMidInContact = fingerEndInContact = false;
        thumbBaseInContact = thumbEndInContact = palmInContact = false;

        fingerBaseHad = fingerMidHad = fingerEndHad = false;
        thumbBaseHad = thumbEndHad = palmHad = false;
    }

    private void FixedUpdate()
    {
        // Clear per–step reward flags
        fbRewarded = fmRewarded = feRewarded =
        tbRewarded = teRewarded = pRewarded = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Six booleans: one for each tag
        sensor.AddObservation(fingerBaseInContact ? 1f : 0f);
        sensor.AddObservation(fingerMidInContact ? 1f : 0f);
        sensor.AddObservation(fingerEndInContact ? 1f : 0f);
        sensor.AddObservation(thumbBaseInContact ? 1f : 0f);
        sensor.AddObservation(thumbEndInContact ? 1f : 0f);
        sensor.AddObservation(palmInContact ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Expect 42 continuous actions: 14 joints × 3 axes each
        var a = actions.ContinuousActions;

        // Rotate *all* animated joints in the scene, ignoring order
        // They must be tagged appropriately in Unity:
        foreach (var jt in joints)
        {
            if (jt == null) continue;
            var t = jt.tag;
            // Determine which slice of the 'a' array to use:
            // you could store a map from tag→(startIndex), or
            // even better, just animate by your existing mechanism.
            // Here, for brevity, we show a toy example rotating *all* by the first 3:
            jt.localRotation *= Quaternion.Euler(
                a[0] * rotationSpeed * Time.deltaTime,
                a[1] * rotationSpeed * Time.deltaTime,
                a[2] * rotationSpeed * Time.deltaTime
            );
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Cylinder")) return;

        // Compute per–step continuous reward
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
                    ProcessContact(
                      ref fingerBaseInContact, ref fingerBaseHad, ref fbRewarded,
                      baseImmReward, rb);
                    break;
                case "FingerMiddle":
                    ProcessContact(
                      ref fingerMidInContact, ref fingerMidHad, ref fmRewarded,
                      midImmReward, rm);
                    break;
                case "FingerEnd":
                    ProcessContact(
                      ref fingerEndInContact, ref fingerEndHad, ref feRewarded,
                      endImmReward, re);
                    break;
                case "ThumbBase":
                    ProcessContact(
                      ref thumbBaseInContact, ref thumbBaseHad, ref tbRewarded,
                      thumbImmReward, rt);
                    break;
                case "ThumbEnd":
                    ProcessContact(
                      ref thumbEndInContact, ref thumbEndHad, ref teRewarded,
                      thumbImmReward, rt);
                    break;
                case "Palm":
                    ProcessContact(
                      ref palmInContact, ref palmHad, ref pRewarded,
                      palmImmReward, rp);
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

    // Called when a new collider of this tag touches the cylinder.
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

    // Called when a collider of this tag stops touching the cylinder.
    void ProcessRelease(
        ref bool inContact,
        ref bool hadContact
    )
    {
        if (inContact && hadContact)
        {
            AddReward(punishValue);
        }
        inContact = false;
    }
}
