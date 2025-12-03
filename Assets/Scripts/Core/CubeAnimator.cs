using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RubikSim.Core
{
    public class CubeAnimator : MonoBehaviour
    {
        [SerializeField, Range(0.05f, 1f)] private float moveDuration = 0.25f;
        [SerializeField, Range(0.1f, 5f)] private float speedMultiplier = 1f;

        private readonly Queue<CubeMove> _moveQueue = new();
        private readonly List<Transform> _workingTransforms = new();

        private CubeState? _state;
        private CubeBuilder? _builder;
        private Transform? _pivot;
        private Coroutine? _queueRoutine;

        public bool IsAnimating => _queueRoutine != null;

        private void Awake()
        {
            var pivotObject = new GameObject("RotationPivot");
            pivotObject.hideFlags = HideFlags.HideInHierarchy;
            _pivot = pivotObject.transform;
            _pivot.SetParent(transform, false);
        }

        public void Initialize(CubeState state, CubeBuilder builder)
        {
            _state = state;
            _builder = builder;
        }

        public void PlayMove(CubeMove move)
        {
            _moveQueue.Enqueue(move);
            EnsureQueue();
        }

        public void PlayAlgorithm(IEnumerable<CubeMove> moves)
        {
            foreach (var move in moves)
            {
                _moveQueue.Enqueue(move);
            }

            EnsureQueue();
        }

        public void ClearQueue()
        {
            _moveQueue.Clear();
            if (_queueRoutine != null)
            {
                StopCoroutine(_queueRoutine);
                _queueRoutine = null;
            }
        }

        public void SetSpeed(float multiplier)
        {
            speedMultiplier = Mathf.Clamp(multiplier, 0.1f, 5f);
        }

        private void EnsureQueue()
        {
            if (_queueRoutine == null)
            {
                _queueRoutine = StartCoroutine(RunQueue());
            }
        }

        private IEnumerator RunQueue()
        {
            while (_moveQueue.Count > 0)
            {
                var move = _moveQueue.Dequeue();
                yield return AnimateMove(move);
            }

            _queueRoutine = null;
        }

        private IEnumerator AnimateMove(CubeMove move)
        {
            if (_state == null || _builder == null || _pivot == null)
            {
                yield break;
            }

            var turns = move.Amount == CubeRotationAmount.Double ? 2 : 1;
            for (var i = 0; i < turns; i++)
            {
                var singleMove = new CubeMove(move.Face, move.Direction, CubeRotationAmount.Single);
                yield return AnimateQuarterTurn(singleMove);
            }
        }

        private IEnumerator AnimateQuarterTurn(CubeMove move)
        {
            if (_state == null || _builder == null || _pivot == null)
            {
                yield break;
            }

            var axis = CubeState.GetAxisFromFace(move.Face);
            var affectedPieces = _state.EnumeratePieces().Where(p => Vector3Int.Dot(p.Position, axis) == 1).ToList();
            _workingTransforms.Clear();
            foreach (var piece in affectedPieces)
            {
                if (_builder.TryGetCubelet(piece.Id, out var cubelet))
                {
                    _workingTransforms.Add(cubelet.transform);
                }
            }

            if (_workingTransforms.Count == 0)
            {
                yield break;
            }

            _pivot.SetParent(_builder.transform, false);
            _pivot.localPosition = Vector3.zero;
            _pivot.localRotation = Quaternion.identity;
            _pivot.localScale = Vector3.one;

            foreach (var tr in _workingTransforms)
            {
                tr.SetParent(_pivot, true);
            }

            var signedAngle = move.Direction == RotationDirection.Clockwise ? -90f : 90f;
            var duration = moveDuration / Mathf.Max(speedMultiplier, 0.001f);
            var targetRotation = Quaternion.AngleAxis(signedAngle, axis);

            var elapsed = 0f;
            while (elapsed < duration)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = t * t * (3f - 2f * t);
                _pivot.localRotation = Quaternion.Slerp(Quaternion.identity, targetRotation, eased);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _pivot.localRotation = targetRotation;

            foreach (var tr in _workingTransforms)
            {
                tr.SetParent(_builder.transform, true);
            }

            _pivot.localRotation = Quaternion.identity;
            _state.ApplyMove(move);
        }
    }
}
