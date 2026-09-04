# Unity 3×3 view and Web build

The project pins Unity **6000.0.68f1**, changeset **e1e9baaf294b**. Install that exact Editor with **Web Build Support** and activate an eligible Unity license. This repository had no existing Unity project to migrate. The choice was checked against the [official release page](https://unity.com/releases/editor/whats-new/6000.0.68f1) on 2026-09-04. The C# source uses Unity 6.0's [C# 9 compiler](https://docs.unity3d.com/6000.0/Documentation/Manual/csharp-compiler.html), avoids records/init-only setters, and targets .NET Standard 2.1 APIs. Only the Editor's built-in physics and JSON modules are dependencies.

## Open, verify, and build

Open the repository root as an existing Unity project. Open `Assets/Scenes/Main.unity`, choose **RubikSim → Configure 3x3 Web project**, and enter Play mode. The scene's `RubikBridge` component constructs the cube, camera, inputs, and session at runtime. `EnsureHost` also bootstraps an empty scene in Play mode. The website provides the accessible controls and teaching text; the Editor canvas supports keyboard and pointer interaction.

From PowerShell in the repository root, after installing and activating the Editor:

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.0.68f1\Editor\Unity.exe'
New-Item -ItemType Directory -Force -Path artifacts | Out-Null
& $unityEditor -batchmode -quit -projectPath (Get-Location).Path -buildTarget WebGL -executeMethod RubikSim.Editor.BuildWeb.VerifyView -logFile artifacts/unity-view.log
& $unityEditor -batchmode -quit -projectPath (Get-Location).Path -buildTarget WebGL -executeMethod RubikSim.Editor.BuildWeb.Build -logFile artifacts/unity-build.log
```

The current workspace has the verified Editor and Web module at `.tools/unity-editor-extracted/Editor/Unity.exe`; set `$unityEditor` to that path to use it. The packages and extraction checks are recorded in [toolchain evidence](evidence/unity-toolchain-2026-09-04.json). They are local tooling, ignored by Git. For automated runs, prefer `scripts/build-web.ps1 -UnityPath $unityEditor`, with `-VerifyViewOnly` for the geometry checks: the wrapper waits for completion and rejects missing or stale result files.

To compile both source branches against genuine Unity assemblies without starting the Editor, first run `scripts/bootstrap-dotnet.ps1`, then:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity-api.ps1 -UnityPath $unityEditor
```

The optional check compiles C# 9/.NET Standard 2.1 with `UNITY_EDITOR;UNITY_WEBGL` against Editor modules, then `UNITY_WEBGL` against the Web player's managed modules. It requires the pinned Editor plus Web Build Support and writes separate logs to `artifacts/unity-api-editor.log` and `artifacts/unity-api-web.log`. It does not invoke the engine, shaders, IL2CPP or browser.

Check each process's exit code and log. `VerifyView` writes `artifacts/unity-view-result.json` only after every check passes. It verifies reconstruction and the actual pre-snap animation endpoints for all face, slice, rotation, and wide turns with all three suffixes. It also exercises a queued turn, prevents a second commit while one is in flight, and checks undo, redo, and reset during partial animation. This is a real Editor geometry check; it does not replace browser screenshots, input tests, or shader visual inspection.

`Build` writes the real player to `website/unity/` and emits `build-manifest.json` containing loader, data, framework, and WebAssembly URLs relative to that directory. A successful build also writes `artifacts/unity-build-result.json` with bytes, duration, warnings, errors, Editor version and `sourceSha256`. The website reads the manifest rather than assuming Unity's generated filename. See the root README for the local server and browser-check commands. Generated player files and raw `artifacts/` output are ignored by Git; selected review records under `docs/evidence/` are preserved.

On **2026-09-04 at 20:48:47 UTC**, the real pinned Editor passed **168 view checks**, recorded in [the unchanged result JSON](evidence/unity-view-2026-09-04.json). Both real-assembly compilation branches also passed with zero warnings and errors. The Web build and live-player browser verification remain pending; `docs/STATUS.md` is the authoritative execution log. The Editor geometry result does not establish browser input, shader appearance or Web performance.

The browser checker recomputes `sourceSha256` from every file under `Assets/`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectVersion.txt`, and `ProjectSettings/EditorBuildSettings.asset`. It rejects a player with a different source fingerprint. Automatically serialized `ProjectSettings.asset` is excluded because Unity can rewrite it during builds; the configuration code in `Assets/Editor/BuildWeb.cs` is included.

## Exact rendering and input conventions

The fixed spatial frame is +X right, +Y up, +Z front, with faces ordered URFDLB. Stickers are arranged row by row as seen from outside each face. White=U, red=R, green=F, yellow=D, orange=L, blue=B. Every colored sticker carries its home-color letter. Separate floating letters label the fixed notation faces. Whole-cube notation rotations may move colors between those fixed faces; camera orbit only moves the camera.

`CubeView` builds 26 black cubies, 54 rounded sticker meshes, reusable color materials, and original geometric letter glyphs. There are no downloaded fonts, art, or third-party model assets. The small opaque shader is stored under `Resources` so the Web build includes it. The view does not infer state from transforms.

- Click a sticker for a clockwise turn of its fixed spatial face. Shift-click reverses it.
- Swipe a sticker to turn the selected outer or middle layer in the swipe direction. Picking is suspended during an existing animation; keyboard and website inputs can queue more moves.
- Right-drag or drag the background to orbit. Wheel zooms. One-finger background drag orbits; two-finger drag and pinch orbit/zoom. A one-finger sticker tap/swipe turns.
- Focus the canvas for keyboard moves: U R F D L B, M E S, and X Y Z (notation x y z). Shift gives the inverse; Alt makes a face turn wide. Press 0 to reset the camera.
- Space toggles playback, left/right arrows step backward/forward, and Escape pauses. Keyboard capture is limited to the focused canvas so typing in HTML forms does not turn the cube.

The application session has one move queue. `TryBeginMove` commits the exact logical move once, then the host animates the old visual state to that already committed target. At the endpoint it reads actual sticker positions, normals, and assigned color materials **before** reconstructing exact resting transforms. `animationAgrees` reports these pre-snap comparisons and remains false for the page lifetime if any comparison fails, so later turns cannot hide a failure. `viewFacelets` independently derives the resting facelet string from actual transforms and materials; `viewAgrees` compares it with the logical state. Transient views report an empty `viewFacelets` string. Reset/import/history actions cancel the visual interpolation and reconstruct the session snapshot. Pause allows the committed turn to finish, then starts no further playback turn.

Lesson highlights follow named physical pieces in the current state, accounting for center-color changes. The active turn also highlights its layer and displays its axis. Explanation and recognition text come from the solver's actual structured plan.

## Browser integration

The website sends:

```javascript
unityInstance.SendMessage('RubikBridge', 'SendCommand', JSON.stringify({ action: 'notation', value: "R U R'" }));
```

The `.jslib` plugin dispatches `window` events named `rubik-state`; `event.detail` contains the snapshot. It has no external network calls. Actions are `notation`, `scramble` (seed), `reset`, `undo`, `redo`, `import` (versioned JSON), `solve`, `cancel`, `play`, `pause`, `next`, `previous`, `jump` (step index), `speed` (moves/second), `resetView`, `snapshot`, and `practice` (supported case ID). All `value` fields are strings. Invalid commands emit an error while retaining the session's valid state.

State fields include `facelets`, `serialized`, `version`, `viewFacelets`, `viewAgrees`, `animationAgrees`, `animating`, `pending`, `solving`, `playing`, `hasPlan`, `canUndo`, `canRedo`, `speed`, `cursor`, `moves`, `steps`, `activeStep`, `status`, and `error`. Every step includes the actual goal, recognition, explanation, source, orientation, before/after snapshots, setup/algorithm/alignment moves, and current highlighted sticker indices. `frameMs` is an exponentially smoothed unscaled frame interval; `solveMs` is the completed wall-clock solve duration. `solveSliceMs` and `maxSolveSliceMs` retain the last and maximum measured solver slice after completion. The JavaScript bridge adds `wasmHeapBytes` from the actual WebAssembly heap buffer.

`solveFrameSamplesMs` and `playbackFrameSamplesMs` contain raw frame intervals attributed to the preceding frame's work, including the final solve slice. Each buffer holds at most 256 samples until the next state publication, then clears; the browser checker accumulates the published samples for percentiles. State publication is normally throttled to 10 Hz, while forced command snapshots can publish sooner. These fields provide measurements when a real player runs; their existence is not a performance result.

For repeatable real input checks, `stickerTargets` reports resting visible sticker centers as `{index,x,y}` normalized from the canvas top left. The positions come from camera projection and are included only when a physics ray actually hits that sticker. Tests still send actual mouse/touch events to the canvas; this read-only geometry data does not turn or otherwise mutate the cube.

## Web choices and verification targets

The build uses WebGL 2, IL2CPP, the built-in render pipeline, no managed threads, and cooperative solver slices. The [Unity Web technical limitations](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-technical-overview.html) and [browser scripting integration](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-interactingwithbrowserscripting.html) were checked on 2026-09-04. C# `Task.Run`, dynamic code generation, a native plugin, and a browser worker are not used. Each host update permits up to 4,096 work items, with the solver's approximately 4 ms time boundary taking precedence.

The version-matched API references confirm [`WebGLMemoryGrowthMode.Geometric`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WebGLMemoryGrowthMode.html), [`WebGLExceptionSupport.FullWithStacktrace`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WebGLExceptionSupport.html), and setting [`GraphicsSettings.defaultRenderPipeline`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.GraphicsSettings-defaultRenderPipeline.html) to null for the built-in pipeline. The [Input documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/Input.html) identifies the legacy Input Manager as built in. This small project deliberately uses that pinned API for direct key, pointer, and touch polling, and configures `activeInputHandler=0`; no additional Input System package or virtual axis configuration is needed. Unity recommends the newer Input System for general new projects; adopting it later would be an explicit migration, not a hidden dependency.

Output compression is disabled for reproducible local serving. Serve `.wasm` as `application/wasm`, JavaScript as `application/javascript`, and `.data` as `application/octet-stream`. No `Content-Encoding` header is appropriate for this output. Web threads are disabled, so this build does not require SharedArrayBuffer isolation headers. Read the [Unity deployment guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html) before changing compression. Do not open the website with `file://`.

Initial WebAssembly heap is 128 MiB, grows geometrically, and is capped at 512 MiB. The renderer targets 60 frames/second and the solver yields after a work budget or approximately 4 ms. The browser checker enforces a 95th-percentile raw Unity frame interval below 33 ms during both solving and playback, completed seeded solves below 10 seconds at the 95th percentile after initialization, a WebAssembly heap within 512 MiB, and an uncompressed player download below 40 MiB. It runs 100 solves from independently generated imported snapshots and checks each returned plan's final phase replay. These gates have not yet passed in the real browser. Record the actual browser, hardware, seeds, median/tail timings, heap, and build download bytes in `STATUS.md`; tune based on measurements without weakening correctness gates.

The Editor/runtime sources in this document are original project code under the repository license. Unity remains separately licensed. Official Unity pages above are references, not copied code or artwork.
