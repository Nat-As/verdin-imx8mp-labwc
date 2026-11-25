// The molecule, drawn with OpenGL ES in an Avalonia OpenGlControlBase. Its GL
// resources live from OnOpenGlInit to OnOpenGlDeinit (the control is in the
// window); the Home page creates a new view each time it is shown.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;

namespace GpuTest
{
    internal sealed class MoleculeView : OpenGlControlBase
    {
        // Renderer of the last view, for the build info page
        public static string GlDescription { get; private set; }

        private readonly Options options;
        private readonly Stopwatch clock = new Stopwatch();
        private MoleculeRenderer renderer;
        private double fpsStart;
        private int fpsFrames;
        private volatile bool finished;
        private Thread pacer;

        public int Frames { get; private set; }
        public event Action<double> FpsChanged;
        public event Action<string> GlReady;
        public event Action<int> Finished;

        public MoleculeView(Options options)
        {
            this.options = options;
        }

        protected override void OnOpenGlInit(GlInterface gl)
        {
            try
            {
                renderer = new MoleculeRenderer();
            }
            catch (Exception e)
            {
                Fail("OpenGL ES setup failed: " + e.Message);
                return;
            }
            string msaa = renderer.Samples > 0 ? $", {renderer.Samples}x MSAA" : "";
            string limit = options.MaxFps > 0 ? $", up to {options.MaxFps} FPS" : ", no frame rate limit";
            Console.WriteLine($"gpu-test: {renderer.Version} on {renderer.Renderer} ({renderer.Vendor})");
            Console.WriteLine($"gpu-test: Avalonia {typeof(Application).Assembly.GetName().Version} window, Wayland backend{msaa}{limit}");
            GlDescription = $"{renderer.Version} on {renderer.Renderer} ({renderer.Vendor})";
            string text = "OpenGL ES: " + renderer.Renderer;
            Dispatcher.UIThread.Post(() => GlReady?.Invoke(text));
            finished = false;
            clock.Restart();
            fpsStart = 0;
            fpsFrames = 0;
            if (options.MaxFps > 0)
            {
                pacer = new Thread(Pace) { IsBackground = true, Name = "frame pacer" };
                pacer.Start();
            }
        }

        // Requests a frame every 1/MaxFps seconds (on a fixed schedule, so the
        // average is exact) while the view is in the window
        private void Pace()
        {
            var period = TimeSpan.FromSeconds(1.0 / options.MaxFps);
            var watch = Stopwatch.StartNew();
            TimeSpan next = period;
            while (!finished)
            {
                TimeSpan wait = next - watch.Elapsed;
                if (wait > TimeSpan.Zero)
                    Thread.Sleep(wait);
                else if (-wait > period)
                    next = watch.Elapsed;   // fell behind (slow frames): don't catch up
                next += period;
                Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Render);
            }
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            finished = true;
            renderer?.Dispose();
            renderer = null;
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (renderer == null || finished)
                return;
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            int w = Math.Max(1, (int)(Bounds.Width * scale));
            int h = Math.Max(1, (int)(Bounds.Height * scale));
            double t = clock.Elapsed.TotalSeconds;
            try
            {
                renderer.Draw((uint)fb, w, h, t);
            }
            catch (Exception e)
            {
                Fail(e.Message);
                return;
            }

            Frames++;
            fpsFrames++;
            if (t - fpsStart >= 0.5)
            {
                double value = fpsFrames / (t - fpsStart);
                fpsStart = t;
                fpsFrames = 0;
                Dispatcher.UIThread.Post(() => FpsChanged?.Invoke(value));
            }
            if (options.MaxFrames > 0 && Frames >= options.MaxFrames)
            {
                finished = true;
                Console.WriteLine("gpu-test: " + Frames + " frames, average " +
                    (Frames / t).ToString("0.0", CultureInfo.InvariantCulture) + " FPS (window)");
                Dispatcher.UIThread.Post(() => Finished?.Invoke(0));
                return;
            }
            if (options.MaxFps == 0)
                RequestNextFrameRendering();
        }

        private void Fail(string message)
        {
            finished = true;
            Console.Error.WriteLine("gpu-test: " + message);
            Dispatcher.UIThread.Post(() =>
            {
                GlReady?.Invoke(message);
                if (options.MaxFrames > 0)
                    Finished?.Invoke(1);
            });
        }
    }
}
