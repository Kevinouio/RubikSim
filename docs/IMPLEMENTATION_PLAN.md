# 3×3 implementation plan

Scope: the user's 3×3 milestone overrides the broader catalogue in PROJECT_SPEC.md. No other puzzle modules are being implemented.

## Initial gap assessment — 2026-09-04

The tracked repository contains README.md, PLANS.md, LICENSE and .gitignore. The user has added docs/PROJECT_SPEC.md; preserve it. Contrary to the old README, no Unity project, scripts, scenes, website or tests exist. No applicable AGENTS.md was found in the repository or parent directories. The original roadmap's separate corner and edge stages conflict with the specification's CFOP requirement; implement four F2L pairs instead.

The machine has .NET runtime 8.0.22 but no SDK, Unity Editor or Unity Hub. Use a repository-local .NET SDK to run independent C# tests. Unity compilation, Web build and live cube browser verification must remain unrun until an Editor with Web support and an activated license is available.

## Ordered work

1. Establish exact 3×3 state, cubie/facelet conversion, notation, validation, versioned snapshots and independent fixtures. Pin a supported Unity LTS patch and C# compatibility; add a local headless test toolchain.
2. Implement a history-free CFOP solver with real aligned-cross, four-pair F2L, OLL and PLL postconditions, bounded cooperative work, explicit outcomes and fully verified solution plans.
3. Implement application history and stale-plan handling, procedural Unity rendering, a single move queue, input/camera controls and tutorial playback.
4. Build accessible static HTML controls around the Unity Web canvas, face editing, local persistence and practice. Add reproducible build/serve scripts.
5. Run independent move/validation/application/case tests and at least 100 seeded history-free solves. Run Unity and actual Web browser checks when tooling permits. Record evidence, limits, performance and exact remaining commands in STATUS.md.

## Integration conventions

Shared C# lives under Assets/Scripts/Core and Assets/Scripts/Solver and has no Unity dependency. Use namespace RubikSim.Core and RubikSim.Solver, C# 9 or earlier and .NET Standard 2.1 APIs. Face order URFDLB. Cubie order URF,UFL,ULB,UBR,DFR,DLF,DBL,DRB; edges UR,UF,UL,UB,DR,DF,DL,DB,FR,FL,BL,BR. Logical coordinates +X right, +Y up, +Z front. Fixed home colors U white, R red, F green, D yellow, L orange, B blue.

Required core API: CubeState.Solved(), Clone(), Apply(Move), Apply(IEnumerable<Move>), IsSolved, ToFacelets(), static FromFacelets(string), Serialize(), static Deserialize(string); public copied cubie arrays CP/CO/EP/EO; Move.ParseSequence(string), Inverse(), ToString(), Face (char), Turns (1/2/3); CubeState carries center orientation for rotations/slices. Any detailed changes must be communicated between implementers before integration.

Solver API will expose a cooperative job whose Advance(workBudget) returns control without threads, a source snapshot/version, structured steps and explicit completion outcomes. Session/UI integration follows the implemented public API.

## Implementation checkpoint

The exact core, genuine CFOP solver, session logic, procedural Unity integration, static website and maintained build/test scripts are implemented. The final independent regression passes all 100 seeds and the documented method cases. The HTML shell is verified in Edge. Steps requiring real Unity import, rendering, Web compilation and actual-player browser verification remain blocked by the absent Editor; STATUS.md contains the evidence and exact next commands. This is not milestone completion, and no additional puzzle module has been started.
