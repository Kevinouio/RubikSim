using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RubikSim.Core;
using RubikSim.Solver;

namespace RubikSim.Tests
{
    public static class SolverTests
    {
        private static int checks;
        private static void Assert(bool condition, string message)
        { checks++; if (!condition) throw new Exception("Solver test: " + message); }

        public static void Run()
        {
            checks = 0; var watch = Stopwatch.StartNew();
            var already = Finish(CfopSolver.CreateJob(CubeState.Solved()));
            Assert(already.Outcome == SolveOutcome.AlreadySolved && already.Plan.Verified && already.Plan.Moves.Count == 0, "already solved");
            Assert(CfopSolver.CreateJob((CubeState)null).Result.Outcome == SolveOutcome.InvalidState, "null state outcome");
            Assert(CfopSolver.CreateJob("not a snapshot").Result.Outcome == SolveOutcome.InvalidState, "malformed state outcome");
            string unsupported = CubeState.Solved().Serialize().Replace("cube-3x3", "cube-9x9");
            Assert(CfopSolver.CreateJob(unsupported).Result.Outcome == SolveOutcome.UnsupportedState, "unsupported puzzle outcome");
            var scramble = CubeState.Solved(); scramble.Apply(Scrambler.Generate(913, 25));
            var cancelled = CfopSolver.CreateJob(scramble);
            long beforeWork = cancelled.WorkDone; cancelled.Advance(1);
            Assert(cancelled.WorkDone == beforeWork + 1 && !cancelled.IsComplete, "bounded single work unit yields");
            while (cancelled.WorkDone < 50 && !cancelled.IsComplete) cancelled.Advance(16);
            Assert(!cancelled.IsComplete, "cold table building remains cancellable");
            cancelled.Cancel(); cancelled.Advance(100);
            Assert(cancelled.Result.Outcome == SolveOutcome.Cancelled && cancelled.Result.Plan == null, "cancel exposes no partial plan");
            var limited = Finish(CfopSolver.CreateJob(scramble, 0, 1));
            Assert(limited.Outcome == SolveOutcome.ResourceLimit && limited.Plan == null, "resource outcome exposes no partial plan");
            var immutable = CfopSolver.CreateJob(scramble, 42); string snapshot = scramble.Serialize(); scramble.Apply("F");
            var immutableResult = Finish(immutable);
            Console.WriteLine("Cross table version=" + SolverJob.CrossTableVersion + " FNV1a32=" + SolverJob.CrossTableChecksum);
            Assert(SolverJob.CrossTableChecksum == SolverJob.ExpectedCrossTableChecksum, "versioned table integrity");
            Assert(immutableResult.Plan.SourceState == snapshot && immutableResult.Plan.SourceVersion == 42, "immutable source snapshot/version");
            Assert(!immutableResult.Plan.Matches(scramble, 42), "edited state rejects old plan");
            Assert(!immutableResult.Plan.Matches(CubeState.Deserialize(snapshot), 43), "changed version rejects old plan");
            Verify(CubeState.Deserialize(snapshot), immutableResult, "snapshot");

            // Independent Kociemba superflip facelet fixture, not generated from our move inverses.
            SolveAndVerify(CubeState.FromFacelets("UBULURUFURURFRBRDRFUFLFRFDFDFDLDRDBDLULBLFLDLBUBRBLBDB"), "independent superflip");
            foreach (string rotations in new[] { "x", "y2", "z' x", "M E S r f'", "x y z M2 U r2 B'" })
            {
                var state = CubeState.Deserialize(snapshot); state.Apply(rotations);
                SolveAndVerify(state, "rotated/slice/wide " + rotations);
            }
            AlgorithmCases();
            AllOllPatterns();
            AllPllPermutations();
            AllF2lPairLocations();
            Console.WriteLine("Solver fast suite PASS: " + checks + " assertions; 16 algorithms x 4 U setups, 216 OLL patterns, 288 PLL permutations, 1044 F2L pair states; " + watch.ElapsedMilliseconds + " ms.");
        }

        private static SolveResult Finish(SolverJob job)
        {
            while (!job.IsComplete) job.Advance(4096);
            return job.Result;
        }
        private static void SolveAndVerify(CubeState state, string label)
        {
            // Serialize and import a new state with no scramble/history information.
            var fresh = CubeState.Deserialize(state.Serialize());
            Verify(fresh, Finish(CfopSolver.CreateJob(fresh)), label);
        }
        private static void Verify(CubeState state, SolveResult result, string label)
        {
            Assert(result.Outcome == SolveOutcome.Solved || result.Outcome == SolveOutcome.AlreadySolved, label + " outcome " + result.Outcome + ": " + result.Message);
            var plan = result.Plan;
            Assert(plan != null && plan.Verified, label + " verified plan");
            Assert(state.Serialize() == plan.SourceState, label + " plan source");
            if (state.IsSolved) { Assert(plan.Moves.Count == 0, label + " solved skip"); return; }
            Assert(plan.Steps.Count == 9, label + " cross, four pairs, two OLL looks, two PLL looks");
            string[] phases = { "Cross", "F2L", "F2L", "F2L", "F2L", "OLL edges", "OLL corners", "PLL corners", "PLL edges" };
            for (int index = 0; index < plan.Steps.Count; index++)
            {
                var step = plan.Steps[index];
                Assert(step.Phase == phases[index], label + " CFOP phase order");
                Assert(step.BeforeState == state.Serialize(), label + " before snapshot");
                Assert(step.Moves.SequenceEqual(step.SetupMoves.Concat(step.AlgorithmMoves).Concat(step.AlignmentMoves)), label + " setup/main/alignment partition");
                Assert(!string.IsNullOrWhiteSpace(step.Goal) && !string.IsNullOrWhiteSpace(step.Recognition) && !string.IsNullOrWhiteSpace(step.Explanation) && !string.IsNullOrWhiteSpace(step.ReferenceOrientation) && step.Source.StartsWith("https://"), label + " teaching content");
                Assert(step.HighlightedPieces.Count > 0, label + " piece highlights");
                foreach (var move in step.Moves) { Assert(CubeState.IsLegal(move), label + " legal replay"); state.Apply(move); }
                Assert(step.AfterState == state.Serialize(), label + " after snapshot");
                // Assertions below use the core cubie arrays directly, independent of solver predicates.
                var cp = state.CP; var co = state.CO; var ep = state.EP; var eo = state.EO;
                for (int i = 4; i < 8; i++) Assert(ep[i] == i && eo[i] == 0, label + " aligned cross");
                int pairs = Math.Min(index, 4);
                for (int i = 0; i < pairs; i++) Assert(cp[i + 4] == i + 4 && co[i + 4] == 0 && ep[i + 8] == i + 8 && eo[i + 8] == 0, label + " actual F2L pair " + i);
                if (index >= 5) for (int i = 0; i < 4; i++) Assert(eo[i] == 0, label + " OLL edges");
                if (index >= 6) for (int i = 0; i < 4; i++) Assert(co[i] == 0, label + " OLL corners");
                if (index >= 7) for (int i = 0; i < 4; i++) Assert(cp[i] == i, label + " PLL corners");
            }
            Assert(state.IsSolved, label + " final solved");
            foreach (var move in plan.Moves.Reverse()) state.Apply(move.Inverse());
            Assert(state.Serialize() == plan.SourceState, label + " complete backward playback");
        }
        private static void AlgorithmCases()
        {
            Assert(AlgorithmLibrary.All.Count == 16, "complete 3+7 OLL and 2+4 PLL case library");
            foreach (var algorithm in AlgorithmLibrary.All)
            {
                var effect = CubeState.Solved(); effect.Apply(algorithm.Moves);
                Assert(effect.Centers == "URFDLB", algorithm.Id + " restores physical centers");
                var cp = effect.CP; var co = effect.CO; var ep = effect.EP; var eo = effect.EO;
                for (int i = 4; i < 8; i++) Assert(cp[i] == i && co[i] == 0 && ep[i] == i && eo[i] == 0, algorithm.Id + " preserves lower layer");
                for (int i = 8; i < 12; i++) Assert(ep[i] == i && eo[i] == 0, algorithm.Id + " preserves middle layer");
                if (algorithm.Phase != "OLL edges") for (int i = 0; i < 4; i++) Assert(eo[i] == 0, algorithm.Id + " preserves oriented edges");
                if (algorithm.Phase.StartsWith("PLL", StringComparison.Ordinal)) for (int i = 0; i < 4; i++) Assert(co[i] == 0, algorithm.Id + " preserves corner orientation");
                if (algorithm.Phase == "PLL edges")
                {
                    // Some published algorithms (notably Z) include a net U offset. The tutor
                    // must expose the restoring AUF separately, then restore exact positions.
                    bool aligns = false;
                    for (int u = 0; u < 4; u++)
                    {
                        cp = effect.CP;
                        if (Enumerable.Range(0, 4).All(i => cp[i] == i)) { aligns = true; break; }
                        effect.Apply("U");
                    }
                    Assert(aligns, algorithm.Id + " preserves corner order up to explicit final U alignment");
                }
                for (int u = 0; u < 4; u++)
                {
                    var sample = CubeState.Solved(); sample.Apply(algorithm.Moves.Reverse().Select(m => m.Inverse()));
                    for (int i = 0; i < u; i++) sample.Apply("U");
                    SolveAndVerify(sample, algorithm.Id + " setup " + u);
                }
            }
        }
        private static void AllOllPatterns()
        {
            int count = 0;
            for (int corners = 0; corners < 27; corners++) for (int edges = 0; edges < 8; edges++)
            {
                var co = new int[8]; var eo = new int[12]; int code = corners, sum = 0;
                for (int i = 0; i < 3; i++) { co[i] = code % 3; code /= 3; sum += co[i]; }
                co[3] = (3 - sum % 3) % 3;
                for (int i = 0; i < 3; i++) eo[i] = (edges >> i) & 1;
                eo[3] = eo[0] ^ eo[1] ^ eo[2];
                SolveAndVerify(CubeState.FromCubies(Enumerable.Range(0, 8).ToArray(), co, Enumerable.Range(0, 12).ToArray(), eo), "OLL orientation " + corners + "/" + edges);
                count++;
            }
            Assert(count == 216, "216 complete legal OLL orientation patterns");
        }
        private static void AllPllPermutations()
        {
            int count = 0;
            foreach (var corners in Permutations4()) foreach (var edges in Permutations4())
            {
                if (Parity(corners) != Parity(edges)) continue;
                var cp = Enumerable.Range(0, 8).ToArray(); var ep = Enumerable.Range(0, 12).ToArray();
                Array.Copy(corners, cp, 4); Array.Copy(edges, ep, 4);
                SolveAndVerify(CubeState.FromCubies(cp, new int[8], ep, new int[12]), "PLL permutation " + count++);
            }
            Assert(count == 288, "288 complete parity-matched PLL permutations");
        }
        private static IEnumerable<int[]> Permutations4()
        {
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) if (b != a)
                for (int c = 0; c < 4; c++) if (c != a && c != b)
                    for (int d = 0; d < 4; d++) if (d != a && d != b && d != c) yield return new[] { a, b, c, d };
        }
        private static void AllF2lPairLocations()
        {
            int count = 0;
            for (int pair = 0; pair < 4; pair++)
                for (int cornerPos = 0; cornerPos < 8; cornerPos++) if (cornerPos < 4 || cornerPos >= 4 + pair)
                    for (int edgePos = 0; edgePos < 12; edgePos++) if (edgePos < 4 || edgePos >= 8 + pair)
                        for (int twist = 0; twist < 3; twist++) for (int flip = 0; flip < 2; flip++)
                        {
                            var cp = Enumerable.Range(0, 8).ToArray(); var ep = Enumerable.Range(0, 12).ToArray(); var co = new int[8]; var eo = new int[12];
                            Swap(cp, pair + 4, cornerPos); Swap(ep, pair + 8, edgePos);
                            co[cornerPos] = twist; co[cornerPos == 0 ? 1 : 0] = (3 - twist) % 3;
                            eo[edgePos] = flip; eo[edgePos == 0 ? 1 : 0] = flip;
                            if (Parity(cp) != Parity(ep))
                            { var free = Enumerable.Range(0, 4).Where(p => p != edgePos).ToArray(); Swap(ep, free[0], free[1]); }
                            SolveAndVerify(CubeState.FromCubies(cp, co, ep, eo), "F2L pair " + pair + " C=" + cornerPos + "/" + twist + " E=" + edgePos + "/" + flip);
                            count++;
                        }
            Assert(count == 1044, "1044 complete legal pair abstractions with prior pairs fixed");
        }
        private static int Parity(int[] p) { int value = 0; for (int i = 0; i < p.Length; i++) for (int j = i + 1; j < p.Length; j++) if (p[i] > p[j]) value ^= 1; return value; }
        private static void Swap(int[] p, int a, int b) { int tmp = p[a]; p[a] = p[b]; p[b] = tmp; }

        public static void RunRegression(int count, int firstSeed)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var timings = new List<double>(); double maxSlice = 0; long initialMemory = GC.GetTotalMemory(true); int totalMoves = 0;
            for (int index = 0; index < count; index++)
            {
                int seed = firstSeed + index; var original = CubeState.Solved(); original.Apply(Scrambler.Generate(seed, 25));
                string snapshot = original.Serialize(); var fresh = CubeState.Deserialize(snapshot); var watch = Stopwatch.StartNew();
                var job = CfopSolver.CreateJob(fresh, seed);
                while (!job.IsComplete) { job.Advance(4096); maxSlice = Math.Max(maxSlice, job.LastSliceMilliseconds); }
                timings.Add(watch.Elapsed.TotalMilliseconds);
                Console.WriteLine("seed=" + seed + " scrambleLength=25 outcome=" + job.Result.Outcome + " moves=" + (job.Result.Plan == null ? 0 : job.Result.Plan.Moves.Count) + " solveMs=" + timings.Last().ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " work=" + job.WorkDone);
                Verify(fresh, job.Result, "seed " + seed); totalMoves += job.Result.Plan.Moves.Count;
            }
            timings.Sort(); long memory = GC.GetTotalMemory(true) - initialMemory;
            Console.WriteLine("History-free regression PASS count=" + count + " seeds=" + firstSeed + ".." + (firstSeed + count - 1) + " medianMs=" + timings[timings.Count / 2].ToString("F3") + " p95Ms=" + timings[Math.Min(timings.Count - 1, (int)Math.Ceiling(timings.Count * .95) - 1)].ToString("F3") + " maxMs=" + timings.Last().ToString("F3") + " maxSliceMs=" + maxSlice.ToString("F3") + " averageMoves=" + ((double)totalMoves / count).ToString("F1") + " retainedManagedBytesDelta=" + memory);
        }
    }
}
