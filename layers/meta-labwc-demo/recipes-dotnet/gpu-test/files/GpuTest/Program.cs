// GPU Test: renders a slowly rotating cyclohexane molecule (C6H12, chair
// conformation, ball-and-stick) with OpenGL ES 2.0 and shows the measured
// frame rate in the top right corner.
//
//   gpu-test [--no-vsync] [--frames N] [--snapshot FILE.ppm] [--headless]
//
// --no-vsync renders as fast as possible instead of at the display refresh
// rate; --frames exits after N frames; --snapshot saves the last frame;
// --headless renders off-screen (EGL pbuffer, 900x560) without a window, to
// measure the GPU without the compositor.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace GpuTest
{
    internal static unsafe class Program
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

        private const string TextVertex = @"
attribute vec2 aPos;
uniform vec2 uScreen;
void main() {
    gl_Position = vec4(aPos.x / uScreen.x * 2.0 - 1.0, 1.0 - aPos.y / uScreen.y * 2.0, 0.0, 1.0);
}";

        private const string TextFragment = @"
precision mediump float;
uniform vec3 uColor;
void main() { gl_FragColor = vec4(uColor, 1.0); }";

        private sealed class Mesh
        {
            public uint Vbo, Ibo;
            public int IndexCount;
        }

        private static int fpsVertCount;

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "access")]
        private static extern int Access(string path, int mode);

        private static int Main(string[] args)
        {
            bool vsync = true, headless = false;
            int maxFrames = 0;
            string snapshot = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--no-vsync": vsync = false; break;
                    case "--frames": maxFrames = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--snapshot": snapshot = args[++i]; break;
                    case "--headless": headless = true; break;
                    default:
                        Console.Error.WriteLine("usage: gpu-test [--no-vsync] [--frames N] [--snapshot FILE.ppm] [--headless]");
                        return 2;
                }
            }
            if (snapshot != null && maxFrames == 0)
                maxFrames = 120;
            if (headless && maxFrames == 0)
                maxFrames = 600;

            // The i.MX GPU driver ends the process if it can't open the GPU
            const string gpu = "/dev/galcore";
            if (File.Exists(gpu) && Access(gpu, 6 /* R_OK | W_OK */) != 0)
            {
                Console.Error.WriteLine($"gpu-test: no access to {gpu} (the GPU): run it as a member of the video group (the desktop user is) or with sudo");
                return 1;
            }

            IntPtr window = IntPtr.Zero, context = IntPtr.Zero, eglDisplay = IntPtr.Zero, eglSurface = IntPtr.Zero;
            if (headless)
            {
                if (!CreateHeadless(900, 560, out eglDisplay, out eglSurface))
                    return 1;
            }
            else if (!CreateWindow(vsync, out window, out context))
                return 1;

            string renderer = Gl.GetString(Gl.RENDERER);
            string version = Gl.GetString(Gl.VERSION);
            Console.WriteLine($"gpu-test: {version} on {renderer} ({Gl.GetString(Gl.VENDOR)})");

            uint meshProgram = BuildProgram(MeshVertex, MeshFragment, "aPos", "aNormal");
            uint textProgram = BuildProgram(TextVertex, TextFragment, "aPos", null);
            int uModel = Gl.GetUniformLocation(meshProgram, "uModel");
            int uViewProj = Gl.GetUniformLocation(meshProgram, "uViewProj");
            int uColor = Gl.GetUniformLocation(meshProgram, "uColor");
            int uLightDir = Gl.GetUniformLocation(meshProgram, "uLightDir");
            int uEye = Gl.GetUniformLocation(meshProgram, "uEye");
            int uTextScreen = Gl.GetUniformLocation(textProgram, "uScreen");
            int uTextColor = Gl.GetUniformLocation(textProgram, "uColor");

            Mesh sphere = Upload(MeshData.UvSphere(24, 36));
            Mesh cylinder = Upload(MeshData.Cylinder(28));
            var (carbons, hydrogens, chBonds) = Cyclohexane.Build();

            Gl.GenBuffers(1, out uint textVbo);
            var textVerts = new List<float>();
            int textVertCount = 0;
            string fpsText = "-- FPS";
            string infoText = "OPENGL ES: " + Sanitize(renderer);
            const string title = "GPU TEST - CYCLOHEXANE C6H12";
            int textW = -1, textH = -1;

            var clock = Stopwatch.StartNew();
            double fpsStart = 0;
            int fpsFrames = 0, frames = 0;
            bool running = true;

            while (running)
            {
                while (!headless && Sdl.PollEvent(out Sdl.Event e) != 0)
                {
                    if (e.Type == Sdl.QUIT ||
                        (e.Type == Sdl.KEYDOWN && (e.KeySym == Sdl.KEY_ESCAPE || e.KeySym == Sdl.KEY_Q)))
                        running = false;
                }

                int w = 900, h = 560;
                if (!headless)
                    Sdl.GL_GetDrawableSize(window, out w, out h);
                if (w <= 0 || h <= 0)
                    continue;
                double t = clock.Elapsed.TotalSeconds;

                // Frame rate, updated twice a second
                fpsFrames++;
                if (t - fpsStart >= 0.5)
                {
                    fpsText = (fpsFrames / (t - fpsStart)).ToString("0.0", CultureInfo.InvariantCulture) + " FPS";
                    fpsStart = t;
                    fpsFrames = 0;
                    textW = -1;   // rebuild the text
                }

                Gl.Viewport(0, 0, w, h);
                Gl.ClearColor(0.07f, 0.075f, 0.09f, 1f);
                Gl.Clear(Gl.COLOR_BUFFER_BIT | Gl.DEPTH_BUFFER_BIT);

                // --- Molecule ---
                Gl.Enable(Gl.DEPTH_TEST);
                Gl.DepthFunc(Gl.LEQUAL);
                Gl.Enable(Gl.CULL_FACE);
                Gl.UseProgram(meshProgram);
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
                    DrawMesh(cylinder, uModel, spin * Mat4.Bond(carbons[k], carbons[(k + 1) % 6], 0.13f));
                foreach (var (c, hPos) in chBonds)
                    DrawMesh(cylinder, uModel, spin * Mat4.Bond(carbons[c], hPos, 0.09f));

                Gl.Uniform3f(uColor, 0.28f, 0.29f, 0.32f);   // carbon
                foreach (var c in carbons)
                    DrawMesh(sphere, uModel, spin * Mat4.Sphere(c, 0.42f));
                Gl.Uniform3f(uColor, 0.95f, 0.95f, 0.97f);   // hydrogen
                foreach (var hPos in hydrogens)
                    DrawMesh(sphere, uModel, spin * Mat4.Sphere(hPos, 0.26f));

                // --- Overlay text ---
                Gl.Disable(Gl.DEPTH_TEST);
                Gl.Disable(Gl.CULL_FACE);
                Gl.UseProgram(textProgram);
                Gl.Uniform2f(uTextScreen, w, h);
                float scale = Math.Max(2, h / 220);
                float margin = 4 * scale;
                if (textW != w || textH != h)
                {
                    textVerts.Clear();
                    PixelFont.AddText(textVerts, fpsText, w - margin - PixelFont.TextWidth(fpsText) * scale, margin, scale);
                    int fpsFloats = textVerts.Count;
                    PixelFont.AddText(textVerts, title, margin, margin, scale);
                    PixelFont.AddText(textVerts, infoText, margin, h - margin - PixelFont.H * scale, scale);
                    textVertCount = textVerts.Count / 2;
                    fpsVertCount = fpsFloats / 2;
                    float[] data = textVerts.ToArray();
                    Gl.BindBuffer(Gl.ARRAY_BUFFER, textVbo);
                    fixed (float* p = data)
                        Gl.BufferData(Gl.ARRAY_BUFFER, (IntPtr)(data.Length * 4), p, Gl.DYNAMIC_DRAW);
                    textW = w;
                    textH = h;
                }
                Gl.BindBuffer(Gl.ARRAY_BUFFER, textVbo);
                Gl.EnableVertexAttribArray(0);
                Gl.VertexAttribPointer(0, 2, Gl.FLOAT, 0, 8, IntPtr.Zero);
                Gl.Uniform3f(uTextColor, 0.45f, 1.0f, 0.55f);          // frame rate
                Gl.DrawArrays(Gl.TRIANGLES, 0, fpsVertCount);
                Gl.Uniform3f(uTextColor, 0.82f, 0.84f, 0.88f);         // title, renderer
                Gl.DrawArrays(Gl.TRIANGLES, fpsVertCount, textVertCount - fpsVertCount);
                Gl.DisableVertexAttribArray(0);

                frames++;
                if (snapshot != null && frames == maxFrames)
                    SaveSnapshot(snapshot, w, h);
                if (headless)
                    Egl.SwapBuffers(eglDisplay, eglSurface);
                else
                    Sdl.GL_SwapWindow(window);
                if (maxFrames > 0 && frames >= maxFrames)
                    running = false;
            }

            if (headless)
            {
                Gl.Finish();
                double avg = frames / clock.Elapsed.TotalSeconds;
                Console.WriteLine("gpu-test: " + frames + " frames, average " +
                    avg.ToString("0.0", CultureInfo.InvariantCulture) + " FPS (headless, no vsync)");
            }
            else
            {
                Sdl.GL_DeleteContext(context);
                Sdl.DestroyWindow(window);
                Sdl.Quit();
            }
            return 0;
        }

        private static bool CreateWindow(bool vsync, out IntPtr window, out IntPtr context)
        {
            window = IntPtr.Zero;
            context = IntPtr.Zero;
            // Window class / Wayland app_id, matching gpu-test.desktop
            Sdl.SetHint("SDL_VIDEO_WAYLAND_WMCLASS", "gpu-test");
            Sdl.SetHint("SDL_VIDEO_X11_WMCLASS", "gpu-test");
            if (Sdl.Init(Sdl.INIT_VIDEO) != 0)
            {
                Console.Error.WriteLine("gpu-test: SDL_Init failed: " + Sdl.GetError());
                return false;
            }

            Sdl.GL_SetAttribute(Sdl.GL_CONTEXT_PROFILE_MASK, Sdl.GL_CONTEXT_PROFILE_ES);
            Sdl.GL_SetAttribute(Sdl.GL_CONTEXT_MAJOR_VERSION, 2);
            Sdl.GL_SetAttribute(Sdl.GL_CONTEXT_MINOR_VERSION, 0);
            Sdl.GL_SetAttribute(Sdl.GL_DOUBLEBUFFER, 1);
            Sdl.GL_SetAttribute(Sdl.GL_DEPTH_SIZE, 24);
            // 4x multisampling for smooth edges; retried without if unavailable
            Sdl.GL_SetAttribute(Sdl.GL_MULTISAMPLEBUFFERS, 1);
            Sdl.GL_SetAttribute(Sdl.GL_MULTISAMPLESAMPLES, 4);

            uint flags = Sdl.WINDOW_OPENGL | Sdl.WINDOW_SHOWN | Sdl.WINDOW_RESIZABLE;
            window = Sdl.CreateWindow("GPU Test", Sdl.WINDOWPOS_CENTERED, Sdl.WINDOWPOS_CENTERED, 900, 560, flags);
            context = window != IntPtr.Zero ? Sdl.GL_CreateContext(window) : IntPtr.Zero;
            if (context == IntPtr.Zero)
            {
                if (window != IntPtr.Zero)
                    Sdl.DestroyWindow(window);
                Sdl.GL_SetAttribute(Sdl.GL_MULTISAMPLEBUFFERS, 0);
                Sdl.GL_SetAttribute(Sdl.GL_MULTISAMPLESAMPLES, 0);
                window = Sdl.CreateWindow("GPU Test", Sdl.WINDOWPOS_CENTERED, Sdl.WINDOWPOS_CENTERED, 900, 560, flags);
                context = window != IntPtr.Zero ? Sdl.GL_CreateContext(window) : IntPtr.Zero;
            }
            if (context == IntPtr.Zero)
            {
                Console.Error.WriteLine("gpu-test: could not create an OpenGL ES 2.0 context: " + Sdl.GetError());
                Sdl.Quit();
                return false;
            }
            Sdl.GL_SetSwapInterval(vsync ? 1 : 0);
            return true;
        }

        // --headless: EGL pbuffer on the default display, no window system
        private static bool CreateHeadless(int w, int h, out IntPtr display, out IntPtr surface)
        {
            surface = IntPtr.Zero;
            display = Egl.GetDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero || Egl.Initialize(display, out _, out _) == 0)
            {
                Console.Error.WriteLine("gpu-test: EGL initialization failed: 0x" + Egl.GetError().ToString("x"));
                return false;
            }
            Egl.BindAPI(Egl.OPENGL_ES_API);
            int[] cfgAttribs = { Egl.SURFACE_TYPE, Egl.PBUFFER_BIT, Egl.RENDERABLE_TYPE, Egl.OPENGL_ES2_BIT,
                Egl.RED_SIZE, 8, Egl.GREEN_SIZE, 8, Egl.BLUE_SIZE, 8, Egl.DEPTH_SIZE, 24, Egl.NONE };
            if (Egl.ChooseConfig(display, cfgAttribs, out IntPtr config, 1, out int count) == 0 || count == 0)
            {
                Console.Error.WriteLine("gpu-test: no EGL pbuffer configuration");
                return false;
            }
            surface = Egl.CreatePbufferSurface(display, config, new[] { Egl.WIDTH, w, Egl.HEIGHT, h, Egl.NONE });
            IntPtr ctx = Egl.CreateContext(display, config, IntPtr.Zero, new[] { Egl.CONTEXT_CLIENT_VERSION, 2, Egl.NONE });
            if (surface == IntPtr.Zero || ctx == IntPtr.Zero || Egl.MakeCurrent(display, surface, surface, ctx) == 0)
            {
                Console.Error.WriteLine("gpu-test: EGL pbuffer setup failed: 0x" + Egl.GetError().ToString("x"));
                return false;
            }
            return true;
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.ToUpperInvariant())
                sb.Append((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || " .:-()/".IndexOf(c) >= 0 ? c : ' ');
            return sb.ToString();
        }

        private static void SetMatrix(int location, Mat4 m)
        {
            fixed (float* p = m.M)
                Gl.UniformMatrix4fv(location, 1, 0, p);
        }

        private static void DrawMesh(Mesh mesh, int uModel, Mat4 model)
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
            return mesh;
        }

        private static uint BuildProgram(string vs, string fs, string attr0, string attr1)
        {
            uint program = Gl.CreateProgram();
            Gl.AttachShader(program, Compile(Gl.VERTEX_SHADER, vs));
            Gl.AttachShader(program, Compile(Gl.FRAGMENT_SHADER, fs));
            Gl.BindAttribLocation(program, 0, attr0);
            if (attr1 != null)
                Gl.BindAttribLocation(program, 1, attr1);
            Gl.LinkProgram(program);
            Gl.GetProgramiv(program, Gl.LINK_STATUS, out int ok);
            if (ok == 0)
            {
                byte* log = stackalloc byte[2048];
                Gl.GetProgramInfoLog(program, 2048, out int len, log);
                throw new InvalidOperationException("link failed: " + Encoding.ASCII.GetString(log, len));
            }
            return program;
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

        // Binary PPM of the current frame (top row first)
        private static void SaveSnapshot(string path, int w, int h)
        {
            var rgba = new byte[w * h * 4];
            fixed (byte* p = rgba)
                Gl.ReadPixels(0, 0, w, h, Gl.RGBA, Gl.UNSIGNED_BYTE, p);
            using var f = File.Create(path);
            byte[] header = Encoding.ASCII.GetBytes($"P6\n{w} {h}\n255\n");
            f.Write(header, 0, header.Length);
            var row = new byte[w * 3];
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = 0; x < w; x++)
                {
                    row[x * 3] = rgba[(y * w + x) * 4];
                    row[x * 3 + 1] = rgba[(y * w + x) * 4 + 1];
                    row[x * 3 + 2] = rgba[(y * w + x) * 4 + 2];
                }
                f.Write(row, 0, row.Length);
            }
            Console.WriteLine($"gpu-test: saved {w}x{h} frame to {path}");
        }
    }
}
