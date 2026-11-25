// Home: the rotating molecule (OpenGL ES) with the title, the frame rate
// (top right) and the GPU. A new MoleculeView is created each time the page is
// shown (Start), and removed when it is left (Stop): that frees the GL
// resources and stops the frame pacer.
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using GpuTest.Ui;

namespace GpuTest.Pages
{
    internal sealed class HomePage : Grid
    {
        private readonly Options options;
        private readonly TextBlock fps, info;
        private MoleculeView view;
        private bool checkedGl;

        public HomePage(Options options)
        {
            this.options = options;
            var title = Label("GPU Test - Cyclohexane C6H12", HorizontalAlignment.Left, VerticalAlignment.Top,
                Color.FromRgb(209, 214, 224));
            fps = Label("-- FPS", HorizontalAlignment.Right, VerticalAlignment.Top, Color.FromRgb(115, 255, 140));
            info = Label("OpenGL ES: starting", HorizontalAlignment.Left, VerticalAlignment.Bottom,
                Color.FromRgb(209, 214, 224));
            Children.Add(title);
            Children.Add(fps);
            Children.Add(info);
        }

        // Called by the window when the page is shown
        public void Start()
        {
            if (view != null)
                return;
            view = new MoleculeView(options);
            view.FpsChanged += value =>
                fps.Text = value.ToString("0.0", CultureInfo.InvariantCulture) + " FPS";
            view.GlReady += text => info.Text = text;
            view.Finished += code => MainWindow.Current?.Shutdown(code);
            Children.Insert(0, view);
            fps.Text = "-- FPS";
            if (checkedGl)
                return;
            checkedGl = true;
            // No OpenGL frame after 15 s: Avalonia could not give the control
            // an OpenGL ES context (no GPU rendering)
            MoleculeView first = view;
            DispatcherTimer.RunOnce(() =>
            {
                if (first.Frames > 0 || first.Parent == null)
                    return;
                const string msg = "OpenGL ES is not available in this window (no GPU rendering)";
                Console.Error.WriteLine("gpu-test: " + msg);
                info.Text = msg;
                if (options.MaxFrames > 0)
                    MainWindow.Current?.Shutdown(1);
            }, TimeSpan.FromSeconds(15));
        }

        // Called before the page is replaced: removing the view runs its GL
        // deinit and stops its frame pacer
        public void Stop()
        {
            if (view == null)
                return;
            Children.Remove(view);
            view = null;
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

    // Pages of the navigation bar that have no function yet
    internal sealed class PlaceholderPage : Border
    {
        public PlaceholderPage(string title, string text)
        {
            Background = AppTheme.Brush(AppTheme.Background);
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 6,
            };
            var t = AppTheme.Title(title);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            var l = AppTheme.Label(text, AppTheme.Dim);
            l.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(t);
            stack.Children.Add(l);
            Child = stack;
        }
    }
}
