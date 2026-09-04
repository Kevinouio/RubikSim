using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RubikSim.Application;
using RubikSim.Core;
using RubikSim.UnityView;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RubikSim.Editor
{
    public static class BuildWeb
    {
        public const string EditorVersion = "6000.0.68f1";
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("RubikSim/Configure 3x3 Web project")]
        public static void Configure()
        {
            if (UnityEngine.Application.unityVersion != EditorVersion)
                throw new InvalidOperationException("Use the pinned Unity " + EditorVersion + "; found " + UnityEngine.Application.unityVersion + ".");
            PlayerSettings.companyName = "RubikSim";
            PlayerSettings.productName = "RubikSim 3x3";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.WebGL, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 512;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            QualitySettings.antiAliasing = 2;
            // The legacy input APIs used by CubeInput are explicitly selected; no Input System package is required.
            AssetDatabase.SaveAssets();
            var playerSettingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (playerSettingsAssets.Length == 0)
                throw new BuildFailedException("Unity has not initialized ProjectSettings/ProjectSettings.asset. Open this project once in Unity " + EditorVersion + ", allow import to finish, then rerun configuration.");
            var settings = new SerializedObject(playerSettingsAssets[0]);
            var inputMode = settings.FindProperty("activeInputHandler");
            if (inputMode == null) throw new BuildFailedException("Pinned Editor is missing the activeInputHandler setting. Configuration cannot verify legacy input mode.");
            inputMode.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo();
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("RubikBridge").AddComponent<CubeHost>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("RubikSim/Build website Unity player")]
        public static void Build()
        {
            Configure();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install Web Build Support for Unity " + EditorVersion + ".");
            string output = Path.GetFullPath("website/unity");
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.WebGL, options = BuildOptions.StrictMode
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Unity Web build failed: " + report.summary.result + "; errors=" + report.summary.totalErrors);
            string FileFor(string suffix) => "Build/" + Path.GetFileName(Directory.GetFiles(Path.Combine(output, "Build"), "*" + suffix).Single());
            var manifest = new WebManifest
            {
                loaderUrl = FileFor(".loader.js"), dataUrl = FileFor(".data"),
                frameworkUrl = FileFor(".framework.js"), codeUrl = FileFor(".wasm"),
                companyName = PlayerSettings.companyName, productName = PlayerSettings.productName,
                productVersion = PlayerSettings.bundleVersion, unityVersion = EditorVersion,
                sourceSha256 = SourceChecksum()
            };
            File.WriteAllText(Path.Combine(output, "build-manifest.json"), JsonUtility.ToJson(manifest, true));
            Directory.CreateDirectory("artifacts");
            File.WriteAllText("artifacts/unity-build-result.json", JsonUtility.ToJson(new BuildEvidence
            {
                result = report.summary.result.ToString(), unityVersion = EditorVersion,
                totalBytes = report.summary.totalSize, elapsedSeconds = report.summary.totalTime.TotalSeconds,
                utc = DateTime.UtcNow.ToString("O"), errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                sourceSha256 = manifest.sourceSha256
            }, true));
            Debug.Log("UNITY_WEB_BUILD_PASS " + output + " " + report.summary.totalSize + " bytes");
        }

        /// <summary>Run in the real pinned Editor, not a substitute engine. Checks presentation at pre-snap endpoints.</summary>
        [MenuItem("RubikSim/Verify renderer and interruption handling")]
        public static void VerifyView()
        {
            Configure();
            var root = new GameObject("Renderer verification");
            var view = root.AddComponent<CubeView>();
            int checks = 0;
            try
            {
                var state = CubeState.Solved();
                view.AbortAndRender(state.ToFacelets());
                var cameraObject = new GameObject("Front presentation verification camera");
                cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -8);
                camera.transform.LookAt(Vector3.zero, Vector3.up);
                camera.aspect = 1;
                bool leftVisible = view.TryGetStickerTarget(21, camera, out Vector2 left);
                bool centerVisible = view.TryGetStickerTarget(22, camera, out Vector2 center);
                bool rightVisible = view.TryGetStickerTarget(23, camera, out Vector2 right);
                Require(leftVisible && centerVisible && rightVisible, "Front camera rays hit actual F-row stickers"); checks++;
                Require(left.x < center.x && center.x < right.x, "Front face columns project left to right"); checks++;
                var frontSticker = root.GetComponentsInChildren<StickerPick>().Single(sticker => sticker.Index == 22);
                foreach (Transform glyph in new[] { frontSticker.transform.Find("Color letter"), root.transform.Find("Fixed notation face F") })
                {
                    Vector3 origin = camera.WorldToViewportPoint(glyph.position);
                    Vector3 printedRight = camera.WorldToViewportPoint(glyph.TransformPoint(Vector3.right));
                    Vector3 printedUp = camera.WorldToViewportPoint(glyph.TransformPoint(Vector3.up));
                    Require(printedRight.x > origin.x, glyph.name + " prints rightward without mirroring"); checks++;
                    Require(printedUp.y > origin.y, glyph.name + " prints upward"); checks++;
                }
                UnityEngine.Object.DestroyImmediate(cameraObject);
                state.Apply("R U F2 L' D B x y' z2 M E' S2 Rw Uw' Fw2");
                view.AbortAndRender(state.ToFacelets());
                Require(view.ReadRenderedFacelets() == state.ToFacelets(), "Snapshot reconstruction"); checks++;
                foreach (string token in new[] { "U", "R", "F", "D", "L", "B", "M", "E", "S", "x", "y", "z", "Rw", "Uw", "Fw", "Lw", "Dw", "Bw" })
                for (int turns = 1; turns <= 3; turns++)
                {
                    var move = Move.Parse(token + (turns == 2 ? "2" : turns == 3 ? "'" : ""));
                    state.Apply(move);
                    view.BeginTurn(move, state.ToFacelets(), .2f);
                    view.AdvanceAnimation(.05f);
                    Require(view.IsAnimating && view.ReadRenderedFacelets() == "", "Mid-turn is marked transient"); checks++;
                    view.AdvanceAnimation(.2f);
                    Require(!view.IsAnimating && view.LastAnimationAgreed, "Animated endpoint " + move); checks++;
                    Require(view.ReadRenderedFacelets() == state.ToFacelets(), "Resting endpoint " + move); checks++;
                }
                var session = new CubeSession();
                view.AbortAndRender(session.State.ToFacelets());
                session.QueueNotation("R U F L D B");
                Require(session.TryBeginMove(out Move first, out CubeState before, out CubeState after), "Queue starts"); checks++;
                view.BeginTurn(first, after.ToFacelets(), 1);
                view.AdvanceAnimation(.2f);
                Require(!session.TryBeginMove(out _, out _, out _), "No double commit while animating"); checks++;
                session.Undo(); view.AbortAndRender(session.State.ToFacelets());
                Require(session.State.IsSolved && !view.IsAnimating && !session.HasPendingMoves && view.ReadRenderedFacelets() == session.State.ToFacelets(), "Undo cancels queue and half-turn"); checks++;
                session.Redo(); view.AbortAndRender(session.State.ToFacelets());
                Require(!session.State.IsSolved && view.ReadRenderedFacelets() == session.State.ToFacelets(), "Redo restores committed turn"); checks++;
                session.QueueNotation("U2 F2"); session.TryBeginMove(out first, out before, out after);
                view.BeginTurn(first, after.ToFacelets(), 1); view.AdvanceAnimation(.1f);
                session.Reset(); view.AbortAndRender(session.State.ToFacelets());
                Require(session.State.IsSolved && !view.IsAnimating && !session.HasPendingMoves && !session.IsMoveInFlight && view.ReadRenderedFacelets() == session.State.ToFacelets(), "Reset aborts to exact solved view"); checks++;
                Directory.CreateDirectory("artifacts");
                File.WriteAllText("artifacts/unity-view-result.json", JsonUtility.ToJson(new ViewEvidence
                { result = "passed", checks = checks, unityVersion = EditorVersion, utc = DateTime.UtcNow.ToString("O") }, true));
                Debug.Log("UNITY_VIEW_VERIFY_PASS checks=" + checks);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("UNITY_VIEW_VERIFY_FAIL: " + message); }

        private static string SourceChecksum()
        {
            string root = Path.GetFullPath(".");
            var files = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories).Concat(new[]
            {
                "Packages/manifest.json", "Packages/packages-lock.json",
                "ProjectSettings/ProjectVersion.txt", "ProjectSettings/EditorBuildSettings.asset"
            }.Where(File.Exists)).Select(file => Path.GetFullPath(file).Substring(root.Length + 1).Replace('\\', '/')).OrderBy(file => file, StringComparer.Ordinal);
            using (var input = new MemoryStream())
            using (var hash = SHA256.Create())
            {
                foreach (string file in files)
                {
                    byte[] pathBytes = Encoding.UTF8.GetBytes(file + "\0"), contents = File.ReadAllBytes(file);
                    input.Write(pathBytes, 0, pathBytes.Length); input.Write(contents, 0, contents.Length); input.WriteByte(0);
                }
                input.Position = 0;
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
            }
        }

        [Serializable] private sealed class WebManifest { public string loaderUrl, dataUrl, frameworkUrl, codeUrl, companyName, productName, productVersion, unityVersion, sourceSha256; }
        [Serializable] private sealed class BuildEvidence { public string result, unityVersion, utc, sourceSha256; public ulong totalBytes; public double elapsedSeconds; public int errors, warnings; }
        [Serializable] private sealed class ViewEvidence { public string result, unityVersion, utc; public int checks; }
    }
}
