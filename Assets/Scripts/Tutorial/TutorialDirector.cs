using System;
using System.Collections;
using RubikSim.Solver;
using UnityEngine;

namespace RubikSim.Core
{
    public class TutorialDirector : MonoBehaviour
    {
        [SerializeField] private Color highlightColor = Color.cyan;
        [SerializeField, Range(0f, 1f)] private float previewDelay = 0.35f;
        [SerializeField, Range(0f, 0.5f)] private float autoStepPause = 0.15f;

        private CubeController? _controller;
        private CubeAnimator? _animator;
        private CubeBuilder? _builder;
        private SolverResult? _currentResult;
        private int _currentIndex;
        private Coroutine? _autoRoutine;
        private Coroutine? _stepRoutine;

        public bool IsRunning => _currentResult != null;

        public event Action<SolverStep?, int, int>? StepChanged;

        public void Initialize(CubeController controller, CubeAnimator animator, CubeBuilder builder)
        {
            _controller = controller;
            _animator = animator;
            _builder = builder;
        }

        public void PlaySolution(SolverResult result, bool autoPlay)
        {
            StopTutorial();
            if (result.Steps.Count == 0)
            {
                return;
            }

            _currentResult = result;
            _currentIndex = 0;

            if (autoPlay)
            {
                _autoRoutine = StartCoroutine(AutoRoutine());
            }
            else
            {
                PreviewCurrentStep();
            }
        }

        public void PlayNextStep()
        {
            if (_currentResult == null || _animator == null || _currentIndex >= _currentResult.Steps.Count)
            {
                return;
            }

            if (_animator.IsAnimating || _stepRoutine != null)
            {
                return;
            }

            var step = _currentResult.Steps[_currentIndex];
            _stepRoutine = StartCoroutine(ExecuteStep(step, false));
        }

        public void StopTutorial()
        {
            if (_autoRoutine != null)
            {
                StopCoroutine(_autoRoutine);
                _autoRoutine = null;
            }

            if (_stepRoutine != null)
            {
                StopCoroutine(_stepRoutine);
                _stepRoutine = null;
            }

            _currentResult = null;
            _currentIndex = 0;
            _controller?.ClearHighlights();
            StepChanged?.Invoke(null, 0, 0);
        }

        private IEnumerator AutoRoutine()
        {
            while (_currentResult != null && _currentIndex < _currentResult.Steps.Count)
            {
                var step = _currentResult.Steps[_currentIndex];
                yield return ExecuteStep(step, true);
                if (autoStepPause > 0f)
                {
                    yield return new WaitForSeconds(autoStepPause);
                }
            }

            StopTutorial();
        }

        private IEnumerator ExecuteStep(SolverStep step, bool autoAdvance)
        {
            if (_controller == null || _animator == null)
            {
                yield break;
            }

            _controller.HighlightPieces(step.HighlightPieces, highlightColor);
            StepChanged?.Invoke(step, _currentIndex + 1, _currentResult!.Steps.Count);

            if (previewDelay > 0f)
            {
                yield return new WaitForSeconds(previewDelay);
            }

            _animator.PlayAlgorithm(step.Moves);
            while (_animator.IsAnimating)
            {
                yield return null;
            }

            _currentIndex++;
            _stepRoutine = null;

            if (_currentResult == null || _currentIndex >= _currentResult.Steps.Count)
            {
                StopTutorial();
            }
            else if (!autoAdvance)
            {
                PreviewCurrentStep();
            }
        }

        private void PreviewCurrentStep()
        {
            if (_currentResult == null || _controller == null)
            {
                StepChanged?.Invoke(null, 0, 0);
                return;
            }

            if (_currentIndex >= _currentResult.Steps.Count)
            {
                StepChanged?.Invoke(null, 0, 0);
                return;
            }

            var step = _currentResult.Steps[_currentIndex];
            _controller.HighlightPieces(step.HighlightPieces, highlightColor);
            StepChanged?.Invoke(step, _currentIndex + 1, _currentResult.Steps.Count);
        }
    }
}
