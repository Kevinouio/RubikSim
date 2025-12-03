using System;
using System.Collections.Generic;
using UnityEngine;

namespace RubikSim.Core
{
    public class CubeBuilder : MonoBehaviour
    {
        public event Action<IReadOnlyDictionary<string, Cubelet>>? CubeBuilt;

        [SerializeField] private CubeColorPalette palette = CubeColorPalette.CreateDefault();
        [SerializeField] private CubeletVisualConfig visualConfig = CubeletVisualConfig.Default;
        [SerializeField] private Material? stickerMaterial;

        private readonly Dictionary<string, Cubelet> _cubelets = new();
        private Material? _sharedStickerMaterial;

        public IReadOnlyDictionary<string, Cubelet> Cubelets => _cubelets;
        public CubeletVisualConfig VisualConfig => visualConfig;
        public CubeColorPalette Palette => palette;

        public void BuildCube(CubeState state)
        {
            ClearCube();
            EnsureMaterial();

            foreach (var piece in state.EnumeratePieces())
            {
                var cubelet = Cubelet.Create(piece, palette, transform, visualConfig, _sharedStickerMaterial!);
                _cubelets[piece.Id] = cubelet;
            }

            CubeBuilt?.Invoke(_cubelets);
        }

        public bool TryGetCubelet(string id, out Cubelet cubelet)
        {
            return _cubelets.TryGetValue(id, out cubelet!);
        }

        public void ClearCube()
        {
            foreach (var cubelet in _cubelets.Values)
            {
                if (cubelet != null)
                {
                    Destroy(cubelet.gameObject);
                }
            }

            _cubelets.Clear();
        }

        public void ApplyPalette(CubeColorPalette newPalette)
        {
            palette = newPalette ?? CubeColorPalette.CreateDefault();
            foreach (var cubelet in _cubelets.Values)
            {
                cubelet.ApplyPalette(palette);
            }
        }

        private void EnsureMaterial()
        {
            if (_sharedStickerMaterial != null)
            {
                return;
            }

            if (stickerMaterial != null)
            {
                _sharedStickerMaterial = new Material(stickerMaterial);
            }
            else
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
                _sharedStickerMaterial = new Material(shader) { name = "StickerSharedMaterial" };
            }

            _sharedStickerMaterial.enableInstancing = true;
        }

        private void OnDestroy()
        {
            if (_sharedStickerMaterial != null)
            {
                Destroy(_sharedStickerMaterial);
                _sharedStickerMaterial = null;
            }
        }
    }
}
