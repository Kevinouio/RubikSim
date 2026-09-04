using System;

namespace RubikSim.Core
{
    public readonly struct Int3 : IEquatable<Int3>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public Int3(int x, int y, int z) { X = x; Y = y; Z = z; }
        public int Component(char axis) => axis == 'x' ? X : axis == 'y' ? Y : axis == 'z' ? Z : throw new ArgumentException("Unknown axis.");
        public bool Equals(Int3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Int3 other && Equals(other);
        public override int GetHashCode() => ((X + 1) * 3 + Y + 1) * 3 + Z + 1;
        public override string ToString() => "(" + X + "," + Y + "," + Z + ")";
        public static bool operator ==(Int3 a, Int3 b) => a.Equals(b);
        public static bool operator !=(Int3 a, Int3 b) => !a.Equals(b);
        public static Int3 operator -(Int3 a) => new Int3(-a.X, -a.Y, -a.Z);
        public static Int3 Cross(Int3 a, Int3 b) => new Int3(a.Y*b.Z-a.Z*b.Y, a.Z*b.X-a.X*b.Z, a.X*b.Y-a.Y*b.X);
    }
    public readonly struct FaceletGeometry
    {
        public Int3 Position { get; }
        public Int3 Normal { get; }
        public FaceletGeometry(Int3 position, Int3 normal) { Position = position; Normal = normal; }
    }
    public readonly struct Sticker
    {
        public int Index { get; }
        public Int3 Position { get; }
        public Int3 Normal { get; }
        public char Color { get; }
        public Sticker(int index, FaceletGeometry geometry, char color)
        { Index = index; Position = geometry.Position; Normal = geometry.Normal; Color = color; }
    }
    /// <summary>Integer geometry. URFDLB faces, each read left-to-right and top-to-bottom looking from outside.</summary>
    public static class CubeGeometry
    {
        public const string Faces = "URFDLB";
        public static FaceletGeometry GetFacelet(int index)
        {
            if (index < 0 || index >= 54) throw new ArgumentOutOfRangeException(nameof(index));
            int face = index / 9, row = index % 9 / 3, col = index % 3;
            switch (face)
            {
                case 0: return new FaceletGeometry(new Int3(col-1, 1, row-1), new Int3(0,1,0));
                case 1: return new FaceletGeometry(new Int3(1, 1-row, 1-col), new Int3(1,0,0));
                case 2: return new FaceletGeometry(new Int3(col-1, 1-row, 1), new Int3(0,0,1));
                case 3: return new FaceletGeometry(new Int3(col-1, -1, 1-row), new Int3(0,-1,0));
                case 4: return new FaceletGeometry(new Int3(-1, 1-row, col-1), new Int3(-1,0,0));
                default: return new FaceletGeometry(new Int3(1-col, 1-row, -1), new Int3(0,0,-1));
            }
        }
        public static int GetIndex(Int3 position, Int3 normal)
        {
            for (int i = 0; i < 54; i++) { var g = GetFacelet(i); if (g.Position == position && g.Normal == normal) return i; }
            throw new ArgumentException("Position and normal do not describe a 3x3 facelet.");
        }
        /// <summary>Right-handed quarter turns around a positive coordinate axis.</summary>
        public static Int3 Rotate(Int3 value, char axis, int quarterTurns)
        {
            int count = ((quarterTurns % 4) + 4) % 4;
            if (axis != 'x' && axis != 'y' && axis != 'z') throw new ArgumentException("Unknown axis.");
            for (int i = 0; i < count; i++)
                value = axis == 'x' ? new Int3(value.X, -value.Z, value.Y) :
                    axis == 'y' ? new Int3(value.Z, value.Y, -value.X) : new Int3(-value.Y, value.X, value.Z);
            return value;
        }
    }
}
