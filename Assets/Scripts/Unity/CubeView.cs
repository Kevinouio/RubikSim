using System;
using System.Collections.Generic;
using RubikSim.Core;
using UnityEngine;

namespace RubikSim.UnityView
{
    /// <summary>Disposable floating-point presentation. Only exact snapshots define cube state.</summary>
    public sealed class CubeView : MonoBehaviour
    {
        private readonly Dictionary<Vector3Int, Transform> pieces = new Dictionary<Vector3Int, Transform>();
        private readonly Dictionary<char, Material> colors = new Dictionary<char, Material>();
        private readonly Dictionary<char, Mesh> glyphs = new Dictionary<char, Mesh>();
        private readonly MeshRenderer[] stickers = new MeshRenderer[54];
        private readonly MeshFilter[] labels = new MeshFilter[54];
        private readonly List<int> highlighted = new List<int>();
        private Material plastic, ink, paleInk;
        private Mesh stickerMesh;
        private Transform pivot;
        private string target;
        private Vector3 turnAxis;
        private float angle, elapsed, duration;
        private LineRenderer axisLine;
        private MaterialPropertyBlock properties;
        public bool IsAnimating { get; private set; }
        public string RestingFacelets { get; private set; }
        public bool LastAnimationAgreed { get; private set; } = true;

        // Core uses +Z toward the observer; Unity cameras look along local +Z.
        // Reflect geometric vectors across XY so a front view keeps logical +X on screen right.
        internal static Vector3 LogicalToView(Vector3 value) => new Vector3(value.x, value.y, -value.z);
        internal static Vector3 LogicalToView(Int3 value) => new Vector3(value.X, value.Y, -value.Z);

        public void Initialize()
        {
            if (plastic != null) return;
            var shader = Resources.Load<Shader>("CubeSurface");
            if (shader == null) throw new InvalidOperationException("Missing procedural cube shader.");
            plastic = Material(shader, new Color(0.045f, 0.055f, 0.075f));
            ink = Material(shader, new Color(0.025f, 0.035f, 0.06f));
            paleInk = Material(shader, new Color(0.98f, 0.98f, 1f));
            colors['U'] = Material(shader, new Color(0.94f, 0.96f, 1f));
            colors['R'] = Material(shader, new Color(0.91f, 0.12f, 0.18f));
            colors['F'] = Material(shader, new Color(0.08f, 0.68f, 0.39f));
            colors['D'] = Material(shader, new Color(1f, 0.81f, 0.08f));
            colors['L'] = Material(shader, new Color(1f, 0.38f, 0.06f));
            colors['B'] = Material(shader, new Color(0.1f, 0.35f, 0.94f));
            foreach (char color in "URFDLB") glyphs[color] = LetterMesh.Create(color);
            for (int face = 0; face < 6; face++)
            {
                var geometry = CubeGeometry.GetFacelet(face * 9 + 4);
                var normal = LogicalToView(geometry.Normal);
                var up = normal.y > .5f ? Vector3.forward : normal.y < -.5f ? Vector3.back : Vector3.up;
                var label = new GameObject("Fixed notation face " + "URFDLB"[face]);
                label.transform.SetParent(transform, false);
                label.transform.localPosition = normal * 1.55f + up * 1.68f;
                // Glyph coordinates are read from their -Z side, like text facing the camera.
                label.transform.localRotation = Quaternion.LookRotation(-normal, up);
                label.transform.localScale = Vector3.one * .23f;
                label.AddComponent<MeshFilter>().sharedMesh = glyphs["URFDLB"[face]];
                label.AddComponent<MeshRenderer>().sharedMaterial = paleInk;
            }
            stickerMesh = CreateStickerMesh();
            properties = new MaterialPropertyBlock();
            pivot = new GameObject("Temporary turn pivot").transform;
            pivot.SetParent(transform, false);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                var position = new Vector3Int(x, y, z);
                var piece = new GameObject("Cubie " + position).transform;
                piece.SetParent(transform, false);
                piece.localPosition = LogicalToView(position);
                pieces[position] = piece;
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Black body";
                body.transform.SetParent(piece, false);
                body.transform.localScale = Vector3.one * 0.96f;
                body.GetComponent<MeshRenderer>().sharedMaterial = plastic;
            }
            for (int i = 0; i < 54; i++)
            {
                var geometry = CubeGeometry.GetFacelet(i);
                var position = new Vector3Int(geometry.Position.X, geometry.Position.Y, geometry.Position.Z);
                var normal = LogicalToView(geometry.Normal);
                var sticker = new GameObject("Sticker " + i);
                sticker.transform.SetParent(pieces[position], false);
                sticker.transform.localPosition = normal * 0.488f;
                var up = normal.y > 0.5f ? Vector3.forward : normal.y < -0.5f ? Vector3.back : Vector3.up;
                sticker.transform.localRotation = Quaternion.LookRotation(normal, up);
                sticker.AddComponent<MeshFilter>().sharedMesh = stickerMesh;
                stickers[i] = sticker.AddComponent<MeshRenderer>();
                var collider = sticker.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.85f, 0.85f, 0.022f);
                var pick = sticker.AddComponent<StickerPick>();
                pick.Index = i;
                var label = new GameObject("Color letter");
                label.transform.SetParent(sticker.transform, false);
                label.transform.localPosition = new Vector3(0f, 0f, 0.004f);
                // Sticker forward is the outward normal for auditing; turn the glyph's printed side out.
                label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                label.transform.localScale = Vector3.one * 0.26f;
                labels[i] = label.AddComponent<MeshFilter>();
                label.AddComponent<MeshRenderer>().sharedMaterial = ink;
            }
            var axis = new GameObject("Active turn axis");
            axis.transform.SetParent(transform, false);
            axisLine = axis.AddComponent<LineRenderer>();
            axisLine.useWorldSpace = false;
            axisLine.positionCount = 2;
            axisLine.startWidth = axisLine.endWidth = 0.025f;
            axisLine.sharedMaterial = paleInk;
            axisLine.enabled = false;
        }

        private static Material Material(Shader shader, Color color)
        {
            var material = new Material(shader) { name = "Cube " + color };
            material.SetColor("_Color", color);
            return material;
        }

        public void AbortAndRender(string facelets)
        {
            Initialize();
            if (facelets == null || facelets.Length != 54) throw new ArgumentException("A view needs exactly 54 facelets.");
            foreach (var pair in pieces)
            {
                pair.Value.SetParent(transform, false);
                pair.Value.localPosition = LogicalToView(pair.Key);
                pair.Value.localRotation = Quaternion.identity;
            }
            pivot.localRotation = Quaternion.identity;
            for (int i = 0; i < 54; i++)
            {
                char color = facelets[i];
                if (!colors.ContainsKey(color)) throw new ArgumentException("Unknown sticker color: " + color);
                stickers[i].sharedMaterial = colors[color];
                labels[i].sharedMesh = glyphs[color];
                labels[i].GetComponent<MeshRenderer>().sharedMaterial = color == 'B' || color == 'R' ? paleInk : ink;
            }
            target = RestingFacelets = facelets;
            IsAnimating = false;
            axisLine.enabled = false;
            ApplyHighlights();
            Physics.SyncTransforms();
        }

        public void BeginTurn(Move move, string afterFacelets, float seconds)
        {
            if (IsAnimating) throw new InvalidOperationException("Only one controlled animation may run at once.");
            turnAxis = move.Axis == 'x' ? Vector3.right : move.Axis == 'y' ? Vector3.up : Vector3.back;
            // Under a reflection, an axial rotation maps to the reflected axis with the opposite angle.
            angle = -move.SignedQuarterTurns * 90f;
            duration = Mathf.Clamp(seconds, 0.025f, 3f);
            elapsed = 0;
            target = afterFacelets;
            foreach (var pair in pieces)
            {
                int coordinate = move.Axis == 'x' ? pair.Key.x : move.Axis == 'y' ? pair.Key.y : pair.Key.z;
                if ((move.LayerMask & (1 << (coordinate + 1))) != 0) pair.Value.SetParent(pivot, true);
            }
            IsAnimating = true;
            axisLine.SetPosition(0, turnAxis * -2.05f);
            axisLine.SetPosition(1, turnAxis * 2.05f);
            axisLine.enabled = true;
            for (int i = 0; i < 54; i++) SetHighlight(i, stickers[i].transform.parent.parent == pivot || highlighted.Contains(i));
        }

        public void AdvanceAnimation(float deltaSeconds)
        {
            if (!IsAnimating) return;
            elapsed += Mathf.Max(0, deltaSeconds);
            float t = Mathf.Clamp01(elapsed / duration);
            pivot.localRotation = Quaternion.AngleAxis(angle * t * t * (3 - 2 * t), turnAxis);
            if (t >= 1f)
            {
                LastAnimationAgreed = ReadRenderedFaceletsCore() == target;
                AbortAndRender(target);
            }
        }

        public void Highlight(IEnumerable<int> indices)
        {
            highlighted.Clear();
            if (indices != null) foreach (int index in indices) if (index >= 0 && index < 54) highlighted.Add(index);
            if (!IsAnimating) ApplyHighlights();
        }

        private void ApplyHighlights()
        {
            for (int i = 0; i < 54; i++) SetHighlight(i, highlighted.Contains(i));
        }

        private void SetHighlight(int index, bool active)
        {
            properties.SetFloat("_Highlight", active ? 1f : 0f);
            stickers[index].SetPropertyBlock(properties);
        }

        /// <summary>Reads actual sticker mesh transforms and assigned visible materials, never target or RestingFacelets.</summary>
        public string ReadRenderedFacelets()
        {
            if (IsAnimating) return "";
            return ReadRenderedFaceletsCore();
        }

        /// <summary>Actual visible sticker center, normalized from the canvas top left for input verification.</summary>
        public bool TryGetStickerTarget(int index, Camera camera, out Vector2 point)
        {
            point = default;
            if (IsAnimating || index < 0 || index >= stickers.Length || stickers[index] == null) return false;
            var sticker = stickers[index];
            Vector3 projected = camera.WorldToViewportPoint(sticker.transform.position);
            if (projected.z <= 0 || projected.x <= 0 || projected.x >= 1 || projected.y <= 0 || projected.y >= 1) return false;
            if (!Physics.Raycast(camera.ViewportPointToRay(projected), out RaycastHit hit) || hit.collider.gameObject != sticker.gameObject) return false;
            point = new Vector2(projected.x, 1f - projected.y);
            return true;
        }

        private string ReadRenderedFaceletsCore()
        {
            var result = new char[54];
            for (int i = 0; i < stickers.Length; i++)
            {
                var sticker = stickers[i];
                Vector3 point = transform.InverseTransformPoint(sticker.transform.position);
                Vector3 direction = transform.InverseTransformDirection(sticker.transform.forward);
                // Independently convert observed Unity transforms back to the core's +Z-front frame.
                point.z = -point.z;
                direction.z = -direction.z;
                int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y), z = Mathf.RoundToInt(point.z);
                int nx = Mathf.RoundToInt(direction.x), ny = Mathf.RoundToInt(direction.y), nz = Mathf.RoundToInt(direction.z);
                if ((direction - new Vector3(nx, ny, nz)).sqrMagnitude > .00001f ||
                    (point - new Vector3(x + .488f * nx, y + .488f * ny, z + .488f * nz)).sqrMagnitude > .00001f)
                    return "non-exact-resting-transform";
                int face, row, col;
                if (ny == 1) { face = 0; row = z + 1; col = x + 1; }
                else if (nx == 1) { face = 1; row = 1 - y; col = 1 - z; }
                else if (nz == 1) { face = 2; row = 1 - y; col = x + 1; }
                else if (ny == -1) { face = 3; row = 1 - z; col = x + 1; }
                else if (nx == -1) { face = 4; row = 1 - y; col = z + 1; }
                else if (nz == -1) { face = 5; row = 1 - y; col = 1 - x; }
                else return "invalid-normal";
                if (row < 0 || row > 2 || col < 0 || col > 2) return "invalid-position";
                int index = face * 9 + row * 3 + col;
                if (result[index] != '\0') return "duplicate-sticker";
                char color = '?';
                foreach (var pair in colors) if (pair.Value == sticker.sharedMaterial) { color = pair.Key; break; }
                result[index] = color;
            }
            return new string(result);
        }

        private static Mesh CreateStickerMesh()
        {
            const float half = 0.425f, corner = 0.065f;
            var vertices = new List<Vector3> { Vector3.zero };
            for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
            {
                float cx = cornerIndex == 0 || cornerIndex == 3 ? half - corner : -half + corner;
                float cy = cornerIndex < 2 ? half - corner : -half + corner;
                for (int segment = 0; segment <= 4; segment++)
                {
                    float radians = (cornerIndex * 90 + segment * 22.5f) * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(cx + Mathf.Cos(radians) * corner, cy + Mathf.Sin(radians) * corner, 0));
                }
            }
            var triangles = new List<int>();
            for (int i = 1; i < vertices.Count; i++) { triangles.Add(0); triangles.Add(i); triangles.Add(i == vertices.Count - 1 ? 1 : i + 1); }
            var mesh = new Mesh { name = "Rounded procedural sticker" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (var material in colors.Values) Release(material);
            foreach (var mesh in glyphs.Values) Release(mesh);
            Release(plastic); Release(ink); Release(paleInk); Release(stickerMesh);
        }

        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
