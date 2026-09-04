# RubikSim

A local Unity/C# 3×3 simulator and CFOP tutor, embedded in an accessible static website. The implemented method is an aligned D cross, four corner/edge F2L pairs, two-look OLL and two-look PLL. Solving uses a fresh state snapshot, never the scramble history.

**Milestone status: incomplete pending the Web build and live-player browser checks.** Independent C# tests pass, including 100 seeded history-free solves and complete selected last-layer case domains. The real Unity Editor passed all 168 view/animation checks; both Editor and Web C# branches compiled against genuine Unity assemblies with zero warnings and errors. See [the evidence and remaining work](docs/STATUS.md). The website shows “Unity build needed” when the generated player is absent.

## Requirements and setup

- Unity **6000.0.68f1**, revision **e1e9baaf294b**, with Web Build Support and a valid activated license. This is an initial project pin, not a migration from an existing project; the original repository contained only planning documents.
- .NET SDK **8.0.419** for independent tests. The code is additionally compiled as C# 9 against .NET Standard 2.1, matching the selected Unity API profile.
- Python 3 for local HTTP serving. Node/npm and installed Edge are only needed for browser test tooling.

From PowerShell in this repository:

```powershell
# Installs only inside .tools, verifies Microsoft's published SHA-512, changes no global PATH.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bootstrap-dotnet.ps1

# C# 9 / netstandard2.1 compatibility build, fixtures/cases/session tests, then 100 solves.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1 -Count 100 -FirstSeed 0

# Short deterministic suite, explicitly omitting the seeded regression.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1 -Fast
```

Tests write `artifacts/test-results.log`, log each seed/outcome, and fail on any invalid replay, false phase boundary, unsolved final state or resource-limit result. Configure larger runs with `-Count` and `-FirstSeed`. No skipped Unity checks are counted as passes.

The current workspace already contains the verified Editor and Web support module at `.tools/unity-editor-extracted/Editor/Unity.exe`. The official packages were checked against Unity's published hashes and valid publisher signatures, then extracted locally without running their installers. These tools are ignored by Git; a fresh checkout needs its own Editor setup. [Package evidence](docs/evidence/unity-toolchain-2026-09-04.json) records their provenance.

An optional check compiles both conditional source branches against the real Editor and Web module assemblies without launching Unity:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity-api.ps1 -UnityPath .tools/unity-editor-extracted/Editor/Unity.exe
```

Pass your installed `Unity.exe` path instead on another machine. Logs are `artifacts/unity-api-editor.log` and `artifacts/unity-api-web.log`; this check does not run the engine, shaders, IL2CPP, or player.

## Build and run the interactive website

Install and activate the pinned Unity Editor first, then:

```powershell
# Actual Unity transform/animation/interruption verification.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-web.ps1 -VerifyViewOnly

# Produces website/unity/ and its generated loader manifest.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-web.ps1

# Keep this terminal running; open http://127.0.0.1:8080.
python scripts/serve.py --port 8080
```

For this workspace, add `-UnityPath .tools/unity-editor-extracted/Editor/Unity.exe` to each build command. For another nonstandard installation, pass that installation's `Unity.exe`. The build scripts check exact Editor version and fresh result evidence. Logs and measured build size go to `artifacts/`. The recorded [real Editor result](docs/evidence/unity-view-2026-09-04.json) passed 168 checks on 2026-09-04. Open `Assets/Scenes/Main.unity` for Editor play mode; the runtime creates the model and camera procedurally.

The server binds only to loopback. Builds are uncompressed, with `.wasm` served as `application/wasm`, `.data` as `application/octet-stream`, and JavaScript as `application/javascript`. Do not add compression headers to these files or open the site using `file://`. Managed threads are disabled; no isolation headers or backend are needed.

## Use

- Click/tap a sticker for its face turn, or drag across a sticker for a layer turn. Shift reverses a click. Right-drag/background drag orbits; wheel or pinch zooms; Reset view restores the camera.
- Use the face buttons or focus the canvas and press U R F D L B, M E S or X Y Z. Shift gives the inverse; Alt makes face turns wide. Camera movement never changes notation.
- Scramble uses a repeatable 25-move sequence from the displayed seed. It is not an official competition scramble or uniformly random state. Reset cancels pending turns; Undo/Redo restore exact committed snapshots.
- Enter spaced notation such as `R U R' U'`, `Rw2`, `M'` or `x`. Invalid batches do not partially apply.
- Export/import versioned JSON, or paint all 54 stickers in the face editor and validate them. Home letters/colors are U white, R red, F green, D yellow, L orange, B blue. Imports check cubie inventory, corner twist, edge flip, parity and proper center orientation.
- Solve current state enables playback only after the full plan and every phase boundary have been verified. Next/Previous, Play/Pause, speed and phase buttons use that plan. Any free-play edit invalidates it. Practice includes Sune and a T permutation case.
- State stays local. “Remember my cube” is opt-in browser storage; export a JSON snapshot for a portable copy. No account, database, telemetry or remote solver is used.

## Browser verification

With the local server running:

```powershell
npm.cmd ci --prefix tools/browser-check --ignore-scripts --no-audit --no-fund

# Requires a real Unity build. Missing player is a failed prerequisite, never a skipped pass.
npm.cmd test --prefix tools/browser-check

# Separately tests the HTML shell and missing-build behavior; does not verify a cube.
npm.cmd run test:shell --prefix tools/browser-check
```

Tests use headless installed Microsoft Edge by default. Set `RUBIKSIM_BROWSER=chrome` for installed Chrome, or `RUBIKSIM_URL` for another local server URL. No browser is installed or replaced by these commands. Test evidence and actual screenshots are written to `artifacts/`.

The live-player test exercises known turns, notation errors, mouse/touch/camera/keyboard, scramble, cancellation, 100 history-free imported seeded solves, phase jumps, backward/automatic playback, pause/speed, practice, editor validation, persistence and reset mid-animation. It checks reported facelets reconstructed from actual mesh transforms/materials, including animation endpoints before snapping. A source SHA-256 check rejects an outdated player. Performance gates cover raw Unity solving/playback frame samples, warm solve duration, WebAssembly heap and player download bytes. The browser result remains pending until the real-player run passes; the shell check is separate evidence.

## Project map

- `Assets/Scripts/Core`: exact states, moves, notation, reachability validation, serialization, geometry and module contract.
- `Assets/Scripts/Solver`: cooperative phase searches, complete selected algorithm library and immutable verified teaching plans.
- `Assets/Scripts/Application`: logical move queue, history, cancellation, state import and tutorial playback.
- `Assets/Scripts/Unity`: procedural view, picking/camera, animation, browser bridge and state/view audit.
- `Assets/Editor/BuildWeb.cs`: pinned build configuration and real-Editor verification entrypoints.
- `website`: static HTML/CSS/JavaScript; no framework or backend.
- `tests`: independent known-effect fixtures, exhaustive selected method cases and deterministic regressions.

Read [the scoped plan](docs/IMPLEMENTATION_PLAN.md), [support matrix](docs/SUPPORT_MATRIX.md), [core/notation conventions](docs/CORE_NOTES.md), [CFOP method](docs/CFOP.md), [Unity integration](docs/UNITY.md), and [sources/licenses](docs/SOURCES.md). The user-supplied [project specification](docs/PROJECT_SPEC.md) is preserved; this goal implements only its 3×3 milestone. Other puzzle modules have not been started.
