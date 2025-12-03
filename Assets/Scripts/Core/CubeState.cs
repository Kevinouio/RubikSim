using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RubikSim.Core
{
    /// <summary>
    /// Logical representation of the cube that tracks cubie positions and orientations.
    /// </summary>
    [Serializable]
    public class CubeState
    {
        private readonly Dictionary<Vector3Int, CubePieceState> _pieces = new();

        private static readonly Dictionary<Vector3Int, CubeFace> AxisToFace = new()
        {
            [Vector3Int.up] = CubeFace.Up,
            [Vector3Int.down] = CubeFace.Down,
            [Vector3Int.right] = CubeFace.Right,
            [Vector3Int.left] = CubeFace.Left,
            [Vector3Int.forward] = CubeFace.Front,
            [Vector3Int.back] = CubeFace.Back
        };

        private static readonly Dictionary<CubeFace, Vector3Int> FaceToAxis = AxisToFace.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        private static readonly Dictionary<CubeFace, FaceOrientation> FaceOrientations = new()
        {
            [CubeFace.Up] = new FaceOrientation(Vector3Int.up, Vector3Int.forward, Vector3Int.right),
            [CubeFace.Down] = new FaceOrientation(Vector3Int.down, Vector3Int.back, Vector3Int.right),
            [CubeFace.Front] = new FaceOrientation(Vector3Int.forward, Vector3Int.up, Vector3Int.left),
            [CubeFace.Back] = new FaceOrientation(Vector3Int.back, Vector3Int.up, Vector3Int.right),
            [CubeFace.Right] = new FaceOrientation(Vector3Int.right, Vector3Int.up, Vector3Int.forward),
            [CubeFace.Left] = new FaceOrientation(Vector3Int.left, Vector3Int.up, Vector3Int.back)
        };

        private static readonly Dictionary<CubeFace, int> FaceLabelOrder = new()
        {
            [CubeFace.Up] = 0,
            [CubeFace.Right] = 1,
            [CubeFace.Front] = 2,
            [CubeFace.Down] = 3,
            [CubeFace.Left] = 4,
            [CubeFace.Back] = 5
        };

        public IReadOnlyDictionary<Vector3Int, CubePieceState> Pieces => _pieces;

        public CubeState()
        {
            ResetToSolved();
        }

        private CubeState(bool skipInit)
        {
        }

        public CubeState Clone()
        {
            var clone = new CubeState(true);
            foreach (var kvp in _pieces)
            {
                clone._pieces[kvp.Key] = kvp.Value.Clone();
            }

            return clone;
        }

        public void ResetToSolved()
        {
            _pieces.Clear();

            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    for (var z = -1; z <= 1; z++)
                    {
                        if (x == 0 && y == 0 && z == 0)
                        {
                            continue;
                        }

                        if (Mathf.Abs(x) + Mathf.Abs(y) + Mathf.Abs(z) > 2 && (x == 0 || y == 0 || z == 0))
                        {
                            continue; // skip hidden center pieces (should never happen but guard)
                        }

                        var position = new Vector3Int(x, y, z);
                        if (Mathf.Abs(x) + Mathf.Abs(y) + Mathf.Abs(z) == 0)
                        {
                            continue;
                        }

                        var stickers = CreateStickers(position);
                        var type = DeterminePieceType(position);
                        var id = BuildPieceId(stickers.Values);
                        var piece = new CubePieceState(id, type, position, stickers);
                        _pieces[position] = piece;
                    }
                }
            }
        }

        public void ApplyAlgorithm(IEnumerable<CubeMove> moves)
        {
            foreach (var move in moves)
            {
                ApplyMove(move);
            }
        }

        public void ApplyAlgorithm(string notation)
        {
            ApplyAlgorithm(CubeNotation.ParseAlgorithm(notation));
        }

        public void ApplyMove(CubeMove move)
        {
            var descriptor = FaceOrientations[move.Face];
            var axis = descriptor.Normal;
            var turns = move.Amount == CubeRotationAmount.Double ? 2 : 1;
            var signedAngle = move.Direction == RotationDirection.Clockwise ? -90f : 90f;

            for (var i = 0; i < turns; i++)
            {
                RotateLayer(axis, signedAngle);
            }
        }

        private void RotateLayer(Vector3Int axis, float angle)
        {
            var affected = _pieces.Values.Where(p => Vector3Int.Dot(p.Position, axis) == 1).ToList();
            var rotation = Quaternion.AngleAxis(angle, axis);

            foreach (var piece in affected)
            {
                _pieces.Remove(piece.Position);
                piece.ApplyRotation(rotation);
                _pieces[piece.Position] = piece;
            }
        }

        public CubeFace[,] GetFaceColors(CubeFace face)
        {
            var descriptor = FaceOrientations[face];
            var colors = new CubeFace[3, 3];

            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    var offsetRow = 1 - row;
                    var offsetCol = col - 1;
                    var position = descriptor.Normal + descriptor.Up * offsetRow + descriptor.Right * offsetCol;
                    if (_pieces.TryGetValue(position, out var piece) && piece.TryGetColor(descriptor.Normal, out var stickerFace))
                    {
                        colors[row, col] = stickerFace;
                    }
                    else
                    {
                        colors[row, col] = AxisToFace[descriptor.Normal];
                    }
                }
            }

            return colors;
        }

        public bool IsSolved()
        {
            foreach (var piece in _pieces.Values)
            {
                foreach (var sticker in piece.Stickers)
                {
                    if (AxisToFace.TryGetValue(sticker.Key, out var expected) && expected == sticker.Value)
                    {
                        continue;
                    }

                    return false;
                }
            }

            return true;
        }

        public CubePieceState FindPiece(string id)
        {
            return _pieces.Values.First(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<CubePieceState> EnumeratePieces(CubePieceType? type = null)
        {
            return type.HasValue ? _pieces.Values.Where(p => p.Type == type.Value) : _pieces.Values;
        }

        public static CubeFace GetFaceFromAxis(Vector3Int axis)
        {
            if (!AxisToFace.TryGetValue(axis, out var face))
            {
                throw new ArgumentException($"Unknown axis {axis}");
            }

            return face;
        }

        public static Vector3Int GetAxisFromFace(CubeFace face)
        {
            return FaceToAxis[face];
        }

        private static Dictionary<Vector3Int, CubeFace> CreateStickers(Vector3Int position)
        {
            var stickers = new Dictionary<Vector3Int, CubeFace>();

            if (position.x != 0)
            {
                var normal = new Vector3Int(Mathf.Clamp(position.x, -1, 1), 0, 0);
                stickers[normal] = AxisToFace[normal];
            }

            if (position.y != 0)
            {
                var normal = new Vector3Int(0, Mathf.Clamp(position.y, -1, 1), 0);
                stickers[normal] = AxisToFace[normal];
            }

            if (position.z != 0)
            {
                var normal = new Vector3Int(0, 0, Mathf.Clamp(position.z, -1, 1));
                stickers[normal] = AxisToFace[normal];
            }

            return stickers;
        }

        private static CubePieceType DeterminePieceType(Vector3Int position)
        {
            var components = 0;
            if (position.x != 0) components++;
            if (position.y != 0) components++;
            if (position.z != 0) components++;

            return components switch
            {
                1 => CubePieceType.Center,
                2 => CubePieceType.Edge,
                3 => CubePieceType.Corner,
                _ => CubePieceType.Center
            };
        }

        private static string BuildPieceId(IEnumerable<CubeFace> faces)
        {
            var ordered = faces.OrderBy(f => FaceLabelOrder[f])
                .Select(f => f switch
                {
                    CubeFace.Up => 'U',
                    CubeFace.Right => 'R',
                    CubeFace.Front => 'F',
                    CubeFace.Down => 'D',
                    CubeFace.Left => 'L',
                    CubeFace.Back => 'B',
                    _ => '?'
                });

            return new string(ordered.ToArray());
        }

        private readonly struct FaceOrientation
        {
            public Vector3Int Normal { get; }
            public Vector3Int Up { get; }
            public Vector3Int Right { get; }

            public FaceOrientation(Vector3Int normal, Vector3Int up, Vector3Int right)
            {
                Normal = normal;
                Up = up;
                Right = right;
            }
        }
    }

    [Serializable]
    public class CubePieceState
    {
        private Dictionary<Vector3Int, CubeFace> _stickers;

        public string Id { get; }
        public CubePieceType Type { get; }
        public Vector3Int Position { get; private set; }
        public IReadOnlyDictionary<Vector3Int, CubeFace> Stickers => _stickers;

        public CubePieceState(string id, CubePieceType type, Vector3Int position, Dictionary<Vector3Int, CubeFace> stickers)
        {
            Id = id;
            Type = type;
            Position = position;
            _stickers = new Dictionary<Vector3Int, CubeFace>(stickers);
        }

        private CubePieceState(CubePieceState other)
        {
            Id = other.Id;
            Type = other.Type;
            Position = other.Position;
            _stickers = new Dictionary<Vector3Int, CubeFace>(other._stickers);
        }

        public CubePieceState Clone() => new(this);

        public void ApplyRotation(Quaternion rotation)
        {
            var rotatedPosition = rotation * (Vector3)Position;
            Position = Round(rotatedPosition);

            var newStickers = new Dictionary<Vector3Int, CubeFace>();
            foreach (var sticker in _stickers)
            {
                var rotatedNormal = rotation * (Vector3)sticker.Key;
                newStickers[Round(rotatedNormal)] = sticker.Value;
            }

            _stickers = newStickers;
        }

        public bool TryGetColor(Vector3Int normal, out CubeFace face)
        {
            return _stickers.TryGetValue(normal, out face);
        }

        private static Vector3Int Round(Vector3 vector)
        {
            return new Vector3Int(Mathf.RoundToInt(vector.x), Mathf.RoundToInt(vector.y), Mathf.RoundToInt(vector.z));
        }
    }
}
