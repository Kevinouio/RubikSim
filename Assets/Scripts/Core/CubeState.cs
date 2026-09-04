using System;
using System.Collections.Generic;
using System.Text;

namespace RubikSim.Core
{
    public enum ImportStatus { Valid, Invalid, Unsupported }
    public sealed class ValidationResult
    {
        public ImportStatus Status { get; }
        public string Code { get; }
        public string Message { get; }
        public bool IsValid => Status == ImportStatus.Valid;
        public ValidationResult(ImportStatus status, string code, string message) { Status = status; Code = code; Message = message; }
        public static ValidationResult Valid => new ValidationResult(ImportStatus.Valid, "valid", "Fully validated standard 3x3 state.");
    }
    public sealed class StateImportException : FormatException
    {
        public ValidationResult Validation { get; }
        public StateImportException(ImportStatus status, string code, string message) : base(message)
        { Validation = new ValidationResult(status, code, message); }
    }

    /// <summary>
    /// Exact, history-free sticker state. Colors are home-face labels; positions use a fixed spatial frame.
    /// Cubie arrays use current centers as the color frame, so whole rotations are solved equivalents.
    /// Returned arrays and snapshots never expose mutable internal state.
    /// </summary>
    public sealed class CubeState
    {
        public const string PuzzleId = "cube-3x3";
        public const int SchemaVersion = 1;
        public const int DefinitionVersion = 1;
        public const string SolvedFacelets = "UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB";
        private char[] facelets;
        private int[] cp, co, ep, eo;
        private static readonly int[,] CornerFacelets = {
            {8,9,20}, {6,18,38}, {0,36,47}, {2,45,11},
            {29,26,15}, {27,44,24}, {33,53,42}, {35,17,51} };
        private static readonly string[] CornerColors = { "URF", "UFL", "ULB", "UBR", "DFR", "DLF", "DBL", "DRB" };
        private static readonly int[,] EdgeFacelets = {
            {5,10}, {7,19}, {3,37}, {1,46}, {32,16}, {28,25},
            {30,43}, {34,52}, {23,12}, {21,41}, {50,39}, {48,14} };
        private static readonly string[] EdgeColors = { "UR", "UF", "UL", "UB", "DR", "DF", "DL", "DB", "FR", "FL", "BL", "BR" };
        private static readonly Dictionary<Move, int[]> Permutations = BuildPermutations();

        private CubeState(char[] values) { facelets = values; }
        public static CubeState Solved() => new CubeState(SolvedFacelets.ToCharArray());
        public CubeState Clone() => new CubeState((char[])facelets.Clone());
        public string ToFacelets() => new string(facelets);
        public int[] CP { get { Decode(); return (int[])cp.Clone(); } }
        public int[] CO { get { Decode(); return (int[])co.Clone(); } }
        public int[] EP { get { Decode(); return (int[])ep.Clone(); } }
        public int[] EO { get { Decode(); return (int[])eo.Clone(); } }
        public string Centers => new string(new[] { facelets[4], facelets[13], facelets[22], facelets[31], facelets[40], facelets[49] });
        public bool IsSolved
        {
            get { for (int i = 0; i < 54; i++) if (facelets[i] != facelets[(i / 9) * 9 + 4]) return false; return true; }
        }
        public bool HasCanonicalCenters => Centers == CubeGeometry.Faces;
        /// <summary>Relabel colors using the present centers. Spatial positions and move names are unchanged.</summary>
        public CubeState ToCanonical() => new CubeState(CanonicalFacelets());
        public Sticker[] GetStickers()
        {
            var result = new Sticker[54];
            for (int i = 0; i < result.Length; i++) result[i] = new Sticker(i, CubeGeometry.GetFacelet(i), facelets[i]);
            return result;
        }
        public void Apply(Move move)
        {
            if (!Permutations.TryGetValue(move, out var map)) throw new ArgumentException("Not a legal 3x3 move.", nameof(move));
            var next = new char[54];
            for (int i = 0; i < 54; i++) next[map[i]] = facelets[i];
            facelets = next; cp = co = ep = eo = null;
        }
        public void Apply(IEnumerable<Move> moves)
        {
            if (moves == null) throw new ArgumentNullException(nameof(moves));
            // Enumerate and validate before committing any turn, including exceptions from lazy enumerables.
            var batch = new List<Move>(moves);
            foreach (var move in batch) if (!Permutations.ContainsKey(move)) throw new ArgumentException("Sequence contains an illegal 3x3 move.", nameof(moves));
            foreach (var move in batch) Apply(move);
        }
        public void Apply(string notation) => Apply(Move.ParseSequence(notation));
        public static bool IsLegal(Move move) => Permutations.ContainsKey(move);
        public ValidationResult Validate()
        {
            try { FromFacelets(ToFacelets()); return ValidationResult.Valid; }
            catch (StateImportException ex) { return ex.Validation; }
        }
        public static CubeState FromFacelets(string text)
        {
            if (text == null) throw Invalid("facelet-length", "Expected 54 facelets in URFDLB face order.");
            if (text.Length > 4096) throw Invalid("facelet-length", "Facelet input is too long; provide 54 letters.");
            var clean = new StringBuilder();
            foreach (char c in text) if (!char.IsWhiteSpace(c)) clean.Append(c);
            if (clean.Length != 54) throw Invalid("facelet-length", "Expected exactly 54 facelets in URFDLB face order; received " + clean.Length + ".");
            var state = new CubeState(clean.ToString().ToCharArray());
            var counts = new int[6];
            foreach (char c in state.facelets)
            {
                int color = CubeGeometry.Faces.IndexOf(c);
                if (color < 0) throw Invalid("facelet-color", "Facelets must use uppercase U R F D L B home-color labels.");
                counts[color]++;
            }
            for (int i = 0; i < 6; i++) if (counts[i] != 9) throw Invalid("color-count", "Color " + CubeGeometry.Faces[i] + " must occur exactly nine times; found " + counts[i] + ".");
            state.ValidateCenters();
            state.Decode();
            return state;
        }
        public static bool TryFromFacelets(string text, out CubeState state, out ValidationResult validation)
        {
            try { state = FromFacelets(text); validation = ValidationResult.Valid; return true; }
            catch (StateImportException ex) { state = null; validation = ex.Validation; return false; }
        }
        public static CubeState FromCubies(int[] corners, int[] twists, int[] edges, int[] flips)
        {
            ValidateArray(corners, 8, 8, "corner permutation"); ValidateArray(twists, 8, 3, "corner orientation");
            ValidateArray(edges, 12, 12, "edge permutation"); ValidateArray(flips, 12, 2, "edge orientation");
            var values = SolvedFacelets.ToCharArray();
            for (int position = 0; position < 8; position++)
                for (int n = 0; n < 3; n++) values[CornerFacelets[position, (n + twists[position]) % 3]] = CornerColors[corners[position]][n];
            for (int position = 0; position < 12; position++)
                for (int n = 0; n < 2; n++) values[EdgeFacelets[position, (n + flips[position]) % 2]] = EdgeColors[edges[position]][n];
            return FromFacelets(new string(values));
        }
        private static void ValidateArray(int[] values, int length, int limit, string name)
        {
            if (values == null || values.Length != length) throw Invalid("cubie-array", "Expected " + length + " entries for " + name + ".");
            foreach (int value in values) if (value < 0 || value >= limit) throw Invalid("cubie-array", "Out-of-range value for " + name + ".");
        }
        private char[] CanonicalFacelets()
        {
            string centers = Centers;
            var result = new char[54];
            for (int i = 0; i < 54; i++)
            {
                int color = centers.IndexOf(facelets[i]);
                if (color < 0) throw Invalid("center-inventory", "Centers must contain every color once.");
                result[i] = CubeGeometry.Faces[color];
            }
            return result;
        }
        private void ValidateCenters()
        {
            string centers = Centers;
            foreach (char color in CubeGeometry.Faces)
                if (centers.IndexOf(color) < 0 || centers.IndexOf(color) != centers.LastIndexOf(color))
                    throw Invalid("center-inventory", "Centers must contain every home color exactly once.");
            var right = CubeGeometry.GetFacelet(centers.IndexOf('R') * 9 + 4).Normal;
            var up = CubeGeometry.GetFacelet(centers.IndexOf('U') * 9 + 4).Normal;
            var front = CubeGeometry.GetFacelet(centers.IndexOf('F') * 9 + 4).Normal;
            if (CubeGeometry.GetFacelet(centers.IndexOf('L') * 9 + 4).Normal != -right ||
                CubeGeometry.GetFacelet(centers.IndexOf('D') * 9 + 4).Normal != -up ||
                CubeGeometry.GetFacelet(centers.IndexOf('B') * 9 + 4).Normal != -front || Int3.Cross(right, up) != front)
                throw Invalid("center-frame", "Center colors must be a proper rotation of the standard color scheme; mirrored or swapped center frames are invalid.");
        }
        private void Decode()
        {
            if (cp != null) return;
            char[] colors = CanonicalFacelets();
            var corners = new int[8]; var twists = new int[8]; var edges = new int[12]; var flips = new int[12];
            var seenCorners = new bool[8]; var seenEdges = new bool[12];
            int twistSum = 0, flipSum = 0;
            for (int p = 0; p < 8; p++)
            {
                int found = -1, orientation = -1;
                for (int piece = 0; piece < 8; piece++) for (int o = 0; o < 3; o++)
                {
                    bool match = true;
                    for (int n = 0; n < 3; n++) if (colors[CornerFacelets[p,(n+o)%3]] != CornerColors[piece][n]) match = false;
                    if (match) { found = piece; orientation = o; }
                }
                if (found < 0) throw Invalid("corner-inventory", "Corner at " + CornerColors[p] + " has impossible colors or a mirrored sticker order.");
                if (seenCorners[found]) throw Invalid("corner-inventory", "Corner " + CornerColors[found] + " appears more than once.");
                seenCorners[found] = true; corners[p] = found; twists[p] = orientation; twistSum += orientation;
            }
            for (int p = 0; p < 12; p++)
            {
                int found = -1, orientation = -1;
                for (int piece = 0; piece < 12; piece++) for (int o = 0; o < 2; o++)
                    if (colors[EdgeFacelets[p,o]] == EdgeColors[piece][0] && colors[EdgeFacelets[p,1-o]] == EdgeColors[piece][1])
                    { found = piece; orientation = o; }
                if (found < 0) throw Invalid("edge-inventory", "Edge at " + EdgeColors[p] + " has impossible colors.");
                if (seenEdges[found]) throw Invalid("edge-inventory", "Edge " + EdgeColors[found] + " appears more than once.");
                seenEdges[found] = true; edges[p] = found; flips[p] = orientation; flipSum += orientation;
            }
            if (twistSum % 3 != 0) throw Invalid("corner-twist", "Corner twists must sum to 0 modulo 3; a single twisted corner is unreachable.");
            if (flipSum % 2 != 0) throw Invalid("edge-flip", "Edge flips must sum to 0 modulo 2; a single flipped edge is unreachable.");
            if (Parity(corners) != Parity(edges)) throw Invalid("permutation-parity", "Corner and edge permutation parity must match; a single pair swap is unreachable.");
            cp = corners; co = twists; ep = edges; eo = flips;
        }
        private static int Parity(int[] values)
        {
            int parity = 0;
            for (int i = 0; i < values.Length; i++) for (int j = i+1; j < values.Length; j++) if (values[i] > values[j]) parity ^= 1;
            return parity;
        }
        private static StateImportException Invalid(string code, string message) => new StateImportException(ImportStatus.Invalid, code, message);
        private static Dictionary<Move,int[]> BuildPermutations()
        {
            var result = new Dictionary<Move,int[]>();
            foreach (char face in "URFDLBMESxyz")
                for (int turns = 1; turns <= 3; turns++) AddPermutation(result, new Move(face, turns));
            foreach (char face in CubeGeometry.Faces)
                for (int turns = 1; turns <= 3; turns++) AddPermutation(result, new Move(face, turns, true));
            return result;
        }
        private static void AddPermutation(Dictionary<Move,int[]> result, Move move)
        {
            var map = new int[54];
            for (int i = 0; i < 54; i++)
            {
                var geometry = CubeGeometry.GetFacelet(i);
                map[i] = move.AffectsPosition(geometry.Position) ? CubeGeometry.GetIndex(
                    CubeGeometry.Rotate(geometry.Position, move.Axis, move.SignedQuarterTurns),
                    CubeGeometry.Rotate(geometry.Normal, move.Axis, move.SignedQuarterTurns)) : i;
            }
            result.Add(move, map);
        }
        public string Serialize() => "{\"schemaVersion\":1,\"puzzle\":\"" + PuzzleId + "\",\"definitionVersion\":1,\"facelets\":\"" + ToFacelets() + "\"}";
        public static CubeState Deserialize(string json)
        {
            Dictionary<string, SnapshotJson.Value> fields;
            try { fields = SnapshotJson.Read(json); }
            catch (FormatException ex) { throw Invalid("snapshot-json", ex.Message); }
            foreach (string name in new[] { "schemaVersion", "puzzle", "definitionVersion", "facelets" })
                if (!fields.ContainsKey(name)) throw Invalid("snapshot-field", "Snapshot is missing required field '" + name + "'.");
            if (fields["schemaVersion"].IsString || fields["definitionVersion"].IsString || !fields["puzzle"].IsString || !fields["facelets"].IsString)
                throw Invalid("snapshot-type", "Snapshot versions must be integers; puzzle and facelets must be strings.");
            if (fields["puzzle"].Text != PuzzleId)
                throw new StateImportException(ImportStatus.Unsupported, "unsupported-puzzle", "Unsupported puzzle: " + fields["puzzle"].Text + ". This milestone supports cube-3x3 only.");
            if (fields["schemaVersion"].Text != "1" || fields["definitionVersion"].Text != "1")
                throw new StateImportException(ImportStatus.Unsupported, "unsupported-version", "This build supports snapshot schema 1 and cube definition 1 only.");
            if (fields.Count != 4) throw new StateImportException(ImportStatus.Unsupported, "unsupported-field", "Snapshot contains unrecognized fields; refusing to silently discard state data.");
            return FromFacelets(fields["facelets"].Text);
        }
        public static bool TryDeserialize(string json, out CubeState state, out ValidationResult validation)
        {
            try { state = Deserialize(json); validation = ValidationResult.Valid; return true; }
            catch (StateImportException ex) { state = null; validation = ex.Validation; return false; }
        }
    }
}
