using System;
using System.Collections.Generic;
using System.Linq;
using RubikSim.Core;

namespace RubikSim.Solver
{
    /// <summary>
    /// Simplified CFOP-style solver. The implementation focuses on clarity and pedagogy rather than optimality.
    /// </summary>
    public class CubeSolver
    {
        private static readonly CubeFace[] CrossOrder = { CubeFace.Front, CubeFace.Right, CubeFace.Back, CubeFace.Left };
        private static readonly (CubeFace sideA, CubeFace sideB)[] CornerTargets =
        {
            (CubeFace.Front, CubeFace.Right),
            (CubeFace.Right, CubeFace.Back),
            (CubeFace.Back, CubeFace.Left),
            (CubeFace.Left, CubeFace.Front)
        };

        private static readonly (CubeFace left, CubeFace right)[] SecondLayerTargets =
        {
            (CubeFace.Front, CubeFace.Right),
            (CubeFace.Right, CubeFace.Back),
            (CubeFace.Back, CubeFace.Left),
            (CubeFace.Left, CubeFace.Front)
        };

        private static readonly CubeFace[] TopRing = { CubeFace.Front, CubeFace.Right, CubeFace.Back, CubeFace.Left };

        private readonly List<SolverStep> _steps = new();

        public SolverResult Solve(CubeState snapshot)
        {
            var working = snapshot.Clone();
            _steps.Clear();

            SolveCross(working);
            SolveFirstLayerCorners(working);
            SolveSecondLayerEdges(working);
            SolveLastLayer(working);

            var result = new SolverResult();
            foreach (var step in _steps)
            {
                result.AddStep(step);
            }

            return result;
        }

        private void SolveCross(CubeState state)
        {
            foreach (var side in CrossOrder)
            {
                PlaceCrossEdge(state, side);
            }
        }

        private void SolveFirstLayerCorners(CubeState state)
        {
            foreach (var target in CornerTargets)
            {
                InsertFirstLayerCorner(state, target.sideA, target.sideB);
            }
        }

        private void SolveSecondLayerEdges(CubeState state)
        {
            foreach (var target in SecondLayerTargets)
            {
                InsertSecondLayerEdge(state, target.left, target.right);
            }
        }

        private void SolveLastLayer(CubeState state)
        {
            OrientLastLayer(state);
            PermuteLastLayer(state);
        }

        #region Cross Helpers

        private void PlaceCrossEdge(CubeState state, CubeFace side)
        {
            var attempts = 0;
            while (attempts++ < 24)
            {
                var edge = FindEdge(state, CubeFace.Down, side);
                if (IsCrossEdgeSolved(edge, side))
                {
                    return;
                }

                var whiteNormal = GetStickerNormal(edge, CubeFace.Down);

                if (edge.Position.y == 1)
                {
                    AlignTopEdge(state, side, edge);
                    continue;
                }

                if (edge.Position.y == -1 && whiteNormal == CubeState.GetAxisFromFace(CubeFace.Down))
                {
                    ApplyAndRecord(state, "Cross", "Spin base layer to reposition edge", RepeatMove(new CubeMove(CubeFace.Down), 1), new[] { edge.Id });
                    continue;
                }

                if (edge.Position.y == -1)
                {
                    var face = CubeState.GetFaceFromAxis(whiteNormal);
                    var alg = $"{FaceToNotation(face)} U {FaceToNotation(face)}'";
                    ApplyAndRecord(state, "Cross", "Free flipped edge", Parse(alg), new[] { edge.Id });
                    continue;
                }

                var faceFromPosition = GetDominantFace(edge.Position);
                string trigger = faceFromPosition switch
                {
                    CubeFace.Front => "F U F'",
                    CubeFace.Right => "R U R'",
                    CubeFace.Back => "B U B'",
                    CubeFace.Left => "L U L'",
                    _ => "U"
                };
                ApplyAndRecord(state, "Cross", "Lift edge to top layer", Parse(trigger), new[] { edge.Id });
            }
        }

        private void AlignTopEdge(CubeState state, CubeFace side, CubePieceState edge)
        {
            var whiteNormal = GetStickerNormal(edge, CubeFace.Down);
            if (whiteNormal != CubeState.GetAxisFromFace(CubeFace.Up))
            {
                var face = CubeState.GetFaceFromAxis(whiteNormal);
                var alg = $"{FaceToNotation(face)} U {FaceToNotation(face)}'";
                ApplyAndRecord(state, "Cross", "Flip top edge", Parse(alg), new[] { edge.Id });
                return;
            }

            var sideNormal = GetStickerNormal(edge, side);
            var currentSide = CubeState.GetFaceFromAxis(sideNormal);
            var turns = GetUTurns(currentSide, side);
            var moves = new List<CubeMove>();
            if (turns > 0)
            {
                moves.AddRange(RepeatMove(new CubeMove(CubeFace.Up), turns));
            }

            moves.AddRange(Parse($"{FaceToNotation(side)}2"));
            ApplyAndRecord(state, "Cross", $"Insert {side} edge", moves, new[] { edge.Id });
        }

        private static bool IsCrossEdgeSolved(CubePieceState edge, CubeFace side)
        {
            var downTarget = CubeState.GetAxisFromFace(CubeFace.Down);
            var sideTarget = CubeState.GetAxisFromFace(side);
            var targetPosition = downTarget + sideTarget;
            var whiteNormal = GetStickerNormal(edge, CubeFace.Down);
            var sideNormal = GetStickerNormal(edge, side);
            return edge.Position == targetPosition && whiteNormal == downTarget && sideNormal == sideTarget;
        }

        #endregion

        #region First layer corners

        private void InsertFirstLayerCorner(CubeState state, CubeFace sideA, CubeFace sideB)
        {
            var attempts = 0;
            while (attempts++ < 40)
            {
                var corner = FindCorner(state, CubeFace.Down, sideA, sideB);
                if (IsCornerSolved(corner, sideA, sideB))
                {
                    return;
                }

                if (corner.Position.y == -1)
                {
                    ApplyAndRecord(state, "F2L", "Evict misoriented corner", Parse("R D R'"), new[] { corner.Id });
                    continue;
                }

                var topPosition = DetermineTopPosition(corner, sideA, sideB);
                var target = GetTopTarget(sideA, sideB);
                var turns = GetUTurns(topPosition, target);
                if (turns > 0)
                {
                    ApplyAndRecord(state, "F2L", "Align corner above target", RepeatMove(new CubeMove(CubeFace.Up), turns), new[] { corner.Id });
                }

                string insertion = sideA switch
                {
                    CubeFace.Front when sideB == CubeFace.Right => "R U R'",
                    CubeFace.Right when sideB == CubeFace.Back => "B U B'",
                    CubeFace.Back when sideB == CubeFace.Left => "L U L'",
                    CubeFace.Left when sideB == CubeFace.Front => "F U F'",
                    _ => "R U R'"
                };
                ApplyAndRecord(state, "F2L", "Insert corner", Parse(insertion), new[] { corner.Id });
            }
        }

        private static bool IsCornerSolved(CubePieceState corner, CubeFace sideA, CubeFace sideB)
        {
            var downAxis = CubeState.GetAxisFromFace(CubeFace.Down);
            var sideAAxis = CubeState.GetAxisFromFace(sideA);
            var sideBAxis = CubeState.GetAxisFromFace(sideB);
            var targetPosition = downAxis + sideAAxis + sideBAxis;
            return corner.Position == targetPosition &&
                   GetStickerNormal(corner, CubeFace.Down) == downAxis &&
                   GetStickerNormal(corner, sideA) == sideAAxis &&
                   GetStickerNormal(corner, sideB) == sideBAxis;
        }

        private CubeFace DetermineTopPosition(CubePieceState corner, CubeFace sideA, CubeFace sideB)
        {
            var sticker = GetStickerNormal(corner, sideA);
            return CubeState.GetFaceFromAxis(sticker);
        }

        private CubeFace GetTopTarget(CubeFace sideA, CubeFace sideB)
        {
            return sideA;
        }

        #endregion

        #region Second layer

        private void InsertSecondLayerEdge(CubeState state, CubeFace left, CubeFace right)
        {
            var guard = 0;
            while (guard++ < 40)
            {
                var edge = FindEdge(state, left, right);
                if (IsSecondLayerSolved(edge, left, right))
                {
                    return;
                }

                if (edge.Position.y == -1)
                {
                    ApplyAndRecord(state, "Second Layer", "Move edge to top", Parse("R D R'"), new[] { edge.Id });
                    continue;
                }

                var topFace = CubeState.GetFaceFromAxis(GetStickerNormal(edge, left));
                var target = left;
                var turns = GetUTurns(topFace, target);
                if (turns > 0)
                {
                    ApplyAndRecord(state, "Second Layer", "Align edge over slot", RepeatMove(new CubeMove(CubeFace.Up), turns), new[] { edge.Id });
                }

                var algorithm = left switch
                {
                    CubeFace.Front when right == CubeFace.Right => "U R U' R' U' F' U F",
                    CubeFace.Right when right == CubeFace.Back => "U B U' B' U' R' U R",
                    CubeFace.Back when right == CubeFace.Left => "U L U' L' U' B' U B",
                    CubeFace.Left when right == CubeFace.Front => "U F U' F' U' L' U L",
                    _ => "U R U' R' U' F' U F"
                };

                ApplyAndRecord(state, "Second Layer", "Insert middle layer edge", Parse(algorithm), new[] { edge.Id });
            }
        }

        private static bool IsSecondLayerSolved(CubePieceState edge, CubeFace left, CubeFace right)
        {
            var leftAxis = CubeState.GetAxisFromFace(left);
            var rightAxis = CubeState.GetAxisFromFace(right);
            var target = leftAxis + rightAxis;
            return edge.Position == target &&
                   GetStickerNormal(edge, left) == leftAxis &&
                   GetStickerNormal(edge, right) == rightAxis;
        }

        #endregion

        #region Last layer (2-look OLL/PLL)

        private void OrientLastLayer(CubeState state)
        {
            // Edge orientation
            var ollPattern = GetOllPattern(state);
            if (ollPattern is OllPattern.Dot)
            {
                ApplyAndRecord(state, "OLL", "Form yellow cross", Parse("F R U R' U' F'"));
                ollPattern = GetOllPattern(state);
            }

            if (ollPattern == OllPattern.Line)
            {
                ApplyAndRecord(state, "OLL", "Yellow line", Parse("F R U R' U' F'"));
            }
            else if (ollPattern == OllPattern.LShape)
            {
                ApplyAndRecord(state, "OLL", "Yellow L-shape", Parse("F U R U' R' F'"));
            }

            // Corner orientation (two-look OLL) using Sune/Antisune
            for (var i = 0; i < 4 && !AllLastLayerOriented(state); i++)
            {
                ApplyAndRecord(state, "OLL", "Corner orientation", Parse("R U R' U R U2 R'"));
            }
        }

        private void PermuteLastLayer(CubeState state)
        {
            if (!AreLastLayerCornersPermuted(state))
            {
                ApplyAndRecord(state, "PLL", "Swap corners", Parse("x' R U' R D2 R' U R D2 R2 x"));
            }

            if (!AreLastLayerEdgesPermuted(state))
            {
                ApplyAndRecord(state, "PLL", "Exchange edges", Parse("R2 U R U R' U' R' U' R' U R'"));
            }
        }

        private bool AllLastLayerOriented(CubeState state)
        {
            var upColors = state.GetFaceColors(CubeFace.Up);
            for (var r = 0; r < 3; r++)
            {
                for (var c = 0; c < 3; c++)
                {
                    if (upColors[r, c] != CubeFace.Up)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool AreLastLayerCornersPermuted(CubeState state)
        {
            var corners = new[]
            {
                state.FindPiece("URF"),
                state.FindPiece("URB"),
                state.FindPiece("ULB"),
                state.FindPiece("ULF")
            };
            return corners.All(c => c.Position.y == 1);
        }

        private bool AreLastLayerEdgesPermuted(CubeState state)
        {
            var edges = new[]
            {
                state.FindPiece("UF"),
                state.FindPiece("UR"),
                state.FindPiece("UB"),
                state.FindPiece("UL")
            };
            return edges.All(e => e.Position.y == 1);
        }

        private static OllPattern GetOllPattern(CubeState state)
        {
            var upColors = state.GetFaceColors(CubeFace.Up);
            var edges = new[] { upColors[0, 1], upColors[1, 2], upColors[2, 1], upColors[1, 0] };
            var orientedCount = edges.Count(color => color == CubeFace.Up);
            if (orientedCount == 4)
            {
                return OllPattern.Cross;
            }

            if (orientedCount == 2)
            {
                if (edges[0] == CubeFace.Up && edges[2] == CubeFace.Up)
                {
                    return OllPattern.Line;
                }

                return OllPattern.LShape;
            }

            return OllPattern.Dot;
        }

        #endregion

        #region Utility helpers

        private void ApplyAndRecord(CubeState state, string phase, string description, IEnumerable<CubeMove> moves, IEnumerable<string>? highlights = null)
        {
            var moveList = moves.ToList();
            if (moveList.Count == 0)
            {
                return;
            }

            state.ApplyAlgorithm(moveList);
            _steps.Add(new SolverStep(phase, description, moveList, highlights));
        }

        private static IEnumerable<CubeMove> Parse(string notation)
        {
            return CubeNotation.ParseAlgorithm(notation);
        }

        private static IEnumerable<CubeMove> RepeatMove(CubeMove move, int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return move;
            }
        }

        private static CubePieceState FindEdge(CubeState state, CubeFace a, CubeFace b)
        {
            return state.EnumeratePieces(CubePieceType.Edge).First(p => HasColors(p, a, b));
        }

        private static CubePieceState FindCorner(CubeState state, CubeFace a, CubeFace b, CubeFace c)
        {
            return state.EnumeratePieces(CubePieceType.Corner).First(p => HasColors(p, a, b, c));
        }

        private static bool HasColors(CubePieceState piece, params CubeFace[] faces)
        {
            return faces.All(face => piece.Stickers.Values.Contains(face));
        }

        private static Vector3Int GetStickerNormal(CubePieceState piece, CubeFace face)
        {
            foreach (var sticker in piece.Stickers)
            {
                if (sticker.Value == face)
                {
                    return sticker.Key;
                }
            }

            return Vector3Int.zero;
        }

        private static string FaceToNotation(CubeFace face)
        {
            return face switch
            {
                CubeFace.Up => "U",
                CubeFace.Right => "R",
                CubeFace.Front => "F",
                CubeFace.Down => "D",
                CubeFace.Left => "L",
                CubeFace.Back => "B",
                _ => ""
            };
        }

        private static int GetUTurns(CubeFace current, CubeFace target)
        {
            var currentIndex = Array.IndexOf(TopRing, current);
            var targetIndex = Array.IndexOf(TopRing, target);
            if (currentIndex < 0 || targetIndex < 0)
            {
                return 0;
            }

            return (targetIndex - currentIndex + 4) % 4;
        }

        private static CubeFace GetDominantFace(Vector3Int position)
        {
            if (position.z > 0)
            {
                return CubeFace.Front;
            }

            if (position.x > 0)
            {
                return CubeFace.Right;
            }

            if (position.z < 0)
            {
                return CubeFace.Back;
            }

            if (position.x < 0)
            {
                return CubeFace.Left;
            }

            return position.y > 0 ? CubeFace.Up : CubeFace.Down;
        }

        private enum OllPattern
        {
            Dot,
            LShape,
            Line,
            Cross
        }

        #endregion
    }
}
