using System;
using System.Collections.Generic;
using System.Text;

namespace RubikSim.Core
{
    /// <summary>A legal 3x3 move in the fixed spatial notation frame.</summary>
    public readonly struct Move : IEquatable<Move>
    {
        public char Face { get; }
        public int Turns { get; }
        public bool Wide { get; }
        public Move(char face, int turns = 1, bool wide = false)
        {
            if ("urfdlb".IndexOf(face) >= 0) { face = char.ToUpperInvariant(face); wide = true; }
            if ("URFDLBMESxyz".IndexOf(face) < 0) throw new ArgumentException("Unknown 3x3 move.", nameof(face));
            if (turns < 1 || turns > 3) throw new ArgumentOutOfRangeException(nameof(turns), "Turns must be 1, 2 or 3.");
            if (wide && "URFDLB".IndexOf(face) < 0) throw new ArgumentException("Only face turns may be wide.", nameof(wide));
            Face = face; Turns = turns; Wide = wide;
        }
        public char Axis => "RLMx".IndexOf(Face) >= 0 ? 'x' : "UDEy".IndexOf(Face) >= 0 ? 'y' : 'z';
        public int SignedQuarterTurns => ("LDBME".IndexOf(Face) >= 0 ? 1 : -1) * (Turns == 3 ? -1 : Turns);
        public int LayerMask => "xyz".IndexOf(Face) >= 0 ? 7 : "MES".IndexOf(Face) >= 0 ? 2 :
            ("LDB".IndexOf(Face) >= 0 ? 1 : 4) | (Wide ? 2 : 0);
        public bool AffectsPosition(Int3 position) => (LayerMask & (1 << (position.Component(Axis) + 1))) != 0;
        public Move Inverse() => new Move(Face, Turns == 2 ? 2 : 4 - Turns, Wide);
        public override string ToString() => Face + (Wide ? "w" : "") + (Turns == 2 ? "2" : Turns == 3 ? "'" : "");
        public bool Equals(Move other) => Face == other.Face && Turns == other.Turns && Wide == other.Wide;
        public override bool Equals(object obj) => obj is Move other && Equals(other);
        public override int GetHashCode() => (Face * 4 + Turns) * 2 + (Wide ? 1 : 0);

        public static Move Parse(string text)
        {
            var moves = ParseSequence(text);
            if (moves.Length != 1) throw new FormatException("Expected exactly one move.");
            return moves[0];
        }
        /// <summary>Parse completely before application. Supports compact sequences, 2Rw, lower-case wide aliases, MES and xyz.</summary>
        public static Move[] ParseSequence(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var moves = new List<Move>();
            int i = 0;
            while (i < text.Length)
            {
                if (char.IsWhiteSpace(text[i])) { i++; continue; }
                int start = i;
                bool prefix = text[i] == '2';
                if (prefix) i++;
                if (i >= text.Length || "URFDLBurfdlbMESxyz".IndexOf(text[i]) < 0)
                    throw NotationError(text, start, "Expected U R F D L B, a wide turn, M E S, or x y z");
                char face = text[i++];
                bool wide = "urfdlb".IndexOf(face) >= 0;
                if (wide) face = char.ToUpperInvariant(face);
                if (i < text.Length && text[i] == 'w')
                {
                    if (wide || "URFDLB".IndexOf(face) < 0) throw NotationError(text, start, "Invalid wide-turn suffix");
                    wide = true; i++;
                }
                if (prefix && !wide) throw NotationError(text, start, "A layer count of 2 requires a wide turn, for example 2Rw");
                int turns = 1;
                if (i < text.Length && text[i] == '2')
                {
                    turns = 2; i++;
                    if (i < text.Length && text[i] == '\'') i++; // A half-turn prime has the same effect.
                }
                else if (i < text.Length && text[i] == '\'') { turns = 3; i++; }
                if (i < text.Length && (text[i] == '\'' || char.IsDigit(text[i]) || text[i] == 'w'))
                    throw NotationError(text, i, "Unexpected move suffix");
                moves.Add(new Move(face, turns, wide));
            }
            return moves.ToArray();
        }
        private static FormatException NotationError(string text, int at, string message) =>
            new FormatException(message + " at character " + (at + 1) + " in '" + text + "'.");
        public static Move[] InvertSequence(IEnumerable<Move> sequence)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            var result = new List<Move>(sequence);
            result.Reverse();
            for (int i = 0; i < result.Count; i++) result[i] = result[i].Inverse();
            return result.ToArray();
        }
        public static string FormatSequence(IEnumerable<Move> sequence)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            var result = new StringBuilder();
            foreach (var move in sequence) { if (result.Length > 0) result.Append(' '); result.Append(move.ToString()); }
            return result.ToString();
        }
    }
}
