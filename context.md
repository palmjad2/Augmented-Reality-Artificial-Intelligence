# Session context — handoff for next Claude Code session

**Written:** 2026-08-31 (end of session)
**Project:** `C:\Users\chris\Augmented-Reality-Artificial-Intelligence` — Unity **6000.3.18f1**, URP, ML-Agents (prosthetic arm grasp agents), OpenXR / XR Simulation. Branch `main`.

Read this first, then skim `MLAGENTS_UPGRADE.md` (addendum at bottom) if you touch ML-Agents.

---

## 1. What was done this session

### Unity CLI (installed, working)
- `winget install Unity.CLI` → **Unity CLI 1.0.0-beta.6**, on PATH as `unity`
  (real exe: `%LOCALAPPDATA%\Microsoft\WindowsApps\unity.exe`).
- `unity doctor` passes. Sees the one installed editor: `6000.3.18f1` at
  `C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Unity.exe`.
- **Not signed in** (`unity auth status`). Personal license works without it. `unity auth login`
  is a browser flow — the user must run it themselves (`! unity auth login`) if cloud features are needed.

### Unity MCP integration (installed, verified end-to-end)
- Route chosen: the CLI's own MCP server (`unity mcp`) + the official **`com.unity.pipeline@0.5.0-exp.1`**
  package in the project (added via `unity pipeline install`). *Not* the `com.unity.ai.assistant` relay route.
- Claude Code config: **project-scoped `.mcp.json`** in repo root:
  ```json
  { "mcpServers": { "unity": { "type": "stdio", "command": "unity",
      "args": ["mcp", "--project-path", "C:\\Users\\chris\\Augmented-Reality-Artificial-Intelligence"] } } }
  ```
  On first start Claude Code shows it as **"Pending approval"** — approve it (prompt on startup, or `/mcp`).
  After approval the `unity` server exposes ~142 tools: `get_scene_hierarchy`, `find_gameobjects`,
  `get/set_component_properties`, `create_script`, `write_text_file`, `eval` (Roslyn C# in-Editor),
  `run_tests`/`list_tests`, `build`, `package_add/remove/resolve`, `get_console_logs`, `recompile`/
  `recompile_status`, `editor_play/pause/stop`, `capture_scene_view`/`capture_game_view`, `menu`, etc.
- Verified: raw MCP `initialize` + `tools/list` against `unity mcp` succeeded (server name `unity-mcp 1.0.0-beta.6`).
- CLI equivalents without MCP: `unity status` (connected editors), `unity pipeline list`,
  `unity command <tool> [args]` (e.g. `unity command recompile_status --json`), `unity test`, `unity build`.

### ML-Agents / Sentis compile errors (fixed)
- Root cause: Unity 6000.3 ships `com.unity.sentis` only as a **built-in shim** (`"type":"shim"`) over
  `com.unity.ai.inference` (Inference Engine, namespace `Unity.InferenceEngine`). It overrode the manifest's
  `com.unity.sentis: 2.1.1` pin, so ML-Agents **3.0.0** (`using Unity.Sentis`) threw 657 CS0234/CS0246 errors
  inside the package. That state also hung the Editor at the "Opening project…" splash.
- Fix (manifest only): `com.unity.ml-agents` **3.0.0 → 4.1.0**; removed the `com.unity.sentis` pin.
  Resolved: `com.unity.ml-agents@4.1.0` + `com.unity.ai.inference@2.6.1`.
- Verified via MCP bridge: `recompile_status` → `completed, failed=false, errors=[]`; 0 `error CS` in Editor.log.
- The three Agent scripts (`Assets/Scripts/ArmGraspAgent.cs`, `ArmGraspAgentCopy.cs`, `CSVArmGraspAgent.cs`)
  needed **no changes** (they only use `Unity.MLAgents`, `.Sensors`, `.Actuators`).

---

## 2. Current state of the machine / repo

- **Unity Editor is running** with this project open (launched via `unity open`, started ~4:21 PM Aug 31).
  Pipeline server on port 7800, `unity status` = ready. Console: 2 warnings (Input Manager deprecation,
  URP Global Settings created) + 1 pre-existing error "Blender could not be found" (a `.blend` asset, no Blender installed).
- **Nothing was committed.** Uncommitted working tree includes many changes that pre-date this session
  (Unity 6 / URP / OpenXR migration, scene edits, package upgrades) plus this session's:
  - `Packages/manifest.json` (+`com.unity.pipeline`, ml-agents 4.1.0, −sentis) and `Packages/packages-lock.json`
  - new `.mcp.json` (should be committed — it's the shared MCP config)
  - `MLAGENTS_UPGRADE.md` addendum, this `context.md`
- Tooling present: Node 24.16, Python 3.14.6 (system; **no `mlagents` installed**), dotnet, winget, Unity Hub 3.18.3.
- Persistent memory for this project already has a note: `unity-cli-mcp-setup` (in the Claude memory dir).

---

## 3. Gotchas learned (don't relearn these)

- The MCP server / `unity command` only work while the Editor has the project open. Check `unity status`
  first; launch with `unity open <repo>`. **Never launch a second instance** while one is open — it errors out.
- Right after launch the bridge returns `503 Server Busy` until import/compile settles (~1 min). Poll
  `unity status --json` for `"state":"ready"` and retry.
- `unity command list` is **not** valid — omit the command name to list tools: `unity command --project-path <repo>`.
- A project that has compile errors on load can hang the Editor at the splash screen forever (Burst can't
  resolve `Assembly-CSharp`; no dialog appears). Symptom: title "Opening project…", CPU flat. Kill it, fix
  the errors, relaunch — waiting does nothing.
- Don't re-add `com.unity.sentis` to the manifest; on 6000.3 it's a shim. ML-Agents ≥4.0 wants
  `com.unity.ai.inference`.
- `.gitignore` ignores `*.vscode/` and `*.exe`; `.mcp.json` is not ignored.

---

## 4. Open items / suggested next steps

1. ~~**Unity skill**~~ **RESOLVED (Aug 31, 2nd session).** The "18-skill Unity pack" is Unity Technologies'
   official **`unity-agent-plugin`** (plugin name `unity`). The user had installed it only in the **Claude desktop
   app** (v0.1.0-beta, 18 skills; cached under `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\
   local-agent-mode-sessions\...\rpm\plugin_014AH5.../skills`), which Claude Code never reads. Registered for Claude
   Code via CLI: `claude plugin marketplace add Unity-Technologies/unity-agent-plugin` then
   `claude plugin install unity@unity-agent-plugin` → **v0.1.1-beta, scope: user, enabled, 29 skills** (repo grew
   since 0.1.0). Install path `~/.claude/plugins/cache/unity-agent-plugin/unity/0.1.1-beta/skills/`. Skills:
   2d-pixel-perfect, audio-setup-mixers, build-live-game, implement-in-app-purchases, initialize-ai-navigation,
   levelplay-unity-integration, localization, manage-sprite-atlas, new-unity-project, optimize-audio,
   optimize-text-mesh-pro, optimize-web, physics-3d-collision, setup-multiplayer-services, setup-vivox-voice-chat,
   shader-graph-create-custom-node, sprite-editor, sprite-segment-3x3grid, tilemap-palette-create,
   tilemap-ruletile-createempty, tilemap-ruletile-createfromsegment, ui, ui-imgui, ui-ugui, ui-uitk, unity-cli,
   unity-package-management, urp-postprocessing, validate-urp-render-graph-renderer-feature.
   They show up as `unity:<skill>` after a Claude Code **restart** (skills load at session start).
   Update later with `claude plugin update unity@unity-agent-plugin`.
2. ~~**Approve the `unity` MCP server**~~ **DONE** — approved; `editor_status` → ready, 6000.3.18f1, project open.
3. **Python trainer**: install the `mlagents` release that pairs with package **4.1.0** (4.1.0 moved
   gym→gymnasium, torch ~2.8). The package's Installation page still shows `mlagents==1.1.0`, which is the
   older 3.0.0-era pairing — confirm the right version in the ML-Agents GitHub release history before training.
   `config/trainer_config.yaml` (behavior `Prosthetic`, PPO) needs no change.
4. Optional cleanup: `com.unity.ide.vscode` is deprecated (Editor warning); the Input Manager deprecation
   warning suggests eventually moving to the Input System package (already in manifest, 1.11.2).
5. Consider committing this session's config changes (`.mcp.json`, manifest/lock, docs) separately from the
   large pre-existing migration diff.
