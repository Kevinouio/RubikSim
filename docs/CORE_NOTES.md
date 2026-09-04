# 3x3 state, notation, and validation

The core is original C# code under `Assets/Scripts/Core`, with no Unity dependency. It models the standard six-color 3x3 only. Picture cubes and oriented center artwork are outside this definition: the ordinary center sticker has no distinguished rotation about its normal.

## Frame and color scheme

Logical coordinates are +X right, +Y up, +Z front. Positions and normals are integers. Faces are ordered **U R F D L B**, and their nine stickers are read left-to-right, top-to-bottom while looking at that face from outside. The home-color labels mean U white, R red, F green, D yellow, L orange, B blue. A letter in a facelet string is a color identity, while a letter in a move is a spatial face.

Camera orbit changes neither the logical frame nor the cube. The physical rotations `x`, `y`, and `z` move all stickers and centers. Slice and wide turns also move centers. Snapshots retain those center colors exactly. The solved predicate checks that every face matches its current center, making all 24 proper whole-cube orientations equivalent.

`CP`, `CO`, `EP`, and `EO` use the color identities established by the **current centers**. `ToCanonical()` returns a new state with colors relabeled by those centers; it does not move any piece or change the notation frame. Thus a solver may use the normalized cubies and replay its face moves directly on the original snapshot. For example, a solved cube rotated by `x` still has solved cubie arrays; applying spatial `F` gives the usual F cubie effect. A tutorial must describe the current D-center color, rather than assuming D always means a yellow or white sticker.

Cubie order is `URF,UFL,ULB,UBR,DFR,DLF,DBL,DRB` and `UR,UF,UL,UB,DR,DF,DL,DB,FR,FL,BL,BR`. A permutation entry gives the piece currently occupying that position. Corner orientation identifies where its U/D reference sticker lies in that position's cyclic face order; edge orientation identifies whether its ordered stickers agree with the position's order. The [published cubie definitions](https://kociemba.org/math/CubeDefs.htm) specify those orders and provide independent move fixtures; the [cubie-level explanation](https://kociemba.org/math/cubielevel.htm) describes the orientation convention. These are conventions and mathematical data, not use of a whole-puzzle two-phase solver.

## Moves

Face moves, primes, double turns, two-layer `Rw`/`2Rw` forms, and `x y z` rotations follow [WCA Article 12](https://www.worldcubeassociation.org/regulations/#12a). Clockwise means viewed from outside the named face. A whole rotation follows the corresponding R, U, or F direction. Only two-layer wide turns are legal on this 3x3; `3Rw` and `1Rw` are rejected.

Teaching extensions are explicit: lowercase `r u f d l b` mean wide moves, and `M E S` mean the middle slice turning in the L, D, and F directions respectively. These aliases are not the WCA Fewest Moves capitalization rules. Identities include `Rw = R M'`, `Uw = U E'`, `Fw = F S`, and `x = R M' L'`. The formatter emits uppercase face names and `w`, and removes a redundant prime on a half turn (`R2'` becomes `R2`). The parser accepts whitespace or compact sequences, but rejects unsupported punctuation, groups, counts, and suffixes with a character location. It parses the entire input before mutation. Batched `Apply` also enumerates and validates the whole sequence before committing it.

`Move.Axis`, `SignedQuarterTurns`, and `LayerMask` describe the exact geometric move. The mask bits select coordinates -1, 0, and +1. `CubeGeometry.Rotate` uses right-handed positive quarter turns about the positive axis. Consequently R, U, and F have a signed quarter turn of -1. `GetStickers()` returns all 54 exact sticker positions, normals, and colors so a view can reconstruct the cube from any snapshot.

## Imports and snapshots

`FromFacelets` accepts exactly 54 uppercase color letters, optionally separated by whitespace. It checks each color count, all six center identities, opposite colors and handedness of the center frame, complete corner and edge inventories, cyclic corner sticker order, corner twist sum modulo 3, edge flip sum modulo 2, and equal corner/edge permutation parity. For this standard, unoriented-center 3x3 domain, these checks establish reachability up to a proper cube rotation. Mirrored frames, one twisted corner, one flipped edge, and one pair swap are rejected.

`TryFromFacelets` and `TryDeserialize` return an explicit `ValidationResult`; throwing variants use `StateImportException`. Malformed or unreachable states are **Invalid**. Unknown puzzle identifiers, schema/definition versions, and unrecognized snapshot fields are **Unsupported**, never silently accepted as validated. This implementation has no partially validated import path.

The version-1 JSON schema is:

```json
{"schemaVersion":1,"puzzle":"cube-3x3","definitionVersion":1,"facelets":"UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB"}
```

It contains no move history. Center orientation is already encoded by the six center facelets. The parser supports this flat schema, field reordering, JSON whitespace, and escaped strings without reflection or a JSON dependency. It rejects duplicate fields, wrong field types, malformed JSON, and trailing content. Every array, clone, and imported state has independent ownership.

## Scrambles and independent evidence

`Scrambler.Generate(seed, length)` produces deterministic **random-move** scrambles using a specified xorshift32 generator and avoiding consecutive moves on the same axis. They are not uniformly random states or official competition scrambles. The unsigned state begins at `uint(seed) XOR 0x9E3779B9`, with `0xA341316C` replacing zero; its steps are XOR-shifts 13 left, 17 right, 5 left. Face and turn choices use modulo 6 and 3.

`tests/CoreTests.cs` tests all six basic moves against independent cubie fixtures, independently specified F/R sticker bands, invalid-state constraints, move laws, atomic rejection, serialization, all 24 center orientations, exact geometry, and 100 seeded legal-state snapshot checks. Those 100 core snapshot cases are distinct from, and do not replace, the required history-free **solver** regression.

The independent Superflip fixture has all pieces home, corners oriented, and all 12 edges flipped:

```text
UBULURUFU RURFRBRDR FUFLFRFDF DFDLDRDBD LULBLFLDL BUBRBLBDB
```

It is checked against the [published Superflip generator](https://kociemba.org/math/oh.htm). The definition of this state is also described in [Rokicki, Twenty-Five Moves Suffice for Rubik's Cube](https://kociemba.org/math/papers/rubik25.pdf). The fixture is imported directly for solver tests, avoiding dependence on this project's scramble generator.

All sources above were accessed 2026-09-04. Original code and explanations use the repository license. External prose, source code, images, and assets were not copied; the references supply notation conventions and mathematical fixtures.

## Checks actually run

On 2026-09-04 with the repository-local .NET SDK 8.0.419:

- `.\.tools\dotnet\dotnet.exe run --project .tools/core-check/CoreCheck.csproj -c Release`: exit 0; **3912 core assertions passed**.
- `.\.tools\dotnet\dotnet.exe build .tools/core-standard/CoreStandard.csproj -c Release`: exit 0; core compiled for **C# 9 / .NET Standard 2.1**, 0 warnings, 0 errors.

The `.tools` projects are temporary independent harnesses. The maintained full suite is the repository's `tests/RubikSim.Tests.csproj`; its actual results belong in `docs/STATUS.md`. These checks do not establish Unity compilation, Web rendering, or browser state/view agreement.
