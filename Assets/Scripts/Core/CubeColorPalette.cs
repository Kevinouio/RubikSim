using System;
using UnityEngine;

namespace RubikSim.Core
{
    [Serializable]
    public class CubeColorPalette
    {
        [SerializeField] private Color up = new(0.98f, 0.98f, 0.98f);
        [SerializeField] private Color right = new(0.83f, 0.14f, 0.14f);
        [SerializeField] private Color front = new(0.05f, 0.52f, 0.17f);
        [SerializeField] private Color down = new(0.98f, 0.82f, 0.11f);
        [SerializeField] private Color left = new(0.96f, 0.45f, 0.09f);
        [SerializeField] private Color back = new(0.05f, 0.25f, 0.78f);

        public Color GetColor(CubeFace face)
        {
            return face switch
            {
                CubeFace.Up => up,
                CubeFace.Right => right,
                CubeFace.Front => front,
                CubeFace.Down => down,
                CubeFace.Left => left,
                CubeFace.Back => back,
                _ => Color.white
            };
        }

        public static CubeColorPalette CreateDefault()
        {
            return new CubeColorPalette();
        }
    }
}
