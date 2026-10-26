// GPU Test: renders a slowly rotating cyclohexane molecule (C6H12, chair
// conformation, ball-and-stick) with OpenGL ES in an Avalonia window (native
// Wayland backend, GPU rendered, 40 % opaque background) and shows the
// measured frame rate in the top right corner.
//
//   gpu-test [--frames N] [--headless [--snapshot FILE.ppm]]
//
// --frames exits after N frames (and prints the average frame rate);
// --headless renders off-screen (EGL on the GPU render node, 900x560, no
// vsync) without a window, to measure the GPU without the compositor;
// --snapshot saves its last frame.
//
// GPU_TEST_MAX_FPS sets the window's frame rate limit (default 60, 0 = none).
// GPU_TEST_DMABUF=1 makes Avalonia present through GBM/DMA-BUF buffers
// (linux-dmabuf) instead of the EGL Wayland window surface.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.OpenGL;

namespace GpuTest
{
    internal static unsafe class Program
    {
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "access")]
        private static extern int Access(string path, int mode);

        private static int Main(string[] args)
        {
            bool headless = false;
            int maxFrames = 0;
            string snapshot = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--frames": maxFrames = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--snapshot": snapshot = args[++i]; break;
                    case "--headless": headless = true; break;
                    case "--no-vsync": break;   // v0.2.0 option; the window always follows the display
                    default:
                        Console.Error.WriteLine("usage: gpu-test [--frames N] [--headless [--snapshot FILE.ppm]]");
                        return 2;
                }
            }

            // The i.MX GPU driver ends the process if it can't open the GPU
            const string gpu = "/dev/galcore";
            if (File.Exists(gpu) && Access(gpu, 6 /* R_OK | W_OK */) != 0)
            {
                Console.Error.WriteLine($"gpu-test: no access to {gpu} (the GPU): run it as a member of the video group (the desktop user is) or with sudo");
                return 1;
            }

            if (headless)
                return RunHeadless(maxFrames > 0 ? maxFrames : 600, snapshot);

            App.Options.MaxFrames = maxFrames;
            if (int.TryParse(Environment.GetEnvironmentVariable("GPU_TEST_MAX_FPS"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int maxFps) && maxFps >= 0)
                App.Options.MaxFps = maxFps;
            try
            {
                return AppBuilder.Configure<App>()
                    .UseWayland()
                    .UseSkia()
                    .UseHarfBuzz()
                    .With(new WaylandPlatformOptions
                    {
                        AppId = "gpu-test",   // matches gpu-test.desktop
                        UseDmabufSwapchain = Environment.GetEnvironmentVariable("GPU_TEST_DMABUF") == "1",
                        // OpenGL ES (the molecule's shaders are GLSL ES), not
                        // desktop OpenGL where the driver has both
                        GlProfiles = new[]
                        {
                            new GlVersion(GlProfileType.OpenGLES, 3, 0),
                            new GlVersion(GlProfileType.OpenGLES, 2, 0),
                        },
                    })
                    .StartWithClassicDesktopLifetime(Array.Empty<string>());
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("gpu-test: could not open the window (Wayland desktop?): " + e.Message);
                return 1;
            }
        }

        // --headless: no window. EGL on the GBM platform of the GPU render node
        // (as the compositor uses it) with a surfaceless context rendering into
        // a framebuffer object; if that is not available (other drivers), a
        // pbuffer on the default EGL display.
        private static int RunHeadless(int frames, string snapshot)
        {
            const int w = 900, h = 560;
            if (!CreateGbmContext(w, h, out uint target) && !CreatePbuffer(w, h))
                return 1;

            var renderer = new MoleculeRenderer();
            Console.WriteLine($"gpu-test: {renderer.Version} on {renderer.Renderer} ({renderer.Vendor})");
            var clock = Stopwatch.StartNew();
            for (int i = 1; i <= frames; i++)
            {
                renderer.Draw(target, w, h, clock.Elapsed.TotalSeconds);
                if (i == frames && snapshot != null)
                    SaveSnapshot(snapshot, w, h);
                Gl.Flush();
            }
            Gl.Finish();
            double avg = frames / clock.Elapsed.TotalSeconds;
            Console.WriteLine("gpu-test: " + frames + " frames, average " +
                avg.ToString("0.0", CultureInfo.InvariantCulture) + " FPS (headless, no vsync)");
            renderer.Dispose();
            return 0;
        }

        private static bool CreatePbuffer(int w, int h)
        {
            IntPtr display = Egl.GetDisplay(IntPtr.Zero);
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
            IntPtr surface = Egl.CreatePbufferSurface(display, config, new[] { Egl.WIDTH, w, Egl.HEIGHT, h, Egl.NONE });
            IntPtr ctx = Egl.CreateContext(display, config, IntPtr.Zero, new[] { Egl.CONTEXT_CLIENT_VERSION, 3, Egl.NONE });
            if (ctx == IntPtr.Zero)
                ctx = Egl.CreateContext(display, config, IntPtr.Zero, new[] { Egl.CONTEXT_CLIENT_VERSION, 2, Egl.NONE });
            if (surface == IntPtr.Zero || ctx == IntPtr.Zero || Egl.MakeCurrent(display, surface, surface, ctx) == 0)
            {
                Console.Error.WriteLine("gpu-test: EGL pbuffer setup failed: 0x" + Egl.GetError().ToString("x"));
                return false;
            }
            return true;
        }

        private static bool CreateGbmContext(int w, int h, out uint target)
        {
            target = 0;
            string node = null;
            try
            {
                foreach (string n in Directory.GetFiles("/dev/dri", "renderD*"))
                {
                    node = n;
                    break;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (node == null)
                return false;
            int fd = Gbm.Open(node, Gbm.O_RDWR | Gbm.O_CLOEXEC);
            IntPtr gbm = fd >= 0 ? Gbm.CreateDevice(fd) : IntPtr.Zero;
            if (gbm == IntPtr.Zero)
                return false;
            IntPtr display = Egl.GetPlatformDisplay(Egl.PLATFORM_GBM_KHR, gbm, IntPtr.Zero);
            if (display == IntPtr.Zero || Egl.Initialize(display, out _, out _) == 0)
                return false;
            Egl.BindAPI(Egl.OPENGL_ES_API);
            int[] cfgAttribs = { Egl.RENDERABLE_TYPE, Egl.OPENGL_ES2_BIT, Egl.NONE };
            if (Egl.ChooseConfig(display, cfgAttribs, out IntPtr config, 1, out int count) == 0 || count == 0)
                return false;
            IntPtr ctx = Egl.CreateContext(display, config, IntPtr.Zero, new[] { Egl.CONTEXT_CLIENT_VERSION, 3, Egl.NONE });
            if (ctx == IntPtr.Zero)
                ctx = Egl.CreateContext(display, config, IntPtr.Zero, new[] { Egl.CONTEXT_CLIENT_VERSION, 2, Egl.NONE });
            if (ctx == IntPtr.Zero || Egl.MakeCurrent(display, IntPtr.Zero, IntPtr.Zero, ctx) == 0)
                return false;

            Gl.GenFramebuffers(1, out target);
            Gl.BindFramebuffer(Gl.FRAMEBUFFER, target);
            Gl.GenRenderbuffers(1, out uint color);
            Gl.BindRenderbuffer(Gl.RENDERBUFFER, color);
            Gl.RenderbufferStorage(Gl.RENDERBUFFER, Gl.RGBA8_OES, w, h);
            Gl.FramebufferRenderbuffer(Gl.FRAMEBUFFER, Gl.COLOR_ATTACHMENT0, Gl.RENDERBUFFER, color);
            Gl.GenRenderbuffers(1, out uint depth);
            Gl.BindRenderbuffer(Gl.RENDERBUFFER, depth);
            Gl.RenderbufferStorage(Gl.RENDERBUFFER, Gl.DEPTH_COMPONENT16, w, h);
            Gl.FramebufferRenderbuffer(Gl.FRAMEBUFFER, Gl.DEPTH_ATTACHMENT, Gl.RENDERBUFFER, depth);
            if (Gl.CheckFramebufferStatus(Gl.FRAMEBUFFER) != Gl.FRAMEBUFFER_COMPLETE)
            {
                Console.Error.WriteLine("gpu-test: off-screen framebuffer incomplete");
                return false;
            }
            Console.WriteLine($"gpu-test: off-screen rendering on {node} (EGL GBM platform)");
            return true;
        }

        // Binary PPM of the current frame (top row first); the transparent
        // background is drawn in the window's colour
        private static void SaveSnapshot(string path, int w, int h)
        {
            var rgba = new byte[w * h * 4];
            fixed (byte* p = rgba)
                Gl.ReadPixels(0, 0, w, h, Gl.RGBA, Gl.UNSIGNED_BYTE, p);
            using var f = File.Create(path);
            byte[] header = Encoding.ASCII.GetBytes($"P6\n{w} {h}\n255\n");
            f.Write(header, 0, header.Length);
            var row = new byte[w * 3];
            byte[] bg = { 18, 19, 23 };
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = 0; x < w; x++)
                {
                    int a = rgba[(y * w + x) * 4 + 3];
                    for (int c = 0; c < 3; c++)
                        row[x * 3 + c] = (byte)Math.Min(255, rgba[(y * w + x) * 4 + c] + bg[c] * (255 - a) / 255);
                }
                f.Write(row, 0, row.Length);
            }
            Console.WriteLine($"gpu-test: saved {w}x{h} frame to {path}");
        }
    }
}
