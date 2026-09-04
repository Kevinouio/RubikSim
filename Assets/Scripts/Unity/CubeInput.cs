using System;
using System.Runtime.InteropServices;
using RubikSim.Core;
using UnityEngine;

namespace RubikSim.UnityView
{
    /// <summary>Camera gestures never change the notation frame or exact cube state.</summary>
    public sealed class CubeInput : MonoBehaviour
    {
        public event Action<string> NotationRequested;
        public event Action<string> ControlRequested;
        public Camera ViewCamera { get; private set; }
        public bool TurnsEnabled = true;
        public bool PickingEnabled = true;
        private float yaw = 34f, pitch = 24f, distance = 8.2f;
        private Vector2 pointerStart, previousPointer;
        private int pickedIndex = -1;
        private bool orbiting, dragging, multiTouch;
        private Vector2 multiTouchCenter;
        private float pinchDistance;
        private static readonly KeyCode[] MoveKeys = { KeyCode.U, KeyCode.R, KeyCode.F, KeyCode.D, KeyCode.L, KeyCode.B, KeyCode.M, KeyCode.E, KeyCode.S, KeyCode.X, KeyCode.Y, KeyCode.Z };

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RubikCanvasHasFocus();
        [DllImport("__Internal")] private static extern void RubikInitializePointerInput();
        [DllImport("__Internal")] private static extern int RubikPointerShift();
#endif

        public void Initialize(Camera camera)
        {
            ViewCamera = camera;
            ResetView();
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
            RubikInitializePointerInput();
#endif
            Input.simulateMouseWithTouches = false;
        }

        public void ResetView() { yaw = 34f; pitch = 24f; distance = 8.2f; ApplyCamera(); }
        public float[] CameraPose => new[] { yaw, pitch, distance };

        private void ApplyCamera()
        {
            if (ViewCamera == null) return;
            float y = yaw * Mathf.Deg2Rad, p = pitch * Mathf.Deg2Rad;
            ViewCamera.transform.position = new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), -Mathf.Cos(y) * Mathf.Cos(p)) * distance;
            ViewCamera.transform.LookAt(Vector3.zero, Vector3.up);
        }

        private void Update()
        {
            if (ViewCamera == null) return;
            if (Input.touchCount > 0) { HandleTouch(); return; }
            multiTouch = false;
            Vector2 pointer = Input.mousePosition;
            bool onCanvas = pointer.x >= 0 && pointer.y >= 0 && pointer.x <= Screen.width && pointer.y <= Screen.height;
            if (onCanvas && Input.GetMouseButtonDown(1)) BeginPointer(pointer, true);
            if (onCanvas && Input.GetMouseButtonDown(0)) BeginPointer(pointer, false);
            if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1))) MovePointer(pointer);
            if (dragging && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))) EndPointer(pointer);
            if (onCanvas && Mathf.Abs(Input.mouseScrollDelta.y) > 0) Zoom(Input.mouseScrollDelta.y * 0.45f);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (RubikCanvasHasFocus() == 0) return;
#endif
            HandleKeyboard();
        }

        private void HandleKeyboard()
        {
            bool inverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool wide = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            const string faces = "urfdlbmesxyz";
            for (int index = 0; index < faces.Length; index++)
            {
                char face = faces[index];
                KeyCode key = MoveKeys[index];
                if (!Input.GetKeyDown(key) || !TurnsEnabled) continue;
                string token = (face == 'x' || face == 'y' || face == 'z' ? face : char.ToUpperInvariant(face)).ToString();
                if (wide && "urfdlb".IndexOf(face) >= 0) token += "w";
                if (inverse) token += "'";
                NotationRequested?.Invoke(token);
            }
            if (Input.GetKeyDown(KeyCode.Alpha0)) ResetView();
            if (Input.GetKeyDown(KeyCode.Space)) ControlRequested?.Invoke("togglePlay");
            if (Input.GetKeyDown(KeyCode.RightArrow)) ControlRequested?.Invoke("next");
            if (Input.GetKeyDown(KeyCode.LeftArrow)) ControlRequested?.Invoke("previous");
            if (Input.GetKeyDown(KeyCode.Escape)) ControlRequested?.Invoke("pause");
        }

        private void HandleTouch()
        {
            if (Input.touchCount >= 2)
            {
                Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                Vector2 center = (a.position + b.position) * 0.5f;
                float separation = Vector2.Distance(a.position, b.position);
                if (multiTouch)
                {
                    Orbit(center - multiTouchCenter);
                    Zoom((separation - pinchDistance) * 0.012f);
                }
                multiTouch = true; dragging = false;
                multiTouchCenter = center; pinchDistance = separation;
                return;
            }
            Touch touch = Input.GetTouch(0);
            if (multiTouch) { if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) multiTouch = false; return; }
            if (touch.phase == TouchPhase.Began) BeginPointer(touch.position, false);
            if (touch.phase == TouchPhase.Moved && dragging) MovePointer(touch.position);
            if (touch.phase == TouchPhase.Ended && dragging) EndPointer(touch.position);
            if (touch.phase == TouchPhase.Canceled) dragging = false;
        }

        private void BeginPointer(Vector2 point, bool forceOrbit)
        {
            pointerStart = previousPointer = point;
            dragging = true;
            pickedIndex = -1;
            if (!forceOrbit && PickingEnabled && Physics.Raycast(ViewCamera.ScreenPointToRay(point), out RaycastHit hit))
            {
                var sticker = hit.collider.GetComponent<StickerPick>();
                if (sticker != null) pickedIndex = sticker.Index;
            }
            orbiting = forceOrbit || pickedIndex < 0;
        }

        private void MovePointer(Vector2 point)
        {
            if (orbiting) Orbit(point - previousPointer);
            previousPointer = point;
        }

        private void EndPointer(Vector2 point)
        {
            dragging = false;
            if (orbiting || pickedIndex < 0 || !TurnsEnabled) return;
            bool inverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#if UNITY_WEBGL && !UNITY_EDITOR
            inverse = RubikPointerShift() != 0;
#endif
            Vector2 delta = point - pointerStart;
            if (delta.magnitude < 15f)
            {
                NotationRequested?.Invoke("URFDLB"[pickedIndex / 9] + (inverse ? "'" : ""));
                return;
            }
            var geometry = CubeGeometry.GetFacelet(pickedIndex);
            Vector3 normal = new Vector3(geometry.Normal.X, geometry.Normal.Y, geometry.Normal.Z);
            Vector3 position = new Vector3(geometry.Position.X, geometry.Position.Y, geometry.Position.Z);
            // Pointer displacement starts in Unity's rendered frame. Convert it to the logical
            // right-handed +Z-front frame before deriving the physical layer and turn direction.
            Vector3 viewDrag = ViewCamera.transform.right * delta.x + ViewCamera.transform.up * delta.y;
            Vector3 logicalDrag = new Vector3(viewDrag.x, viewDrag.y, -viewDrag.z);
            Vector3 drag = Vector3.ProjectOnPlane(logicalDrag, normal);
            Vector3 axis = Vector3.Cross(normal, drag);
            float ax = Mathf.Abs(axis.x), ay = Mathf.Abs(axis.y), az = Mathf.Abs(axis.z);
            int dimension = ax > ay && ax > az ? 0 : ay > az ? 1 : 2;
            int layer = Mathf.RoundToInt(position[dimension]);
            Vector3 unit = dimension == 0 ? Vector3.right : dimension == 1 ? Vector3.up : Vector3.forward;
            float signedMotion = Vector3.Dot(Vector3.Cross(unit, position + normal * .488f), drag);
            if (Mathf.Abs(signedMotion) < 0.001f) return;
            char face = dimension == 0 ? (layer > 0 ? 'R' : layer < 0 ? 'L' : 'M') :
                        dimension == 1 ? (layer > 0 ? 'U' : layer < 0 ? 'D' : 'E') :
                                         (layer > 0 ? 'F' : layer < 0 ? 'B' : 'S');
            bool naturalPositive = face == 'L' || face == 'D' || face == 'B' || face == 'M' || face == 'E';
            bool reverse = signedMotion > 0 != naturalPositive;
            NotationRequested?.Invoke(face + (reverse ? "'" : ""));
        }

        private void Orbit(Vector2 delta)
        {
            yaw -= delta.x * 0.28f;
            pitch = Mathf.Clamp(pitch - delta.y * 0.28f, -82f, 82f);
            ApplyCamera();
        }

        private void Zoom(float amount)
        {
            distance = Mathf.Clamp(distance - amount, 5f, 13f);
            ApplyCamera();
        }
    }
}
