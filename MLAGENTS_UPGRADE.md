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

---

## Addendum (2026-08-31): Python trainer for package 4.1.0

There is **no PyPI `mlagents` release that pairs with Unity package 4.x** — PyPI's latest is the
release-22 trainer (pairs with package 3.0.0), and the package's Installation page still says
`mlagents==1.1.0`, which is stale. Do **not** `pip install mlagents`.

The 4.1.0 trainer is source-only from the `develop` branch (latest tag `release_23` = package
4.0.0). `setup.py` pins `python_requires=">=3.10.1,<=3.10.12"` and `torch>=2.1.1,<=2.8.0`.

Installed on this machine:
- Python **3.10.11** via `winget install Python.Python.3.10` (system 3.14 / 3.12 will not install it).
  Available as `py -3.10`.
- Clone: `C:\Users\chris\ml-agents` (`develop` @ `3ecb446`, `--depth 1`).
- Venv: `C:\Users\chris\ml-agents\venv` — `torch 2.8.0+cpu`, then
  `pip install ./ml-agents-envs` and `pip install ./ml-agents`.
- `pip show mlagents` → **1.2.0.dev0** (mlagents-envs 1.2.0.dev0, Communicator API 1.5.0).

Smoke run (verified):
```
C:\Users\chris\ml-agents\venv\Scripts\mlagents-learn.exe config\trainer_config.yaml --run-id=<id>
```
then Play in the Editor (`Dynamic_Scene`). Trainer log:
`Connected to Unity environment with package version 4.1.0 and communication version 1.5.0`,
`Connected new brain: Prosthetic?team=0`, then
`Prosthetic. Step: 10000. Time Elapsed: 67.5 s. Mean Reward: 0.543 ... Training.`
Unity console: `Registered Communicator in Agent`. `config/trainer_config.yaml` needed no changes.

To run from a fresh shell: `C:\Users\chris\ml-agents\venv\Scripts\activate` (or call the exe by
full path as above). Update the trainer with `git -C C:\Users\chris\ml-agents pull` followed by
re-running the two `pip install ./…` commands.

Fallback (not needed): the `release_23` branch trainer would also handshake with package 4.1.0
(same Communicator API 1.5.0) but lacks the gymnasium / LSTM-SAC fixes on `develop`.

### GPU (CUDA) benchmark — 2026-08-31

A second venv `C:\Users\chris\ml-agents\venv-cuda` has `torch 2.8.0+cu126` (RTX 3090 detected,
`torch.cuda.is_available() == True`) plus the same `mlagents 1.2.0.dev0`. Fresh runs from the
Editor (`Dynamic_Scene`, same `trainer_config.yaml`, back-to-back, run-ids `bench_cpu` / `bench_cuda`):

| device | Step 10000 | Step 20000 | steady-state per 10k |
|--------|-----------:|-----------:|---------------------:|
| `--torch-device cpu`  | 53.3 s | 94.8 s  | ~41.5 s |
| `--torch-device cuda` | 89.3 s | 164.1 s | ~74.9 s |

**CUDA is ~1.7–1.8× slower** for this project. GPU was in use (36 % util, ~1 GB VRAM), so it is
the expected outcome for a small network (2×128 + LSTM 128) on vector observations: the per-step
CPU↔GPU transfer overhead outweighs any compute gain, and the real bottleneck is Unity stepping
physics. **Train on CPU** (`venv`). Keep `venv-cuda` only for experiments with visual observations
or much larger networks; otherwise it can be deleted. The larger speedups available are on the Unity
side: a standalone build with `--env <exe> --num-envs N`, and `--time-scale`.

Note: ML-Agents exits Play mode in the Editor automatically when the trainer disconnects.

### Parallel headless environments (standalone build) — 2026-08-31

Built `Dynamic_Scene` as a Windows player: `Builds\Prosthetic\Prosthetic.exe` (StandaloneWindows64,
0 errors; `/Builds/` is gitignored — rebuild with the `unity` MCP `build` tool or File ▸ Build after
scene/script changes). To make headless instances start cleanly, **Initialize XR on Startup** was
turned off for the Standalone group in `Assets/XR/XRGeneralSettings.asset` (`m_InitManagerOnStart: 0`;
its only Standalone loader was the deprecated Windows MR loader). Re-enable if a PC-VR standalone
build is ever needed.

Benchmarks (same `trainer_config.yaml`, i9-12900F 16C/24T, steady-state seconds per 10k steps):

| setup | per 10k steps | vs Editor |
|-------|--------------:|----------:|
| Editor, CPU torch                     | ~41.5 s | 1.0× |
| Editor, CUDA torch                    | ~74.9 s | 0.55× |
| build, `--num-envs 8`, CPU torch      | **~12.3 s** (10k @ 27.0 s, 30k @ 54.7 s) | **~3.4×** |
| build, `--num-envs 8`, CUDA torch     | ~34 s (30k @ 109.5 s) | 1.2× |
| build, `--num-envs 16`, CPU torch     | ~16 s (30k @ 57.6 s) | ~2.6× |

Conclusions: CPU torch + 8 headless envs is the fastest configuration; more envs don't help (the
single-threaded PPO update becomes the bottleneck); CUDA loses in every configuration for this
network. **Standard training command** (cmd or PowerShell, from the repo root):

```
C:\Users\chris\ml-agents\venv\Scripts\mlagents-learn.exe config\trainer_config.yaml --run-id=<id> --env Builds\Prosthetic\Prosthetic.exe --num-envs 8 --no-graphics
```
No Play button needed — the trainer launches the 8 players itself. Add `--resume` to continue a run.
Note: with `--num-envs` the Editor is not involved, so scene/script changes require a rebuild first.

### Watching a trained model in the Editor (inference) — 2026-09-01

**Bug fixed:** pressing Play with no trainer listening puts the agent in inference mode, and it threw
`InvalidOperationException: Tensor data cannot be read from, use .ReadbackAndClone()` every step. Cause:
`ArmAnimation ▸ Behavior Parameters ▸ Inference Device` was **ComputeShader** (GPU backend) and
ML-Agents 4.1.0 indexes tensors directly (`ApplierImpl.cs:47`, `ObservationWriter.cs:193`), which the
Inference Engine only allows for CPU tensors. Fix: Inference Device → **Default** (CPU/Burst) on both
`BehaviorParameters` in `Dynamic_Scene` (saved). Verified: 510 agent steps / 10 s, 0 errors, arm animates.

**Training does not update the Editor's model by itself.** The scene references the static asset
`Assets/Models/Prosthetic.onnx`; training writes `results\<run-id>\Prosthetic.onnx` (at each checkpoint —
`checkpoint_interval: 500000` — and when training stops with Ctrl+C). To make the Editor use a trained policy:

```
tools\deploy_model.cmd <run-id>
```
copies `results\<run-id>\Prosthetic.onnx` → `Assets/Models/Prosthetic.onnx` (previous kept as `.onnx.bak`,
gitignored), Unity reimports, then press Play to watch. Verified end-to-end with run 001's model
(exported by the `develop` trainer, loads and runs under package 4.1.0).

Workflow: `mlagents-learn … --env Builds\Prosthetic\Prosthetic.exe --num-envs 8 --no-graphics` → Ctrl+C when
done → `tools\deploy_model.cmd <run-id>` → Play in Editor. Do **not** press Play while `--env` training runs
(harmless, but the Editor just runs inference on the old model and is unrelated to the training).
