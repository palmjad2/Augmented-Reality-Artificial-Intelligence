# ML-Agents 2.0.2 → 3.0.0 Upgrade — Agent Script Audit

**Project:** Augmented-Reality-Artificial-Intelligence
**Package:** `com.unity.ml-agents` — now `3.0.0` (confirmed in `Packages/manifest.json`)
**Scope:** every `.cs` file in `Assets/Scripts/` that inherits from `Agent`
**Constraint honored:** no `.unity` / `.prefab` files touched; no game logic, reward
values, or observations changed.
**Date:** 2026-06-19

---

## Agent scripts found

`grep ": Agent"` over `Assets/Scripts/` returns exactly three subclasses:

1. `Assets/Scripts/ArmGraspAgent.cs`
2. `Assets/Scripts/ArmGraspAgentCopy.cs`
3. `Assets/Scripts/CSVArmGraspAgent.cs`

---

## Breaking-change checklist (from the task) vs. actual code

Each script was scanned for every deprecated pattern in the upgrade notes. **None were
present** — all three scripts were already written against the modern (2.0+/3.0.0)
ML-Agents API, so no API rewrites were necessary.

| Old API (pre-3.0.0) | New API (3.0.0) | Found in scripts? | Action |
|---|---|---|---|
| `using MLAgents` | `using Unity.MLAgents` | No — already `using Unity.MLAgents;` | none |
| (missing) | `using Unity.MLAgents.Actuators` for `ActionBuffers` | Already present | none |
| (missing) | `using Unity.MLAgents.Sensors` for `VectorSensor` | Already present | none |
| `OnActionReceived(float[] vectorAction)` | `OnActionReceived(ActionBuffers actions)` | Already `ActionBuffers` | none |
| `vectorAction[i]` | `actions.ContinuousActions[i]` | Already `actions.ContinuousActions` | none |
| `CollectObservations()` | `CollectObservations(VectorSensor sensor)` | Already takes `VectorSensor` | none |
| `AddVectorObs(...)` | `sensor.AddObservation(...)` | Already `sensor.AddObservation` | none |
| `GiveLikelyRewardForPotentialAction(...)` | `AddReward(...)` | Not used anywhere | none |
| `AgentReset()` | `OnEpisodeBegin()` | Already `OnEpisodeBegin()` | none |
| `Done()` | `EndEpisode()` | Neither called in any script | none |

Verification command (all patterns, zero matches):

```
grep -nE "AddVectorObs|AgentReset|\bDone\s*\(|GiveLikelyRewardForPotentialAction|using MLAgents|OnActionReceived\s*\(\s*float|CollectObservations\s*\(\s*\)|vectorAction" Assets/Scripts/
→ no matches
```

---

## Changes actually made

No API call was rewritten, because none of the deprecated patterns existed.

The only edits were a **cleanup of three stale comments**: a previous pass (under an
earlier, now-superseded task that assumed 2.0.2 was still installed) had added a
`// TODO(fix):` block above the `using Unity.MLAgents;` line in each of the three Agent
scripts, warning that 2.0.2 + Barracuda would not compile under Unity 6. With 3.0.0 now
installed (Barracuda replaced by Sentis), those comments were factually wrong and have
been removed.

| File | Edit |
|------|------|
| `Assets/Scripts/ArmGraspAgent.cs` | Removed stale 5-line `TODO(fix)` comment above `using Unity.MLAgents;` |
| `Assets/Scripts/ArmGraspAgentCopy.cs` | Removed stale 5-line `TODO(fix)` comment above `using Unity.MLAgents;` |
| `Assets/Scripts/CSVArmGraspAgent.cs` | Removed stale 5-line `TODO(fix)` comment above `using Unity.MLAgents;` |

Per-script confirmation of the resulting 3.0.0-correct surface:

- **ArmGraspAgent.cs** — `Initialize`, `OnEpisodeBegin`, `CollectObservations(VectorSensor)`,
  `OnActionReceived(ActionBuffers)` using `actions.ContinuousActions`, reward via `AddReward`. ✅
- **ArmGraspAgentCopy.cs** — same override set; contact rewards via `AddReward`. ✅
- **CSVArmGraspAgent.cs** — same override set; `OnCollisionStay`/`OnCollisionExit`
  reward handling via `AddReward`. ✅

---

## Result

All three `Agent` scripts are API-compatible with `com.unity.ml-agents` 3.0.0 with no
code-logic changes. After Unity reimports the upgraded package and recompiles, these
scripts should build clean.

> Note: the API-mapping list in the task (e.g. `AddVectorObs`, `AgentReset`, `Done`) is
> the **0.x/1.x → 2.0** migration. Those signatures were already adopted in this codebase,
> and the 2.0 → 3.0 jump did not further change the C# `Agent` surface (it swapped the
> Barracuda inference backend for Sentis), so the scripts carry over unchanged.

### Housekeeping
`COMPILE_ERRORS.md` and `MIGRATION_FIXES.md` (written under the earlier task, stating that
2.0.2 was installed) were stale once 3.0.0 landed and have been **deleted**.

---

## Addendum (2026-08-31): 3.0.0 → 4.1.0

3.0.0 did **not** compile on Unity 6000.3.18f1 after all. Unity 6000.3 no longer ships a real
`com.unity.sentis`: the built-in `com.unity.sentis@2.2.0` is a `"type": "shim"` that only
depends on `com.unity.ai.inference` (Inference Engine, namespace `Unity.InferenceEngine`). The
manifest pin `"com.unity.sentis": "2.1.1"` was overridden by that built-in shim, so ML-Agents
3.0.0's `using Unity.Sentis` produced 657 `CS0234`/`CS0246` errors (`Tensor`, `TensorShape`,
`Model`, `ModelAsset`, `BackendType` not found) inside the package itself. A project in that
state also hung the Editor at the "Opening project…" splash (Burst: `Failed to resolve assembly
'Assembly-CSharp'`).

Fix applied to `Packages/manifest.json`:
- `com.unity.ml-agents`: `3.0.0` → `4.1.0` (targets `com.unity.ai.inference` 2.6.1, which
  Unity 6000.3 already resolves)
- removed the dead `com.unity.sentis` pin

Result: `recompile_status` → `completed, failed=false, errors=[]`; 0 `error CS` lines in
Editor.log; `Assembly-CSharp.dll` built. The three `Agent` scripts needed no changes — 4.x kept
the `Agent`/`ActionBuffers`/`VectorSensor` surface and only swapped the inference backend
(4.0.0 also raised the minimum Editor to 6000.0 and merged `com.unity.ml-agents.extensions`
into the main package). Update the Python `mlagents` trainer to the release that pairs with
4.1.0 before training again.
