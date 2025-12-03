using System.Collections.Generic;
using UnityEngine;

namespace RubikSim.Core
{
    public class Cubelet : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private MeshRenderer bodyRenderer;

        private readonly Dictionary<CubeFace, StickerBinding> _stickers = new();
        private bool _highlighted;
        private Color _highlightColor = Color.white;

        public string PieceId { get; private set; } = string.Empty;
        public CubePieceType PieceType { get; private set; }
        public Vector3Int Coordinates { get; private set; }

        public static Cubelet Create(CubePieceState state, CubeColorPalette palette, Transform parent, CubeletVisualConfig config, Material sharedStickerMaterial)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubelet = go.AddComponent<Cubelet>();
            cubelet.Initialize(state, palette, config, sharedStickerMaterial);
            cubelet.transform.SetParent(parent, false);
            return cubelet;
        }

        public void Initialize(CubePieceState state, CubeColorPalette palette, CubeletVisualConfig config, Material sharedStickerMaterial)
        {
            PieceId = state.Id;
            PieceType = state.Type;
            Coordinates = state.Position;
            name = $"Cubelet_{state.Id}";

            var spacing = config.LayerSpacing;
            transform.localPosition = new Vector3(state.Position.x * spacing, state.Position.y * spacing, state.Position.z * spacing);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * config.CubeletSize;

            bodyRenderer = bodyRenderer != null ? bodyRenderer : GetComponent<MeshRenderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            _stickers.Clear();
            BuildStickers(state, palette, config, sharedStickerMaterial);
        }

        public void ApplyPalette(CubeColorPalette palette)
        {
            foreach (var binding in _stickers.Values)
            {
                binding.BaseColor = palette.GetColor(binding.ColorFace);
                UpdateSticker(binding);
            }
        }

        public void SetHighlight(bool highlighted, Color color)
        {
            _highlighted = highlighted;
            _highlightColor = color;

            foreach (var binding in _stickers.Values)
            {
                UpdateSticker(binding);
            }
        }

        private void BuildStickers(CubePieceState piece, CubeColorPalette palette, CubeletVisualConfig config, Material sharedStickerMaterial)
        {
            foreach (var sticker in piece.Stickers)
            {
                var face = CubeState.GetFaceFromAxis(sticker.Key);
                var stickerObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                stickerObject.name = $"{PieceId}_{face}_Sticker";
                stickerObject.transform.SetParent(transform, false);
                stickerObject.transform.localPosition = (Vector3)sticker.Key * (0.5f * config.CubeletSize + config.StickerOffset);
                stickerObject.transform.localRotation = Quaternion.LookRotation(sticker.Key);
                stickerObject.transform.localScale = Vector3.one * config.StickerScale;

                var quadCollider = stickerObject.GetComponent<Collider>();
                if (quadCollider != null)
                {
                    DestroyImmediate(quadCollider);
                }

                var renderer = stickerObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = sharedStickerMaterial;
                var binding = new StickerBinding(face, sticker.Value, renderer)
                {
                    BaseColor = palette.GetColor(sticker.Value)
                };
                _stickers[face] = binding;
                UpdateSticker(binding);
            }
        }

        private void UpdateSticker(StickerBinding binding)
        {
            var finalColor = _highlighted ? Color.Lerp(binding.BaseColor, _highlightColor, 0.55f) : binding.BaseColor;
            binding.MaterialBlock ??= new MaterialPropertyBlock();
            binding.MaterialBlock.SetColor(BaseColorId, finalColor);
            binding.MaterialBlock.SetColor(EmissionColorId, _highlighted ? _highlightColor * 0.35f : Color.black);
            binding.Renderer.SetPropertyBlock(binding.MaterialBlock);
        }

        private sealed class StickerBinding
        {
            public StickerBinding(CubeFace face, CubeFace colorFace, MeshRenderer renderer)
            {
                Face = face;
                ColorFace = colorFace;
                Renderer = renderer;
            }

            public CubeFace Face { get; }
            public CubeFace ColorFace { get; }
            public MeshRenderer Renderer { get; }
            public MaterialPropertyBlock? MaterialBlock { get; set; }
            public Color BaseColor { get; set; }
        }
    }
}
