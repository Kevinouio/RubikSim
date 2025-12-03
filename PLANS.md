# Implementation Roadmap

Short, actionable steps to turn the design into a working Unity/WebGL Rubik’s Cube tutorial.

## Project setup
- Create Unity 3D project (URP optional), set up Git LFS if storing large assets, enforce .NET 4.x.
- Establish folder structure: `Scripts/Controllers`, `Scripts/Model`, `Scripts/UI`, `Prefabs/Pieces`, `Materials`, `Scenes`, `Tests`.
- Add baseline scene with lighting, camera, and placeholder UI canvas.

## Cube model and state
- Build cubelet prefabs (corner/edge/center) with colored materials; assemble a solved cube under `RubikCube` root.
- Implement logical state (`CubeState`) as facelet array; define face indexing and color mapping.
- Add move application on the model: 90° rotations with temporary parent pivot to avoid drift; lock input during animations.
- Keep 3D state and logical state in sync; add `IsSolved()` and basic state dump/debug helpers.

## Controls and camera
- Raycast from pointer to identify clicked cubie/face; map drag direction to face turns (quantized to 90°).
- Implement camera orbit + zoom (mouse/touch) without altering cube state; clamp zoom and rotation speeds.
- Add input gating (`isRotating`, `isScrambling`) to prevent overlapping moves.

## Core actions
- Scramble: generate 20–30 random moves without immediate inverses/repeats; disable input while running; show notation string.
- Reset: return to solved state (rebuild or apply stored inverse scramble).
- Manual move input: add keyboard shortcuts (U/D/L/R/F/B with primes/doubles) for testing.

## Solver (CFOP, likely 2-look OLL/PLL to start)
- Implement move parser `ApplyMoves("R U R' U'")` operating on `CubeState`.
- Phase functions: Cross, First Layer Corners, Second Layer edges, OLL (2-look), PLL (2-look); each returns move list.
- Detect last-layer cases via `CubeState`; store algorithm library for OLL/PLL patterns.
- Integrate solver with animation pipeline to step through generated moves.

## UI and tutorial layer
- UI Canvas: buttons (Shuffle, Reset, Solve, Step, Play/Pause), text panels for current algorithm/step, optional timer/move counter.
- Highlighting: glow/outline pieces involved in current step; face hover highlight for interaction feedback.
- Tutorial mode: advance-by-step with on-screen instructions; disable conflicting buttons during animations.
- Add loading/progress for WebGL build; ensure layout scales for desktop browser.

## Testing and tooling
- Editor playmode tests: verify `CubeState` rotations/inverses return to solved, scramble produces solvable state, solver solves sample scrambles.
- Manual QA scripts to run random scrambles + solver loops; log failures.
- Set up WebGL build profile; smoke-test in local browser for performance/input correctness.

## Stretch (after core is stable)
- Undo/redo move stack; custom scramble input; localization scaffolding; audio/FX polish; Add solvers for other cubes using other algorithms; mobile touch refinements.
