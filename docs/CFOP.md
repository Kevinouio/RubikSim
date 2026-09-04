# 3×3 CFOP solver and teaching contract

The implemented method is **an aligned D cross, four corner/edge F2L pairs, two-look OLL, then two-look PLL**. The D-center color is the first-layer color, and the U-center color is the last-layer color. The fixed default D color is yellow; this is a valid CFOP orientation. Cube rotations or slices can change the present center colors. Camera orbit never changes move names.

Method reference: [J Perm's CFOP tutorial](https://jperm.net/3x3/cfop), checked 2026-09-04. Algorithm references: [two-look OLL](https://jperm.net/algs/2look/oll) and [two-look PLL](https://jperm.net/algs/2look/pll). Functional move sequences were checked against the author's corresponding trainer data at `/lib/2lookoll.js` and `/lib/2lookpll.js` on that date. The implementation, search, recognition descriptions and explanations are original. No tutorial prose, artwork, trainer code or scramble database is redistributed.

## Actual phase boundaries

1. **Cross**: DR, DF, DL and DB occupy their home positions with zero flip. A same-colored cross without matching side centers does not pass.
2. **F2L pair 1**: DFR + FR solved.
3. **F2L pair 2**: DLF + FL solved, retaining pair 1.
4. **F2L pair 3**: DBL + BL solved, retaining pairs 1–2.
5. **F2L pair 4**: DRB + BR solved, retaining pairs 1–3.
6. **OLL edges**: all four U edges oriented, all F2L pieces still solved.
7. **OLL corners**: all four U corners also oriented, all F2L pieces still solved.
8. **PLL corners**: U corners in exact home positions, with U still oriented and F2L solved.
9. **PLL edges**: all six faces solved and aligned with their present centers.

Zero-move phases explicitly report already achieved goals. An already-solved source returns `AlreadySolved` with an empty verified plan. Earlier work may be disturbed temporarily *inside* an algorithm. Postconditions are checked at the advertised boundaries, after any displayed alignment moves. No optimization crosses teaching boundaries.

## Cross and F2L are phase searches

The cross uses an exact breadth-first distance table for only four named edges: 12 × 11 × 10 × 9 positions × 2⁴ orientations = **190,080 states**. It descends that distance using legal face turns. Untracked corners and edges are not solved by this search.

Each F2L search tracks **one target corner and its matching edge together**. Allowed actions are U/U2/U′ and the conjugates `S U^n S′`, where S is a clockwise or counterclockwise R/F/L/B turn and n is 1, 2 or 3. These are ordinary slot extraction/insertion triggers. Only triggers preserving all previously completed pairs are allowed; every trigger preserves the cross at its boundary. A small reverse breadth-first search finds the sequence for the current pair location and orientation. Thus the first three pair boundaries are actual solved pairs, rather than relabeled separate corner and edge stages. The final pair search does not orient or permute the last layer.

The tested abstract F2L domains have respectively 384, 294, 216 and 150 states, totaling **1,044**. States place the current target pieces outside previously fixed slots, with arbitrary legal orientation. Tests compensate orientation/parity with untracked U pieces, solve a fresh serialized state and inspect exact pair and cross postconditions.

There is no scramble input to the solver and no move-history dependency. There is no whole-puzzle search or unrelated fallback.

## Complete two-look last layer

The library in `Assets/Scripts/Solver/AlgorithmLibrary.cs` contains:

| Look | Cases |
| --- | --- |
| OLL edges | I-shape, L-shape, Dot |
| OLL corners | H, Pi, U, T, L, Antisune, Sune |
| PLL corners | Headlights / T permutation, Diagonal / Y permutation |
| PLL edges | H, Z, Ua, Ub permutations |

Matching tries the four legal U setups and, for PLL, the four final U alignments. It selects an algorithm only if its resulting exact state meets that look's postcondition and preserves earlier phases. Corner/edge positions and orientations in the recognition text refer to the actual before-state. The setup moves place that pattern in the published algorithm's orientation. This also handles U offsets in the source algorithms: the published Z sequence, for example, requires a final alignment, which is shown in `AlignmentMoves`.

All 216 legal last-layer orientation patterns (27 corner × 8 edge) are tested, as are all 288 oriented last-layer permutations with matched parity. Every library algorithm is tested with four U setups. These are complete case domains for their respective looks; they are not a claim of enumerating every full 3×3 state.

Notation is standard clockwise-as-viewed-at-the-face URFDLB, prime and `2`. The library additionally uses lowercase `r` and `f` as two-layer wide turns, and M in the L direction. Source parentheses that only group triggers were removed without changing moves. No algorithm requires parser grouping syntax. Physical centers are restored at each library boundary. The core also supports the user's documented slices, wide moves and rotations.

## Immutable plans, outcomes and cooperative work

`CfopSolver.CreateJob(state, version, maxWork)` clones the source. The overload accepting a serialized snapshot distinguishes invalid and unsupported imports. `SolverJob.Advance(workBudget)` performs bounded work and returns after at most that many iterator work units or approximately 4 ms, checked between units. A unit is a table expansion, small candidate evaluation or other fixed-size operation. `Cancel()` returns `Cancelled`, and exhaustion returns `ResourceLimit`; neither enables a partial plan. A resource limit never asserts that the state is impossible.

All substantial table building is included in these yielding units. No task pool, thread, native plugin, reflection or dynamic code generation is used by the solver. The application should call `Advance` on successive frames. Exact browser timings remain a separate Web-build verification gate; .NET console measurements are not browser measurements.

Every `SolutionStep` carries method, phase, case, goal, actual recognition, reference orientation, source URL, highlighted piece IDs, setup/main/alignment moves, before/after snapshots and its postcondition. `SolutionPlan` independently replays every step with the core model, verifies the advertised boundaries and confirms the final solved condition before setting `Verified`. `Matches(state, version)` rejects stale plans, including a version change that returns to identical facelets. Backward playback is covered by inverse replay to the exact source snapshot.

## Reproducible table data and checks

The cross table is generated deterministically in memory on the first nonsolved job and cached only after completion and verification. There is no downloadable or platform-specific table file. Its version is `aligned-D-edges-v1-URFDLB-pos2flip-depth-plus-one`. The 24⁴-byte array stores depth plus one; impossible overlapping-piece entries are zero. The complete byte array has **FNV-1a 32-bit checksum `1CDF2115`**, checked before publication. Changing coordinate conventions requires an explicit version/integrity review.

Retained cross data is 331,776 bytes. The transient BFS queue has 190,080 32-bit entries (760,320 bytes). Each F2L search has at most 576 table entries and a small trigger library. Last-layer recognition is a finite catalog scan. The default work limit is 2,000,000 yielding units, comfortably above the measured roughly 191,000-unit cold table/solve path. No filesystem cache, external solver service or scramble history is needed.

`tests/SolverTests.cs` exposes `Run()` and `RunRegression(count, firstSeed)`. The fast suite covers outcomes, cancellation during table construction, immutable snapshots, stale-plan checks, every algorithm/case domain above, independently published superflip facelets, and states containing rotations, slices and wide moves. The regression creates a 25-move deterministic random-move scramble, serializes it, imports a fresh state, solves, checks legal replay and each real phase boundary, and inversely replays the entire plan. It logs every seed/outcome and median, p95 and maximum desktop timing. These scrambles are not uniformly random states or official competition scrambles.

Actual delivered-command results and any Unity/Web blockers are recorded in `docs/STATUS.md` by the milestone integration run.
