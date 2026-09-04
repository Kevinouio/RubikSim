using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RubikSim.Core;

namespace RubikSim.Solver
{
    public static class CfopSolver
    {
        public static SolverJob CreateJob(CubeState source, long version = 0, long maxWork = 2000000)
        { return new SolverJob(source, version, maxWork); }
        public static SolverJob CreateJob(string serialized, long version = 0, long maxWork = 2000000)
        {
            try { return new SolverJob(CubeState.Deserialize(serialized), version, maxWork); }
            catch (StateImportException ex) { return new SolverJob(ex.Validation.Status == ImportStatus.Unsupported ? SolveOutcome.UnsupportedState : SolveOutcome.InvalidState, ex.Message); }
            catch (NotSupportedException ex) { return new SolverJob(SolveOutcome.UnsupportedState, ex.Message); }
            catch (ArgumentException ex) { return new SolverJob(SolveOutcome.InvalidState, ex.Message); }
            catch (FormatException ex) { return new SolverJob(SolveOutcome.InvalidState, ex.Message); }
        }
        public static bool CheckPostcondition(CubeState state, SolutionStep step)
        {
            var cube = new PieceState(state);
            if (!cube.CrossSolved) return false;
            for (int pair = 0; pair < step.CompletedPairs; pair++) if (!cube.PairSolved(pair)) return false;
            if (step.Phase == "OLL edges") return cube.FirstTwoLayersSolved && cube.EdgesOriented;
            if (step.Phase == "OLL corners") return cube.FirstTwoLayersSolved && cube.LastLayerOriented;
            if (step.Phase == "PLL corners") return cube.FirstTwoLayersSolved && cube.LastLayerOriented && cube.TopCornersSolved;
            if (step.Phase == "PLL edges") return state.IsSolved;
            return true;
        }
    }

    // Exact position/orientation of each named piece. C = 3*position+twist, E = 2*position+flip.
    internal sealed class PieceState
    {
        internal readonly int[] C = new int[8], E = new int[12];
        internal PieceState(CubeState state)
        {
            var cp = state.CP; var co = state.CO; var ep = state.EP; var eo = state.EO;
            for (int pos = 0; pos < 8; pos++) C[cp[pos]] = pos * 3 + co[pos];
            for (int pos = 0; pos < 12; pos++) E[ep[pos]] = pos * 2 + eo[pos];
        }
        private PieceState() { }
        internal PieceState Clone() { var result = new PieceState(); Array.Copy(C, result.C, 8); Array.Copy(E, result.E, 12); return result; }
        internal void Apply(Transform move)
        { for (int i = 0; i < 8; i++) C[i] = move.C[C[i]]; for (int i = 0; i < 12; i++) E[i] = move.E[E[i]]; }
        internal bool CrossSolved { get { for (int i = 4; i < 8; i++) if (E[i] != 2 * i) return false; return true; } }
        internal bool PairSolved(int pair) { return C[4 + pair] == 3 * (4 + pair) && E[8 + pair] == 2 * (8 + pair); }
        internal bool FirstTwoLayersSolved { get { if (!CrossSolved) return false; for (int p = 0; p < 4; p++) if (!PairSolved(p)) return false; return true; } }
        internal bool EdgesOriented { get { for (int i = 0; i < 4; i++) if (E[i] % 2 != 0) return false; return true; } }
        internal bool LastLayerOriented { get { if (!EdgesOriented) return false; for (int i = 0; i < 4; i++) if (C[i] % 3 != 0) return false; return true; } }
        internal bool TopCornersSolved { get { for (int i = 0; i < 4; i++) if (C[i] != 3 * i) return false; return true; } }
        internal bool Solved { get { if (!FirstTwoLayersSolved || !TopCornersSolved) return false; for (int i = 0; i < 4; i++) if (E[i] != 2 * i) return false; return true; } }
    }

    internal sealed class Transform
    {
        internal readonly int[] C = new int[24], E = new int[24];
        internal readonly Move[] Moves;
        internal Transform(IEnumerable<Move> moves)
        {
            Moves = moves.ToArray(); var state = CubeState.Solved(); state.Apply(Moves);
            var cp = state.CP; var co = state.CO; var ep = state.EP; var eo = state.EO;
            for (int pos = 0; pos < 8; pos++) for (int ori = 0; ori < 3; ori++) C[cp[pos] * 3 + ori] = pos * 3 + (ori + co[pos]) % 3;
            for (int pos = 0; pos < 12; pos++) for (int ori = 0; ori < 2; ori++) E[ep[pos] * 2 + ori] = pos * 2 + (ori ^ eo[pos]);
        }
        internal Transform Inverse() { return new Transform(Moves.Reverse().Select(move => move.Inverse())); }
        internal bool PreservesPair(int pair) { return C[(pair + 4) * 3] == (pair + 4) * 3 && E[(pair + 8) * 2] == (pair + 8) * 2; }
    }

    public sealed class SolverJob
    {
        public const string CrossTableVersion = "aligned-D-edges-v1-URFDLB-pos2flip-depth-plus-one";
        public const string ExpectedCrossTableChecksum = "1CDF2115";
        public static string CrossTableChecksum { get; private set; }
        public bool IsComplete { get { return Result != null; } }
        public SolveResult Result { get; private set; }
        public string Progress { get; private set; }
        public long WorkDone { get; private set; }
        public double LastSliceMilliseconds { get; private set; }
        private readonly CubeState source;
        private readonly string sourceSerialized;
        private readonly long sourceVersion, maxWork;
        private readonly IEnumerator<int> work;
        private bool cancelled;
        private readonly List<SolutionStep> steps = new List<SolutionStep>();
        private CubeState current;
        private PieceState pieces;
        private readonly List<Transform> faces = new List<Transform>();
        private readonly List<Transform> triggers = new List<Transform>();
        private static byte[] cachedCross;
        private byte[] cross;
        private const int CrossSize = 24 * 24 * 24 * 24;
        private static readonly string[] CornerNames = { "URF", "UFL", "ULB", "UBR", "DFR", "DLF", "DBL", "DRB" };
        private static readonly string[] EdgeNames = { "UR", "UF", "UL", "UB", "DR", "DF", "DL", "DB", "FR", "FL", "BL", "BR" };
        private static readonly Move[] Empty = new Move[0];

        internal SolverJob(SolveOutcome outcome, string message) { Result = new SolveResult(outcome, null, message); Progress = message; }
        internal SolverJob(CubeState state, long version, long limit)
        {
            if (state == null) { Result = new SolveResult(SolveOutcome.InvalidState, null, "A cube state is required."); Progress = Result.Message; return; }
            source = state.Clone(); sourceSerialized = source.Serialize(); sourceVersion = version; maxWork = Math.Max(0, limit);
            current = source.Clone(); pieces = new PieceState(current); Progress = "Preparing CFOP"; work = Run().GetEnumerator();
        }
        public void Cancel() { cancelled = true; if (!IsComplete) Complete(SolveOutcome.Cancelled, null, "Solve cancelled; no partial plan is enabled."); }
        // No threads or Task.Run. Each yield is a bounded table node, candidate, or fixed-size operation.
        // Time budget supplements work count, so unusually slow browsers still yield each frame.
        public void Advance(int workBudget = 512)
        {
            if (IsComplete) return;
            var watch = Stopwatch.StartNew();
            int count = 0;
            while (!IsComplete && count < Math.Max(1, workBudget) && (count == 0 || watch.Elapsed.TotalMilliseconds < 4))
            {
                if (cancelled) { Complete(SolveOutcome.Cancelled, null, "Solve cancelled."); break; }
                if (WorkDone >= maxWork) { Complete(SolveOutcome.ResourceLimit, null, "CFOP work limit reached. This does not imply an impossible state."); break; }
                WorkDone++; count++;
                if (!work.MoveNext() && !IsComplete) Complete(SolveOutcome.ResourceLimit, null, "CFOP ended without a verified complete solution.");
            }
            LastSliceMilliseconds = watch.Elapsed.TotalMilliseconds;
        }
        private void Complete(SolveOutcome outcome, SolutionPlan plan, string message)
        { Result = new SolveResult(outcome, plan, message); Progress = message; }

        private IEnumerable<int> Run()
        {
            if (source.IsSolved)
            { Complete(SolveOutcome.AlreadySolved, new SolutionPlan(sourceSerialized, sourceVersion, steps), "Already solved in the current center frame."); yield break; }
            foreach (char face in "URFDLB") for (int t = 1; t <= 3; t++)
            { faces.Add(new Transform(Move.ParseSequence(face + (t == 1 ? "" : t == 2 ? "2" : "'")))); yield return 1; }
            foreach (int tick in PrepareCross()) yield return tick;
            Progress = "Aligned cross: place the four D edges beside matching centers";
            var crossMoves = new List<Move>();
            int crossKey = EncodeCross(pieces);
            if (cross[crossKey] == 0) { Complete(SolveOutcome.UnsupportedState, null, "The edge abstraction is outside the legal cross domain."); yield break; }
            while (cross[crossKey] > 1)
            {
                bool found = false;
                foreach (var move in faces)
                {
                    int next = MoveCross(crossKey, move);
                    if (cross[next] + 1 != cross[crossKey]) continue;
                    crossMoves.AddRange(move.Moves); crossKey = next; found = true; break;
                }
                if (!found) throw new InvalidOperationException("Cross table has no descending neighbor.");
                yield return 1;
            }
            AddStep("Cross", crossMoves.Count == 0 ? "Already aligned" : "Four-edge placement", "Align the four D edges with D and the four side centers.",
                DescribePieces(pieces, Enumerable.Range(4, 4), new int[0]),
                "Track the D-color edges as a group. The sequence restores their side colors beside matching centers; solved-looking D stickers alone are insufficient.",
                AlgorithmLibrary.MethodSource, 0, EdgeNames.Skip(4).Take(4), Empty, crossMoves, Empty, "Aligned D cross");
            yield return 1;

            // Each conjugate opens one slot, turns U, and closes that slot. Its inverse is also present.
            for (int turn = 1; turn <= 3; turn++) triggers.Add(new Transform(Move.ParseSequence("U" + (turn == 1 ? "" : turn == 2 ? "2" : "'"))));
            foreach (char side in "RFLB") foreach (string direction in new[] { "", "'" }) foreach (string top in new[] { "U", "U2", "U'" })
            {
                string start = side + direction, end = side + (direction == "" ? "'" : "");
                var trigger = new Transform(Move.ParseSequence(start + " " + top + " " + end));
                var probe = new PieceState(CubeState.Solved()); probe.Apply(trigger);
                if (!probe.CrossSolved) throw new InvalidOperationException("F2L trigger disturbs the cross at its boundary.");
                triggers.Add(trigger); yield return 1;
            }

            for (int pair = 0; pair < 4; pair++)
            {
                Progress = "F2L pair " + (pair + 1) + ": " + CornerNames[pair + 4] + " + " + EdgeNames[pair + 8];
                var allowed = triggers.Where(t => Enumerable.Range(0, pair).All(t.PreservesPair)).ToArray();
                var inverse = allowed.Select(t => t.Inverse()).ToArray();
                var toGoal = new int[576]; var via = new int[576]; var seen = new bool[576]; var queue = new int[576];
                int goal = (pair + 4) * 3 * 24 + (pair + 8) * 2;
                int start = pieces.C[pair + 4] * 24 + pieces.E[pair + 8];
                int head = 0, tail = 1; queue[0] = goal; seen[goal] = true;
                while (head < tail && !seen[start])
                {
                    int key = queue[head++];
                    for (int m = 0; m < inverse.Length; m++)
                    {
                        int previous = inverse[m].C[key / 24] * 24 + inverse[m].E[key % 24];
                        if (seen[previous]) continue;
                        seen[previous] = true; toGoal[previous] = key; via[previous] = m; queue[tail++] = previous;
                    }
                    yield return 1;
                }
                if (!seen[start]) { Complete(SolveOutcome.ResourceLimit, null, "F2L pair case is not reachable with the preserving trigger set."); yield break; }
                var setup = new List<Move>(); var main = new List<Move>(); int cursor = start;
                while (cursor != goal)
                {
                    var selected = allowed[via[cursor]];
                    if (main.Count == 0 && selected.Moves.Length == 1) setup.AddRange(selected.Moves); else main.AddRange(selected.Moves);
                    cursor = toGoal[cursor]; yield return 1;
                }
                string name = CornerNames[pair + 4] + "/" + EdgeNames[pair + 8];
                AddStep("F2L", "Pair " + (pair + 1) + " " + name + (start == goal ? " already solved" : " position " + start),
                    "Solve the " + name + " corner/edge pair together and preserve the cross and " + pair + " completed pairs.",
                    DescribePieces(pieces, new[] { pair + 8 }, new[] { pair + 4 }),
                    start == goal ? "This pair already fills its slot with matching side colors; retain it while working on later pairs." :
                    "Locate both named pieces. Use the U layer to bring them together; the side-turn triggers extract or insert the pair while closing every opened slot. Both pieces must finish solved at this boundary.",
                    AlgorithmLibrary.MethodSource, pair + 1, new[] { CornerNames[pair + 4], EdgeNames[pair + 8] }, setup, main, Empty,
                    "Aligned cross and first " + (pair + 1) + " complete F2L pairs");
                yield return 1;
            }

            foreach (string phase in new[] { "OLL edges", "OLL corners", "PLL corners", "PLL edges" })
            {
                Progress = phase + ": recognizing the current last-layer case";
                TeachingAlgorithm selected = null; Move[] chosenSetup = null, chosenAlignment = null;
                // Include an empty algorithm, allowing skips and pure final U alignment.
                var choices = new List<TeachingAlgorithm> { null }; choices.AddRange(AlgorithmLibrary.All.Where(a => a.Phase == phase));
                bool done = false;
                foreach (var algorithm in choices)
                {
                    var transform = algorithm == null ? null : new Transform(algorithm.Moves);
                    for (int pre = 0; pre < 4 && !done; pre++) for (int post = 0; post < (phase.StartsWith("PLL", StringComparison.Ordinal) ? 4 : 1); post++)
                    {
                        var candidate = pieces.Clone(); var before = UMoves(pre); var after = UMoves(post);
                        if (pre > 0) candidate.Apply(faces[pre - 1]);
                        if (transform != null) candidate.Apply(transform);
                        if (post > 0) candidate.Apply(faces[post - 1]);
                        if (PhaseGoal(candidate, phase))
                        { selected = algorithm; chosenSetup = before; chosenAlignment = after; done = true; break; }
                        yield return 1;
                    }
                    if (done) break;
                }
                if (!done) { Complete(SolveOutcome.ResourceLimit, null, "No verified " + phase + " case matched; no partial solution is enabled."); yield break; }
                string actual = DescribePieces(pieces, Enumerable.Range(0, 4), Enumerable.Range(0, 4));
                AddStep(phase, selected == null ? "Already achieved / U alignment" : selected.Id,
                    GoalText(phase), (selected == null ? "This phase is already achieved or only needs U alignment. " : selected.Recognition + " ") + actual +
                    " Setup " + (chosenSetup.Length == 0 ? "none" : string.Join(" ", chosenSetup.Select(m => m.ToString()))) + " places this actual pattern in the algorithm's reference orientation.",
                    phase.StartsWith("OLL", StringComparison.Ordinal) ? "Orient the U-color stickers while restoring all F2L slots. Piece positions around U may still be wrong until PLL." :
                    "Move the correctly oriented U pieces to their matching centers. The separately displayed final U turn restores center alignment.",
                    selected == null ? AlgorithmLibrary.MethodSource : selected.Source, 4,
                    phase.EndsWith("edges", StringComparison.Ordinal) ? EdgeNames.Take(4) : CornerNames.Take(4),
                    chosenSetup, selected == null ? Empty : selected.Moves, chosenAlignment, GoalText(phase));
                yield return 1;
            }
            Progress = "Verifying complete solution and every phase boundary";
            // Recheck independently with the core model before exposing any playback plan.
            var plan = new SolutionPlan(sourceSerialized, sourceVersion, steps);
            Complete(SolveOutcome.Solved, plan, "Verified CFOP solution: " + plan.Moves.Count + " moves.");
        }

        private IEnumerable<int> PrepareCross()
        {
            if (cachedCross != null) { cross = cachedCross; yield break; }
            Progress = "Building cross table (190080 legal four-edge states; first solve only)";
            cross = new byte[CrossSize]; var queue = new int[190080];
            int goal = EncodeCross(new PieceState(CubeState.Solved())); cross[goal] = 1; queue[0] = goal;
            int head = 0, tail = 1;
            while (head < tail)
            {
                int key = queue[head++];
                foreach (var move in faces)
                {
                    int next = MoveCross(key, move); if (cross[next] != 0) continue;
                    cross[next] = (byte)(cross[key] + 1); queue[tail++] = next;
                }
                if ((head & 4095) == 0) Progress = "Building cross table: " + head + "/190080 states expanded";
                yield return 1;
            }
            if (tail != 190080) throw new InvalidOperationException("Cross domain coverage mismatch: " + tail);
            uint checksum = 2166136261;
            for (int offset = 0; offset < cross.Length; offset++)
            {
                checksum = unchecked((checksum ^ cross[offset]) * 16777619);
                if ((offset & 511) == 511) yield return 1;
            }
            CrossTableChecksum = checksum.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
            if (CrossTableChecksum != ExpectedCrossTableChecksum) throw new InvalidOperationException("Cross table integrity mismatch for " + CrossTableVersion);
            cachedCross = cross;
        }
        private static int EncodeCross(PieceState state) { return ((state.E[4] * 24 + state.E[5]) * 24 + state.E[6]) * 24 + state.E[7]; }
        private static int MoveCross(int key, Transform move)
        { int d = key % 24; key /= 24; int c = key % 24; key /= 24; int b = key % 24; int a = key / 24; return ((move.E[a] * 24 + move.E[b]) * 24 + move.E[c]) * 24 + move.E[d]; }
        private static Move[] UMoves(int turns) { return turns == 0 ? Empty : Move.ParseSequence(turns == 1 ? "U" : turns == 2 ? "U2" : "U'").ToArray(); }
        private static bool PhaseGoal(PieceState cube, string phase)
        {
            if (!cube.FirstTwoLayersSolved) return false;
            if (phase == "OLL edges") return cube.EdgesOriented;
            if (phase == "OLL corners") return cube.LastLayerOriented;
            if (phase == "PLL corners") return cube.LastLayerOriented && cube.TopCornersSolved;
            return cube.Solved;
        }
        private static string GoalText(string phase)
        {
            switch (phase)
            {
                case "OLL edges": return "Orient all four U edges while keeping F2L solved.";
                case "OLL corners": return "Orient the entire U face while keeping F2L solved.";
                case "PLL corners": return "Put all four U corners in their correct positions and keep U oriented.";
                default: return "Permute U edges and finish with all six faces solved beside their centers.";
            }
        }
        private static string DescribePieces(PieceState cube, IEnumerable<int> edges, IEnumerable<int> corners)
        {
            var parts = new List<string>();
            foreach (int id in corners) parts.Add(CornerNames[id] + " at " + CornerNames[cube.C[id] / 3] + " (twist " + cube.C[id] % 3 + ")");
            foreach (int id in edges) parts.Add(EdgeNames[id] + " at " + EdgeNames[cube.E[id] / 2] + (cube.E[id] % 2 == 0 ? " (oriented)" : " (flipped)"));
            return string.Join("; ", parts) + ".";
        }
        private void AddStep(string phase, string id, string goal, string recognition, string explanation, string sourceUrl, int pairs,
            IEnumerable<string> highlighted, IEnumerable<Move> setup, IEnumerable<Move> main, IEnumerable<Move> alignment, string postcondition)
        {
            string before = current.Serialize(); current.Apply(setup); current.Apply(main); current.Apply(alignment);
            pieces = new PieceState(current);
            var step = new SolutionStep(phase, id, goal, recognition, explanation, sourceUrl, before, current.Serialize(), postcondition, pairs, highlighted, setup, main, alignment);
            if (!CfopSolver.CheckPostcondition(current, step)) throw new InvalidOperationException("Internal phase postcondition failure: " + phase);
            steps.Add(step);
        }
    }
}
