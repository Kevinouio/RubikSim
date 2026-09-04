using System;
using RubikSim.Application;
using RubikSim.Core;
using RubikSim.Solver;

namespace RubikSim.Tests
{
    internal static class SessionTests
    {
        private static int checks;
        private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception("Session: " + message); }
        private static void Reject(Action action, string message) { bool rejected=false; try { action(); } catch (FormatException) { rejected=true; } Check(rejected, message); }
        private static void Drain(CubeSession session)
        {
            int turns=0;
            while (session.TryBeginMove(out var move, out var before, out var after))
            {
                before.Apply(move); Check(before.Serialize()==after.Serialize(), "animation event has exact move target");
                Check(!session.TryBeginMove(out _, out _, out _), "only one move can be in flight");
                session.CompleteMove(); if (++turns>4096) throw new Exception("Queue did not drain.");
            }
        }
        private static void Solve(CubeSession session)
        { session.StartSolve(); int loops=0; while(session.IsSolving){session.TickSolve(512);if(++loops>100000)throw new Exception("Solve did not terminate.");} Check(session.HasPlan,"full verified plan accepted"); }
        internal static void Run()
        {
            var session=new CubeSession();string solved=session.Export();
            var copy=session.State;copy.Apply("R");Check(session.State.IsSolved,"state property is an independent snapshot");
            session.QueueNotation("R U");Reject(()=>session.QueueNotation("F illegal"),"invalid notation rejected atomically");
            Check(session.TryBeginMove(out var first,out var before,out var after),"queue starts");Check(first.ToString()=="R","first queued move");
            Check(session.Version==1 && session.IsMoveInFlight,"commit occurs once at animation start");
            session.CompleteMove();Drain(session);var expected=CubeState.Solved();expected.Apply("R U");Check(session.Export()==expected.Serialize(),"malformed batch did not append partial F");
            session.Undo();expected.Apply("U'");Check(session.Export()==expected.Serialize(),"undo restores snapshot");session.Redo();expected.Apply("U");Check(session.Export()==expected.Serialize(),"redo restores snapshot");
            session.QueueNotation("F B D");session.TryBeginMove(out _,out _,out _);session.Undo();Check(!session.HasPendingMoves&&!session.IsMoveInFlight,"undo cancels pending queue and settles current turn");Check(session.Export()==expected.Serialize(),"undo reverses committed in-flight move");
            session.QueueNotation("R U F");session.TryBeginMove(out _,out _,out _);session.Reset();Check(session.State.IsSolved&&!session.HasPendingMoves&&!session.IsMoveInFlight&&!session.CanUndo,"reset discards queue/history and settles cube");
            session.Scramble(42);Drain(session);string scramble=session.Export();Solve(session);var plan=session.Plan;
            string withPlan=session.Export();Reject(()=>session.Import("{}"),"invalid import rejected");Check(session.Plan==plan&&session.Export()==withPlan,"invalid import preserves plan and current state");
            session.StepForward();session.TryBeginMove(out _,out _,out _);Check(session.Cursor==1,"step cursor advances on commit");session.Pause();session.CompleteMove();Check(!session.TryBeginMove(out _,out _,out _),"pause does not schedule next move");
            session.StepBackward();Check(session.Cursor==0&&session.Export()==scramble,"backward playback returns exact source snapshot");
            session.JumpToStep(3);Check(session.Export()==session.Plan.Steps[3].BeforeState,"phase jump restores documented before-state");session.JumpToStep(0);
            session.Play();Drain(session);Check(session.State.IsSolved&&session.Cursor==session.Plan.Moves.Count&&!session.IsPlaying,"playback reaches verified solved result");
            while(session.Cursor>0)session.StepBackward();Check(session.Export()==scramble,"full backward playback restores scramble without history");
            session.QueueNotation("R");Check(!session.HasPlan,"manual departure invalidates plan before turn starts");Drain(session);Check(!session.TryAcceptPlan(plan),"stale plan rejected after user edits");
            session.Import(scramble);Check(!session.TryAcceptPlan(plan),"matching stickers with newer version still reject old plan");
            session.StartSolve();session.TickSolve(1);session.CancelSolve();Check(!session.IsSolving&&!session.HasPlan&&session.LastOutcome==SolveOutcome.Cancelled,"cancelled search exposes no partial plan");
            session.StartSolve();session.Reset();session.TickSolve(10);Check(session.State.IsSolved&&!session.HasPlan&&!session.IsSolving,"reset cancels solve and ignores old result");
            session.Import(scramble);session.Undo();Check(session.Export()==solved,"import is undoable");session.Redo();Check(session.Export()==scramble,"import is redoable");
            foreach(string practice in new[]{"sune","t-perm"}){session.Practice(practice);Check(!session.State.IsSolved,"practice is a nontrivial supported case");Solve(session);session.Play();Drain(session);Check(session.State.IsSolved,"practice solves and plays");}
            session.Speed=99;Check(session.Speed==12,"speed upper clamp");session.Speed=.1f;Check(session.Speed==.5f,"speed lower clamp");
            LifecycleInterleavings();
            ZeroMovePhaseSelection();
            SolveTimingSurvivesCompletion();
            Console.WriteLine("PASS session: "+checks+" assertions (queue, snapshots, atomic edits, cancellation, playback, practice, stale plans)");
        }
        private static void SolveTimingSurvivesCompletion()
        {
            var session = new CubeSession();
            session.Practice("sune");
            session.StartSolve();
            Check(session.LastSolveSliceMilliseconds == 0 && session.MaxSolveSliceMilliseconds == 0, "new solve clears prior slice measurements");
            while (session.IsSolving) session.TickSolve(4096);
            Check(session.HasPlan && session.LastSolveSliceMilliseconds > 0, "completed solve retains its measured final slice after releasing the job");
            double finalSlice = session.LastSolveSliceMilliseconds;
            Check(session.MaxSolveSliceMilliseconds >= finalSlice, "maximum solve slice includes the completing slice");
            session.TickSolve(4096);
            Check(session.LastSolveSliceMilliseconds == finalSlice, "idle updates preserve completed solve evidence");
            session.StartSolve();
            Check(session.SolveMilliseconds == 0 && session.LastSolveSliceMilliseconds == 0 && session.MaxSolveSliceMilliseconds == 0, "repeat solve resets all timing fields");
            session.CancelSolve();
        }
        private static void LifecycleInterleavings()
        {
            var session = new CubeSession();
            session.QueueNotation("R U F' L2");
            string untouched = session.Export(); long version = session.Version;
            ExpectFailure<InvalidOperationException>(() => session.StartSolve(), "solve blocked by pending free moves");
            Check(session.Export() == untouched && session.Version == version && session.HasPendingMoves, "rejected solve leaves pending state intact");
            session.TryBeginMove(out _, out _, out _);
            string committed = session.Export(); version = session.Version;
            ExpectFailure<InvalidOperationException>(() => session.StartSolve(), "solve blocked by in-flight move");
            Reject(() => session.Import("{}"), "invalid import during animation");
            Reject(() => session.QueueNotation("B Q"), "invalid notation during animation");
            Check(session.IsMoveInFlight && session.HasPendingMoves && session.Export() == committed && session.Version == version, "invalid operations preserve in-flight target and queue");
            session.CompleteMove(); Drain(session);
            Check(session.Version == 4, "four rapid queued moves commit exactly four times after rejected commands");
            string source = session.Export(); Solve(session); var plan = session.Plan;
            session.Play(); session.TryBeginMove(out _, out _, out _);
            committed = session.Export(); version = session.Version;
            Reject(() => session.Import("{}"), "invalid import while solution playing");
            Check(session.Plan == plan && session.IsPlaying && session.IsMoveInFlight && session.Cursor == 1 && session.Version == version && session.Export() == committed, "invalid import preserves active playback lifecycle");
            session.StepBackward();
            Check(session.Plan == plan && !session.IsPlaying && !session.IsMoveInFlight && !session.HasPendingMoves && session.Cursor == 0 && session.Export() == source, "backward during animation cancels presentation and restores source");
            session.StepForward(); session.StepForward();
            Check(session.TryBeginMove(out _, out _, out _) && session.Cursor == 1, "repeated next before a frame schedules only one move");
            session.CompleteMove();
            Check(!session.TryBeginMove(out _, out _, out _) && session.Cursor == 1, "repeated next has no extra queued turn");
            session.Play(); session.TryBeginMove(out _, out _, out _);
            session.JumpToStep(5);
            Check(session.Plan == plan && !session.IsMoveInFlight && !session.IsPlaying && session.Export() == plan.Steps[5].BeforeState, "phase jump during animation restores exact phase snapshot");
            int cursor = session.Cursor; committed = session.Export();
            ExpectFailure<ArgumentOutOfRangeException>(() => session.JumpToStep(-1), "invalid phase index");
            ExpectFailure<ArgumentException>(() => session.Practice("unsupported"), "unsupported practice case");
            Check(session.Plan == plan && session.Cursor == cursor && session.Export() == committed, "invalid phase/practice preserves tutorial");
            session.JumpToStep(0); session.StepForward(); session.Reset();
            Check(!session.TryBeginMove(out _, out _, out _) && session.State.IsSolved && !session.HasPlan, "reset before scheduled next prevents stale commit");

            session.Import(source); session.StartSolve(); session.TickSolve(1);
            session.QueueNotation("F");
            Check(!session.IsSolving && !session.HasPlan && session.LastOutcome == SolveOutcome.Cancelled, "manual input cancels current search before queueing");
            session.TickSolve(4096); Drain(session);
            Check(!session.HasPlan && session.Export() != source, "cancelled job cannot publish over manual change");
            session.Undo(); Check(session.Export() == source && session.CanRedo, "undo exposes alternate branch");
            Reject(() => session.QueueNotation("F Q"), "invalid alternate move"); Check(session.CanRedo, "invalid alternate move preserves redo");
            session.QueueNotation("B"); Drain(session); Check(!session.CanRedo, "committed alternate move clears redo branch");

            var limited = new CubeSession(); Solve(limited); var solvedPlan = limited.Plan;
            ExpectFailure<InvalidOperationException>(() => limited.QueueNotation(new string('R',4097)), "queue limit rejects oversized batch");
            Check(limited.Plan == solvedPlan && limited.State.IsSolved && limited.Version == 0 && !limited.HasPendingMoves, "queue limit leaves an existing verified plan intact");
            limited.QueueNotation("R U");
            ExpectFailure<InvalidOperationException>(() => limited.QueueNotation(new string('F',4095)), "queue limit includes previously queued moves");
            Drain(limited); Check(limited.Version == 2, "overflow rejection does not append partial turns");
            limited.Undo(); limited.Undo(); Check(limited.State.IsSolved, "overflow preserved exactly the original two turns");
            limited.Speed = 4;
            ExpectFailure<ArgumentException>(() => limited.Speed = float.NaN, "NaN speed rejected");
            ExpectFailure<ArgumentException>(() => limited.Speed = float.PositiveInfinity, "infinite speed rejected");
            Check(limited.Speed == 4, "invalid speed leaves playback setting intact");
        }
        private static void ExpectFailure<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { Check(true, message); return; }
            throw new Exception("Session: expected " + typeof(T).Name + ": " + message);
        }
        private static void ZeroMovePhaseSelection()
        {
            var session = new CubeSession(); session.Practice("sune"); Solve(session);
            string source = session.Export();
            for (int index = 0; index < 6; index++)
            {
                Check(session.Plan.Steps[index].Moves.Count == 0, "Sune has an already achieved phase " + index);
                session.JumpToStep(index);
                Check(session.SelectedStepIndex == index && session.Cursor == 0 && session.Export() == source,
                    "zero-move jump retains distinct explanation selection without changing facelets");
                session.Pause(); Check(session.SelectedStepIndex == index, "pause retains selected zero-move lesson");
            }
            ExpectFailure<ArgumentOutOfRangeException>(() => session.JumpToStep(99), "out-of-range lesson selection");
            Check(session.SelectedStepIndex == 5, "rejected jump retains selected explanation");
            session.StepForward();
            Check(session.TryBeginMove(out _, out _, out _) && session.SelectedStepIndex == -1,
                "actual playback clears manual phase selection so the moving algorithm is described");
            session.CompleteMove(); session.StepBackward();
            Check(session.SelectedStepIndex == -1 && session.Export() == source, "backward restores automatic phase selection");
            session.JumpToStep(2); session.QueueNotation("R");
            Check(session.SelectedStepIndex == -1 && !session.HasPlan, "free play clears selected lesson and its stale plan");
        }
    }
}
