using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class ArmGraspAgent : Agent
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees of rotation per action unit.")]
    public float rotationSpeed = 90f;

    //–– Superquadric shape parameters ––
    public float sq_a1, sq_a2, sq_a3;
    public float sq_e1, sq_e2;
    public float sq_tx, sq_ty, sq_tz;
    public float sq_rx, sq_ry, sq_rz;

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

    //–– Human-like secure-grasp reward weights (tunable in Inspector) ––
    [Header("Grasp Reward Weights")]
    public float weightContact = 1.0f;
    public float weightStability = 2.0f;
    public float weightOpposition = 1.5f;
    public float weightJointLimit = 0.5f;
    public float weightTime = 0.01f;

    [Header("Shape Randomization")]
    [Tooltip("If true, RandomizeShape() runs at the start of every episode.")]
    public bool randomizeShapeOnEpisode = true;

    // Total number of articulated joints (used by the joint-limit penalty).
    const int JOINT_COUNT = 14;

    // Cached reference to the graspable target (tagged "Cylinder").
    private Transform targetTransform;
    private Rigidbody targetRigidbody;

    // Flat list of all 14 joint transforms, built from the tag groups.
    private Transform[] allJoints;

    // World-space contact points gathered during the current physics step.
    // Cleared each FixedUpdate after the step reward has consumed them.
    private readonly List<Vector3> stepContactPoints = new List<Vector3>();

    public override void Initialize()
    {
        // Find all joints by tag
        fingerBaseJoints = FindTransformsWithTag("FingerBase");
        fingerMidJoints = FindTransformsWithTag("FingerMiddle");
        fingerEndJoints = FindTransformsWithTag("FingerEnd");
        thumbBaseJoints = FindTransformsWithTag("ThumbBase");
        thumbEndJoints = FindTransformsWithTag("ThumbEnd");
        palmJoints = FindTransformsWithTag("Palm");

        // Build a flat array of every joint so the joint-limit penalty can
        // iterate them uniformly.
        var joints = new List<Transform>();
        joints.AddRange(fingerBaseJoints);
        joints.AddRange(fingerMidJoints);
        joints.AddRange(fingerEndJoints);
        joints.AddRange(thumbBaseJoints);
        joints.AddRange(thumbEndJoints);
        joints.AddRange(palmJoints);
        allJoints = joints.ToArray();
        if (allJoints.Length != JOINT_COUNT)
            Debug.LogWarning($"ArmGraspAgent: expected {JOINT_COUNT} joints, found {allJoints.Length}. " +
                             "Joint-limit penalty still divides by " + JOINT_COUNT + ".");

        CacheTarget();
    }

    // Find and cache the graspable target object (tagged "Cylinder") and its Rigidbody.
    private void CacheTarget()
    {
        var go = GameObject.FindWithTag("Cylinder");
        if (go != null)
        {
            targetTransform = go.transform;
            targetRigidbody = go.GetComponent<Rigidbody>();
        }
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

        stepContactPoints.Clear();

        // Make sure we still have a target reference (it can be null if the
        // object spawns after Initialize, or was re-created between episodes).
        if (targetTransform == null) CacheTarget();

        // Give every episode a different object shape so the policy generalises.
        if (randomizeShapeOnEpisode) RandomizeShape();
    }

    private void FixedUpdate()
    {
        // Apply the multi-component grasp reward using the contact flags and
        // contact points gathered during the previous physics step.
        ComputeStepReward();

        // Clear per‑step reward guards
        fbRewarded = fmRewarded = feRewarded =
        tbRewarded = teRewarded = pRewarded = false;

        // Reset the contact-point buffer for the upcoming physics step.
        stepContactPoints.Clear();
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

        // Superquadric shape parameters (11 values)
        // Set externally by SuperquadricClient.cs
        sensor.AddObservation(sq_a1);
        sensor.AddObservation(sq_a2);
        sensor.AddObservation(sq_a3);
        sensor.AddObservation(sq_e1);
        sensor.AddObservation(sq_e2);
        sensor.AddObservation(sq_tx);
        sensor.AddObservation(sq_ty);
        sensor.AddObservation(sq_tz);
        sensor.AddObservation(sq_rx);
        sensor.AddObservation(sq_ry);
        sensor.AddObservation(sq_rz);
    }

    public void SetSuperquadricParams(
        float a1, float a2, float a3,
        float e1, float e2,
        float tx, float ty, float tz,
        float rx, float ry, float rz)
    {
        sq_a1 = a1; sq_a2 = a2; sq_a3 = a3;
        sq_e1 = e1; sq_e2 = e2;
        sq_tx = tx; sq_ty = ty; sq_tz = tz;
        sq_rx = rx; sq_ry = ry; sq_rz = rz;
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

            // Record the world-space contact point for the opposition score.
            stepContactPoints.Add(cp.point);

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

    // ───────────────────────── Human-like secure-grasp reward ─────────────────────────
    //
    // Combined per-step reward:
    //   r =  weightContact     * contactScore
    //      + weightStability   * stabilityScore
    //      + weightOpposition  * oppositionScore
    //      - weightJointLimit  * jointLimitPenalty
    //      - weightTime
    //
    // This is ADDED on top of the existing event-based contact shaping above.
    private void ComputeStepReward()
    {
        float contactScore = ComputeContactScore();
        float stabilityScore = ComputeStabilityScore();
        float oppositionScore = ComputeOppositionScore();
        float jointLimitPenalty = ComputeJointLimitPenalty();

        float r = weightContact * contactScore
                + weightStability * stabilityScore
                + weightOpposition * oppositionScore
                - weightJointLimit * jointLimitPenalty
                - weightTime;

        AddReward(r);
    }

    // Number of the 6 contact regions currently touching the object.
    private int ContactCount()
    {
        int c = 0;
        if (fingerBaseInContact) c++;
        if (fingerMidInContact) c++;
        if (fingerEndInContact) c++;
        if (thumbBaseInContact) c++;
        if (thumbEndInContact) c++;
        if (palmInContact) c++;
        return c;
    }

    // (a) CONTACT SCORE (0–1, +0.5 full-grasp bonus).
    //     Fraction of the 6 contact regions engaged, plus a bonus when the palm
    //     and at least two fingertips (FingerEnd, ThumbEnd) touch together.
    private float ComputeContactScore()
    {
        float score = ContactCount() / 6.0f;

        int fingertips = 0;
        if (fingerEndInContact) fingertips++;
        if (thumbEndInContact) fingertips++;
        if (palmInContact && fingertips >= 2) score += 0.5f;

        return score;
    }

    // (b) STABILITY SCORE (0 or 1).
    //     Object is "secured" if it is nearly motionless while at least 3
    //     contact regions are active.
    private float ComputeStabilityScore()
    {
        if (ContactCount() < 3) return 0f;
        if (targetRigidbody == null) return 0f;

        bool stable = targetRigidbody.velocity.magnitude < 0.1f
                   && targetRigidbody.angularVelocity.magnitude < 0.1f;
        return stable ? 1f : 0f;
    }

    // (c) OPPOSITION SCORE (0 or 1).
    //     Rewards an opposing (pinch/enclosing) grasp: at least one pair of
    //     contact points sits on opposite sides of the object center, i.e. the
    //     dot product of their (center→contact) directions is < -0.3.
    //
    //     NOTE: the brief asked to "use sq_a1/a2/a3 to estimate the object
    //     center", but those parameters are the superquadric half-extents
    //     (size), not a position. The true center is the object's transform
    //     position (the fitted translation lives in sq_tx/ty/tz), so that is
    //     what we use here. See GRASP_REWARD_CHANGES.md.
    private float ComputeOppositionScore()
    {
        if (stepContactPoints.Count < 2) return 0f;

        Vector3 center = (targetTransform != null)
            ? targetTransform.position
            : new Vector3(sq_tx, sq_ty, sq_tz);

        for (int i = 0; i < stepContactPoints.Count; i++)
        {
            Vector3 di = stepContactPoints[i] - center;
            if (di.sqrMagnitude < 1e-8f) continue;
            di.Normalize();

            for (int j = i + 1; j < stepContactPoints.Count; j++)
            {
                Vector3 dj = stepContactPoints[j] - center;
                if (dj.sqrMagnitude < 1e-8f) continue;
                dj.Normalize();

                if (Vector3.Dot(di, dj) < -0.3f)
                    return 1f; // found an opposing pair → enclosing grasp
            }
        }
        return 0f;
    }

    // (d) JOINT LIMIT PENALTY (0–1).
    //     Fraction of the 14 joints whose local Z rotation lies outside
    //     [-90, 90] degrees. localEulerAngles returns 0–360, so the angle is
    //     first folded into the signed (-180, 180] range before the test.
    private float ComputeJointLimitPenalty()
    {
        if (allJoints == null || allJoints.Length == 0) return 0f;

        int outOfRange = 0;
        for (int i = 0; i < allJoints.Length; i++)
        {
            if (allJoints[i] == null) continue;

            float z = allJoints[i].localEulerAngles.z;
            if (z > 180f) z -= 360f; // fold to (-180, 180]

            if (z < -90f || z > 90f) outOfRange++;
        }
        return outOfRange / (float)JOINT_COUNT;
    }

    // ───────────────────────── Shape randomization ─────────────────────────
    //
    // Picks fresh superquadric parameters within reasonable ranges, pushes them
    // into the observation vector via SetSuperquadricParams, and scales the
    // target object so its visual size matches the new params. Called from
    // OnEpisodeBegin so every training episode presents a different object.
    public void RandomizeShape()
    {
        // Radii (half-extents) and shape exponents.
        float a1 = Random.Range(0.05f, 0.3f); // object radius (x)
        float a2 = Random.Range(0.05f, 0.3f); // object radius (y)
        float a3 = Random.Range(0.1f, 0.5f);  // object half-height (z)
        float e1 = Random.Range(0.1f, 1.0f);  // shape: cylinder → sphere
        float e2 = Random.Range(0.1f, 1.0f);

        // Keep the object where it currently is.
        float tx = sq_tx, ty = sq_ty, tz = sq_tz;
        if (targetTransform != null)
        {
            Vector3 p = targetTransform.position;
            tx = p.x; ty = p.y; tz = p.z;
        }

        // Slight random tilt.
        float rx = Random.Range(-0.3f, 0.3f);
        float ry = Random.Range(-0.3f, 0.3f);
        float rz = Random.Range(-0.3f, 0.3f);

        SetSuperquadricParams(a1, a2, a3, e1, e2, tx, ty, tz, rx, ry, rz);

        // Match the visual mesh to the new params (full extent = 2 × half-extent).
        if (targetTransform != null)
            targetTransform.localScale = new Vector3(a1 * 2f, a2 * 2f, a3 * 2f);
    }
}
