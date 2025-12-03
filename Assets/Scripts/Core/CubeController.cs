using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RubikSim.Core
{
    [DisallowMultipleComponent]
    public class CubeController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private CubeBuilder builder = default!;
        [SerializeField] private CubeAnimator animator = default!;
        [SerializeField] private CubeInputHandler? inputHandler;
        [SerializeField] private TutorialDirector? tutorialDirector;
        [SerializeField] private CubeUIController? uiController;

        [Header("Settings")]
        [SerializeField, Range(10, 40)] private int scrambleLength = 25;

        private CubeState _state = default!;
        private CubeSolver _solver = default!;
        private readonly System.Random _random = new();
        private readonly List<CubeMove> _lastScramble = new();
        private Coroutine? _scrambleRoutine;
        private int _moveCount;
        private bool _isScrambling;
        private bool _lastSolvedState;

        public event Action<int>? MoveCountChanged;
        public event Action<bool>? SolveStateChanged;
        public event Action<string>? ScrambleChanged;

        public CubeState State => _state;
        public Transform CubeRoot => builder.transform;
        public bool IsBusy => _isScrambling || animator.IsAnimating || (tutorialDirector != null && tutorialDirector.IsRunning);
        public bool CanAcceptInput => !IsBusy;

        private void Awake()
        {
            _state = new CubeState();
            _solver = new CubeSolver();
        }

        private void Start()
        {
            builder.BuildCube(_state);
            animator.Initialize(_state, builder);
            animator.MoveCompleted += OnMoveCompleted;

            if (inputHandler != null)
            {
                inputHandler.Initialize(this);
            }

            if (tutorialDirector != null)
            {
                tutorialDirector.Initialize(this, animator, builder);
            }

            if (uiController != null)
            {
                uiController.Initialize(this, tutorialDirector);
            }

            ResetCube();
        }

        private void OnDestroy()
        {
            animator.MoveCompleted -= OnMoveCompleted;
        }

        public void QueueManualMove(CubeMove move)
        {
            if (!CanAcceptInput)
            {
                return;
            }

            animator.PlayMove(move);
        }

        public void PlayMoveSequence(IEnumerable<CubeMove> moves)
        {
            animator.PlayAlgorithm(moves);
        }

        public void HighlightPieces(IEnumerable<string> pieceIds, Color color)
        {
            builder.HighlightPieces(pieceIds, color);
        }

        public void ClearHighlights()
        {
            builder.ClearHighlights();
        }

        public void Scramble()
        {
            if (_scrambleRoutine != null)
            {
                StopCoroutine(_scrambleRoutine);
            }

            _scrambleRoutine = StartCoroutine(ScrambleRoutine());
        }

        public void ResetCube()
        {
            if (_scrambleRoutine != null)
            {
                StopCoroutine(_scrambleRoutine);
                _scrambleRoutine = null;
            }

            animator.ClearQueue();
            _state.ResetToSolved();
            builder.ClearCube();
            builder.BuildCube(_state);
            _moveCount = 0;
            MoveCountChanged?.Invoke(_moveCount);
            _lastScramble.Clear();
            ScrambleChanged?.Invoke(string.Empty);
            _isScrambling = false;
            _lastSolvedState = true;
            SolveStateChanged?.Invoke(true);
            builder.ClearHighlights();
        }

        public void RequestSolve(bool autoPlay)
        {
            if (IsBusy)
            {
                return;
            }

            builder.ClearHighlights();
            var workingState = _state.Clone();
            var result = _solver.Solve(workingState);
            if (tutorialDirector != null)
            {
                tutorialDirector.PlaySolution(result, autoPlay);
            }
            else
            {
                animator.PlayAlgorithm(result.AllMoves);
            }
        }

        public string GetScrambleNotation()
        {
            return CubeScrambler.Format(_lastScramble);
        }

        private IEnumerator ScrambleRoutine()
        {
            _isScrambling = true;
            builder.ClearHighlights();
            tutorialDirector?.StopTutorial();
            animator.ClearQueue();
            _state.ResetToSolved();
            builder.ClearCube();
            builder.BuildCube(_state);
            _moveCount = 0;
            MoveCountChanged?.Invoke(_moveCount);

            _lastScramble.Clear();
            var scramble = CubeScrambler.Generate(scrambleLength, _random);
            _lastScramble.AddRange(scramble);
            ScrambleChanged?.Invoke(CubeScrambler.Format(scramble));

            animator.PlayAlgorithm(scramble);
            while (animator.IsAnimating)
            {
                yield return null;
            }

            _isScrambling = false;
            CheckSolved();
        }

        private void OnMoveCompleted(CubeMove move)
        {
            _moveCount++;
            MoveCountChanged?.Invoke(_moveCount);
            CheckSolved();
        }

        private void CheckSolved()
        {
            var solved = _state.IsSolved();
            if (solved != _lastSolvedState)
            {
                _lastSolvedState = solved;
                SolveStateChanged?.Invoke(solved);
            }
        }
    }
}
