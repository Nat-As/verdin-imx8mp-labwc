// Draws the rotating cyclohexane molecule (C6H12, chair conformation,
// ball-and-stick) with OpenGL ES. Used by the Avalonia window (MoleculeView)
// and by the off-screen benchmark (--headless).
//
// Rendering goes into an own framebuffer with a depth buffer, multisampled
// (4x) when the context supports it (OpenGL ES 3.0), and is then resolved
// with a blit into the target framebuffer. The background stays transparent,
// so that the window's (40 % opaque) background shows through.
using System;
using System.Text;

namespace GpuTest
{
    internal sealed unsafe class MoleculeRenderer : IDisposable
    {
        private const string MeshVertex = @"
attribute vec3 aPos;
attribute vec3 aNormal;
uniform mat4 uModel;
uniform mat4 uViewProj;
varying vec3 vNormal;
varying vec3 vWorld;
void main() {
    vec4 world = uModel * vec4(aPos, 1.0);
    vWorld = world.xyz;
    vNormal = normalize(mat3(uModel[0].xyz, uModel[1].xyz, uModel[2].xyz) * aNormal);
    gl_Position = uViewProj * world;
}";

        private const string MeshFragment = @"
precision mediump float;
uniform vec3 uColor;
uniform vec3 uLightDir;
uniform vec3 uEye;
varying vec3 vNormal;
varying vec3 vWorld;
void main() {
    vec3 n = normalize(vNormal);
    vec3 l = normalize(uLightDir);
    vec3 v = normalize(uEye - vWorld);
    vec3 h = normalize(l + v);
    float diffuse = max(dot(n, l), 0.0);
    float fill = max(dot(n, -l), 0.0) * 0.15;
    float spec = pow(max(dot(n, h), 0.0), 48.0);
    float rim = pow(1.0 - max(dot(n, v), 0.0), 3.0) * 0.25;
    vec3 c = uColor * (0.22 + 0.78 * diffuse + fill) + vec3(spec * 0.6 + rim);
    gl_FragColor = vec4(c, 1.0);
}";

        private sealed class Mesh
        {
            public uint Vbo, Ibo;
            public int IndexCount;
        }

        private readonly uint program;
        private readonly int uModel, uViewProj, uColor, uLightDir, uEye;
        private readonly Mesh sphere, cylinder;
        private readonly System.Collections.Generic.List<Vec3> carbons, hydrogens;
        private readonly System.Collections.Generic.List<(int c, Vec3 h)> chBonds;
        private readonly bool blit;
        private uint fbo, colorRb, depthRb;
        private int fbWidth, fbHeight, samples;

        public string Renderer { get; }
        public string Version { get; }
        public string Vendor { get; }
        public int Samples => samples;

        // Needs a current OpenGL ES context
        public MoleculeRenderer()
        {
            Renderer = Gl.GetString(Gl.RENDERER);
            Version = Gl.GetString(Gl.VERSION);
            Vendor = Gl.GetString(Gl.VENDOR);
            // glBlitFramebuffer and multisampled render buffers: OpenGL ES 3.0
            blit = Version.StartsWith("OpenGL ES 3", StringComparison.Ordinal);
            if (blit)
            {
                Gl.GetIntegerv(Gl.MAX_SAMPLES, out int max);
                samples = Math.Min(4, Math.Max(0, max));
            }

            program = BuildProgram(MeshVertex, MeshFragment);
            uModel = Gl.GetUniformLocation(program, "uModel");
            uViewProj = Gl.GetUniformLocation(program, "uViewProj");
            uColor = Gl.GetUniformLocation(program, "uColor");
            uLightDir = Gl.GetUniformLocation(program, "uLightDir");
            uEye = Gl.GetUniformLocation(program, "uEye");
            sphere = Upload(MeshData.UvSphere(24, 36));
            cylinder = Upload(MeshData.Cylinder(28));
            (carbons, hydrogens, chBonds) = Cyclohexane.Build();
        }

        // Renders frame time t (seconds) into the framebuffer target (w x h pixels)
        public void Draw(uint target, int w, int h, double t)
        {
            if (blit)
            {
                EnsureFramebuffer(w, h);
                Gl.BindFramebuffer(Gl.FRAMEBUFFER, fbo);
            }
            else
                Gl.BindFramebuffer(Gl.FRAMEBUFFER, target);

            Gl.Viewport(0, 0, w, h);
            Gl.ClearColor(0f, 0f, 0f, 0f);
            Gl.Clear(Gl.COLOR_BUFFER_BIT | Gl.DEPTH_BUFFER_BIT);

            Gl.Enable(Gl.DEPTH_TEST);
            Gl.DepthFunc(Gl.LEQUAL);
            Gl.Enable(Gl.CULL_FACE);
            Gl.Disable(Gl.BLEND);
            Gl.UseProgram(program);
            float aspect = (float)w / h;
            var eye = new Vec3(0, 0, 8.5f);
            var proj = Mat4.Perspective(MathF.PI / 4, aspect, 0.5f, 50f);
            var view = Mat4.Translate(-eye.X, -eye.Y, -eye.Z);
            SetMatrix(uViewProj, proj * view);
            Gl.Uniform3f(uLightDir, 0.5f, 0.8f, 0.6f);
            Gl.Uniform3f(uEye, eye.X, eye.Y, eye.Z);
            // Slow spin around the vertical axis, with a gentle tilt
            var spin = Mat4.RotateX(0.45f + 0.15f * MathF.Sin((float)t * 0.3f)) * Mat4.RotateY((float)t * 0.35f);

            Gl.Uniform3f(uColor, 0.70f, 0.72f, 0.76f);
            for (int k = 0; k < 6; k++)
                DrawMesh(cylinder, spin * Mat4.Bond(carbons[k], carbons[(k + 1) % 6], 0.13f));
            foreach (var (c, hPos) in chBonds)
                DrawMesh(cylinder, spin * Mat4.Bond(carbons[c], hPos, 0.09f));

            Gl.Uniform3f(uColor, 0.28f, 0.29f, 0.32f);   // carbon
            foreach (var c in carbons)
                DrawMesh(sphere, spin * Mat4.Sphere(c, 0.42f));
            Gl.Uniform3f(uColor, 0.95f, 0.95f, 0.97f);   // hydrogen
            foreach (var hPos in hydrogens)
                DrawMesh(sphere, spin * Mat4.Sphere(hPos, 0.26f));

            Gl.Disable(Gl.DEPTH_TEST);
            Gl.Disable(Gl.CULL_FACE);
            Gl.UseProgram(0);

            if (blit)
            {
                // Resolve (multisampled) into the target
                Gl.BindFramebuffer(Gl.READ_FRAMEBUFFER, fbo);
                Gl.BindFramebuffer(Gl.DRAW_FRAMEBUFFER, target);
                Gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h, Gl.COLOR_BUFFER_BIT, Gl.NEAREST);
                Gl.BindFramebuffer(Gl.FRAMEBUFFER, target);
            }
        }

        private void EnsureFramebuffer(int w, int h)
        {
            if (fbo != 0 && w == fbWidth && h == fbHeight)
                return;
            DeleteFramebuffer();
            Gl.GenFramebuffers(1, out fbo);
            Gl.BindFramebuffer(Gl.FRAMEBUFFER, fbo);
            Gl.GenRenderbuffers(1, out colorRb);
            Gl.BindRenderbuffer(Gl.RENDERBUFFER, colorRb);
            Gl.RenderbufferStorageMultisample(Gl.RENDERBUFFER, samples, Gl.RGBA8_OES, w, h);
            Gl.FramebufferRenderbuffer(Gl.FRAMEBUFFER, Gl.COLOR_ATTACHMENT0, Gl.RENDERBUFFER, colorRb);
            Gl.GenRenderbuffers(1, out depthRb);
            Gl.BindRenderbuffer(Gl.RENDERBUFFER, depthRb);
            Gl.RenderbufferStorageMultisample(Gl.RENDERBUFFER, samples, Gl.DEPTH_COMPONENT16, w, h);
            Gl.FramebufferRenderbuffer(Gl.FRAMEBUFFER, Gl.DEPTH_ATTACHMENT, Gl.RENDERBUFFER, depthRb);
            Gl.BindRenderbuffer(Gl.RENDERBUFFER, 0);
            if (Gl.CheckFramebufferStatus(Gl.FRAMEBUFFER) != Gl.FRAMEBUFFER_COMPLETE)
                throw new InvalidOperationException($"render framebuffer ({samples}x MSAA) incomplete");
            fbWidth = w;
            fbHeight = h;
        }

        private void DeleteFramebuffer()
        {
            if (fbo != 0)
                Gl.DeleteFramebuffers(1, ref fbo);
            if (colorRb != 0)
                Gl.DeleteRenderbuffers(1, ref colorRb);
            if (depthRb != 0)
                Gl.DeleteRenderbuffers(1, ref depthRb);
            fbo = colorRb = depthRb = 0;
        }

        public void Dispose() => DeleteFramebuffer();

        private static void SetMatrix(int location, Mat4 m)
        {
            fixed (float* p = m.M)
                Gl.UniformMatrix4fv(location, 1, 0, p);
        }

        private void DrawMesh(Mesh mesh, Mat4 model)
        {
            SetMatrix(uModel, model);
            Gl.BindBuffer(Gl.ARRAY_BUFFER, mesh.Vbo);
            Gl.BindBuffer(Gl.ELEMENT_ARRAY_BUFFER, mesh.Ibo);
            Gl.EnableVertexAttribArray(0);
            Gl.EnableVertexAttribArray(1);
            Gl.VertexAttribPointer(0, 3, Gl.FLOAT, 0, 24, IntPtr.Zero);
            Gl.VertexAttribPointer(1, 3, Gl.FLOAT, 0, 24, (IntPtr)12);
            Gl.DrawElements(Gl.TRIANGLES, mesh.IndexCount, Gl.UNSIGNED_SHORT, IntPtr.Zero);
            Gl.DisableVertexAttribArray(1);
            Gl.DisableVertexAttribArray(0);
            Gl.BindBuffer(Gl.ARRAY_BUFFER, 0);
            Gl.BindBuffer(Gl.ELEMENT_ARRAY_BUFFER, 0);
        }

        private static Mesh Upload(MeshData data)
        {
            var mesh = new Mesh { IndexCount = data.Indices.Count };
            float[] v = data.Vertices.ToArray();
            ushort[] i = data.Indices.ToArray();
            Gl.GenBuffers(1, out mesh.Vbo);
            Gl.BindBuffer(Gl.ARRAY_BUFFER, mesh.Vbo);
            fixed (float* p = v)
                Gl.BufferData(Gl.ARRAY_BUFFER, (IntPtr)(v.Length * 4), p, Gl.STATIC_DRAW);
            Gl.GenBuffers(1, out mesh.Ibo);
            Gl.BindBuffer(Gl.ELEMENT_ARRAY_BUFFER, mesh.Ibo);
            fixed (ushort* p = i)
                Gl.BufferData(Gl.ELEMENT_ARRAY_BUFFER, (IntPtr)(i.Length * 2), p, Gl.STATIC_DRAW);
            Gl.BindBuffer(Gl.ARRAY_BUFFER, 0);
            Gl.BindBuffer(Gl.ELEMENT_ARRAY_BUFFER, 0);
            return mesh;
        }

        private static uint BuildProgram(string vs, string fs)
        {
            uint p = Gl.CreateProgram();
            Gl.AttachShader(p, Compile(Gl.VERTEX_SHADER, vs));
            Gl.AttachShader(p, Compile(Gl.FRAGMENT_SHADER, fs));
            Gl.BindAttribLocation(p, 0, "aPos");
            Gl.BindAttribLocation(p, 1, "aNormal");
            Gl.LinkProgram(p);
            Gl.GetProgramiv(p, Gl.LINK_STATUS, out int ok);
            if (ok == 0)
            {
                byte* log = stackalloc byte[2048];
                Gl.GetProgramInfoLog(p, 2048, out int len, log);
                throw new InvalidOperationException("link failed: " + Encoding.ASCII.GetString(log, len));
            }
            return p;
        }

        private static uint Compile(uint type, string source)
        {
            uint shader = Gl.CreateShader(type);
            Gl.ShaderSource(shader, 1, new[] { source }, null);
            Gl.CompileShader(shader);
            Gl.GetShaderiv(shader, Gl.COMPILE_STATUS, out int ok);
            if (ok == 0)
            {
                byte* log = stackalloc byte[2048];
                Gl.GetShaderInfoLog(shader, 2048, out int len, log);
                throw new InvalidOperationException("shader compile failed: " + Encoding.ASCII.GetString(log, len));
            }
            return shader;
        }
    }
}
