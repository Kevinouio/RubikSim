# Scoped support matrix

Updated 2026-09-04. “Verified (headless)” means independent C# evidence, not a rendered Unity/Web release. The real Editor geometry checks now pass; release readiness still requires the live Web player checks.

| Puzzle | Simulation | State import and validation | Solver | Tutorial | Web verification |
| --- | --- | --- | --- | --- | --- |
| 3×3×3 | Exact moves verified (headless); 168 actual Unity geometry/interruption checks passed; live input pending | Verified (headless), full standard-3×3 reachability constraints and 24 proper center frames | Verified (headless), CFOP with four F2L pairs, two-look OLL/PLL; 100 seeds plus complete selected case domains | Structured steps and session playback verified (headless); Unity highlights/HTML live playback implemented/unverified | HTML shell verified in Edge; real Web build in progress, live player checks unrun |

All other core/expansion/future puzzles in PROJECT_SPEC.md are outside this goal and have no implementations or selector entries. This table does not imply broad catalogue completion or coverage of every twisty puzzle.

The normal selector has only a clearly marked 3×3 development entry. Its label explicitly states that Web verification is pending. No rendered mesh, passing empty-state test or unrun test is counted as release support.
