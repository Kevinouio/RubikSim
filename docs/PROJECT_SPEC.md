# Finish the Rubik’s Simulator and Tutorial Website

You are working in my existing project repository. Implement the
project, not just a design document, mockup, or list of suggestions.

## 1. Goal and existing direction

Build a browser-based twisty-puzzle simulator and tutor. Users must
be able to choose a puzzle, inspect its 3D model, turn it freely,
scramble it, enter or load a state, and follow a correct solution
with notation and explanations.

Keep the original direction:
- Unity for the interactive 3D simulation.
- Mostly C# for puzzle logic and solvers.
- A website that embeds the Unity Web build.
- CFOP as the 3×3 teaching method.

Expand to other puzzle families with one established teaching method
per puzzle, rather than many competing methods for one puzzle.

The long-term goal is broad puzzle coverage. Treat “all kinds of
Rubik’s cubes” as an expanding, documented catalogue—not a claim
that a finite release includes every twisty puzzle ever made.

Read all applicable global and repository instructions, existing
design documents, code, tests, scenes, package files, and build
settings first.

Preserve working code and uncommitted user changes. Do not replace
the stack, rewrite the repository, upgrade Unity, or change global
settings without a concrete need. Record any required migration
and its reason. Do not publish, purchase services, or expose secrets.

If the repository has little code, establish the project using a
suitable supported Unity version and compatible C# tooling, checked
against official documentation. Pin exact versions. Do not assume
the newest language features work in the selected Unity version.

## 2. Supported catalogue and order

Deliver complete puzzle modules in this order.

A complete module includes:
state, legal moves, 3D rendering, controls, notation, scrambling,
a state-based solver, teaching steps, and tests.

### First: working 3×3 end to end

Finish one usable 3×3 experience before spreading work across many
incomplete models. This is the first milestone, not the final scope.

### Core catalogue

Standard cubes:
- 2×2×2
- 3×3×3
- 4×4×4
- 5×5×5
- 6×6×6
- 7×7×7

Other puzzle families:
- Pyraminx
- Skewb
- Megaminx
- Square-1
- Rubik’s Clock

Build a parameterized NxNxN simulator. Test every advertised size;
generic rendering does not establish generic solver support.

Do not count brands, colors, one-handed solving, or blindfolded
solving as new mechanical puzzle types.

### Expansion catalogue after the core works

Continue with:
- Mirror Cube
- Fisher Cube
- Windmill Cube
- Axis Cube
- 2×2×3 cuboid
- 3×3×2 cuboid

Research and implement each model’s actual move rules and solved
condition. Reuse a base solver only where the mapping is valid, and
add any required orientation or shape-restoration steps.

Maintain researched future entries for larger cubes, Kilominx,
Gigaminx, Master Pyraminx, Square-2, Gear Cube, and other families.

These are not substitutes for finishing the named core and expansion
modules. Do not claim that these examples exhaust all puzzles.

Track separate support fields for:
- Simulation
- State import and validation
- Solver
- Tutorial
- Web verification

Use explicit statuses such as planned, in progress,
implemented/unverified, and verified.

Do not present unfinished puzzles as fully supported in the normal
selector.

## 3. Solving methods

Use published, teachable methods. Verify their phase order, case
recognition, notation, and algorithms against reliable sources.
Document the exact variant selected.

2×2:
Use Ortega: solve one face, orient the opposite face, then permute
both layers. Do not confuse a solved face with a fully solved first
layer. Include needed final alignments.

3×3:
Use CFOP:
1. Aligned cross.
2. Four first-two-layer corner/edge pairs.
3. Orientation of the last layer.
4. Permutation of the last layer.

A complete two-look OLL and two-look PLL variant is an acceptable
first teaching implementation; name that variant clearly.

Do not call a corners-first beginner solution CFOP.

4×4 through 7×7:
Use reduction: solve centers, group the appropriate edge pieces,
then use the 3×3 solver on a validated reduced representation.

Implement the size-specific edge-pairing cases and parity
procedures. Do not apply even-cube parity rules indiscriminately
to odd cubes. Preserve the reduced structure at the required
stage boundaries.

Pyraminx:
Use a documented beginner layer-by-layer method, including tips,
axial pieces, and edges.

Skewb:
Use one documented beginner method with its actual corner and
center stages.

Megaminx:
Use a documented layer-by-layer method with its own last-layer cases.

Square-1:
Use a documented beginner method that restores cube shape, then
solves piece placement and the middle layer, including parity
where that method requires it.

Select and document one exact phase sequence rather than mixing
incompatible tutorials.

Clock:
Use a documented beginner pin-and-wheel method that solves both
sides. Model pin settings and coupled dial movements; do not treat
Clock as a face-turn cube.

Expansion puzzles:
Use a documented method appropriate to the actual mechanism and
visible solved condition. Describe any adaptation from a base puzzle.

A human-method solver may use bounded search for a phase, setup
moves, or case selection. Its output must still satisfy the named
method’s real phase goals.

An unrelated whole-puzzle search followed by arbitrary “CFOP” labels
is not acceptable. Do not add an opaque fallback that silently
replaces the teaching method.

## 4. State and solver correctness

The solver must solve the current state without access to scramble
or move history.

Reversing the scramble is an undo feature, not the solver.

The required proof-by-test workflow is:
1. Scramble or load a known legal state.
2. Serialize it.
3. Start a fresh puzzle instance with no history.
4. Import the state.
5. Solve it.
6. Apply every returned move.
7. Verify the solved condition.

Store exact logical state independently of Unity transforms. Use
discrete piece identities, positions, orientations, and
puzzle-specific state as needed.

Floating-point animation must never define the logical state.

Specify the coordinate frame, face labels, color scheme, orientation
conventions, and solved equivalences.

Separate camera movement from physical puzzle reorientation and
notation-frame changes. Account for indistinguishable pieces and
whole-puzzle rotations where appropriate.

A shape puzzle’s solved test must not merely check sticker colors
or hidden piece IDs.

Validate imported states using puzzle-specific constraints.
Color counts alone are not sufficient.

For 3×3, include piece inventory, corner twist, edge flip, and
matching permutation parity checks. Do not impose those exact
constraints on unrelated puzzles.

Distinguish invalid, unsupported, and not-fully-validated imports.
Do not claim full reachability validation unless implemented.

Return explicit solve outcomes:
solved, already solved, invalid state, unsupported state, cancelled,
or resource limit.

A timeout is not evidence that a state is impossible.
Never return a partial plan as a complete solution.

Solve from an immutable snapshot. Associate each plan with its
source state/version and reject stale plans after user edits.

Verify the full result before enabling complete-solution playback.

## 5. Architecture

Adapt these responsibilities to the existing repository rather than
forcing a needless folder rewrite.

Core:
Puzzle definitions, exact state, legal moves, notation, validation,
serialization, and solved predicates. Keep it free of Unity
dependencies so logic can run in headless tests.

Solvers:
Method stages, case recognition, algorithm data, bounded search,
cancellation/progress, and structured solution plans.

Unity view:
Meshes, materials, picking, camera, highlights, and animations
driven by logical move events.

Application/UI:
Puzzle selection, state editing, history, playback, lessons,
persistence, and browser integration.

Define a puzzle module contract for:
- Creating a solved state.
- Parsing and formatting moves.
- Testing move legality.
- Applying and inverting moves.
- Generating scrambles.
- Validating, importing, and exporting state.
- Supplying render data.
- Finding supported solvers.

Share useful interfaces and family implementations, but do not force
all puzzles into an NxN array or a single quarter-turn type.

Square-1 needs state-dependent slash legality. Clock needs pins and
coupled dials. Other families have their own axes and turn orders.

Add versioned serialization with a puzzle identifier,
definition/version information, state, and required orientation or
shape data.

State snapshots must not require the original move history.

## 6. 3D models and interaction

Create usable, accurate models—not decorative meshes that cannot turn.

Prefer procedural meshes and reusable materials where suitable.
Use checked-in authored assets only when they add value and their
license allows it.

Model the visible pieces, stickers or colored faces, gaps, cuts,
axes, and shape changes. Decorative details must not alter move
legality.

Build a renderer that can reconstruct a resting view from any
valid state snapshot.

Provide orbit, zoom, reset view, mouse/touch controls where the
target platform supports them, keyboard moves, and clear
on-screen controls.

Separate camera gestures from turns. Each puzzle must expose
controls appropriate to its mechanics.

Use one controlled move queue. Choose and document when a logical
move commits, ensure it commits exactly once, and animate to the
resulting exact state.

Reset, cancellation, undo, rapid input, and puzzle changes must not
leave half-applied turns or desynchronize state and view.

Highlight the active face, axis, layer, and relevant pieces during
instruction. Keep labels tied to the notation frame.

Add color-independent face labels or patterns and text instructions
outside the 3D view.

## 7. Notation and teaching output

Implement a structured, puzzle-specific move representation with
parsers and formatters.

Support the notation each selected method needs, including cube
face turns, inverses, double turns, wide/inner turns, slices,
rotations, and the distinct notation for non-cube puzzles.

Use WCA notation where applicable and document any teaching notation
or aliases. Translate algorithms explicitly when a source uses
another convention.

Reject malformed or illegal sequences with useful errors and no
unexplained partial state changes.

Each solution step must include:
- Method, phase, case identifier where relevant, and current goal.
- Recognition cues tied to the actual state and highlighted pieces.
- Setup moves, main algorithm, and restoring/final alignment moves.
- Plain-language explanation and reference orientation.
- Expected before/after state and phase postcondition.
- Source reference for the method or algorithm.

Test phase postconditions at the documented boundaries.

A method can disturb pieces temporarily within an algorithm.
Do not impose a false requirement that every intermediate turn
preserve all earlier work.

Do not optimize across teaching boundaries in a way that makes
the displayed stages false.

Algorithm records must contain enough recognition and orientation
data to apply them correctly, not just a string and a name.

Verify each algorithm’s claimed effect and cover all required
cases of the chosen method.

## 8. Website features

Provide a working website with the Unity simulation embedded and:
- Puzzle selector showing verified capabilities and teaching method.
- Scramble, reset, undo/redo, notation input, and free play.
- State import/export and manual state entry for supported
  representations; provide a 3×3 face editor first.
- Solve from current state, next hint, automatic playback, pause,
  previous/next move, speed control, and jump to phase.
- Readable move sequence with the current move highlighted,
  phase progress, recognition cues, and explanations.
- A local practice mode for cases already supported by the tutor.

After a user departs from a solution, recompute from the new state
or clearly invalidate the old plan.

Implement backward playback with reliable snapshots or tested
inverse moves.

Use accessible HTML controls and text alongside the canvas where
needed, not a canvas-only explanation.

Keep the existing website stack. Do not add accounts, payment,
a database, or a backend unless the current project genuinely
requires them.

Keep states local by default.

Label seeded random-move scrambles honestly. Do not call them
uniformly random states or official competition scrambles without
implementing and verifying those properties.

## 9. Web performance and builds

Check the selected Unity version’s official Web documentation
before choosing APIs and dependencies.

Do not assume desktop threading, native plugins, filesystem access,
reflection, or dynamic code generation work in the browser.

Run expensive solving in bounded work slices that yield so
rendering, cancellation, and input remain responsive.

Do not assume wrapping CPU work in async/await or Task.Run makes
it safe for Unity Web. Any separate worker implementation must
actually work in the deployed build.

Set and measure practical frame-time, solve-time, memory, and
download-size targets.

Report hardware, browser, puzzle size, seed set, and median/tail
timings. Cache or precompute tables where justified; include
reproducible table generation, versioning, and integrity checks.

Provide repeatable build scripts and local serving instructions,
including required MIME types, compression headers, and any
isolation headers the chosen features need.

Test the built website, not just Unity Editor play mode.

If Unity or its required license is unavailable in the execution
environment, still implement and run the independent C# tests
where possible.

Provide the build configuration and exact remaining commands.
Clearly label Unity/Web checks as unrun.

Do not invent screenshots, build results, or browser test results.

## 10. Tests and acceptance criteria

Use the repository’s test tools where practical. Add a deterministic
fast suite and a configurable broader regression suite.

For every supported puzzle, test:

1. Exact known move effects against independently specified
   fixtures. Self-generated inverse tests alone can preserve
   a shared bug.

2. Move/inverse identity, correct turn order, sequence inversion,
   and legal-move constraints.

3. Parser/formatter and state serialization round trips,
   including invalid inputs.

4. Already-solved, known difficult, phase-case, parity, and
   puzzle-specific edge cases.

5. History-free solving after serialization into a fresh instance.

6. Legal replay of every solution, every claimed phase
   postcondition, and the actual final solved state.

7. Undo/redo, cancellation, reset, stale-plan rejection, and
   agreement between rendered resting state and logical state.

8. Web controls, loading/error states, playback, and responsiveness
   in a real browser when available.

Include at least 100 deterministic random legal-state solve cases
per advertised core puzzle in the full regression suite, plus
case-specific tests.

Include independently sourced state fixtures so tests do not rely
only on the project’s own scrambler.

Log seeds and resource-limit outcomes. Do not suppress failures,
weaken tests to get a pass, or count skipped tests as passed.

Random tests are evidence, not proof of complete state coverage.

Record the supported domain, the chosen method’s case coverage,
known limits, and any unverified claims.

A puzzle is release-ready only when its model, moves, state
handling, solver, explanations, and playback meet these checks.

A rendered mesh, interface stub, or solved-state-only solver does
not meet that standard.

## 11. Execution and delivery

After inspecting the repository, write a short gap assessment
and an ordered implementation plan. Then implement immediately.

Work in complete milestones:

1. Audit, exact state/moves, notation, serialization, and core tests.

2. Complete the 3×3 model, controls, CFOP tutor, and a local Web demo.

3. Add 2×2 and 4×4, including history-free solvers and parity tests.

4. Add 5×5 through 7×7 with verified size-specific reduction.

5. Add Pyraminx, Skewb, Megaminx, Square-1, and Clock, completing
   each module before proceeding.

6. Add the named expansion puzzles, then broaden the researched
   catalogue.

7. Run cross-puzzle regressions, browser tests, performance checks,
   and documentation cleanup throughout and again before delivery.

After each milestone, run the relevant checks, fix failures,
update status, and continue while the environment and task budget
allow.

Do not stop at a scaffold or ask routine questions that repository
inspection can resolve. Choose conservative defaults and record them.

Maintain:
- README setup, use, and build instructions.
- docs/IMPLEMENTATION_PLAN.md
- docs/STATUS.md
- docs/SOURCES.md
- A puzzle support matrix.
- Per-puzzle method and notation notes.

Record source URLs, access dates, algorithm conventions, and
licenses. Write original explanations; do not copy tutorial prose,
artwork, or code without permission or a suitable license.

At the end, report exactly:
- What changed.
- Which puzzles work.
- Which commands ran and their actual results.
- How to run the project.
- What remains incomplete or blocked.

Keep a precise next step in docs/STATUS.md if a real limit stops
the work.

Never call the full project finished when supported-catalogue
work remains.

## 12. Starting references to verify

Use these as starting points, not as permission to copy their
content. Read current documentation and confirm compatibility
with this repository.

WCA puzzle categories and notation:
https://www.worldcubeassociation.org/regulations/

3×3 CFOP:
https://jperm.net/3x3/cfop

2×2 Ortega:
https://www.speedcube.us/blogs/speedcubing-solutions/how-to-solve-a-2x2-using-ortega-method-intermediate

4×4 reduction:
https://www.cubeskills.com/tutorials/beginners-method-for-solving-the-4x4-cube

Big-cube tutorials:
https://www.cubeskills.com/tutorials

Pyraminx:
https://www.jaapsch.net/puzzles/pyraminx.htm

Skewb:
https://www.jaapsch.net/puzzles/skewb.htm

Megaminx:
https://www.jaapsch.net/puzzles/megaminx.htm

Square-1:
https://www.jaapsch.net/puzzles/square1.htm

Clock:
https://www.jaapsch.net/puzzles/clock.htm

Unity Web constraints; use the matching version’s page:
https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-technical-overview.html

Begin by inspecting the repository, identifying the current working
features and gaps, and implementing the first missing milestone.