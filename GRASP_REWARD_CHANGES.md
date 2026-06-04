# Grasp Reward — Change Log

All changes made to add a human-like secure-grasp reward to the RL pipeline.

## Files changed
- `Assets/Scripts/ArmGraspAgent.cs` — new reward components, shape randomization, target caching.
- `Config/trainer_config.yaml` — updated hyperparameters.
- `REWARD_DESIGN.md` — new design document (created).
- `GRASP_REWARD_CHANGES.md` — this file (created).

## Files explicitly NOT touched (per instructions)
- No `.unity` or `.prefab` files were modified.
- `Assets/Scripts/MeshSampler.cs` — untouched.
- `Assets/Scripts/SuperquadricClient.cs` — read for context only, untouched.

---

## `ArmGraspAgent.cs`

### Added
- `using System.Collections.Generic;` (needed for `List<>`).
- **Tunable weights** (Inspector): `weightContact = 1.0`, `weightStability = 2.0`,
  `weightOpposition = 1.5`, `weightJointLimit = 0.5`, `weightTime = 0.01`.
- `randomizeShapeOnEpisode` (bool, default true) — toggles per-episode randomization.
- `JOINT_COUNT = 14` constant.
- Cached target references: `targetTransform`, `targetRigidbody`, populated by new
  `CacheTarget()` (finds the object tagged `"Cylinder"`).
- `allJoints` — flat `Transform[]` built in `Initialize()` from the six tag groups;
  logs a warning if the count ≠ 14 (still divides the penalty by 14 as specified).
- `stepContactPoints` — `List<Vector3>` of world-space contact points for the
  current physics step, populated in `OnCollisionStay`.
- **Reward methods:** `ComputeStepReward()`, `ComputeContactScore()`,
  `ComputeStabilityScore()`, `ComputeOppositionScore()`, `ComputeJointLimitPenalty()`.
- **`RandomizeShape()`** — picks new superquadric params, calls
  `SetSuperquadricParams(...)`, and rescales the target mesh.

### Modified
- `Initialize()` — builds `allJoints`, calls `CacheTarget()`.
- `OnEpisodeBegin()` — clears `stepContactPoints`, re-caches target if null,
  calls `RandomizeShape()` when `randomizeShapeOnEpisode` is true.
- `FixedUpdate()` — now calls `ComputeStepReward()` (consuming the previous
  step's contacts) **before** clearing the per-step guards, then clears
  `stepContactPoints`.
- `OnCollisionStay(...)` — records `cp.point` into `stepContactPoints` for each
  contact (after the existing null check, before the tag switch).

### Reward formula (added each FixedUpdate step)
```
r =  weightContact    * contactScore
   + weightStability  * stabilityScore
   + weightOpposition * oppositionScore
   - weightJointLimit * jointLimitPenalty
   - weightTime
```
Component definitions: see `REWARD_DESIGN.md`.

### Design decisions & deviations from the brief (please review)

1. **Extended, not replaced.** The brief allowed "replace or extend." The new
   multi-component reward is **added on top of** the existing event-based
   contact shaping (immediate/continuous contact rewards + release punishment),
   which was left intact to avoid silently discarding tuned behavior. If you'd
   prefer a clean replacement, say so and I'll strip the legacy shaping.

2. **Object center for opposition.** The brief said "use `sq_a1/a2/a3` to
   estimate object center." Those parameters are the superquadric **half-extents
   (size)**, not a position, so using them as a center would be physically
   meaningless. I use the target's **`transform.position`** as the center
   (the fitted translation is `sq_tx/ty/tz`, available as a fallback). Flagging
   this so you can confirm the intent.

3. **Euler-angle folding in the joint-limit check.** `localEulerAngles.z`
   returns `0…360`, so I fold it to signed `(-180, 180]` before the `[-90, 90]`
   test. Without this, every joint between 90° and 270° would falsely count as
   out-of-range. The penalty still divides by the literal `14` as specified.

4. **"At least 2 fingertips"** for the full-grasp bonus is interpreted as the
   two fingertip tag groups present in the rig: `FingerEnd` and `ThumbEnd`.

5. **Visual scale mapping.** `localScale = (a1*2, a2*2, a3*2)` follows the brief
   literally (full extent = 2 × half-extent). This assumes the mesh's height
   axis is **Z** (where `a3` is half-height). If the cylinder mesh is tall along
   **Y**, swap the Y/Z scale axes so the visual matches the params.

6. **One-step contact lag.** `stepContactPoints` is consumed in the next
   `FixedUpdate` then cleared, so opposition uses contacts from the immediately
   preceding physics step (a single-step delay; intentional and harmless).

### Assumptions to verify in-editor
- The graspable object is tagged `"Cylinder"` and has a `Rigidbody`.
- The six joint tags total exactly 14 transforms (a warning logs otherwise).
- Finger flex happens about each joint's local **X/Z** as the rig expects
  (the joint-limit penalty only inspects local **Z**).

---

## `Config/trainer_config.yaml`
- `max_steps`: 200000 → **500000**
- `learning_rate`: `0.0003` → **`3.0e-4`** (same value, requested notation)
- `hidden_units`: 128 → **256**
- `batch_size`: **2048** (already correct, unchanged)
- `buffer_size`: **20480** (already correct, unchanged)
- `num_layers`: **2** (already correct, unchanged)
- **Removed** `threaded: true`.
