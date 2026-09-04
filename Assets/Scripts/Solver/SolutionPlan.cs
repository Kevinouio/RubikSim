using System;
using System.Collections.Generic;
using System.Linq;
using RubikSim.Core;

namespace RubikSim.Solver
{
    public enum SolveOutcome { Solved, AlreadySolved, InvalidState, UnsupportedState, Cancelled, ResourceLimit }

    public sealed class SolveResult
    {
        public SolveOutcome Outcome { get; private set; }
        public SolutionPlan Plan { get; private set; }
        public string Message { get; private set; }
        internal SolveResult(SolveOutcome outcome, SolutionPlan plan, string message)
        { Outcome = outcome; Plan = plan; Message = message; }
    }

    public sealed class SolutionStep
    {
        public string Method { get { return "CFOP: intuitive pair insertion, two-look OLL and PLL"; } }
        public string Phase { get; private set; }
        public string CaseId { get; private set; }
        public string Goal { get; private set; }
        public string Recognition { get; private set; }
        public string Explanation { get; private set; }
        public string ReferenceOrientation { get { return "Keep the D-center color below and U-center color above. F remains the labeled front; camera orbit does not change notation."; } }
        public string Source { get; private set; }
        public string BeforeState { get; private set; }
        public string AfterState { get; private set; }
        public string Postcondition { get; private set; }
        public int CompletedPairs { get; private set; }
        public IReadOnlyList<string> HighlightedPieces { get; private set; }
        public IReadOnlyList<Move> SetupMoves { get; private set; }
        public IReadOnlyList<Move> AlgorithmMoves { get; private set; }
        public IReadOnlyList<Move> AlignmentMoves { get; private set; }
        public IReadOnlyList<Move> Moves { get; private set; }
        internal SolutionStep(string phase, string caseId, string goal, string recognition, string explanation,
            string source, string before, string after, string postcondition, int completedPairs,
            IEnumerable<string> pieces, IEnumerable<Move> setup, IEnumerable<Move> main, IEnumerable<Move> alignment)
        {
            Phase = phase; CaseId = caseId; Goal = goal; Recognition = recognition; Explanation = explanation;
            Source = source; BeforeState = before; AfterState = after; Postcondition = postcondition;
            CompletedPairs = completedPairs; HighlightedPieces = Array.AsReadOnly(pieces.ToArray());
            SetupMoves = Array.AsReadOnly(setup.ToArray()); AlgorithmMoves = Array.AsReadOnly(main.ToArray());
            AlignmentMoves = Array.AsReadOnly(alignment.ToArray());
            Moves = Array.AsReadOnly(SetupMoves.Concat(AlgorithmMoves).Concat(AlignmentMoves).ToArray());
        }
    }

    public sealed class SolutionPlan
    {
        public string SourceState { get; private set; }
        public long SourceVersion { get; private set; }
        public IReadOnlyList<SolutionStep> Steps { get; private set; }
        public IReadOnlyList<Move> Moves { get; private set; }
        public bool Verified { get; private set; }
        internal SolutionPlan(string sourceState, long version, IEnumerable<SolutionStep> steps)
        {
            SourceState = sourceState; SourceVersion = version;
            Steps = Array.AsReadOnly(steps.ToArray());
            Moves = Array.AsReadOnly(Steps.SelectMany(step => step.Moves).ToArray());
            var replay = CubeState.Deserialize(sourceState);
            foreach (var step in Steps)
            {
                if (replay.Serialize() != step.BeforeState) throw new InvalidOperationException("Solution before-state mismatch.");
                replay.Apply(step.Moves);
                if (replay.Serialize() != step.AfterState || !CfopSolver.CheckPostcondition(replay, step))
                    throw new InvalidOperationException("Solution phase verification failed: " + step.Phase);
            }
            if (!replay.IsSolved) throw new InvalidOperationException("Incomplete solution rejected.");
            Verified = true;
        }
        public bool Matches(CubeState state, long version)
        { return Verified && version == SourceVersion && state.Serialize() == SourceState; }
    }
}
