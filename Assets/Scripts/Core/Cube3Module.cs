using System;

namespace RubikSim.Core
{
    /// <summary>The delivered module contract; does not advertise simulation or solvers for other puzzles.</summary>
    public sealed class Cube3Module
    {
        public string Id => CubeState.PuzzleId;
        public int DefinitionVersion => CubeState.DefinitionVersion;
        public string TeachingMethod => "CFOP";
        public CubeState CreateSolvedState() => CubeState.Solved();
        public Move[] ParseMoves(string text) => Move.ParseSequence(text);
        public string FormatMoves(Move[] moves) => Move.FormatSequence(moves);
        public bool IsLegalMove(Move move) => CubeState.IsLegal(move);
        public Move InvertMove(Move move) => move.Inverse();
        public void ApplyMove(CubeState state, Move move) => state.Apply(move);
        public CubeState Import(string snapshot) => CubeState.Deserialize(snapshot);
        public string Export(CubeState state) => state.Serialize();
        public ValidationResult Validate(CubeState state) => state == null ? new ValidationResult(ImportStatus.Invalid, "missing-state", "State is required.") : state.Validate();
        public Sticker[] RenderData(CubeState state) => state.GetStickers();
        public string[] SupportedSolvers => new[] { "cfop-two-look" };
        /// <summary>Reproducible random-move scramble, not a uniform random state or competition scramble.</summary>
        public Move[] GenerateScramble(int seed, int length = 25) => Scrambler.Generate(seed, length);
    }
    public static class Scrambler
    {
        public static Move[] Generate(int seed, int length = 25)
        {
            if (length < 0 || length > 10000) throw new ArgumentOutOfRangeException(nameof(length), "Length must be from 0 to 10000.");
            uint randomState = unchecked((uint)seed) ^ 0x9E3779B9u;
            if (randomState == 0) randomState = 0xA341316Cu;
            var result = new Move[length]; char previousAxis = '\0';
            for (int i = 0; i < length; i++)
            {
                Move next;
                do { next = new Move(CubeGeometry.Faces[(int)(Next(ref randomState) % 6)], 1 + (int)(Next(ref randomState) % 3)); }
                while (next.Axis == previousAxis);
                result[i] = next; previousAxis = next.Axis;
            }
            return result;
        }
        private static uint Next(ref uint state) { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
    }
}
