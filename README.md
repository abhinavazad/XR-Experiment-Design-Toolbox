# XR Experiment Design Toolbox — Scripts

Unity C# scripts for conducting within-subjects behavioural experiments across extended reality (XR) modalities (AR / MR / VR) in indoor environments.

## Supported Platforms

Scripts use preprocessor directives (`#if MAGICLEAP`, `#if OCULUS`, `#if UNITY_EDITOR`) to compile platform-specific input and spatial-anchor code for:

| Platform | Directive | Target |
|---|---|---|
| Magic Leap 2 | `MAGICLEAP` | ML2 OST-HMD |
| Meta Quest 3 | `OCULUS` | Quest 3 VST-HMD |
| Unity Editor | `UNITY_EDITOR` | Desktop preview |

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                    ButtonActivation                              │
│  XR mode selector (PR/AR/MR/VR) · global debug logger ·          │
│  UI panel toggling · passthrough control · log file I/O          │
└────────────────┬─────────────────────────────────────────────────┘
                 │ reads XRMode, saves logs to ExperimentManager.filePath
                 ▼
┌──────────────────────────────────────────────────────────────────┐
│                    ExperimentManager                             │
│  Experiment lifecycle (Start / Stop) · CSV tracking data ·       │
│  collision detection · particle effects · audio control ·        │
│  directory/file I/O setup                                        │
└───────┬──────────────────────┬───────────────────────────────────┘
        │ calls saveFileatStart│ calls saveFileatStop
        ▼                      ▼
┌─────────────────────────┐  ┌───────────────────┐
│ AnchorReinitiateManager │  │ ButtonActivation  │
│ Procrustes registration │  │ (log persistence) │
│ + scene alignment       │  └───────────────────┘
└──────────┬──────────────┘
           │ reads placeablePrefabs, anchor data
           ▼
┌──────────────────────────┐
│    AnchorUIManager       │
│ Spatial anchor CRUD ·    │
│ JSON serialisation ·     │
│ dropdown prefab picker · │
│ undo/redo stack          │
└──────────────────────────┘
```

## Core Scripts

### ExperimentManager.cs (872 lines)
Central orchestrator. Manages the full experiment lifecycle — start/stop toggling, per-trial directory creation, periodic CSV data capture (position, rotation, collisions at configurable Hz), audio cueing, particle-effect control, and experiment-summary file output.

### AnchorUIManager.cs (677 lines)
Handles real-time placement and persistence of spatial anchors. Provides a dropdown prefab selector, anchor placement via controller raycasting, JSON save/load of anchor configurations, and undo/delete operations. Singleton pattern.

### AnchorReinitiateManager.cs (790 lines)
Implements Procrustes-based scene re-registration. The experimenter places registration cubes on known physical markers; the script computes an optimal rigid-body (or similarity) transform to align the virtual scene to the physical environment. Saves pre- and post-registration anchor data plus alignment metrics.

### ButtonActivation.cs (386 lines)
Global UI controller. Manages the XR-mode dropdown (PR / AR / MR / VR), passthrough toggling (Quest 3), one-at-a-time panel switching, on/off bulk toggling of UI panels, and the global debug log capture with file output.

### FamiliaritySceneManager.cs (139 lines)
Handles scene transitions between the Familiarity walk-through scene and the Main Experiment scene. Retains participant ID across scenes and auto-triggers `ExperimentManager.ToggleStart()` upon arrival.

### ObjectInfoDisplayManager.cs (246 lines)
Debug/visualisation utility. Overlays real-time 3D TextMeshPro labels showing position and rotation above tagged GameObjects. Togglable at runtime via a UI toggle or keypress.

### MeasuringTape.cs (231 lines)
In-XR distance measurement tool. Three-press workflow on the controller: (1) spawn start sphere, (2) extrude line, (3) place end sphere and display distance label. Includes a clear/reset function.

### SceneReloader.cs (328 lines)
Editor and runtime utility to reload a scene layout from a JSON anchor file. Instantiates prefabs at saved positions/rotations under a designated parent. Includes a scene-save function (Editor only) and fallback pink-cube instantiation for missing prefabs. Used by `ReplayMovement`.

### PrefabPlacer.cs (109 lines)
Lightweight JSON-to-scene loader. Reads an anchor JSON file from an absolute path, deserialises prefab names with position and rotation data, and instantiates the matching prefabs at runtime. Shares the same serialisable data schema (`PrefabData`, `PositionData`, `RotationData`, `AnchorData`, `AnchorsData`) as SceneReloader but without the editor-save, coordinate-shift, or fallback-cube features — useful as a minimal standalone scene reconstructor.

### ReplayMovement.cs (115 lines)
Replays recorded participant trajectories from CSV data on an avatar GameObject. Parses elapsed time, position, and rotation, then interpolates movement frame-by-frame. Uses `SceneReloader.anchor0Pos/Rot` for coordinate re-alignment.

### trackingScript.cs (136 lines)
Standalone camera position/orientation logger. Writes timestamped CSV at 10 Hz and provides a 3D TextMeshPro display of live coordinates.

## Data Output Structure

```
<persistentDataPath>/Project/
└── YYYY-MM-DD/
    └── <XRMode>_Exp<ID>_<timestamp>/
        ├── <ID>_<XRMode>_Participant_Data_<ts>.csv       ← tracking data
        ├── Experiment_Summary_<ts>.txt                    ← summary metrics
        ├── <XRMode>_<anchor>_BeforeReg.json               ← pre-registration anchors
        ├── <XRMode>_<anchor>_PostReg.json                 ← post-registration anchors
        ├── <XRMode>_RegistrationData_<ts>.json            ← Procrustes alignment
        └── Full_DebugLogs_<ts>.txt                        ← runtime debug log
```

## Setup Checklist

1. Add scenes to **Build Settings** (Familiarity + Main scene must both be ticked).
2. Set the correct scripting define symbol in **Player Settings → Other Settings** (`MAGICLEAP` or `OCULUS`).
3. Assign Inspector references for each script (camera, colliders, UI elements, prefabs, layers, audio clips).
4. Configure Unity tags (E.g.`FireHazard`, `Start`, `Finish1`, `Finish2`, `Sign`, `RegistrationObjects`, `UI1`) and layers (`InvisibleLayer`, `InvisibleLayer2`, `InteractiveObjects`).
5. For scene reloading, place the anchor JSON file in the project and assign it in the `SceneReloader` Inspector field.
