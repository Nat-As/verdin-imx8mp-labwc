// Matrices (column-major, as OpenGL expects), meshes and the cyclohexane
// molecule.
using System;
using System.Collections.Generic;

namespace GpuTest
{
    internal struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
        public Vec3 Normalized => this * (1f / Length);
        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    internal sealed class Mat4
    {
        public readonly float[] M = new float[16];   // column-major

        public static Mat4 Identity()
        {
            var r = new Mat4();
            r.M[0] = r.M[5] = r.M[10] = r.M[15] = 1;
            return r;
        }

        public static Mat4 operator *(Mat4 a, Mat4 b)
        {
            var r = new Mat4();
            for (int c = 0; c < 4; c++)
                for (int row = 0; row < 4; row++)
                {
                    float s = 0;
                    for (int k = 0; k < 4; k++)
                        s += a.M[k * 4 + row] * b.M[c * 4 + k];
                    r.M[c * 4 + row] = s;
                }
            return r;
        }

        public static Mat4 Perspective(float fovY, float aspect, float near, float far)
        {
            float f = 1f / MathF.Tan(fovY / 2);
            var r = new Mat4();
            r.M[0] = f / aspect;
            r.M[5] = f;
            r.M[10] = (far + near) / (near - far);
            r.M[11] = -1;
            r.M[14] = 2 * far * near / (near - far);
            return r;
        }

        public static Mat4 Translate(float x, float y, float z)
        {
            var r = Identity();
            r.M[12] = x; r.M[13] = y; r.M[14] = z;
            return r;
        }

        public static Mat4 RotateX(float a)
        {
            var r = Identity();
            float c = MathF.Cos(a), s = MathF.Sin(a);
            r.M[5] = c; r.M[6] = s; r.M[9] = -s; r.M[10] = c;
            return r;
        }

        public static Mat4 RotateY(float a)
        {
            var r = Identity();
            float c = MathF.Cos(a), s = MathF.Sin(a);
            r.M[0] = c; r.M[2] = -s; r.M[8] = s; r.M[10] = c;
            return r;
        }

        public static Mat4 Sphere(Vec3 center, float radius)
        {
            var r = Identity();
            r.M[0] = r.M[5] = r.M[10] = radius;
            r.M[12] = center.X; r.M[13] = center.Y; r.M[14] = center.Z;
            return r;
        }

        // Maps the unit cylinder (radius 1 around z, z from 0 to 1) onto a
        // bond from a to b with the given radius
        public static Mat4 Bond(Vec3 a, Vec3 b, float radius)
        {
            Vec3 d = b - a;
            float len = d.Length;
            Vec3 w = d * (1f / len);
            Vec3 helper = MathF.Abs(w.Y) < 0.9f ? new Vec3(0, 1, 0) : new Vec3(1, 0, 0);
            Vec3 u = Vec3.Cross(helper, w).Normalized;
            Vec3 v = Vec3.Cross(w, u);
            var r = Identity();
            r.M[0] = u.X * radius; r.M[1] = u.Y * radius; r.M[2] = u.Z * radius;
            r.M[4] = v.X * radius; r.M[5] = v.Y * radius; r.M[6] = v.Z * radius;
            r.M[8] = w.X * len; r.M[9] = w.Y * len; r.M[10] = w.Z * len;
            r.M[12] = a.X; r.M[13] = a.Y; r.M[14] = a.Z;
            return r;
        }
    }

    // Interleaved position + normal vertices and 16-bit indices
    internal sealed class MeshData
    {
        public readonly List<float> Vertices = new List<float>();
        public readonly List<ushort> Indices = new List<ushort>();

        public static MeshData UvSphere(int stacks, int slices)
        {
            var m = new MeshData();
            for (int i = 0; i <= stacks; i++)
            {
                float phi = MathF.PI * i / stacks;
                for (int j = 0; j <= slices; j++)
                {
                    float theta = 2 * MathF.PI * j / slices;
                    float x = MathF.Sin(phi) * MathF.Cos(theta), y = MathF.Cos(phi), z = MathF.Sin(phi) * MathF.Sin(theta);
                    m.Vertices.AddRange(new[] { x, y, z, x, y, z });
                }
            }
            for (int i = 0; i < stacks; i++)
                for (int j = 0; j < slices; j++)
                {
                    int a = i * (slices + 1) + j, b = a + slices + 1;
                    // Counter-clockwise seen from outside (front faces)
                    m.Indices.AddRange(new[] { (ushort)a, (ushort)(a + 1), (ushort)b, (ushort)(a + 1), (ushort)(b + 1), (ushort)b });
                }
            return m;
        }

        public static MeshData Cylinder(int slices)
        {
            var m = new MeshData();
            for (int j = 0; j <= slices; j++)
            {
                float theta = 2 * MathF.PI * j / slices;
                float x = MathF.Cos(theta), y = MathF.Sin(theta);
                m.Vertices.AddRange(new[] { x, y, 0f, x, y, 0f });
                m.Vertices.AddRange(new[] { x, y, 1f, x, y, 0f });
            }
            for (int j = 0; j < slices; j++)
            {
                int a = 2 * j;
                m.Indices.AddRange(new[] { (ushort)a, (ushort)(a + 2), (ushort)(a + 1), (ushort)(a + 1), (ushort)(a + 2), (ushort)(a + 3) });
            }
            return m;
        }
    }

    // Cyclohexane (C6H12) in the chair conformation, in Angstrom
    internal static class Cyclohexane
    {
        public const float CC = 1.53f, CH = 1.09f, RingPucker = 0.25f;

        public static (List<Vec3> carbons, List<Vec3> hydrogens, List<(int c, Vec3 h)> chBonds) Build()
        {
            float r = MathF.Sqrt(CC * CC - 4 * RingPucker * RingPucker);   // ring radius
            var carbons = new List<Vec3>();
            for (int k = 0; k < 6; k++)
            {
                float a = k * MathF.PI / 3;
                float up = (k % 2 == 0) ? RingPucker : -RingPucker;
                carbons.Add(new Vec3(r * MathF.Cos(a), up, r * MathF.Sin(a)));
            }
            var hydrogens = new List<Vec3>();
            var bonds = new List<(int, Vec3)>();
            for (int k = 0; k < 6; k++)
            {
                Vec3 c = carbons[k];
                float sign = c.Y > 0 ? 1 : -1;
                // Axial: parallel to the ring axis, on the side of the pucker
                Vec3 axial = c + new Vec3(0, sign * CH, 0);
                // Equatorial: outwards, tilted slightly to the other side
                Vec3 outward = new Vec3(c.X, 0, c.Z).Normalized;
                Vec3 eqDir = (outward + new Vec3(0, -sign * 0.34f, 0)).Normalized;
                Vec3 equatorial = c + eqDir * CH;
                hydrogens.Add(axial);
                hydrogens.Add(equatorial);
                bonds.Add((k, axial));
                bonds.Add((k, equatorial));
            }
            return (carbons, hydrogens, bonds);
        }
    }

    // 5x7 pixel font for the overlay text (upper case, digits, a few signs)
    internal static class PixelFont
    {
        public const int W = 5, H = 7;

        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            [' '] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" },
            ['.'] = new[] { "00000", "00000", "00000", "00000", "00000", "01100", "01100" },
            [':'] = new[] { "00000", "01100", "01100", "00000", "01100", "01100", "00000" },
            ['-'] = new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" },
            ['('] = new[] { "00010", "00100", "01000", "01000", "01000", "00100", "00010" },
            [')'] = new[] { "01000", "00100", "00010", "00010", "00010", "00100", "01000" },
            ['/'] = new[] { "00000", "00001", "00010", "00100", "01000", "10000", "00000" },
            ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
            ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
            ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
            ['3'] = new[] { "11111", "00010", "00100", "00010", "00001", "10001", "01110" },
            ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
            ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
            ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
            ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
            ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
            ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" },
            ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
            ['B'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
            ['C'] = new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" },
            ['D'] = new[] { "11100", "10010", "10001", "10001", "10001", "10010", "11100" },
            ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
            ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
            ['G'] = new[] { "01110", "10001", "10000", "10111", "10001", "10001", "01111" },
            ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
            ['I'] = new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" },
            ['J'] = new[] { "00111", "00010", "00010", "00010", "00010", "10010", "01100" },
            ['K'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
            ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
            ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
            ['N'] = new[] { "10001", "10001", "11001", "10101", "10011", "10001", "10001" },
            ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
            ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
            ['Q'] = new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" },
            ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
            ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
            ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
            ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
            ['V'] = new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
            ['W'] = new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" },
            ['X'] = new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" },
            ['Y'] = new[] { "10001", "10001", "10001", "01010", "00100", "00100", "00100" },
            ['Z'] = new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" },
        };

        // Width in font pixels, with one pixel between characters
        public static int TextWidth(string s) => s.Length == 0 ? 0 : s.Length * (W + 1) - 1;

        // Adds two triangles per lit font pixel; (x, y) is the top-left corner
        // in screen pixels, "scale" screen pixels per font pixel
        public static void AddText(List<float> verts, string text, float x, float y, float scale)
        {
            foreach (char raw in text.ToUpperInvariant())
            {
                if (!Glyphs.TryGetValue(raw, out var rows))
                    rows = Glyphs[' '];
                for (int ry = 0; ry < H; ry++)
                    for (int rx = 0; rx < W; rx++)
                    {
                        if (rows[ry][rx] != '1')
                            continue;
                        float x0 = x + rx * scale, y0 = y + ry * scale, x1 = x0 + scale, y1 = y0 + scale;
                        verts.AddRange(new[] { x0, y0, x1, y0, x0, y1, x0, y1, x1, y0, x1, y1 });
                    }
                x += (W + 1) * scale;
            }
        }
    }
}
