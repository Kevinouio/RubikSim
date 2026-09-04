using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using RubikSim.Application;
using RubikSim.Core;
using RubikSim.Solver;
using UnityEngine;

namespace RubikSim.UnityView
{
    /// <summary>Unity/Web adapter. The Unity-free session owns every commit and all lesson history.</summary>
    public sealed class CubeHost : MonoBehaviour
    {
        private CubeSession session;
        private CubeView view;
        private CubeInput input;
        private string lastFingerprint, error = "", activeMove = "";
        private float nextPublish, frameMilliseconds;
        private bool animationAgreementFailed;
        private bool previousSolveWork, previousPlaybackWork;
        private readonly List<float> solveFrames = new List<float>(256), playbackFrames = new List<float>(256);
        public CubeSession Session => session;
        public CubeView View => view;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RubikPublishState(string json);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureHost()
        {
            if (UnityEngine.Object.FindFirstObjectByType<CubeHost>() == null)
                new GameObject("RubikBridge").AddComponent<CubeHost>();
        }

        private void Awake()
        {
            gameObject.name = "RubikBridge";
            session = new CubeSession();
            UnityEngine.Application.targetFrameRate = 60;
            UnityEngine.Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            var cube = new GameObject("Exact cube presentation");
            cube.transform.SetParent(transform, false);
            view = cube.AddComponent<CubeView>();
            view.AbortAndRender(session.State.ToFacelets());
            var cameraObject = new GameObject("Orbit camera");
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .072f, .11f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 50;
            camera.fieldOfView = 42;
            input = gameObject.AddComponent<CubeInput>();
            input.Initialize(camera);
            input.NotationRequested += notation => Execute("notation", notation);
            input.ControlRequested += action => Execute(action, "");
        }

        private void Start() { Publish(true); }

        private void Update()
        {
            // Delta time includes the preceding frame's work, including the slice that completed a solve.
            float interval = Time.unscaledDeltaTime * 1000;
            if (previousSolveWork && solveFrames.Count < 256) solveFrames.Add(interval);
            if (previousPlaybackWork && playbackFrames.Count < 256) playbackFrames.Add(interval);
            previousSolveWork = session.IsSolving;
            previousPlaybackWork = session.IsPlaying || view.IsAnimating || session.HasPendingMoves;
            frameMilliseconds = Mathf.Lerp(frameMilliseconds, Time.unscaledDeltaTime * 1000, .06f);
            bool wasAnimating = view.IsAnimating;
            view.AdvanceAnimation(Time.unscaledDeltaTime);
            if (wasAnimating && !view.IsAnimating)
            {
                session.CompleteMove();
                if (!view.LastAnimationAgreed) animationAgreementFailed = true;
            }
            session.TickSolve(4096); // Solver also yields at its measured ~4 ms slice boundary.
            if (!view.IsAnimating && session.TryBeginMove(out Move move, out CubeState before, out CubeState after))
            {
                if (view.ReadRenderedFacelets() != before.ToFacelets())
                {
                    error = "View invariant failed before a queued turn. The view was rebuilt from exact state.";
                    animationAgreementFailed = true;
                    view.AbortAndRender(before.ToFacelets());
                }
                activeMove = move.ToString();
                view.BeginTurn(move, after.ToFacelets(), 1f / session.Speed);
            }
            input.PickingEnabled = !view.IsAnimating;
            Publish(false);
        }

        /// <summary>Called by unityInstance.SendMessage('RubikBridge','SendCommand',JSON.stringify({action,value})).</summary>
        [UnityEngine.Scripting.Preserve]
        public void SendCommand(string json)
        {
            try
            {
                var command = JsonUtility.FromJson<BrowserCommand>(json);
                if (command == null || string.IsNullOrEmpty(command.action)) throw new FormatException("A command action is required.");
                Execute(command.action, command.value ?? "");
            }
            catch (Exception exception) { error = exception.Message; Publish(true); }
        }

        private void Execute(string action, string value)
        {
            try
            {
                if (action != "snapshot") error = "";
                bool snap = false;
                switch (action)
                {
                    case "notation": session.QueueNotation(value); break;
                    case "scramble": session.Scramble(int.Parse(value, CultureInfo.InvariantCulture)); break;
                    case "reset": session.Reset(); snap = true; break;
                    case "undo": session.Undo(); snap = true; break;
                    case "redo": session.Redo(); snap = true; break;
                    case "import": session.Import(value); snap = true; break;
                    case "solve": session.StartSolve(); break;
                    case "cancel": session.CancelSolve(); break;
                    case "play": session.Play(); break;
                    case "pause": session.Pause(); break;
                    case "togglePlay": if (session.IsPlaying) session.Pause(); else session.Play(); break;
                    case "next": session.StepForward(); break;
                    case "previous": session.StepBackward(); snap = !session.IsMoveInFlight; break;
                    case "jump": session.JumpToStep(int.Parse(value, CultureInfo.InvariantCulture)); snap = true; break;
                    case "speed": session.Speed = float.Parse(value, CultureInfo.InvariantCulture); break;
                    case "resetView": input.ResetView(); break;
                    case "practice": session.Practice(value); snap = true; break;
                    case "snapshot": break;
                    default: throw new ArgumentException("Unknown command: " + action);
                }
                if (snap) { view.AbortAndRender(session.State.ToFacelets()); activeMove = ""; }
                Publish(true);
            }
            catch (Exception exception) { error = exception.Message; Publish(true); }
        }

        public BrowserState CaptureState()
        {
            var state = session.State;
            var plan = session.Plan;
            var steps = new List<BrowserStep>();
            int start = 0, active = -1;
            int displayCursor = view.IsAnimating ? Math.Max(0, session.Cursor - 1) : session.Cursor;
            if (plan != null)
            {
                for (int i = 0; i < plan.Steps.Count; i++)
                {
                    var step = plan.Steps[i];
                    if (displayCursor >= start) active = i;
                    steps.Add(new BrowserStep
                    {
                        phase = step.Phase, caseId = step.CaseId, goal = step.Goal,
                        recognition = step.Recognition, explanation = step.Explanation,
                        orientation = step.ReferenceOrientation, source = step.Source,
                        before = step.BeforeState, after = step.AfterState,
                        start = start, count = step.Moves.Count,
                        setup = Tokens(step.SetupMoves), algorithm = Tokens(step.AlgorithmMoves), alignment = Tokens(step.AlignmentMoves),
                        highlights = HighlightIndices(state, step.HighlightedPieces)
                    });
                    start += step.Moves.Count;
                }
            }
            if (session.SelectedStepIndex >= 0 && session.SelectedStepIndex < steps.Count)
                active = session.SelectedStepIndex;
            if (active >= 0) view.Highlight(steps[active].highlights); else view.Highlight(Array.Empty<int>());
            string rendered = view.ReadRenderedFacelets();
            var targets = new List<StickerTarget>();
            for (int index = 0; index < 54; index++)
                if (view.TryGetStickerTarget(index, input.ViewCamera, out Vector2 point))
                    targets.Add(new StickerTarget { index = index, x = point.x, y = point.y });
            return new BrowserState
            {
                ready = true, version = session.Version, facelets = state.ToFacelets(), viewFacelets = rendered,
                viewAgrees = !view.IsAnimating && rendered == state.ToFacelets(), animationAgrees = !animationAgreementFailed,
                serialized = state.Serialize(), status = session.Status, error = error,
                solved = state.IsSolved, animating = view.IsAnimating, pending = session.HasPendingMoves,
                solving = session.IsSolving, playing = session.IsPlaying, speed = session.Speed,
                cursor = session.Cursor, totalMoves = plan == null ? 0 : plan.Moves.Count,
                currentMove = view.IsAnimating ? activeMove : "", moves = plan == null ? Array.Empty<string>() : Tokens(plan.Moves),
                steps = steps.ToArray(), activeStep = active, solverProgress = session.SolverProgress,
                frameMs = frameMilliseconds, solveMs = (float)session.SolveMilliseconds,
                solveSliceMs = (float)session.LastSolveSliceMilliseconds, maxSolveSliceMs = (float)session.MaxSolveSliceMilliseconds,
                camera = input.CameraPose, stickerTargets = targets.ToArray(),
                solveFrameSamplesMs = solveFrames.ToArray(), playbackFrameSamplesMs = playbackFrames.ToArray(),
                canUndo = session.CanUndo, canRedo = session.CanRedo, hasPlan = session.HasPlan,
                outcome = session.LastOutcome.HasValue ? session.LastOutcome.Value.ToString() : ""
            };
        }

        private static string[] Tokens(IEnumerable<Move> moves) => moves.Select(move => move.ToString()).ToArray();

        private static int[] HighlightIndices(CubeState state, IReadOnlyList<string> names)
        {
            var requested = new HashSet<string>(names.Select(name => new string(name.OrderBy(c => c).ToArray())));
            var groups = new Dictionary<Int3, List<int>>();
            for (int i = 0; i < 54; i++)
            {
                var position = CubeGeometry.GetFacelet(i).Position;
                if (!groups.ContainsKey(position)) groups[position] = new List<int>();
                groups[position].Add(i);
            }
            string canonical = state.ToCanonical().ToFacelets();
            var indices = new List<int>();
            foreach (var group in groups.Values)
            {
                string colors = new string(group.Select(index => canonical[index]).OrderBy(c => c).ToArray());
                if (requested.Contains(colors)) indices.AddRange(group);
            }
            return indices.ToArray();
        }

        private void Publish(bool force)
        {
            if (session == null || view == null || input == null) return;
            if (!force && Time.realtimeSinceStartup < nextPublish) return;
            string fingerprint = session.Version + "/" + view.IsAnimating + "/" + session.HasPendingMoves + "/" +
                session.IsSolving + "/" + session.SolverProgress + "/" + session.Status + "/" + session.IsPlaying + "/" +
                session.Cursor + "/" + session.Speed + "/" + error + "/" + session.HasPlan + "/" + string.Join(",", input.CameraPose);
            if (!force && fingerprint == lastFingerprint && solveFrames.Count == 0 && playbackFrames.Count == 0) return;
            lastFingerprint = fingerprint;
            nextPublish = Time.realtimeSinceStartup + .1f;
            var snapshot = CaptureState();
#if UNITY_WEBGL && !UNITY_EDITOR
            RubikPublishState(JsonUtility.ToJson(snapshot));
#endif
            solveFrames.Clear(); playbackFrames.Clear();
        }

        [Serializable] private sealed class BrowserCommand { public string action = ""; public string value = ""; }
        [Serializable] public sealed class BrowserState
        {
            public bool ready, viewAgrees, animationAgrees, solved, animating, pending, solving, playing, canUndo, canRedo, hasPlan;
            public long version;
            public string facelets, viewFacelets, serialized, status, error, currentMove, solverProgress, outcome;
            public float speed, frameMs, solveMs, solveSliceMs, maxSolveSliceMs;
            public int cursor, totalMoves, activeStep;
            public string[] moves;
            public BrowserStep[] steps;
            public StickerTarget[] stickerTargets;
            public float[] camera;
            public float[] solveFrameSamplesMs, playbackFrameSamplesMs;
        }
        [Serializable] public sealed class StickerTarget { public int index; public float x, y; }
        [Serializable] public sealed class BrowserStep
        {
            public string phase, caseId, goal, recognition, explanation, orientation, source, before, after;
            public int start, count;
            public string[] setup, algorithm, alignment;
            public int[] highlights;
        }
    }
}
