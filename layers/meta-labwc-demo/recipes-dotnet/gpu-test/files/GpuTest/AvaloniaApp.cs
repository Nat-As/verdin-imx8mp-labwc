// The GPU Test window: Avalonia on its native Wayland backend (Avalonia.Wayland,
// OpenGL ES through EGL, so the whole window is rendered on the GPU). The
// molecule is drawn with OpenGL ES in an OpenGlControlBase, under Avalonia
// text for the title, the frame rate (top right) and the GPU. The window
// background is 40 % opaque: the desktop shows through around the molecule.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Styling;
using Avalonia.Themes.Simple;
using Avalonia.Threading;

namespace GpuTest
{
    internal sealed class Options
    {
        public int MaxFrames;
        // Frame rate limit (GPU_TEST_MAX_FPS, 0 = none): Avalonia's Wayland
        // backend does not wait for the display, so the frames are paced here
        public int MaxFps = 60;
    }

    internal sealed class App : Application
    {
        public static Options Options = new Options();

        public override void Initialize()
        {
            Styles.Add(new SimpleTheme());
            RequestedThemeVariant = ThemeVariant.Dark;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow(desktop, Options);
            base.OnFrameworkInitializationCompleted();
        }
    }

    internal sealed class MainWindow : Window
    {
        private readonly TextBlock fps, info;

        public MainWindow(IClassicDesktopStyleApplicationLifetime lifetime, Options options)
        {
            Title = "GPU Test";
            Width = 900;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            // 40 % opaque background (the old GPU Test background colour)
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = new SolidColorBrush(Color.FromArgb(102, 18, 19, 23));

            var view = new MoleculeView(options);
            var title = Label("GPU Test - Cyclohexane C6H12", HorizontalAlignment.Left, VerticalAlignment.Top,
                Color.FromRgb(209, 214, 224));
            fps = Label("-- FPS", HorizontalAlignment.Right, VerticalAlignment.Top, Color.FromRgb(115, 255, 140));
            info = Label("OpenGL ES: starting", HorizontalAlignment.Left, VerticalAlignment.Bottom,
                Color.FromRgb(209, 214, 224));
            var grid = new Grid();
            grid.Children.Add(view);
            grid.Children.Add(title);
            grid.Children.Add(fps);
            grid.Children.Add(info);
            Content = grid;

            view.FpsChanged += value =>
                fps.Text = value.ToString("0.0", CultureInfo.InvariantCulture) + " FPS";
            view.GlReady += text => info.Text = text;
            view.Finished += code => lifetime.Shutdown(code);
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape || e.Key == Key.Q)
                    Close();
            };

            // No OpenGL frame after 15 s: Avalonia could not give the control
            // an OpenGL ES context (no GPU rendering)
            DispatcherTimer.RunOnce(() =>
            {
                if (view.Frames > 0)
                    return;
                const string msg = "OpenGL ES is not available in this window (no GPU rendering)";
                Console.Error.WriteLine("gpu-test: " + msg);
                info.Text = msg;
                if (options.MaxFrames > 0)
                    lifetime.Shutdown(1);
            }, TimeSpan.FromSeconds(15));
        }

        private static TextBlock Label(string text, HorizontalAlignment h, VerticalAlignment v, Color color) =>
            new TextBlock
            {
                Text = text,
                HorizontalAlignment = h,
                VerticalAlignment = v,
                Margin = new Thickness(12, 8),
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(color),
            };
    }

    internal sealed class MoleculeView : OpenGlControlBase
    {
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
            string text = "OpenGL ES: " + renderer.Renderer;
            Dispatcher.UIThread.Post(() => GlReady?.Invoke(text));
            clock.Start();
            if (options.MaxFps > 0)
            {
                pacer = new Thread(Pace) { IsBackground = true, Name = "frame pacer" };
                pacer.Start();
            }
        }

        // Requests a frame every 1/MaxFps seconds (on a fixed schedule, so the
        // average is exact)
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
