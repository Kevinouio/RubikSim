using System.Collections.Generic;
using UnityEngine;

namespace RubikSim.UnityView
{
    /// <summary>Original line glyphs avoid font downloads and make every color identifiable in monochrome.</summary>
    internal static class LetterMesh
    {
        public static Mesh Create(char letter)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Stroke(params Vector2[] points)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    var a = points[i - 1]; var b = points[i]; var delta = (b - a).normalized;
                    var side = new Vector2(-delta.y, delta.x) * 0.055f;
                    int first = vertices.Count;
                    vertices.Add(a - side); vertices.Add(a + side); vertices.Add(b + side); vertices.Add(b - side);
                    triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                    triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
                }
            }
            Vector2 P(float x, float y) => new Vector2(x, y);
            switch (letter)
            {
                case 'U': Stroke(P(-.35f,.5f),P(-.35f,-.25f),P(-.2f,-.5f),P(.2f,-.5f),P(.35f,-.25f),P(.35f,.5f)); break;
                case 'R': Stroke(P(-.35f,-.5f),P(-.35f,.5f),P(.15f,.5f),P(.35f,.3f),P(.35f,.1f),P(.15f,0),P(-.35f,0)); Stroke(P(0,0),P(.4f,-.5f)); break;
                case 'F': Stroke(P(-.35f,-.5f),P(-.35f,.5f),P(.35f,.5f)); Stroke(P(-.35f,0),P(.2f,0)); break;
                case 'D': Stroke(P(-.35f,-.5f),P(-.35f,.5f),P(.1f,.5f),P(.35f,.25f),P(.35f,-.25f),P(.1f,-.5f),P(-.35f,-.5f)); break;
                case 'L': Stroke(P(-.35f,.5f),P(-.35f,-.5f),P(.35f,-.5f)); break;
                case 'B': Stroke(P(-.35f,-.5f),P(-.35f,.5f),P(.1f,.5f),P(.35f,.3f),P(.35f,.15f),P(.1f,0),P(-.35f,0)); Stroke(P(.1f,0),P(.35f,-.15f),P(.35f,-.3f),P(.1f,-.5f),P(-.35f,-.5f)); break;
            }
            var mesh = new Mesh { name = "Color label " + letter };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
