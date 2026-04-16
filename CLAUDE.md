# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Unity 6 (6000.0.38f1) game simulating a race track photographer. The player controls a free-look camera with realistic physical camera simulation (aperture, shutter, ISO, AF, lenses) while AI-controlled motorcycles race around a spline-based track.

**Render pipeline:** HDRP 17.0.3
**Key external plugin:** Curvy Splines (`Assets/Plugins/Curvy/`) — wraps `CurvySpline` for all track path math.

## Design Documents

開發特定系統功能前，**必須先閱讀對應的設計文件**理解需求與已完成進度；新增或修改功能後，**必須同步更新設計文件**（狀態、變更紀錄等）。

| 系統 | 設計文件路徑 |
|------|-------------|
| BikeAI MotoGP 仿真 | `Assets/Core/Moto/Runtime/BikeAI/BikeAI_MotoGP_Design.md` |

## Coding Principles

- **註解語言**：所有 C# 程式碼內的註解一律使用繁體中文。
- **功能完成後提供測試步驟**：每完成一項功能或修改，必須在回覆中附上具體的 Unity Editor 操作步驟，說明如何驗證該功能正常運作。

## Unity Compile and Build Validation

Unity Editor compilation is the final source of truth for this project.

When modifying any C# scripts:
- `dotnet build` may be used to catch basic syntax or reference errors, but a passing build does **not** guarantee the code will compile correctly in Unity Editor.
- Always prioritize Unity compatibility, existing project conventions, and minimal changes.

**Safety rules — do not do the following unless explicitly requested:**
- Introduce APIs, packages, or namespaces not already used in the project.
- Modify asmdef files, `Packages/manifest.json`, or `ProjectSettings`.
- Rename classes, files, namespaces, MonoBehaviour types, ScriptableObject types, or serialized fields.
- Change public method signatures, event contracts, or existing API contracts.
- Remove or alter Unity lifecycle methods or existing behavior.
- Make unrelated refactors or cleanup — prefer minimal, localized edits.

**Before finalizing any change, self-check for:**
- Missing or incorrect `using` statements
- Invalid Unity API usage
- Type or namespace mismatches
- Broken references caused by renames
- Serialized field compatibility issues
- Access modifier issues

## Development Workflow

This is a Unity project. All building, running, and testing is done through the Unity Editor — there are no CLI build or test commands. Open the project in Unity 6000.0.38f1.

**Main scenes:**
- `Assets/Core/Moto/BikeAI.unity` — AI racing test scene
- `Assets/Race_PIT+LANE/Race_Lane.unity` — main race lane scene

## Code Architecture

All custom source lives under `Assets/Core/`. There are no `.asmdef` files; all scripts compile into `Assembly-CSharp`.

### BikeAI System (`Assets/Core/Moto/Runtime/BikeAI/`)

Three-layer architecture:

1. **`BikeAITrafficManager`** (singleton) — global registry of all active agents. Sorts agents by `DistanceOnTrack` each `LateUpdate`. Provides `GetFrontAgent()` and `IsOffsetBlocked()` for inter-agent queries.

2. **`BikeAITrackAgent`** — the per-bike `MonoBehaviour`. Owns movement state (`distanceOnTrack`, `currentSpeed`, `currentOffset`) and advances along the spline each `Update`. Calls `strategy.Tick()` at a LOD-throttled interval (full rate when within `nearUpdateDistance` of the camera, else every `farUpdateInterval` seconds). Self-registers/unregisters with `BikeAITrafficManager` on enable/disable.

3. **`BikeAIStrategy_Follow`** — stateless strategy component (separate GameObject). Called by the agent's `Tick()`. Implements: curve look-ahead slowdown, follow-distance speed matching, and a 4-state overtake state machine (`None → Preparing → Overtaking → Returning`). Sets speed/offset on the agent via `SetTargetSpeed` / `SetTargetOffset`.

Track geometry is queried through **`TrackSplineProvider`**, which wraps a Curvy `CurvySpline`. All external callers use world-distance (meters) rather than Curvy's internal TF (0–1 normalized). **`TrackWidthProfile`** provides keyframed left/right track width for clamping lateral offsets.

### Photography System (`Assets/Core/`)

**`TrackPhotoRig`** (`[ExecuteAlways]`) — the core camera simulation. Manages:
- Two swappable `ZoomLens` presets (24-70mm / 100-400mm), switched with `1`/`2`, scroll-wheel zoom
- Physical camera properties written directly to Unity's `Camera` component each frame
- AF-C with subject velocity prediction (`afLeadSec`)
- Panning assist (soft-lock toward `subject`)
- Handheld shake via dual-frequency Perlin noise
- Shutter priority auto-ISO metering from `mainLight.intensity` via `luxToEV100` curve
- Virtual ND filter (reduces exposure compensation)
- Drive modes: Single / ContinuousLow / ContinuousHigh / Custom
- Rolling shutter approximation via projection matrix shear
- IMGUI viewfinder overlay (rule-of-thirds, center cross, horizon, safe frame, mirror blackout)
- Fires `OnShutter` event for capture pipeline

**Photo capture pipeline:**
```
TrackPhotoRig.OnShutter
  → PhotoCaptureManager.HandleShutter()
      → CopyColorAfterPost.RequestCapture()   ← schedules next-frame blit
      → Coroutine polls CopyColorAfterPost.lastCapturedFrame
          → AsyncGPUReadback from RTHandle
              → Texture2D
                  → OnPhotoReady callback
                      → PhotoPrintSpawner.SpawnPhoto()  ← spawns world-space Quad
```

**`CopyColorAfterPost`** is an HDRP `CustomPass`. It **must** be placed in a `CustomPassVolume` with `Injection Point = After Post Process` on the same camera as `PhotoCaptureManager`. It blits `cameraColorBuffer` into a pre-allocated `RTHandle`.

**`PhotoRigController`** — keyboard input layer for all rig parameters. Uses reflection to call the private `SwitchLens()` method on `TrackPhotoRig`.

**`PhotoFreeCam`** — free-look WASD camera. Temporarily disables `panAssist` while the user moves the mouse/camera, then re-enables it after `panAssistResumeDelay`.

**`TrackPhotoHUD`** — IMGUI bottom bar displaying shutter speed, aperture, exposure comp, and focal length.

## Key Design Constraints

- `CopyColorAfterPost` uses static fields for the capture request. Only one capture can be in flight at a time.
- `TrackPhotoRig` is `[ExecuteAlways]` — its `OnValidate` and `OnEnable` run in the Editor. Avoid side-effecting operations that shouldn't run outside Play mode.
- `TrackSplineProvider.IsValid()` must be checked before any spline query — Curvy splines may not be initialized at Start.
- Offset clamping (`SetTargetOffset`) goes through `TrackSplineProvider.ClampOffset()` which delegates to `TrackWidthProfile`; skip `TrackWidthProfile` on an agent and offsets are unclamped.
