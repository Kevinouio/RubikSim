using System;
using System.Collections.Generic;
using RubikSim.Core;

public static class CoreTests
{
    private static int assertions;
    public const string SuperflipFacelets = "UBULURUFURURFRBRDRFUFLFRFDFDFDLDRDBDLULBLFLDLBUBRBLBDB";
    public static void Run()
    {
        assertions = 0;
        KnownMoveEffects(); KnownPattern(); MoveLaws(); Notation(); InvalidStates(); Snapshots(); CenterFrames(); Geometry(); Scrambles();
        Console.WriteLine("PASS CoreTests: " + assertions + " assertions (independent fixtures, legality, notation, validation, snapshots, all center frames, geometry).");
    }
    private static void KnownMoveEffects()
    {
        // Independent mathematical fixtures transcribed from Kociemba's published cubie move definitions.
        // https://kociemba.org/math/CubeDefs.htm (accessed 2026-09-04). Production moves use integer geometry instead.
        int[][] corners = {
            new[]{3,0,1,2,4,5,6,7}, new[]{4,1,2,0,7,5,6,3}, new[]{1,5,2,3,0,4,6,7},
            new[]{0,1,2,3,5,6,7,4}, new[]{0,2,6,3,4,1,5,7}, new[]{0,1,3,7,4,5,2,6} };
        int[][] twists = {
            new[]{0,0,0,0,0,0,0,0}, new[]{2,0,0,1,1,0,0,2}, new[]{1,2,0,0,2,1,0,0},
            new[]{0,0,0,0,0,0,0,0}, new[]{0,1,2,0,0,2,1,0}, new[]{0,0,1,2,0,0,2,1} };
        int[][] edges = {
            new[]{3,0,1,2,4,5,6,7,8,9,10,11}, new[]{8,1,2,3,11,5,6,7,4,9,10,0},
            new[]{0,9,2,3,4,8,6,7,1,5,10,11}, new[]{0,1,2,3,5,6,7,4,8,9,10,11},
            new[]{0,1,10,3,4,5,9,7,8,2,6,11}, new[]{0,1,2,11,4,5,6,10,8,9,3,7} };
        int[][] flips = {
            new int[12], new int[12], new[]{0,1,0,0,0,1,0,0,1,1,0,0},
            new int[12], new int[12], new[]{0,0,0,1,0,0,0,1,0,0,1,1} };
        for (int f = 0; f < 6; f++)
        {
            var state = CubeState.Solved(); state.Apply(new Move("URFDLB"[f]));
            Equal(corners[f], state.CP, "known " + "URFDLB"[f] + " corner permutation");
            Equal(twists[f], state.CO, "known " + "URFDLB"[f] + " corner orientation");
            Equal(edges[f], state.EP, "known " + "URFDLB"[f] + " edge permutation");
            Equal(flips[f], state.EO, "known " + "URFDLB"[f] + " edge orientation");
            Equal(state.ToFacelets(), CubeState.FromCubies(corners[f],twists[f],edges[f],flips[f]).ToFacelets(), "independent cubies to facelets");
        }
        var front = CubeState.Solved(); front.Apply("F");
        Equal("UUUUUULLL" + "URRURRURR" + "FFFFFFFFF" + "RRRDDDDDD" + "LLDLLDLLD" + "BBBBBBBBB", front.ToFacelets(), "known F sticker bands");
        var right = CubeState.Solved(); right.Apply("R");
        Equal("UUFUUFUUF" + "RRRRRRRRR" + "FFDFFDFFD" + "DDBDDBDDB" + "LLLLLLLLL" + "UBBUBBUBB", right.ToFacelets(), "known R sticker bands");
    }
    private static void KnownPattern()
    {
        var imported = CubeState.FromFacelets(SuperflipFacelets);
        Equal(Identity(8), imported.CP, "superflip corners"); Equal(new int[8], imported.CO, "superflip twists");
        Equal(Identity(12), imported.EP, "superflip edges"); Equal(new[]{1,1,1,1,1,1,1,1,1,1,1,1}, imported.EO, "superflip flips");
        // Published generator: https://kociemba.org/math/oh.htm (2026-09-04).
        var generated = CubeState.Solved(); generated.Apply("D' R2 F' D2 F2 U2 L' R D' R2 B F R' U2 L' F2 R' U2 R' U'");
        Equal(SuperflipFacelets, generated.ToFacelets(), "published superflip generator matches independent state");
    }
    private static void MoveLaws()
    {
        var start = CubeState.Solved(); start.Apply("R U2 B' L D F2 R'");
        foreach (string token in "U R F D L B M E S x y z Uw Rw Fw Dw Lw Bw".Split(' '))
        {
            Move quarter = Move.Parse(token);
            var fourth = start.Clone(); for (int n = 0; n < 4; n++) fourth.Apply(quarter);
            Equal(start.ToFacelets(), fourth.ToFacelets(), token + " order 4");
            for (int turns = 1; turns <= 3; turns++)
            {
                var move = new Move(quarter.Face, turns, quarter.Wide);
                var state = start.Clone(); state.Apply(move); state.Apply(move.Inverse());
                Equal(start.ToFacelets(), state.ToFacelets(), move + " inverse");
            }
        }
        var sequence = Move.ParseSequence("Rw U M' z F2 S E' Lw2 x'");
        var combined = start.Clone(); combined.Apply(sequence); combined.Apply(Move.InvertSequence(sequence));
        Equal(start.ToFacelets(), combined.ToFacelets(), "sequence inversion");
        foreach (string identity in new[]{ "Rw|R M'", "Lw|L M", "Uw|U E'", "Dw|D E", "Fw|F S", "Bw|B S'", "x|R M' L'", "y|U E' D'", "z|F S B'" })
        {
            string[] sides = identity.Split('|'); var a = start.Clone(); var b = start.Clone(); a.Apply(sides[0]); b.Apply(sides[1]);
            Equal(a.ToFacelets(), b.ToFacelets(), identity);
        }
        Check(!CubeState.IsLegal(default(Move)), "default move illegal");
        Throws<ArgumentException>(() => start.Apply(new[]{new Move('R'),default(Move)}), "invalid batch rejected");
        var expected = CubeState.Solved(); expected.Apply("R U2 B' L D F2 R'");
        Equal(expected.ToFacelets(), start.ToFacelets(), "invalid batch leaves state unchanged");
        Throws<InvalidOperationException>(() => start.Apply(BrokenSequence()), "lazy batch failure");
        Equal(expected.ToFacelets(), start.ToFacelets(), "lazy failure leaves state unchanged");
        Throws<FormatException>(() => start.Apply("R U ?"), "notation failure before commit");
        Equal(expected.ToFacelets(), start.ToFacelets(), "notation failure leaves state unchanged");
    }
    private static IEnumerable<Move> BrokenSequence() { yield return new Move('R'); throw new InvalidOperationException("enumeration failed"); }
    private static void Notation()
    {
        string formatted = Move.FormatSequence(Move.ParseSequence("RUR'U' 2Rw2 r' M E2 S' x y2 z' F2'"));
        Equal("R U R' U' Rw2 Rw' M E2 S' x y2 z' F2", formatted, "notation canonical form");
        Equal(formatted, Move.FormatSequence(Move.ParseSequence(formatted)), "notation round trip");
        Equal(0, Move.ParseSequence("  \r\n\t").Length, "empty sequence");
        foreach (string invalid in new[]{"R3", "R''", "R22", "3Rw", "1Rw", "2R", "M2w", "xw", "R,U", "(R U)", "R U Q", "R++", "RwW", "R'2", "2", "rww", "Rw0"})
            Throws<FormatException>(() => Move.ParseSequence(invalid), "invalid notation " + invalid);
        Throws<ArgumentNullException>(() => Move.ParseSequence(null), "null notation");
        Throws<ArgumentException>(() => new Move('Q'), "unknown move");
        Throws<ArgumentException>(() => new Move('x',1,true), "wide rotation");
        Throws<ArgumentOutOfRangeException>(() => new Move('R',0), "zero turn");
        Throws<FormatException>(() => Move.Parse("R U"), "single move parser");
    }
    private static void InvalidStates()
    {
        Invalid(CubeState.SolvedFacelets.Substring(1), "facelet-length");
        Invalid(CubeState.SolvedFacelets.Replace('U','X'), "facelet-color");
        char[] colors = CubeState.SolvedFacelets.ToCharArray(); colors[0] = 'R'; Invalid(new string(colors), "color-count");
        colors = CubeState.SolvedFacelets.ToCharArray(); Cycle(colors,8,9,20); Invalid(new string(colors), "corner-twist");
        colors = CubeState.SolvedFacelets.ToCharArray(); Swap(colors,5,10); Invalid(new string(colors), "edge-flip");
        colors = CubeState.SolvedFacelets.ToCharArray(); Swap(colors,10,19); Invalid(new string(colors), "permutation-parity");
        colors = CubeState.SolvedFacelets.ToCharArray(); Swap(colors,9,20); Swap(colors,26,15); Invalid(new string(colors), "corner-inventory");
        colors = CubeState.SolvedFacelets.ToCharArray(); Swap(colors,10,25); Invalid(new string(colors), "edge-inventory");
        colors = CubeState.SolvedFacelets.ToCharArray(); Swap(colors,4,13); Invalid(new string(colors), "center-frame");
        colors = CubeState.SolvedFacelets.ToCharArray(); Swap(colors,4,10); Invalid(new string(colors), "center-inventory");
        string reflected = CubeState.SolvedFacelets.Replace('R','X').Replace('L','R').Replace('X','L'); Invalid(reflected, "center-frame");
        int[] cp = Identity(8), co = new int[8], ep = Identity(12), eo = new int[12];
        cp[0] = 1; cp[1] = 0;
        Throws<StateImportException>(() => CubeState.FromCubies(cp,co,ep,eo), "corner permutation parity");
        cp = Identity(8); co[0] = 1; co[1] = 2; eo[0] = 1; eo[1] = 1;
        Check(CubeState.FromCubies(cp,co,ep,eo).Validate().IsValid, "compensating twists and flips legal");
        Throws<StateImportException>(() => CubeState.FromCubies(new int[7],co,ep,eo), "bad cubie array length");
        co[0] = 3; Throws<StateImportException>(() => CubeState.FromCubies(cp,co,ep,eo), "bad cubie orientation range");
    }
    private static void Snapshots()
    {
        var state = CubeState.Solved(); state.Apply("R U2 F' M E2 z Rw'");
        string snapshot = state.Serialize(); var imported = CubeState.Deserialize(snapshot);
        Equal(state.ToFacelets(), imported.ToFacelets(), "snapshot preserves state and physical center colors");
        Equal(snapshot, imported.Serialize(), "stable versioned serialization");
        Check(!snapshot.Contains("history") && !snapshot.Contains("scramble"), "snapshot history free");
        imported.Apply("R"); Check(state.ToFacelets() != imported.ToFacelets(), "snapshot ownership");
        var cloned = state.Clone(); cloned.Apply("F"); Check(state.ToFacelets() != cloned.ToFacelets(), "clone ownership");
        int[] copies = state.CP; copies[0] = -1; Check(state.CP[0] >= 0, "cubie arrays copied");
        string reordered = " { \"facelets\" : \"" + state.ToFacelets() + "\", \"definitionVersion\":1, \"\\u0070uzzle\":\"cube-3x3\", \"schemaVersion\":1 } ";
        Equal(state.ToFacelets(), CubeState.Deserialize(reordered).ToFacelets(), "JSON order whitespace and escape");
        InvalidJson("{}", ImportStatus.Invalid);
        InvalidJson(snapshot + "junk", ImportStatus.Invalid);
        InvalidJson(snapshot.Replace("\"schemaVersion\":1", "\"schemaVersion\":\"1\""), ImportStatus.Invalid);
        InvalidJson(snapshot.Replace("\"schemaVersion\":1", "\"schemaVersion\":1.0"), ImportStatus.Invalid);
        InvalidJson(snapshot.Replace("\"schemaVersion\":1", "\"schemaVersion\":01"), ImportStatus.Invalid);
        InvalidJson(snapshot.Replace("\"schemaVersion\":1", "\"schemaVersion\":true"), ImportStatus.Invalid);
        InvalidJson(snapshot.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), ImportStatus.Unsupported);
        InvalidJson(snapshot.Replace("\"definitionVersion\":1", "\"definitionVersion\":9"), ImportStatus.Unsupported);
        InvalidJson(snapshot.Replace("cube-3x3", "cube-4x4"), ImportStatus.Unsupported);
        InvalidJson(snapshot.Replace("{", "{\"extra\":1,"), ImportStatus.Unsupported);
        InvalidJson(snapshot.Replace("{", "{\"schemaVersion\":1,"), ImportStatus.Invalid);
        InvalidJson(snapshot.Replace("cube-3x3", "cube-3x3\n"), ImportStatus.Invalid);
        InvalidJson(snapshot.Replace("cube-3x3", "cube-3x3\\q"), ImportStatus.Invalid);
        InvalidJson(snapshot.Replace("cube-3x3", "cube-3x3\\uXXXX"), ImportStatus.Invalid);
        InvalidJson(snapshot.Substring(0,snapshot.Length-1) + ",}", ImportStatus.Invalid);
        InvalidJson(null, ImportStatus.Invalid);
    }
    private static void CenterFrames()
    {
        var seen = new HashSet<string>(); var queue = new Queue<CubeState>(); queue.Enqueue(CubeState.Solved());
        while (queue.Count > 0)
        {
            var frame = queue.Dequeue(); if (!seen.Add(frame.Centers)) continue;
            Check(frame.IsSolved, "whole rotation is solved equivalent " + frame.Centers);
            Check(CubeState.FromFacelets(frame.ToFacelets()).Validate().IsValid, "proper center frame validated");
            Equal(CubeState.SolvedFacelets, frame.ToCanonical().ToFacelets(), "center relabeling");
            var changed = frame.Clone(); changed.Apply("R U F2 L' B D'");
            var reference = CubeState.Solved(); reference.Apply("R U F2 L' B D'");
            Equal(reference.ToFacelets(), changed.ToCanonical().ToFacelets(), "spatial moves commute with color relabeling");
            Equal(reference.CP, changed.CP, "cubie frame independent of center colors");
            foreach (char rotation in "xyz") { var next = frame.Clone(); next.Apply(new Move(rotation)); queue.Enqueue(next); }
        }
        Equal(24, seen.Count, "all proper center orientations");
        foreach (string token in new[]{"M","E","S","Rw","Lw","Uw","Dw","Fw","Bw"})
        {
            var state = CubeState.Solved(); state.Apply(token);
            Check(CubeState.FromFacelets(state.ToFacelets()).Validate().IsValid, token + " center-normalized reachability");
            Equal(state.ToCanonical().CP, state.CP, token + " normalized arrays");
        }
    }
    private static void Geometry()
    {
        var occupied = new HashSet<string>(); var positions = new Dictionary<Int3,int>();
        foreach (var sticker in CubeState.Solved().GetStickers())
        {
            Check(occupied.Add(sticker.Position + ":" + sticker.Normal), "unique sticker location");
            Equal(sticker.Index, CubeGeometry.GetIndex(sticker.Position,sticker.Normal), "geometry index round trip");
            Check(sticker.Position.X * sticker.Normal.X + sticker.Position.Y * sticker.Normal.Y + sticker.Position.Z * sticker.Normal.Z == 1, "sticker on exposed face");
            if (!positions.ContainsKey(sticker.Position)) positions.Add(sticker.Position,0); positions[sticker.Position]++;
        }
        Equal(26, positions.Count, "26 visible pieces");
        int corners = 0, edges = 0, centers = 0;
        foreach (int count in positions.Values) { if (count == 3) corners++; else if (count == 2) edges++; else if (count == 1) centers++; }
        Equal(8,corners,"geometry corners"); Equal(12,edges,"geometry edges"); Equal(6,centers,"geometry centers");
        Equal(new Int3(1,1,1), CubeGeometry.GetFacelet(8).Position, "U9 at URF");
        Equal(new Int3(1,1,1), CubeGeometry.GetFacelet(9).Position, "R1 at URF");
        Equal(new Int3(1,1,1), CubeGeometry.GetFacelet(20).Position, "F3 at URF");
        Equal(new Int3(1,1,-1), CubeGeometry.Rotate(new Int3(1,1,1),'x',-1), "R carries URF to UBR");
        Equal(new Int3(-1,1,1), CubeGeometry.Rotate(new Int3(1,1,1),'y',-1), "U carries URF to UFL");
        Equal(new Int3(1,-1,1), CubeGeometry.Rotate(new Int3(1,1,1),'z',-1), "F carries URF to DFR");
    }
    private static void Scrambles()
    {
        Equal(Move.FormatSequence(Scrambler.Generate(42)),Move.FormatSequence(Scrambler.Generate(42)),"seed repeatability");
        Check(Move.FormatSequence(Scrambler.Generate(42)) != Move.FormatSequence(Scrambler.Generate(43)),"seed variation");
        for (int seed = 0; seed < 100; seed++)
        {
            var moves = Scrambler.Generate(seed,25); var state = CubeState.Solved(); state.Apply(moves);
            Check(!state.IsSolved,"scramble changed state seed " + seed);
            Equal(state.ToFacelets(),CubeState.Deserialize(state.Serialize()).ToFacelets(),"seeded snapshot " + seed);
            for (int i = 1; i < moves.Length; i++) Check(moves[i].Axis != moves[i-1].Axis,"random-move axis choice");
            // Mixed moves exercise validation after slices/rotations as well as outer turns.
            state.Apply("M E' S2 Rw x' Uw2 z");
            Check(state.Validate().IsValid,"mixed-move legal state seed " + seed);
            Equal(state.ToCanonical().ToFacelets(),CubeState.FromCubies(state.CP,state.CO,state.EP,state.EO).ToFacelets(),"cubie reconstruction seed " + seed);
        }
        Throws<ArgumentOutOfRangeException>(() => Scrambler.Generate(1,-1),"negative scramble length");
    }
    private static void Invalid(string facelets,string code)
    {
        Check(!CubeState.TryFromFacelets(facelets,out CubeState state,out ValidationResult validation),"invalid state rejected " + code);
        Check(state == null,"invalid import has no partial state"); Equal(ImportStatus.Invalid,validation.Status,"invalid classification"); Equal(code,validation.Code,"invalid reason");
    }
    private static void InvalidJson(string json,ImportStatus expected)
    {
        Check(!CubeState.TryDeserialize(json,out CubeState state,out ValidationResult result),"invalid snapshot rejected");
        Check(state == null,"invalid snapshot no state"); Equal(expected,result.Status,"snapshot classification");
    }
    private static int[] Identity(int length) { var values = new int[length]; for (int i = 0; i < length; i++) values[i] = i; return values; }
    private static void Swap(char[] values,int a,int b) { char t = values[a]; values[a] = values[b]; values[b] = t; }
    private static void Cycle(char[] values,int a,int b,int c) { char t = values[c]; values[c] = values[b]; values[b] = values[a]; values[a] = t; }
    private static void Check(bool value,string message) { assertions++; if (!value) throw new Exception("CoreTests: " + message); }
    private static void Equal<T>(T expected,T actual,string message) { Check(EqualityComparer<T>.Default.Equals(expected,actual),message + "; expected " + expected + ", actual " + actual); }
    private static void Equal(int[] expected,int[] actual,string message)
    {
        Check(expected.Length == actual.Length,message + " length");
        for (int i = 0; i < expected.Length; i++) Check(expected[i] == actual[i],message + " index " + i + "; expected " + expected[i] + ", actual " + actual[i]);
    }
    private static void Throws<T>(Action action,string message) where T : Exception
    {
        try { action(); } catch (T) { assertions++; return; }
        throw new Exception("CoreTests: expected " + typeof(T).Name + ": " + message);
    }
}
