# 3×3 milestone status

**2026-09-04: milestone NOT complete. The first actual Web build succeeded; real browser checks found input and visual bugs, now being fixed.** No other puzzle module was started. The user-provided PROJECT_SPEC.md is preserved, and the user's narrower 3×3 goal controls scope. The previous missing-Editor blocker has been resolved with verified local package extraction and the user's Hub setup. A full browser pass is still required.

## What exists now

- Exact, independent C# state with integer geometry; all face, inverse, double, wide, slice and whole-cube turns; notation errors rejected atomically.
- Full standard-3×3 import validation: piece inventory, color/center frame, corner twist, edge flip and permutation parity. Versioned JSON snapshots have no history dependency.
- CFOP solver with an aligned D cross, **four actual corner/edge F2L pairs**, complete two-look OLL and PLL, explicit setups/final alignment, recognition and source references. No scramble reversal, unrelated whole-puzzle fallback or relabeled corners-first solution.
- Cooperative solver work including table generation, cancellation/resource outcomes, immutable source state/version, full replay/phase verification before playback. Deterministic cross table checksum `1CDF2115`.
- Session-owned move queue, exact commit at animation start, reset, undo/redo, invalid-edit atomicity, stale-plan rejection, forward/backward playback, pause/speed/phase jump and supported practice cases.
- Procedural Unity cube, labeled stickers/faces, active-piece/layer/axis highlights, pointer/touch/keyboard moves, orbit/zoom, browser bridge and independent transform/material audits before endpoint snapping. **168 actual Editor geometry/interruption checks passed; browser interaction remains unverified.**
- Accessible static website with player loader, controls, face editor, JSON import/export/download, opt-in local persistence and explicit missing-build/error UI. No backend, account or remote state.
- Pinned Unity project, deterministic metadata, Web build and local serving scripts, C# compatibility/test harness and real-player browser checks.

## Acceptance evidence

| Required condition | Actual evidence | Status |
| --- | --- | --- |
| Exact turns, notation and legal state handling | 3,912 core assertions: all six independent move tables, F/R sticker bands, move laws, all center frames, invalid twist/flip/parity/inventory/reflection, parser and snapshot tests | Verified in independent C# |
| State-based CFOP teaching solver | 325,740 solver assertions: actual cross/pair/OLL/PLL boundaries checked independently through core cubie arrays; direct superflip fixture; rotated/slice/wide sources; immutable/cancelled/resource outcomes | Verified in independent C# |
| Complete selected method cases | All 16 algorithms at four U setups, all 216 legal OLL orientation patterns, all 288 parity-matched PLL permutations and all 1,044 F2L pair abstractions | Verified in independent C#; random coverage is not a proof of every complete cube state |
| At least 100 seeded history-free solves | Seeds 0–99, 25 random moves each → serialize → fresh import → solve → legal replay → each real phase postcondition → solved → full inverse replay | **100/100 solved; zero resource limits/failures** |
| History and tutorial playback | 347 session assertions: rapid queue/reset/undo/redo, in-flight interruption, invalid imports/notation, stale plans, distinct zero-move phase selection, previous/next, pause, speed, practice and retained solve timing | Verified in independent C# |
| Unity 3D model, turns and camera | Both real-assembly C# branches compile with 0 warnings/errors; actual Unity 6000.0.68f1 VerifyView passed all 168 snapshot, animation endpoint and interruption assertions | **Geometry verified in Unity; live camera/input pending** |
| Website embeds real Unity Web player | Actual Unity build succeeded at 20:50:48 UTC, 24,867,124 bytes, 92.795 s, 0 errors/warnings; browser loaded the real player and matched its current source fingerprint | **First build/load verified; fixes require rebuild** |
| Browser cube controls, playback, state/view agreement | First run passed load, independently specified R, malformed notation atomicity, undo/redo, keyboard, orbit and all 18 face buttons. Failed Shift-click after HTML focus; screenshot exposed mirrored glyphs/spatial handedness | **Failed; fixing and rerunning. Later checks not reached.** |
| HTML controls/layout/error handling | Edge 152.0.4191.62 checked desktop/mobile layout, 54-sticker editor, 18 move buttons, disabled controls when Unity is absent, no JavaScript errors | Verified for website shell only |
| Web responsiveness, heap and player download targets | Instrumentation and targets in UNITY.md; actual Web build/browser performance not available | **Unrun** |
| Setup/build/test documentation | README commands match delivered scripts and pinned versions; source/method/notation/support documents provided | Implemented; successful Unity commands remain unverified |

## Commands actually run

Run from `C:\Users\kevin\Desktop\GithubProjects\RubikSim` on 2026-09-04 unless otherwise noted. These results are observations, not expected outputs.

| Command | Result |
| --- | --- |
| `git status --short`, repository/parent instruction and file inspection | Initial tracked repository contained only README.md, PLANS.md, LICENSE, .gitignore; user-added PROJECT_SPEC.md preserved. No applicable AGENTS.md found. |
| `dotnet --info` | Global .NET runtime 8.0.22 present, **no SDK**. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bootstrap-dotnet.ps1` | Exit 0; SDK archive SHA-512 verified against Microsoft release metadata; local SDK **8.0.419**, bundled runtime 8.0.25. No global install/PATH changes. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1 -Count 100 -FirstSeed 0` | Exit 0. Latest run **20:38:50 UTC** after the solve timing fix: C# 9/netstandard2.1 compatibility build 0 warnings/errors; core 3,912, solver 325,740, session 347 assertions; **100/100 history-free solves passed**. |
| `.tools/dotnet/dotnet.exe run --project tests/RubikSim.Tests.csproj -c Release -- --count 0` | Exit 0 during session lifecycle review; seeded regression explicitly NOT RUN in that invocation, later run by the full command above. |
| `node --check website/app.js`; `node --check tools/browser-check/check.mjs` | Exit 0, JavaScript syntax valid. |
| `Get-Content -Raw Assets/Plugins/WebGL/RubikBrowser.jslib \| node --check` | Exit 0, bridge JavaScript syntax valid. |
| `python -m py_compile scripts/serve.py`; `python scripts/write-core-metadata.py` | Exit 0; server syntax checked and missing shared-asset metadata generated while preserving existing GUIDs. |
| `.tools/dotnet/dotnet.exe run --project .tools/unity-syntax/Syntax.csproj` | Exit 0 in temporary review harness: six Unity/Editor files parsed under C# 9, zero syntax errors. **Not a Unity API/engine compile.** Static scene/script GUID references also matched. |
| `npm.cmd install --prefix tools/browser-check --ignore-scripts --no-audit --no-fund` | Exit 0; two packages installed, Playwright pinned to 1.56.1; lockfile checked in. |
| `python scripts/serve.py --port 8080` | Server started successfully on **127.0.0.1:8080**, serving only website/. |
| `npm.cmd run test:shell --prefix tools/browser-check` | Exit 0. Final run **20:22:03 UTC**: five shell checks passed in actual headless Edge 152.0.4191.62; desktop 1440×1100 and mobile 390×844 screenshots captured; no horizontal overflow or browser JS errors. **No cube was rendered.** |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-web.ps1` | Exit **1** at explicit prerequisite: Unity.exe not found at the pinned path. No engine started and no build result exists. |
| `npm.cmd test --prefix tools/browser-check` | Exit **1** through npm (check.mjs requests exit 2): missing `website/unity/build-manifest.json`; live cube checks **NOT RUN**. |
| Unity command/path/default-directory and Windows uninstall-registry inspection | No registered Unity Editor/Hub or executable found. A valid license cannot be checked without the Editor. |
| `git diff --check` | Exit 0, no whitespace errors in tracked diff. Git reports existing Windows LF→CRLF conversion notices. |
| `git check-ignore --no-index --quiet -- <path>` for 14 representative paths; `git diff --check -- .gitignore` | Updated `.gitignore` at the user's request: all 6 generated-path ignore checks and all 8 required-project-file inclusion checks passed. Local Unity/SDK downloads, caches, Web output and test artifacts stay ignored; source metadata, settings, lockfiles and review evidence stay trackable. No files were deleted or untracked. |
| `python .tools/nsis-research/unpack_unity_nsisbi.py <official-installer> --expected-md5 <official-hash> --manifest <local-json> --output <workspace-staging>` | Both signed official packages verified before extraction. Editor: 46,383 files, 7,774,185,345 bytes, 180.2 s; Web: 14,517 files, 4,342,549,717 bytes, 59.7 s. Every payload CRC passed; sizes match official installed sizes. No installer was executed. See toolchain evidence for exact commands/hashes. An initial Editor catalog rejected an internal doubled separator; narrow safe normalization resolved it before extraction. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity-api.ps1 -UnityPath .tools/unity-editor-extracted/Editor/Unity.exe` | Exit 0: Editor and Web platform module compilations each 0 warnings/errors. Initial harness attempts incorrectly included a legacy monolithic assembly and used Editor modules for the Web branch; correcting the real module references resolved those harness failures. This command does not execute the engine. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-web.ps1 -UnityPath .tools/unity-editor-extracted/Editor/Unity.exe -VerifyViewOnly` | Exit 0; **actual Unity 6000.0.68f1 passed all 168 renderer/interruption assertions at 20:48:47 UTC**. Fresh result JSON and engine log exist. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-web.ps1 -UnityPath .tools/unity-editor-extracted/Editor/Unity.exe` | Exit 0; first actual Web build **Succeeded**, 0 errors/warnings, 24,867,124 bytes, 92.795 s, 20:50:48 UTC. Build source SHA256 `6fc4b4fc5d8924c3ac29511cc2060764464f185bbbcb04782af7af5ab04b78f7`. This predates the input/visual corrections now in progress. |
| `npm.cmd test --prefix tools/browser-check` after the first real build | Exit 1 at actual Shift-click assertion after 9 recorded checks passed. The canvas loses the held Shift key when focus comes from an HTML button; the bridge now records the click event's own modifier. Screenshot review also found mirrored letters and a reflected left/right frame; a rendering-coordinate fix and independent projection regression are in progress. A favicon 404 was also recorded and an original SVG favicon added. Full playback/100-browser-solve/performance checks were **not reached**. |

The in-app browser bootstrap returned “Browser is not available: iab”; after reading its troubleshooting guidance, testing used the installed Edge via local Playwright. No browser installation or replacement was performed.

Historical temporary probes for isolated core and solver work also passed; CORE_NOTES.md records their exact commands. The maintained integrated command above is authoritative for the delivered shared code. An early integrated compile before Program.cs existed failed for missing Main; completing the harness resolved that, and the final maintained build passes. No failure was converted to a skipped pass.

## Recorded evidence and performance

- [Final headless output, including every seed](evidence/headless-2026-09-04.log).
- [Actual shell browser results](evidence/browser-shell-2026-09-04.json).
- [Verified Unity package/toolchain provenance](evidence/unity-toolchain-2026-09-04.json).
- [Actual Unity Editor renderer checks](evidence/unity-view-2026-09-04.json).
- [First actual Web build](evidence/unity-first-build-2026-09-04.json).
- [First real browser run, including its failure](evidence/browser-first-run-2026-09-04.json).
- Local generated screenshots: `artifacts/website-shell-desktop.png`, `artifacts/website-shell-mobile.png`. They show the actual **missing Unity build** page, not a simulator screenshot.
- Full current runtime log: `artifacts/test-results.log`. Generated files and local tools are ignored by Git; the small evidence records above are checked in with the source changes.

Hardware: **Intel Core i9-14900K**, 32 logical CPUs, 34,047,066,112 bytes system RAM; Windows build **10.0.26200**; x64 .NET runtime **8.0.25**. Browser shell: Microsoft Edge **152.0.4191.62**.

Latest **warm desktop** 3×3 regression, seeds **0–99**, 25 moves each: median **0.372 ms**, p95 **0.745 ms**, maximum **1.163 ms**; maximum measured solver slice **1.160 ms**; mean solution **75.7 moves**. The cross table had already been initialized by the case suite; these numbers exclude cold generation and browser frame scheduling. Full requested test execution took **1.227 s**, peak process working set **56.8 MiB**. Retained managed-memory delta across the warm regression was 35,320 bytes, not a Unity heap measurement.

Cross cache is 331,776 bytes; the transient BFS queue is 760,320 bytes. Tables regenerate deterministically, yield during construction, and are cached only after size and checksum verification. No binary table download is required.

Actual Unity frame time, Web solve median/tail, WebAssembly heap and download size are **unmeasured**. Targets remain p95 frame interval <33 ms, p95 seeded solve <10 s after initialization, heap ≤512 MiB and uncompressed player <40 MiB. They must be measured and tuned on the real build; desktop timings do not satisfy those gates.

## Exact blocker and next steps

Current resumed work: verified official Editor and Web packages have been fully extracted to ignored `.tools/unity-editor-extracted/Editor`; both API branches compile and the actual Editor renderer check passes. The user handled Hub setup and the engine is able to run. The real Web player build is running. A browser audit also identified missing final-slice timing: the session now retains final/maximum timings, tested by five additional assertions, and the Web bridge reports actual heap bytes and categorized raw Unity frame intervals. Visible sticker projections support genuine pointer checks. Expanded browser checks cover 100 imported snapshots and reject stale source fingerprints; they remain **unrun** until the player exists.

Continuation review on 2026-09-04 rechecked the working tree and missing Editor. The build wrapper again exited 1 before starting an engine. Independent source review found and fixed a genuine zero-move lesson ambiguity: several already-achieved phases share one move cursor, so a phase jump must retain a separate selected step. CubeSession now retains that selection, CubeHost uses it for explanation/highlights, and actual playback clears it. The added headless regression passes for all six initially empty phases of Sune practice. The unused, ineffective manual `highlight` bridge action was removed; automatic lesson highlighting remains intact. Live-player browser checks now assert these distinct phase selections and wait for actual keyboard/state-restoration events instead of accepting a preceding idle snapshot. `node --check tools/browser-check/check.mjs` passed; the changed real-player checks remain **unrun**. No further independently actionable issue was found in the bounded source review.

Available tooling now: **Unity 6000.0.68f1 (e1e9baaf294b) with Web Build Support**, at `.tools/unity-editor-extracted/Editor/Unity.exe`. Pass this path with `-UnityPath` for the commands below. No license credentials were requested, copied or exposed. The remaining gates are successful Web build, actual browser controls/playback/state/view/performance checks, and visual review.

Historical blocker revalidation before the current resume: the pinned executable was absent, command discovery and Windows installation registry found no Editor/Hub, and `website/unity/build-manifest.json` was absent. The previous run recorded the goal as blocked after three consecutive goal turns. The user has since resumed the goal and begun Hub setup; local package staging and additional verification work are now in progress.

After that tooling is available, continue **only this 3×3 milestone**:

```powershell
# 1. Run genuine scene import/API compilation and all 168 view assertions; fix every failure.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-web.ps1 -VerifyViewOnly

# 2. Build the actual Unity Web player; inspect fresh artifact JSON and engine log.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-web.ps1

# 3. In one terminal, serve the website.
python scripts/serve.py --port 8080

# 4. In another terminal, run the maintained actual-player browser tests.
npm.cmd ci --prefix tools/browser-check --ignore-scripts --no-audit --no-fund
npm.cmd test --prefix tools/browser-check
```

Then visually inspect the actual canvas (sticker labels, geometry, turning direction, highlights), exercise pointer/touch/zoom controls at desktop/mobile sizes, verify every reported state/view comparison, and record real frame/solve/heap/download measurements. The live-player test also verifies saved-state restoration and correct current-move highlighting after the integration fixes. Add additional browser cases if live inspection finds a missing control path. Fix compilation/runtime/test failures, rerun only affected checks plus the required final regression, and update this status with evidence. Do not infer a pass from the code or planned tests.

No broader catalogue work, publication, purchases, global settings changes or commits were performed. The milestone must remain incomplete until all required Unity/Web checks pass.
