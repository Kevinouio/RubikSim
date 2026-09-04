using System;
using System.Collections.Generic;
using RubikSim.Core;

namespace RubikSim.Solver
{
    public sealed class TeachingAlgorithm
    {
        public string Phase { get; private set; }
        public string Id { get; private set; }
        public string Notation { get; private set; }
        public string Recognition { get; private set; }
        public string Source { get; private set; }
        public IReadOnlyList<Move> Moves { get; private set; }
        internal TeachingAlgorithm(string phase, string id, string notation, string recognition, string source)
        {
            Phase = phase; Id = id; Notation = notation; Recognition = recognition; Source = source;
            Moves = Array.AsReadOnly(Move.ParseSequence(notation).ToArrayCompat());
        }
    }

    public static class AlgorithmLibrary
    {
        public const string MethodSource = "https://jperm.net/3x3/cfop";
        public const string OllSource = "https://jperm.net/algs/2look/oll";
        public const string PllSource = "https://jperm.net/algs/2look/pll";
        // Functional move sequences checked against the author's trainer data on 2026-09-04.
        // Explanations are original. Lowercase f/r are two-layer turns, M follows L.
        public static readonly IReadOnlyList<TeachingAlgorithm> All = Array.AsReadOnly(new[]
        {
            new TeachingAlgorithm("OLL edges", "I-shape", "F R U R' U' F'", "Two oriented U edges form a straight line.", OllSource),
            new TeachingAlgorithm("OLL edges", "L-shape", "f R U R' U' f'", "Two oriented U edges meet at a right angle.", OllSource),
            new TeachingAlgorithm("OLL edges", "Dot", "F R U R' U' F' f R U R' U' f'", "None of the four U edges has its U-color sticker facing up.", OllSource),
            new TeachingAlgorithm("OLL corners", "H", "R U R' U R U' R' U R U2 R'", "No U corners face up; side U-color stickers form two opposite pairs.", OllSource),
            new TeachingAlgorithm("OLL corners", "Pi", "R U2 R2 U' R2 U' R2 U2 R", "No U corners face up; the side-sticker pattern differs from the H case.", OllSource),
            new TeachingAlgorithm("OLL corners", "U", "R2 D R' U2 R D' R' U2 R'", "Two U corners face up, with the remaining U stickers on the same side face.", OllSource),
            new TeachingAlgorithm("OLL corners", "T", "r U R' U' r' F R F'", "Two adjacent U corners face up; the other U stickers face opposite side directions.", OllSource),
            new TeachingAlgorithm("OLL corners", "L", "F R' F' r U R U' r'", "Two diagonally opposite U corners face up.", OllSource),
            new TeachingAlgorithm("OLL corners", "Antisune", "R U2 R' U' R U' R'", "One U corner faces up; the other three have the same reverse twist.", OllSource),
            new TeachingAlgorithm("OLL corners", "Sune", "R U R' U R U2 R'", "One U corner faces up; the other three have the same twist.", OllSource),
            new TeachingAlgorithm("PLL corners", "Headlights (T permutation)", "R U R' U' R' F R2 U' R' U' R U R' F'", "One side has matching corner side colors (headlights); two adjacent corners need exchanging.", PllSource),
            new TeachingAlgorithm("PLL corners", "Diagonal (Y permutation)", "F R U' R' U' R U R' F' R U R' U' R' F R F'", "No side has matching corner side colors; two diagonal corners need exchanging.", PllSource),
            new TeachingAlgorithm("PLL edges", "H permutation", "M2 U M2 U2 M2 U M2", "Opposite U edges exchange in two pairs.", PllSource),
            new TeachingAlgorithm("PLL edges", "Z permutation", "M' U M2 U M2 U M' U2 M2", "Adjacent U edges exchange in two pairs.", PllSource),
            new TeachingAlgorithm("PLL edges", "Ua permutation", "R U' R U R U R U' R' U' R2", "Three U edges cycle; one U edge stays fixed.", PllSource),
            new TeachingAlgorithm("PLL edges", "Ub permutation", "R2 U R U R' U' R' U' R' U R'", "Three U edges cycle in the opposite direction; one stays fixed.", PllSource)
        });
        internal static T[] ToArrayCompat<T>(this IEnumerable<T> values) { return new List<T>(values).ToArray(); }
    }
}
