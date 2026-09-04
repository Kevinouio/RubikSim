using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RubikSim.Core;
using RubikSim.Solver;

namespace RubikSim.Application
{
    /// <summary>Single owner of state, history and playback. A turn commits exactly once at animation start.</summary>
    public sealed class CubeSession
    {
        private CubeState state = CubeState.Solved();
        private readonly Queue<Move> pending = new Queue<Move>();
        private readonly Stack<string> undo = new Stack<string>(), redo = new Stack<string>();
        private readonly List<string> playbackStates = new List<string>();
        private SolverJob job;
        private Stopwatch solveWatch;
        private bool requestedStep;
        private float speed = 3;
        public CubeState State => state.Clone();
        public long Version { get; private set; }
        public SolutionPlan Plan { get; private set; }
        public int Cursor { get; private set; }
        // Zero-move phases can share the same cursor. Preserve the user's explicit lesson selection.
        public int SelectedStepIndex { get; private set; } = -1;
        public bool IsPlaying { get; private set; }
        public bool IsSolving => job != null && !job.IsComplete;
        public bool IsMoveInFlight { get; private set; }
        public bool HasPendingMoves => pending.Count != 0 || requestedStep;
        public bool CanUndo => undo.Count != 0;
        public bool CanRedo => redo.Count != 0;
        public bool HasPlan => Plan != null && Plan.Verified;
        public string Status { get; private set; } = "Ready. Scramble, enter moves, or import a state.";
        public string SolverProgress => job == null ? Status : job.Progress;
        public SolveOutcome? LastOutcome { get; private set; }
        public double SolveMilliseconds { get; private set; }
        public double LastSolveSliceMilliseconds { get; private set; }
        public double MaxSolveSliceMilliseconds { get; private set; }
        public float Speed
        {
            get => speed;
            set { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Speed must be a finite number."); speed = Math.Max(.5f, Math.Min(12f, value)); }
        }

        public void QueueNotation(string notation)
        {
            var moves = Move.ParseSequence(notation); // Parse the entire batch before changing session state.
            if (moves.Length == 0) return;
            if (pending.Count + moves.Length > 4096) throw new InvalidOperationException("Move queue limit is 4096 turns.");
            InvalidatePlan("Free play: the previous solution has been invalidated.");
            foreach (var move in moves) pending.Enqueue(move);
            Status = "Queued " + moves.Length + " move(s).";
        }

        public void Scramble(int seed)
        {
            var sequence = Scrambler.Generate(seed, 25);
            QueueNotation(Move.FormatSequence(sequence));
            Status = "Seed " + seed + " random-move scramble: " + Move.FormatSequence(sequence);
        }

        /// <summary>The renderer calls only while idle. The returned before/after snapshots are independent.</summary>
        public bool TryBeginMove(out Move move, out CubeState before, out CubeState after)
        {
            move = default; before = after = null;
            if (IsMoveInFlight || IsSolving) return false;
            if (pending.Count > 0)
            {
                move = pending.Dequeue(); before = state.Clone();
                undo.Push(state.Serialize()); redo.Clear(); state.Apply(move); Version++;
            }
            else if ((requestedStep || IsPlaying) && HasPlan && Cursor < Plan.Moves.Count)
            {
                if (!CheckPlaybackState()) return false;
                move = Plan.Moves[Cursor]; before = state.Clone();
                undo.Push(state.Serialize()); redo.Clear(); state.Apply(move); Version++; Cursor++;
                requestedStep = false;
                if (state.Serialize() != playbackStates[Cursor]) throw new InvalidOperationException("Playback move disagrees with its verified snapshot.");
                Status = "Move " + Cursor + " of " + Plan.Moves.Count + ": " + move;
                if (Cursor == Plan.Moves.Count) { IsPlaying = false; Status = "Solution complete. The cube is solved."; }
            }
            else return false;
            SelectedStepIndex = -1; IsMoveInFlight = true; after = state.Clone(); return true;
        }
        public void CompleteMove() { IsMoveInFlight = false; }
        public void Reset()
        {
            ClearActivity(); undo.Clear(); redo.Clear(); state = CubeState.Solved(); Version++;
            Status = "Cube reset to the home orientation.";
        }
        public void Undo()
        {
            ClearActivity();
            if (undo.Count == 0) { Status = "Nothing to undo."; return; }
            redo.Push(state.Serialize()); state = CubeState.Deserialize(undo.Pop()); Version++; Status = "Undid the last committed change.";
        }
        public void Redo()
        {
            ClearActivity();
            if (redo.Count == 0) { Status = "Nothing to redo."; return; }
            undo.Push(state.Serialize()); state = CubeState.Deserialize(redo.Pop()); Version++; Status = "Redid the change.";
        }
        public void Import(string serialized)
        {
            var imported = CubeState.Deserialize(serialized); // Invalid imports leave queue/history/plan intact too.
            ClearActivity(); undo.Push(state.Serialize()); redo.Clear(); state = imported; Version++;
            Status = "State imported and fully validated for a standard 3×3.";
        }
        public string Export() => state.Serialize();
        public void StartSolve()
        {
            if (IsMoveInFlight || HasPendingMoves) throw new InvalidOperationException("Wait for the queued moves to finish before solving.");
            InvalidatePlan("Finding a CFOP solution from the current state.");
            SolveMilliseconds = LastSolveSliceMilliseconds = MaxSolveSliceMilliseconds = 0;
            job = CfopSolver.CreateJob(state.Clone(), Version); solveWatch = Stopwatch.StartNew(); LastOutcome = null;
        }
        public void TickSolve(int budget = 512)
        {
            if (job == null) return;
            job.Advance(budget);
            // Retain the final slice after the completed job is released, including one-slice warm solves.
            LastSolveSliceMilliseconds = job.LastSliceMilliseconds;
            MaxSolveSliceMilliseconds = Math.Max(MaxSolveSliceMilliseconds, LastSolveSliceMilliseconds);
            if (!job.IsComplete) return;
            var completed = job; job = null;
            solveWatch.Stop(); SolveMilliseconds = solveWatch.Elapsed.TotalMilliseconds;
            LastOutcome = completed.Result.Outcome; Status = completed.Result.Message;
            if (completed.Result.Plan != null) TryAcceptPlan(completed.Result.Plan);
        }
        public void CancelSolve()
        {
            if (job != null) { job.Cancel(); LastOutcome = SolveOutcome.Cancelled; job = null; solveWatch.Stop(); SolveMilliseconds = solveWatch.Elapsed.TotalMilliseconds; }
            Status = "Solve cancelled. No partial solution is enabled.";
        }
        public bool TryAcceptPlan(SolutionPlan plan)
        {
            if (plan == null || !plan.Matches(state, Version) || IsMoveInFlight || pending.Count > 0)
            { Status = "Rejected a stale or unverified solution. Solve the current state again."; return false; }
            var replay = state.Clone(); var snapshots = new List<string> { replay.Serialize() };
            foreach (var move in plan.Moves) { replay.Apply(move); snapshots.Add(replay.Serialize()); }
            if (!replay.IsSolved) throw new InvalidOperationException("Cannot enable an incomplete solution.");
            Plan = plan; Cursor = 0; SelectedStepIndex = -1; playbackStates.Clear(); playbackStates.AddRange(snapshots);
            IsPlaying = false; requestedStep = false;
            Status = plan.Moves.Count == 0 ? "Already solved." : "Verified a complete CFOP solution: " + plan.Moves.Count + " moves.";
            return true;
        }
        public void Play() { if (CheckPlaybackState() && Cursor < Plan.Moves.Count) { IsPlaying = true; Status = "Playing the verified solution."; } }
        public void Pause() { IsPlaying = false; requestedStep = false; Status = "Playback paused. The current turn finishes at its exact resting state."; }
        public void StepForward()
        {
            if (IsMoveInFlight || HasPendingMoves) return;
            if (CheckPlaybackState() && Cursor < Plan.Moves.Count) { IsPlaying = false; requestedStep = true; }
        }
        public void StepBackward()
        {
            if (!CheckPlaybackState() || Cursor == 0) return;
            IsPlaying = false; requestedStep = false; IsMoveInFlight = false; pending.Clear();
            undo.Push(state.Serialize()); redo.Clear(); Cursor--; SelectedStepIndex = -1; state = CubeState.Deserialize(playbackStates[Cursor]); Version++;
            Status = "Returned to move " + Cursor + " using the verified state snapshot.";
        }
        public void JumpToStep(int index)
        {
            if (!HasPlan || index < 0 || index >= Plan.Steps.Count) throw new ArgumentOutOfRangeException(nameof(index), "Choose an available tutorial step.");
            if (!CheckPlaybackState()) return;
            int nextCursor = Plan.Steps.Take(index).Sum(step => step.Moves.Count);
            IsPlaying = false; requestedStep = false; IsMoveInFlight = false; pending.Clear();
            undo.Push(state.Serialize()); redo.Clear(); Cursor = nextCursor; SelectedStepIndex = index; state = CubeState.Deserialize(playbackStates[Cursor]); Version++;
            Status = "At the start of " + Plan.Steps[index].Phase + ".";
        }
        public void Practice(string caseId)
        {
            string algorithm;
            switch (caseId)
            {
                case "sune": algorithm = "R U R' U R U2 R'"; break;
                case "t-perm": algorithm = "R U R' U' R' F R2 U' R' U' R U R' F'"; break;
                default: throw new ArgumentException("Supported practice cases are sune and t-perm.", nameof(caseId));
            }
            var practice = CubeState.Solved(); practice.Apply(Move.InvertSequence(Move.ParseSequence(algorithm)));
            Import(practice.Serialize()); Status = "Loaded " + caseId + ". Solve the current state for recognition and explanation.";
        }
        private bool CheckPlaybackState()
        {
            if (!HasPlan) { Status = "Solve the current state to enable playback."; return false; }
            if (Cursor < 0 || Cursor >= playbackStates.Count || state.Serialize() != playbackStates[Cursor])
            { InvalidatePlan("Cube changed: the old solution is stale. Solve again."); return false; }
            return true;
        }
        private void InvalidatePlan(string reason)
        {
            if (job != null) { job.Cancel(); job = null; solveWatch.Stop(); LastOutcome = SolveOutcome.Cancelled; }
            Plan = null; Cursor = 0; SelectedStepIndex = -1; playbackStates.Clear(); IsPlaying = false; requestedStep = false; Status = reason;
        }
        private void ClearActivity()
        { InvalidatePlan("Cube changed."); pending.Clear(); IsMoveInFlight = false; }
    }
}
