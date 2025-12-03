# RubikSim Scaffolding

This repository currently contains the starter scaffolding for the "Rubik’s Cube 3x3 Interactive Tutorial" project. The goal is to provide a clean Unity structure aligned with the design document so the real implementation can be filled in later.

## What’s Included

- Unity project folders (`Assets`, `Packages`, `ProjectSettings`) so the project opens in the editor without errors.
- Script folders for each subsystem mentioned in the doc (Core, Solver, Interactions, Tutorial, UI).
- Placeholder MonoBehaviours (e.g., `CubeController`, `CubeSolver`) that describe their intended roles but contain no logic yet.
- A short note in `Assets/Scripts/README.md` summarizing how to expand the scaffolding.

## Getting Started

1. Open the project in Unity 2022.3 LTS (or newer).
2. Create a new scene and drop the placeholder components where you need them.
3. Replace the placeholder scripts with real implementations when you begin building the cube model, controls, solver, UI, and tutorial systems described in the design doc.

## Next Steps

- Implement the core cube data model/animation under `Assets/Scripts/Core`.
- Flesh out `CubeSolver` with the CFOP phases (Cross, F2L, OLL, PLL) once the cube logic is ready.
- Add real input handling, tutorial sequencing, and UI wiring in their respective folders.

Until then, this repository serves as a lightweight starting point that mirrors the final architecture without committing to any specific implementation details.
