using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using RubikSim.Core;

namespace RubikSim.Tools
{
    /// <summary>
    /// Reproduces browser integration inputs using the production core. These move effects are
    /// generated integration expectations; independent move fixtures live in tests/CoreTests.cs.
    /// </summary>
    internal static class BrowserFixtures
    {
        private const string GeneratorVersion = "cube-3x3-browser-fixtures-v1";
        private const string FixturePath = "tools/browser-check/fixtures/seeded-states.json";

        private static int Main(string[] args)
        {
            try
            {
                bool verify = false;
                string output = null;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--verify") verify = true;
                    else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
                    else throw new ArgumentException("Usage: BrowserFixtures [--verify] [--output path]");
                }
                output = output == null ? Path.Combine(FindRepositoryRoot(), FixturePath) : Path.GetFullPath(output);
                string json = Generate();
                if (verify)
                {
                    if (!File.Exists(output) || File.ReadAllText(output) != json)
                        throw new InvalidOperationException("Browser fixtures differ from the deterministic generator. Regenerate and review: " + output);
                    Console.WriteLine("PASS browser fixtures exactly match " + GeneratorVersion + ": 100 seeds 0..99, 25 moves per state, 27 generated move effects.");
                }
                else
                {
                    string directory = Path.GetDirectoryName(output);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllText(output, json, new UTF8Encoding(false));
                    Console.WriteLine("WROTE " + output + ": 100 seeds 0..99, 25 moves per state, 27 generated move effects; " + GeneratorVersion + ".");
                }
                Console.WriteLine("These fixtures are generated from the production core. No independent move-fixture, solver, Unity, or browser checks run by this tool.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL browser fixtures: " + exception.Message);
                return 1;
            }
        }

        private static string Generate()
        {
            var effects = new Dictionary<string, string>();
            foreach (char face in "URFDLBMES")
                for (int turns = 1; turns <= 3; turns++)
                {
                    var move = new Move(face, turns);
                    var state = CubeState.Solved(); state.Apply(move);
                    effects.Add(move.ToString(), state.ToFacelets());
                }
            var states = new List<object>();
            for (int seed = 0; seed < 100; seed++)
            {
                var moves = Scrambler.Generate(seed, 25);
                var state = CubeState.Solved(); state.Apply(moves);
                string serialized = state.Serialize();
                if (CubeState.Deserialize(serialized).ToFacelets() != state.ToFacelets())
                    throw new InvalidOperationException("Snapshot round trip failed for seed " + seed + ".");
                states.Add(new { seed, notation = Move.FormatSequence(moves), serialized, facelets = state.ToFacelets() });
            }
            var document = new
            {
                formatVersion = 1,
                puzzle = CubeState.PuzzleId,
                schemaVersion = CubeState.SchemaVersion,
                definitionVersion = CubeState.DefinitionVersion,
                generatorVersion = GeneratorVersion,
                generator = "tools/RubikSim.BrowserFixtures.csproj; RubikSim.Core.CubeState + Scrambler.Generate(seed,25)",
                scrambleKind = "Seeded random-move scrambles; not uniform random states or official competition scrambles",
                knownMoveEffects = effects,
                states
            };
            // Stable property/seed/move ordering and LF ending; no generation timestamp or machine path.
            return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        }

        private static string FindRepositoryRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "Assets/Scripts/Core/CubeState.cs")) &&
                        File.Exists(Path.Combine(directory.FullName, "tools/RubikSim.BrowserFixtures.csproj")))
                        return directory.FullName;
                    directory = directory.Parent;
                }
            }
            throw new DirectoryNotFoundException("Cannot locate the RubikSim repository. Supply --output with the fixture destination.");
        }
    }
}
