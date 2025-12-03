using System;
using System.Collections.Generic;

namespace RubikSim.Core
{
    public enum CubeFace
    {
        Up,
        Right,
        Front,
        Down,
        Left,
        Back
    }

    public enum RotationDirection
    {
        Clockwise,
        CounterClockwise
    }

    public enum CubeRotationAmount
    {
        Single,
        Double
    }

    /// <summary>
    /// Represents a single move on the cube (e.g. R, U', F2).
    /// </summary>
    [Serializable]
    public struct CubeMove
    {
        public CubeFace Face;
        public RotationDirection Direction;
        public CubeRotationAmount Amount;

        public CubeMove(CubeFace face, RotationDirection direction = RotationDirection.Clockwise, CubeRotationAmount amount = CubeRotationAmount.Single)
        {
            Face = face;
            Direction = direction;
            Amount = amount;
        }

        public CubeMove Inverse()
        {
            if (Amount == CubeRotationAmount.Double)
            {
                return this; // double turns are their own inverse
            }

            return new CubeMove(Face, Direction == RotationDirection.Clockwise ? RotationDirection.CounterClockwise : RotationDirection.Clockwise, CubeRotationAmount.Single);
        }

        public override string ToString()
        {
            return CubeNotation.EncodeMove(this);
        }
    }

    /// <summary>
    /// Helper methods for parsing/encoding sequences of moves.
    /// </summary>
    public static class CubeNotation
    {
        private static readonly Dictionary<char, CubeFace> FaceLookup = new()
        {
            ['U'] = CubeFace.Up,
            ['R'] = CubeFace.Right,
            ['F'] = CubeFace.Front,
            ['D'] = CubeFace.Down,
            ['L'] = CubeFace.Left,
            ['B'] = CubeFace.Back
        };

        public static IEnumerable<CubeMove> ParseAlgorithm(string algorithm)
        {
            if (string.IsNullOrWhiteSpace(algorithm))
            {
                yield break;
            }

            var tokens = algorithm.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                yield return ParseMove(token.Trim());
            }
        }

        public static CubeMove ParseMove(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Move token cannot be empty", nameof(token));
            }

            var faceChar = char.ToUpperInvariant(token[0]);
            if (!FaceLookup.TryGetValue(faceChar, out var face))
            {
                throw new ArgumentException($"Unknown move face '{token[0]}'", nameof(token));
            }

            var direction = RotationDirection.Clockwise;
            var amount = CubeRotationAmount.Single;

            for (var i = 1; i < token.Length; i++)
            {
                var suffix = token[i];
                if (suffix == '\'')
                {
                    direction = RotationDirection.CounterClockwise;
                }
                else if (suffix == '2')
                {
                    amount = CubeRotationAmount.Double;
                }
            }

            return new CubeMove(face, direction, amount);
        }

        public static string EncodeMove(CubeMove move)
        {
            var faceChar = move.Face switch
            {
                CubeFace.Up => 'U',
                CubeFace.Right => 'R',
                CubeFace.Front => 'F',
                CubeFace.Down => 'D',
                CubeFace.Left => 'L',
                CubeFace.Back => 'B',
                _ => '?'
            };

            var suffix = move.Amount == CubeRotationAmount.Double
                ? "2"
                : move.Direction == RotationDirection.CounterClockwise ? "'" : string.Empty;

            return $"{faceChar}{suffix}";
        }
    }
}
