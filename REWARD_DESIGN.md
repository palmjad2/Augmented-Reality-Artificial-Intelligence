# Reward Design — Human-Like Secure Grasp

This document describes the multi-component reward added to `ArmGraspAgent.cs`
for training the 14-DoF prosthetic hand to produce stable, human-like grasps.

- **Observations:** 17 values — 6 contact flags + 11 superquadric params.
- **Actions:** 6 continuous values — one per finger/thumb/palm joint group.
- **Target object:** tagged `"Cylinder"`, parameterized by a fitted superquadric.

---

## 1. Reward components

The combined per-step reward is:

```
r =  weightContact    * contactScore       (0 … 1.5)
   + weightStability  * stabilityScore      (0 or 1)
   + weightOpposition * oppositionScore      (0 or 1)
   - weightJointLimit * jointLimitPenalty    (0 … 1)
   - weightTime                              (constant)
```

All weights are `public` floats, tunable in the Unity Inspector.

| Component         | Weight (default) | Range      | Sign | Purpose |
|-------------------|------------------|------------|------|---------|
| `weightContact`   | 1.0              | 0 … 1.5    | +    | Encourage establishing many contact regions and a full grasp. |
| `weightStability` | 2.0              | 0 or 1     | +    | The dominant term — reward actually *holding* the object still. |
| `weightOpposition`| 1.5              | 0 or 1     | +    | Reward an *enclosing/opposing* grip, not a one-sided push. |
| `weightJointLimit`| 0.5              | 0 … 1      | −    | Discourage driving joints into unnatural/over-flexed poses. |
| `weightTime`      | 0.01             | constant   | −    | Small per-step cost to favor efficient, prompt grasps. |

### (a) Contact score `[0 … 1.5]`
`contactCount / 6` over the six regions (FingerBase, FingerMiddle, FingerEnd,
ThumbBase, ThumbEnd, Palm). A **+0.5 bonus** is added when the **palm** and at
least **two fingertips** (FingerEnd + ThumbEnd) touch simultaneously — a
heuristic for a full, wrapped grasp. Max value is therefore 1.5.

### (b) Stability score `{0, 1}`
Read the target's `Rigidbody`. Score `1` when both
`velocity.magnitude < 0.1` **and** `angularVelocity.magnitude < 0.1`,
else `0`. **Only evaluated when ≥ 3 contact regions are active** — a motionless
object that nobody is holding should not be rewarded.

### (c) Opposition score `{0, 1}`
Collect the world-space contact points gathered during the physics step. For
every pair, take the directions from the **object center** to each contact and
score `1` if any pair is opposing — `dot(dirA, dirB) < -0.3`. All contacts on
the same side → `0`. This separates a real pinch/wrap (thumb opposing fingers)
from contacts that merely pile up on one face.

> **Object center note:** the original brief said to estimate the center from
> `sq_a1/a2/a3`, but those are the superquadric *half-extents* (size), not a
> position. The implementation uses the target object's `transform.position`
> (the fitted translation is `sq_tx/ty/tz`). See `GRASP_REWARD_CHANGES.md`.

### (d) Joint-limit penalty `[0 … 1]`
For each of the 14 joints, fold `localEulerAngles.z` into signed `(-180, 180]`
and count those outside `[-90, 90]`. Penalty = `count / 14`. The fold is
required because Unity reports Euler angles in `0…360`, so e.g. `270°` is really
`-90°`; comparing the raw value would flag every joint between 90° and 270°.

### (e) Time penalty
Constant `-weightTime` each step. Keeps episodes from dawdling once a grasp is
achievable.

> The new reward is **added on top of** the pre-existing event-based contact
> shaping (immediate + continuous contact rewards, release punishment) already
> in `OnCollisionStay` / `OnCollisionExit`. See the changelog for rationale.

---

## 2. Shape randomization

`RandomizeShape()` runs in `OnEpisodeBegin()` (gated by
`randomizeShapeOnEpisode`) so each episode presents a different object. It picks
new params, pushes them through `SetSuperquadricParams(...)`, and rescales the
target mesh to match.

| Param      | Range          | Meaning / why |
|------------|----------------|---------------|
| `a1`, `a2` | 0.05 … 0.30    | Object radius (half-extents in X/Y). Spans thin pens to graspable cans without exceeding the hand's span. |
| `a3`       | 0.10 … 0.50    | Half-height (Z). Short pucks → tall bottles. |
| `e1`       | 0.10 … 1.00    | Shape exponent: ~0.1 ≈ box/cylinder, ~1.0 ≈ rounded/sphere. Curriculum across hard edges to soft shapes. |
| `e2`       | 0.10 … 1.00    | Second shape exponent (cross-section roundness). |
| `tx,ty,tz` | unchanged      | Object stays at its current position so the hand's reachable workspace is fixed. |
| `rx,ry,rz` | -0.30 … 0.30   | Small random tilt (radians) so the policy can't assume a perfectly upright object. |

Visual scaling: `localScale = (a1*2, a2*2, a3*2)` — full extent is twice the
half-extent. (Direct param→axis mapping; if the mesh's height axis is not Z,
adjust the axis order — see changelog caveat.)

---

## 3. What a successful grasp looks like (numerically)

A well-formed secure grasp on a typical step should produce roughly:

| Term                         | Value | Weighted contribution |
|------------------------------|-------|-----------------------|
| contactScore (palm+2 tips, ~4/6 regions) | ~0.67 + 0.5 = **1.17** | × 1.0 = **+1.17** |
| stabilityScore (object still, ≥3 contacts) | **1**   | × 2.0 = **+2.00** |
| oppositionScore (thumb opposes fingers)    | **1**   | × 1.5 = **+1.50** |
| jointLimitPenalty (joints in range)        | **~0**  | × 0.5 = **−0.00** |
| time                                       | const   | **−0.01** |
| **Per-step total**                         |         | **≈ +4.66** |

A failed/empty grasp (no contact, object untouched and possibly drifting):
contact `0`, stability `0` (also gated off), opposition `0`, joint penalty
possibly `>0`, minus the time cost → **slightly negative per step**. The agent
is thus pushed from "do nothing / flail" toward "wrap, oppose, and hold still".

---

## 4. Known limitations & what to watch during training

- **Overlap with legacy shaping.** The new dense reward is layered on top of the
  older event-based contact rewards. If early training over-values mere
  touching (reward climbs but objects aren't actually held), reduce the legacy
  contact constants or `weightContact`, and lean on `weightStability`.
- **Binary stability/opposition are sparse and noisy.** Both flip 0↔1 at a hard
  threshold. Expect a jagged reward curve early on. If learning stalls, soften
  the velocity threshold (0.1) or smooth opposition into a graded score.
- **One-step lag on opposition.** Contact points are consumed in the
  `FixedUpdate` *after* the step that produced them, then cleared. This is a
  single physics-step delay — harmless, but be aware when debugging.
- **Joint-limit axis assumption.** The penalty only inspects local **Z**. If a
  finger flexes about a different local axis, that motion is not penalized.
  Confirm the rig's flex axis matches.
- **Visual-scale axis assumption.** `RandomizeShape` maps `a3` (half-height) to
  the Z scale. If the cylinder mesh's height is along Y, swap the axes so the
  visual matches the physics/observation.
- **Center proxy.** Opposition uses `transform.position` as the center. For very
  off-center or hollow shapes this is an approximation.
- **Rigidbody assumption.** Stability needs a `Rigidbody` on the `"Cylinder"`
  object; if absent, stability is always 0 (logged-free — verify the tag/rb).
- **Watch:** mean reward trend, episode length (should fall as grasps get
  efficient), and the fraction of episodes reaching stability=1. A reward that
  rises while stability stays low signals reward hacking via contact spamming.
