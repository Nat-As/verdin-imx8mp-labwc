// The application window: Avalonia on its native Wayland backend
// (Avalonia.Wayland, OpenGL ES through EGL, so the whole window is rendered on
// the GPU). Pages above a navigation bar that is the same on every page
// (design rule: Home, Menu, Book, Settings); an on-screen keyboard and
// dialogs are drawn in the window. Home is the rotating molecule (OpenGL ES),
// Settings the system settings. In the desktop session the window background
// is 40 % opaque; with --kiosk the window fills the screen.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using GpuTest.Pages;
using GpuTest.Ui;

namespace GpuTest
{
    internal sealed class Options
    {
        public int MaxFrames;
        // Frame rate limit (GPU_TEST_MAX_FPS, 0 = none): Avalonia's Wayland
        // backend does not wait for the display, so the frames are paced here
        public int MaxFps = 60;
        // Full screen without decorations, for the kiosk session (--kiosk)
        public bool Kiosk;
    }

    internal sealed class App : Application
    {
        public static Options Options = new Options();

        public override void Initialize()
        {
            Styles.Add(AppTheme.Fluent());
            Styles.Add(AppTheme.Styles());
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
        public static MainWindow Current { get; private set; }

        private readonly Options options;
        private readonly Border pageHost = new Border();
        private readonly NavBar nav = new NavBar();
        private readonly Dictionary<NavItem, Control> pages = new Dictionary<NavItem, Control>();
        private readonly IClassicDesktopStyleApplicationLifetime lifetime;

        public TouchKeyboard Keyboard { get; } = new TouchKeyboard();
        public DialogLayer Dialogs { get; } = new DialogLayer();
        public NavItem Page { get; private set; }

        public MainWindow(IClassicDesktopStyleApplicationLifetime lifetime, Options options)
        {
            Current = this;
            this.lifetime = lifetime;
            this.options = options;
            Title = "GPU Test";
            FontSize = AppTheme.FontSize;
            if (options.Kiosk)
            {
                WindowDecorations = WindowDecorations.None;
                WindowState = WindowState.FullScreen;
                Background = AppTheme.Brush(AppTheme.Background);
                Opened += (_, _) => MoveToKioskOutput();
            }
            else
            {
                Width = 900;
                Height = 560;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                // 40 % opaque background (the old GPU Test background colour)
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
                Background = new SolidColorBrush(Color.FromArgb(102, 18, 19, 23));
            }

            var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto") };
            root.Children.Add(pageHost);
            Grid.SetRow(Keyboard, 1);
            root.Children.Add(Keyboard);
            Grid.SetRow(nav, 2);
            root.Children.Add(nav);
            Grid.SetRowSpan(Dialogs, 3);
            root.Children.Add(Dialogs);
            Content = root;

            nav.Selected += Navigate;
            Navigate(NavItem.Home);

            // The keyboard follows the focused text box
            AddHandler(GotFocusEvent, (_, e) =>
            {
                if (TextBoxOf(e.Source) is TextBox box)
                    Keyboard.Show(box);
            }, RoutingStrategies.Bubble);
            // Tapping a text box that already has the focus brings the keyboard back
            AddHandler(PointerReleasedEvent, (_, e) =>
            {
                if (TextBoxOf(e.Source) is TextBox box && box.IsFocused)
                    Keyboard.Show(box);
            }, RoutingStrategies.Tunnel, handledEventsToo: true);
            KeyDown += (_, e) =>
            {
                if (!options.Kiosk && (e.Key == Key.Escape || e.Key == Key.Q) && TextBoxOf(e.Source) == null)
                    Close();
            };
        }

        public void Shutdown(int code) => lifetime.Shutdown(code);

        public void Navigate(NavItem item)
        {
            Keyboard.Close();
            if (!pages.TryGetValue(item, out Control page))
            {
                page = item switch
                {
                    NavItem.Home => new HomePage(options),
                    NavItem.Settings => new SettingsPage(),
                    NavItem.Menu => new PlaceholderPage("Menu", "This page is not used yet."),
                    _ => new PlaceholderPage("Library", "This page is not used yet."),
                };
                pages[item] = page;
            }
            if (pageHost.Child is HomePage oldHome && page != oldHome)
                oldHome.Stop();
            else if (pageHost.Child is SettingsPage oldSettings && page != oldSettings)
                oldSettings.Left();
            Page = item;
            pageHost.Child = page;
            if (page is HomePage home)
                home.Start();
            else if (page is SettingsPage settings)
                settings.Shown();
            nav.SetActive(item);
        }

        private static TextBox TextBoxOf(object source) =>
            source is Visual v ? v as TextBox ?? v.FindAncestorOfType<TextBox>() : null;

        // ARGOS_KIOSK_OUTPUT=<output name> (e.g. LVDS-1): put the full screen
        // window on that screen (best effort: the compositor decides)
        private void MoveToKioskOutput()
        {
            string name = Environment.GetEnvironmentVariable("ARGOS_KIOSK_OUTPUT");
            if (string.IsNullOrEmpty(name) || Screens == null)
                return;
            foreach (Screen s in Screens.All)
            {
                if (s.DisplayName == null || !s.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (Screens.ScreenFromWindow(this) == s)
                    return;
                Console.WriteLine($"gpu-test: moving to screen {s.DisplayName}");
                WindowState = WindowState.Normal;
                Position = s.Bounds.Position;
                WindowState = WindowState.FullScreen;
                return;
            }
            Console.WriteLine($"gpu-test: screen {name} (ARGOS_KIOSK_OUTPUT) not found");
        }
    }
}
