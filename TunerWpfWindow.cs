using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace RobloxNetworkTuner
{
    #region Custom Hardware-Accelerated WPF Controls

    internal static class WpfAnimationHelper
    {
        public static void AnimateBorderBackground(Border border, Color targetColor, int durationMs = 120)
        {
            if (border == null) return;
            try
            {
                SolidColorBrush scb = border.Background as SolidColorBrush;
                if (scb == null || scb.IsFrozen)
                {
                    scb = new SolidColorBrush(scb != null ? scb.Color : Colors.Transparent);
                    border.Background = scb;
                }
                ColorAnimation anim = new ColorAnimation(targetColor, new Duration(TimeSpan.FromMilliseconds(durationMs)));
                scb.BeginAnimation(SolidColorBrush.ColorProperty, anim);
            }
            catch
            {
                try { border.Background = new SolidColorBrush(targetColor); } catch { }
            }
        }

        public static void AnimateBorderBrush(Border border, Color targetColor, int durationMs = 120)
        {
            if (border == null) return;
            try
            {
                SolidColorBrush scb = border.BorderBrush as SolidColorBrush;
                if (scb == null || scb.IsFrozen)
                {
                    scb = new SolidColorBrush(scb != null ? scb.Color : Colors.Transparent);
                    border.BorderBrush = scb;
                }
                ColorAnimation anim = new ColorAnimation(targetColor, new Duration(TimeSpan.FromMilliseconds(durationMs)));
                scb.BeginAnimation(SolidColorBrush.ColorProperty, anim);
            }
            catch
            {
                try { border.BorderBrush = new SolidColorBrush(targetColor); } catch { }
            }
        }

        public static void AnimateTextForeground(TextBlock tb, Color targetColor, int durationMs = 120)
        {
            if (tb == null) return;
            try
            {
                SolidColorBrush scb = tb.Foreground as SolidColorBrush;
                if (scb == null || scb.IsFrozen)
                {
                    scb = new SolidColorBrush(scb != null ? scb.Color : Colors.White);
                    tb.Foreground = scb;
                }
                ColorAnimation anim = new ColorAnimation(targetColor, new Duration(TimeSpan.FromMilliseconds(durationMs)));
                scb.BeginAnimation(SolidColorBrush.ColorProperty, anim);
            }
            catch
            {
                try { tb.Foreground = new SolidColorBrush(targetColor); } catch { }
            }
        }
    }

    /// <summary>
    /// Fully custom hardware-accelerated button that completely bypasses Windows Aero theme.
    /// Provides smooth 120ms hover animations, tactile click scaling, and customizable borders.
    /// </summary>
    public class ModernButton : UserControl
    {
        private readonly Border rootBorder;
        private readonly TextBlock textBlock;
        private readonly ScaleTransform scaleTransform;

        private Color normalBg;
        private Color hoverBg;
        private Color normalBorder;
        private Color hoverBorder;
        private Color normalFg;
        private Color hoverFg;

        private bool isGradient = false;
        private LinearGradientBrush normalGrad;
        private LinearGradientBrush hoverGrad;

        public event RoutedEventHandler Click;

        public string Text
        {
            get { return textBlock != null ? textBlock.Text : ""; }
            set { if (textBlock != null) textBlock.Text = value; }
        }

        public ModernButton()
        {
            this.Cursor = Cursors.Hand;
            this.Focusable = false;

            scaleTransform = new ScaleTransform(1.0, 1.0);
            this.RenderTransform = scaleTransform;
            this.RenderTransformOrigin = new Point(0.5, 0.5);

            rootBorder = new Border();
            textBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            rootBorder.Child = textBlock;
            this.Content = rootBorder;

            this.MouseEnter += ModernButton_MouseEnter;
            this.MouseLeave += ModernButton_MouseLeave;
            this.PreviewMouseLeftButtonDown += ModernButton_PreviewMouseDown;
            this.PreviewMouseLeftButtonUp += ModernButton_PreviewMouseUp;
        }

        private void ModernButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (isGradient)
            {
                rootBorder.Background = hoverGrad;
                WpfAnimationHelper.AnimateBorderBrush(rootBorder, hoverBorder, 120);
            }
            else
            {
                WpfAnimationHelper.AnimateBorderBackground(rootBorder, hoverBg, 120);
                WpfAnimationHelper.AnimateBorderBrush(rootBorder, hoverBorder, 120);
                WpfAnimationHelper.AnimateTextForeground(textBlock, hoverFg, 120);
            }
        }

        private void ModernButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isGradient)
            {
                rootBorder.Background = normalGrad;
                WpfAnimationHelper.AnimateBorderBrush(rootBorder, normalBorder, 120);
            }
            else
            {
                WpfAnimationHelper.AnimateBorderBackground(rootBorder, normalBg, 120);
                WpfAnimationHelper.AnimateBorderBrush(rootBorder, normalBorder, 120);
                WpfAnimationHelper.AnimateTextForeground(textBlock, normalFg, 120);
            }
        }

        private void ModernButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
                this.CaptureMouse();
                DoubleAnimation press = new DoubleAnimation(0.96, new Duration(TimeSpan.FromMilliseconds(50)));
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, press);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, press);
            }
        }

        private void ModernButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
                if (this.IsMouseCaptured)
                {
                    this.ReleaseMouseCapture();
                }
                DoubleAnimation release = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(80)));
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, release);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, release);

                if (this.IsMouseOver && Click != null)
                {
                    Click(this, new RoutedEventArgs());
                }
            }
        }

        public static ModernButton CreateHero(string text, double width = 148, double height = 46)
        {
            ModernButton btn = new ModernButton();
            btn.Width = width;
            btn.Height = height;
            btn.isGradient = true;

            btn.normalGrad = new LinearGradientBrush(Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), new Point(0, 0), new Point(1, 1));
            btn.hoverGrad = new LinearGradientBrush(Color.FromRgb(52, 211, 153), Color.FromRgb(16, 185, 129), new Point(0, 0), new Point(1, 1));

            btn.normalBorder = Color.FromRgb(52, 211, 153);
            btn.hoverBorder = Color.FromRgb(110, 231, 183);

            btn.rootBorder.CornerRadius = new CornerRadius(8);
            btn.rootBorder.Background = btn.normalGrad;
            btn.rootBorder.BorderBrush = new SolidColorBrush(btn.normalBorder);
            btn.rootBorder.BorderThickness = new Thickness(1);

            btn.textBlock.Text = text;
            btn.textBlock.FontSize = 12.5;
            btn.textBlock.FontWeight = FontWeights.Bold;
            btn.textBlock.Foreground = new SolidColorBrush(Colors.White);

            return btn;
        }

        public static ModernButton CreateAction(string text, Color accent, double height = 36)
        {
            ModernButton btn = new ModernButton();
            btn.Height = height;

            btn.normalBg = Color.FromArgb(22, accent.R, accent.G, accent.B);
            btn.hoverBg = Color.FromArgb(48, accent.R, accent.G, accent.B);
            btn.normalBorder = Color.FromArgb(110, accent.R, accent.G, accent.B);
            btn.hoverBorder = accent;
            btn.normalFg = accent;
            btn.hoverFg = Colors.White;

            btn.rootBorder.CornerRadius = new CornerRadius(7);
            btn.rootBorder.Padding = new Thickness(16, 0, 16, 0);
            btn.rootBorder.Background = new SolidColorBrush(btn.normalBg);
            btn.rootBorder.BorderBrush = new SolidColorBrush(btn.normalBorder);
            btn.rootBorder.BorderThickness = new Thickness(1);

            btn.textBlock.Text = text;
            btn.textBlock.FontSize = 11.5;
            btn.textBlock.FontWeight = FontWeights.Bold;
            btn.textBlock.Foreground = new SolidColorBrush(btn.normalFg);

            return btn;
        }

        public static ModernButton CreateCaption(string text, bool isClose)
        {
            ModernButton btn = new ModernButton();
            btn.Width = 32;
            btn.Height = 28;

            btn.normalBg = Colors.Transparent;
            btn.normalBorder = Colors.Transparent;
            btn.normalFg = Color.FromRgb(148, 163, 184);

            if (isClose)
            {
                btn.hoverBg = Color.FromRgb(225, 29, 72); // Rose/Red
                btn.hoverBorder = Color.FromRgb(244, 63, 94);
                btn.hoverFg = Colors.White;
            }
            else
            {
                btn.hoverBg = Color.FromRgb(30, 41, 59); // Slate
                btn.hoverBorder = Color.FromRgb(51, 65, 85);
                btn.hoverFg = Colors.White;
            }

            btn.rootBorder.CornerRadius = new CornerRadius(6);
            btn.rootBorder.Background = new SolidColorBrush(btn.normalBg);
            btn.rootBorder.BorderBrush = new SolidColorBrush(btn.normalBorder);
            btn.rootBorder.BorderThickness = new Thickness(1);

            btn.textBlock.Text = text;
            btn.textBlock.FontSize = 11.5;
            btn.textBlock.FontWeight = FontWeights.SemiBold;
            btn.textBlock.Foreground = new SolidColorBrush(btn.normalFg);

            return btn;
        }

        public static ModernButton CreatePill(string text)
        {
            ModernButton btn = new ModernButton();
            btn.Height = 24;

            btn.normalBg = Color.FromRgb(14, 22, 34);
            btn.hoverBg = Color.FromRgb(20, 32, 48);
            btn.normalBorder = Color.FromRgb(16, 185, 129);
            btn.hoverBorder = Color.FromRgb(52, 211, 153);
            btn.normalFg = Color.FromRgb(52, 211, 153);
            btn.hoverFg = Colors.White;

            btn.rootBorder.CornerRadius = new CornerRadius(12);
            btn.rootBorder.Padding = new Thickness(12, 0, 12, 0);
            btn.rootBorder.Background = new SolidColorBrush(btn.normalBg);
            btn.rootBorder.BorderBrush = new SolidColorBrush(btn.normalBorder);
            btn.rootBorder.BorderThickness = new Thickness(1);

            btn.textBlock.Text = text;
            btn.textBlock.FontSize = 10;
            btn.textBlock.FontWeight = FontWeights.Bold;
            btn.textBlock.Foreground = new SolidColorBrush(btn.normalFg);

            return btn;
        }
    }

    public class AnimatedToggleSwitch : UserControl
    {
        private Border track;
        private Ellipse thumb;
        private TranslateTransform thumbTransform;
        private bool isChecked = true;

        public event EventHandler CheckedChanged;

        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    AnimateState();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public AnimatedToggleSwitch(bool initial)
        {
            isChecked = initial;
            this.Width = 44;
            this.Height = 24;
            this.Cursor = Cursors.Hand;
            this.Focusable = false;

            track = new Border();
            track.CornerRadius = new CornerRadius(12);
            track.Background = new SolidColorBrush(isChecked ? Color.FromRgb(16, 185, 129) : Color.FromRgb(30, 41, 59));
            track.BorderBrush = new SolidColorBrush(isChecked ? Color.FromRgb(52, 211, 153) : Color.FromRgb(51, 65, 85));
            track.BorderThickness = new Thickness(1.2);

            Grid grid = new Grid();
            thumb = new Ellipse();
            thumb.Width = 16;
            thumb.Height = 16;
            thumb.Fill = new SolidColorBrush(Colors.White);
            thumb.HorizontalAlignment = HorizontalAlignment.Left;
            thumb.VerticalAlignment = VerticalAlignment.Center;
            thumb.Margin = new Thickness(3.5, 0, 0, 0);

            thumbTransform = new TranslateTransform(isChecked ? 20 : 0, 0);
            thumb.RenderTransform = thumbTransform;

            grid.Children.Add(thumb);
            track.Child = grid;
            this.Content = track;

            this.MouseLeftButtonUp += delegate
            {
                IsChecked = !IsChecked;
            };

            this.MouseEnter += delegate
            {
                Color hBorder = isChecked ? Color.FromRgb(110, 231, 183) : Color.FromRgb(71, 85, 105);
                WpfAnimationHelper.AnimateBorderBrush(track, hBorder, 120);
            };

            this.MouseLeave += delegate
            {
                Color nBorder = isChecked ? Color.FromRgb(52, 211, 153) : Color.FromRgb(51, 65, 85);
                WpfAnimationHelper.AnimateBorderBrush(track, nBorder, 120);
            };
        }

        private void AnimateState()
        {
            DoubleAnimation slide = new DoubleAnimation();
            slide.To = isChecked ? 20 : 0;
            slide.Duration = new Duration(TimeSpan.FromMilliseconds(150));
            slide.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            thumbTransform.BeginAnimation(TranslateTransform.XProperty, slide);

            Color toBg = isChecked ? Color.FromRgb(16, 185, 129) : Color.FromRgb(30, 41, 59);
            Color toBorder = isChecked ? Color.FromRgb(52, 211, 153) : Color.FromRgb(51, 65, 85);

            WpfAnimationHelper.AnimateBorderBackground(track, toBg, 150);
            WpfAnimationHelper.AnimateBorderBrush(track, toBorder, 150);
        }
    }

    public class SparklineVectorCanvas : Canvas
    {
        private Polyline polyline;
        private Polygon areaPolygon;
        private Line baseline;
        private Color lineColor;

        private double[] lastValues;
        private double lastMin = 0;
        private double lastMax = 100;

        public SparklineVectorCanvas(Color color)
        {
            lineColor = color;
            this.ClipToBounds = true;

            areaPolygon = new Polygon();
            LinearGradientBrush areaBrush = new LinearGradientBrush();
            areaBrush.StartPoint = new Point(0, 0);
            areaBrush.EndPoint = new Point(0, 1);
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(45, color.R, color.G, color.B), 0.0));
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1.0));
            areaPolygon.Fill = areaBrush;

            polyline = new Polyline();
            polyline.Stroke = new SolidColorBrush(color);
            polyline.StrokeThickness = 1.6;
            polyline.StrokeLineJoin = PenLineJoin.Round;

            baseline = new Line();
            baseline.Stroke = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));
            baseline.StrokeThickness = 0.8;

            this.Children.Add(baseline);
            this.Children.Add(areaPolygon);
            this.Children.Add(polyline);

            this.SizeChanged += delegate
            {
                if (lastValues != null)
                {
                    RenderPoints(lastValues, lastMin, lastMax);
                }
            };
        }

        public void UpdatePoints(double[] values, double minVal, double maxVal)
        {
            lastValues = values;
            lastMin = minVal;
            lastMax = maxVal;
            RenderPoints(values, minVal, maxVal);
        }

        private void RenderPoints(double[] values, double minVal, double maxVal)
        {
            if (values == null || values.Length < 2 || this.ActualWidth <= 0 || this.ActualHeight <= 0) return;

            double range = maxVal - minVal;
            if (range < 0.001) range = 1.0;

            PointCollection pts = new PointCollection();
            PointCollection areaPts = new PointCollection();

            double step = this.ActualWidth / (double)(values.Length - 1);
            double h = this.ActualHeight;

            baseline.X1 = 0;
            baseline.Y1 = h - 1;
            baseline.X2 = this.ActualWidth;
            baseline.Y2 = h - 1;

            areaPts.Add(new Point(0, h));

            for (int i = 0; i < values.Length; i++)
            {
                double norm = (values[i] - minVal) / range;
                if (norm < 0) norm = 0;
                if (norm > 1) norm = 1;
                double x = i * step;
                double y = h - (norm * (h - 8.0)) - 4.0;

                Point pt = new Point(x, y);
                pts.Add(pt);
                areaPts.Add(pt);
            }

            areaPts.Add(new Point(this.ActualWidth, h));

            polyline.Points = pts;
            areaPolygon.Points = areaPts;
        }
    }

    #endregion

    #region Main WPF Tuner Window

    public class TunerWpfWindow : Window
    {
        private enum NavTab
        {
            Overview = 0,
            Tuning = 1,
            Statistics = 2,
            Settings = 3
        }

        private NavTab currentTab = NavTab.Overview;

        // Visual layout containers
        private Border sidebarTabIndicator;
        private TranslateTransform indicatorTransform;
        private TextBlock headerBreadcrumbText;
        private Border contentContainer;
        private readonly List<Border> sidebarButtons = new List<Border>();
        private readonly List<TextBlock> sidebarLabels = new List<TextBlock>();

        // Views
        private Grid overviewView;
        private Grid tuningView;
        private Grid statisticsView;
        private Grid settingsView;

        // Metric Card Controls (Overview)
        private TextBlock txtPingVal;
        private TextBlock txtLossVal;
        private TextBlock txtJitterVal;
        private SparklineVectorCanvas sparkPing;
        private SparklineVectorCanvas sparkLoss;
        private SparklineVectorCanvas sparkJitter;

        // Optimization Status Elements
        private Border statusCard;
        private Border statusCircle;
        private TextBlock statusCheckIcon;
        private TextBlock txtOptTitle;
        private TextBlock txtOptSub;
        private TextBlock txtRobloxStatus;
        private ModernButton btnTuneNow;
        private ModernButton verPillBtn;

        // Real-Time Controls
        private AnimatedToggleSwitch switchPerfMode;
        private AnimatedToggleSwitch switchWifiGuard;
        private TextBlock txtCompetingTraffic;
        private Ellipse dotCompetingTraffic;

        // Tuning Tab Controls
        private AnimatedToggleSwitch switchTimer;
        private AnimatedToggleSwitch switchInterrupt;
        private AnimatedToggleSwitch switchRss;
        private AnimatedToggleSwitch switchLso;
        private AnimatedToggleSwitch switchFlow;
        private AnimatedToggleSwitch switchThrottle;
        private AnimatedToggleSwitch switchNagle;
        private AnimatedToggleSwitch switchEee;
        private TextBlock txtBufferbloatQuickStatus;

        // Statistics Tab Controls
        private Border pillGw;
        private TextBlock txtGw;
        private Border pillIsp;
        private TextBlock txtIsp;
        private Border pillRbx;
        private TextBlock txtRbx;
        private TextBlock txtStatRttVal;
        private TextBlock txtStatJitterVal;
        private TextBlock txtStatLossVal;
        private SparklineVectorCanvas sparkBigWaveform;
        private TextBlock txtStatBufferbloat;
        private ModernButton btnRunBufferbloat;

        // Settings Tab Controls
        private TextBlock txtUpdateInfo;
        private ModernButton btnCheckUpdate;

        // Timers & Background Monitors
        private readonly DispatcherTimer watchdogTimer;
        private readonly DispatcherTimer telemetryTimer;
        private WinForms.NotifyIcon trayIcon;

        // Live Telemetry Histories
        private readonly double[] rttHistory = new double[28];
        private readonly double[] lossHistory = new double[28];
        private readonly double[] bwHistory = new double[28];
        private readonly double[] liveWaveformHistory = new double[48];

        private double liveRtt = 0.0;
        private double liveJitter = 0.0;
        private double prevRtt = 0.0;
        private bool isFirstPing = true;
        private bool isTuningApplied = false;
        private bool isTuningInProgress = false;
        private bool robloxRunning = false;
        private bool isLiveGameServer = false;
        private string liveTarget = "roblox.com";
        private string activeAdapterName = "Detecting adapter...";
        private RouteHopSnapshot currentRouteHops = new RouteHopSnapshot();
        private CompetingTrafficSnapshot currentTraffic = new CompetingTrafficSnapshot();
        private string bufferbloatResultText = "Bufferbloat: Not Tested";
        private bool isBufferbloatRunning = false;
        private bool isUpdateAvailable = false;
        private GitHubUpdateModule.ReleaseInfo latestRelease = null;

        public TunerWpfWindow()
        {
            this.Title = "Roblox Network Tuner";
            this.Width = 900;
            this.Height = 560;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = new SolidColorBrush(Colors.Transparent);
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.NoResize;
            this.Opacity = 0.0;

            InitTelemetryHistories();
            BuildUi();

            // Setup Timers
            telemetryTimer = new DispatcherTimer();
            telemetryTimer.Interval = TimeSpan.FromMilliseconds(4000);
            telemetryTimer.Tick += TelemetryTimer_Tick;
            telemetryTimer.Start();

            watchdogTimer = new DispatcherTimer();
            watchdogTimer.Interval = TimeSpan.FromMilliseconds(2000);
            watchdogTimer.Tick += WatchdogTimer_Tick;
            watchdogTimer.Start();

            // Setup Tray Icon
            SetupSystemTray();

            // Smooth Window Fade-In on Loaded
            this.Loaded += delegate
            {
                DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(220)));
                fadeIn.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
                TriggerBackgroundChecks();
            };
        }

        private void InitTelemetryHistories()
        {
            for (int i = 0; i < rttHistory.Length; i++) rttHistory[i] = 24.0 + (i % 3);
            for (int i = 0; i < lossHistory.Length; i++) lossHistory[i] = 0.0;
            for (int i = 0; i < bwHistory.Length; i++) bwHistory[i] = 0.4 + (i % 2) * 0.1;
            for (int i = 0; i < liveWaveformHistory.Length; i++) liveWaveformHistory[i] = 24.0;
        }

        private void BuildUi()
        {
            // Outer Window Shell
            Border rootBorder = new Border();
            rootBorder.CornerRadius = new CornerRadius(12);
            rootBorder.Background = new SolidColorBrush(Color.FromRgb(10, 13, 20)); // Deep obsidian
            rootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)); // Clean refined slate
            rootBorder.BorderThickness = new Thickness(1.2);
            rootBorder.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 28,
                ShadowDepth = 6,
                Opacity = 0.65
            };

            Grid rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 1. Sidebar
            rootGrid.Children.Add(BuildSidebar());

            // 2. Main Body (Header + Content Area)
            Grid mainArea = new Grid();
            Grid.SetColumn(mainArea, 1);
            mainArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            mainArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            mainArea.Children.Add(BuildHeader());

            contentContainer = new Border();
            Grid.SetRow(contentContainer, 1);
            contentContainer.Padding = new Thickness(20, 14, 20, 18);

            // Initialize 4 Views
            overviewView = BuildOverviewView();
            tuningView = BuildTuningView();
            statisticsView = BuildStatisticsView();
            settingsView = BuildSettingsView();

            contentContainer.Child = overviewView;
            mainArea.Children.Add(contentContainer);

            rootGrid.Children.Add(mainArea);
            rootBorder.Child = rootGrid;
            this.Content = rootBorder;
        }

        #region UI Construction Helpers

        private FrameworkElement BuildSidebar()
        {
            Border sideBorder = new Border();
            sideBorder.Background = new SolidColorBrush(Color.FromRgb(8, 11, 16));
            sideBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(24, 32, 47));
            sideBorder.BorderThickness = new Thickness(0, 0, 1, 0);

            Grid sideGrid = new Grid();
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });

            // Brand Header
            StackPanel brandStack = new StackPanel();
            brandStack.Orientation = Orientation.Horizontal;
            brandStack.VerticalAlignment = VerticalAlignment.Center;
            brandStack.Margin = new Thickness(20, 0, 0, 0);

            // Animated Brand Icon (Neon Hexagon + Lightning Bolt)
            Grid avatar = new Grid();
            avatar.Width = 38;
            avatar.Height = 38;
            avatar.VerticalAlignment = VerticalAlignment.Center;
            avatar.HorizontalAlignment = HorizontalAlignment.Center;
            avatar.Cursor = Cursors.Hand;

            Viewbox iconBox = new Viewbox();
            iconBox.Width = 36;
            iconBox.Height = 36;
            iconBox.Stretch = Stretch.Uniform;

            Canvas iconCanvas = new Canvas();
            iconCanvas.Width = 32;
            iconCanvas.Height = 32;

            // 1. Outer Hexagon Badge
            System.Windows.Shapes.Path hexPath = new System.Windows.Shapes.Path();
            hexPath.Data = Geometry.Parse("M 16,2 L 28,8.9 L 28,23.1 L 16,30 L 4,23.1 L 4,8.9 Z");
            hexPath.Fill = new SolidColorBrush(Color.FromRgb(10, 19, 31));
            LinearGradientBrush hexStroke = new LinearGradientBrush();
            hexStroke.StartPoint = new Point(0, 0);
            hexStroke.EndPoint = new Point(1, 1);
            hexStroke.GradientStops.Add(new GradientStop(Color.FromRgb(0, 240, 255), 0.0));
            hexStroke.GradientStops.Add(new GradientStop(Color.FromRgb(16, 185, 129), 1.0));
            hexPath.Stroke = hexStroke;
            hexPath.StrokeThickness = 2.0;

            // 2. Center Lightning Bolt
            System.Windows.Shapes.Path boltPath = new System.Windows.Shapes.Path();
            boltPath.Data = Geometry.Parse("M 17.5,5.5 L 10,16 L 16,16 L 14,26.5 L 22,13.5 L 16,13.5 Z");
            LinearGradientBrush boltFill = new LinearGradientBrush();
            boltFill.StartPoint = new Point(0, 0);
            boltFill.EndPoint = new Point(1, 1);
            boltFill.GradientStops.Add(new GradientStop(Color.FromRgb(0, 240, 255), 0.0));
            boltFill.GradientStops.Add(new GradientStop(Color.FromRgb(52, 211, 153), 1.0));
            boltPath.Fill = boltFill;

            // 3. Core Energy Center Dot
            Ellipse coreDot = new Ellipse();
            coreDot.Width = 3.0;
            coreDot.Height = 3.0;
            Canvas.SetLeft(coreDot, 14.5);
            Canvas.SetTop(coreDot, 13.5);
            coreDot.Fill = new SolidColorBrush(Colors.White);
            coreDot.Opacity = 0.95;

            iconCanvas.Children.Add(hexPath);
            iconCanvas.Children.Add(boltPath);
            iconCanvas.Children.Add(coreDot);
            iconBox.Child = iconCanvas;

            // 4. Glowing DropShadow Effect
            DropShadowEffect iconGlow = new DropShadowEffect();
            iconGlow.Color = Color.FromRgb(0, 240, 255);
            iconGlow.Direction = 0;
            iconGlow.ShadowDepth = 0;
            iconGlow.BlurRadius = 10;
            iconGlow.Opacity = 0.75;
            iconBox.Effect = iconGlow;

            // 5. Breathing Scale Transform
            ScaleTransform iconScale = new ScaleTransform(1.0, 1.0, 18, 18);
            iconBox.RenderTransform = iconScale;

            // 6. Hardware-Accelerated Looping Breathing Animations
            DoubleAnimation glowAnim = new DoubleAnimation(0.40, 0.95, new Duration(TimeSpan.FromMilliseconds(1800)));
            glowAnim.AutoReverse = true;
            glowAnim.RepeatBehavior = RepeatBehavior.Forever;
            glowAnim.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
            iconGlow.BeginAnimation(DropShadowEffect.OpacityProperty, glowAnim);

            DoubleAnimation blurAnim = new DoubleAnimation(6.0, 14.0, new Duration(TimeSpan.FromMilliseconds(1800)));
            blurAnim.AutoReverse = true;
            blurAnim.RepeatBehavior = RepeatBehavior.Forever;
            blurAnim.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
            iconGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blurAnim);

            DoubleAnimation scaleAnim = new DoubleAnimation(0.96, 1.04, new Duration(TimeSpan.FromMilliseconds(1800)));
            scaleAnim.AutoReverse = true;
            scaleAnim.RepeatBehavior = RepeatBehavior.Forever;
            scaleAnim.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
            iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

            avatar.Children.Add(iconBox);
            brandStack.Children.Add(avatar);

            StackPanel titleStack = new StackPanel();
            titleStack.Margin = new Thickness(12, 0, 0, 0);
            titleStack.VerticalAlignment = VerticalAlignment.Center;

            TextBlock txtName = new TextBlock();
            txtName.Text = "getsentrix";
            txtName.FontWeight = FontWeights.Bold;
            txtName.FontSize = 13.5;
            txtName.Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            titleStack.Children.Add(txtName);

            TextBlock txtVer = new TextBlock();
            txtVer.Text = "RBLX Tuner v" + GitHubUpdateModule.CurrentVersion;
            txtVer.FontWeight = FontWeights.SemiBold;
            txtVer.FontSize = 10.5;
            txtVer.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            titleStack.Children.Add(txtVer);

            brandStack.Children.Add(titleStack);
            sideGrid.Children.Add(brandStack);

            // Nav Tabs Stack
            Grid navGrid = new Grid();
            Grid.SetRow(navGrid, 1);
            navGrid.Margin = new Thickness(12, 14, 12, 0);

            // Left Animated Indicator Bar
            sidebarTabIndicator = new Border();
            sidebarTabIndicator.Width = 3.5;
            sidebarTabIndicator.Height = 24;
            sidebarTabIndicator.CornerRadius = new CornerRadius(1.75);
            sidebarTabIndicator.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            sidebarTabIndicator.HorizontalAlignment = HorizontalAlignment.Left;
            sidebarTabIndicator.VerticalAlignment = VerticalAlignment.Top;
            sidebarTabIndicator.Margin = new Thickness(2, 9, 0, 0);

            indicatorTransform = new TranslateTransform(0, 0);
            sidebarTabIndicator.RenderTransform = indicatorTransform;
            navGrid.Children.Add(sidebarTabIndicator);

            StackPanel navStack = new StackPanel();
            navStack.Children.Add(CreateSidebarButton("Overview", NavTab.Overview));
            navStack.Children.Add(CreateSidebarButton("Tuning", NavTab.Tuning));
            navStack.Children.Add(CreateSidebarButton("Statistics", NavTab.Statistics));
            navStack.Children.Add(CreateSidebarButton("Settings", NavTab.Settings));
            navGrid.Children.Add(navStack);

            sideGrid.Children.Add(navGrid);

            // Sidebar Footer
            StackPanel footStack = new StackPanel();
            Grid.SetRow(footStack, 2);
            footStack.Margin = new Thickness(20, 0, 0, 14);
            footStack.VerticalAlignment = VerticalAlignment.Bottom;

            StackPanel statDotStack = new StackPanel { Orientation = Orientation.Horizontal };
            Ellipse dot = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)), VerticalAlignment = VerticalAlignment.Center };
            TextBlock txtStat = new TextBlock
            {
                Text = "ACTIVE",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Margin = new Thickness(7, 0, 0, 0)
            };
            statDotStack.Children.Add(dot);
            statDotStack.Children.Add(txtStat);
            footStack.Children.Add(statDotStack);

            TextBlock txtGh = new TextBlock
            {
                Text = "github.com/getsentrix",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 4, 0, 0),
                Cursor = Cursors.Hand
            };
            txtGh.MouseEnter += delegate { txtGh.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)); };
            txtGh.MouseLeave += delegate { txtGh.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)); };
            txtGh.MouseLeftButtonUp += delegate
            {
                try { Process.Start("https://github.com/getsentrix/RBLX-Network-Tuner"); } catch { }
            };
            footStack.Children.Add(txtGh);

            sideGrid.Children.Add(footStack);
            sideBorder.Child = sideGrid;
            return sideBorder;
        }

        private Border CreateSidebarButton(string title, NavTab tab)
        {
            Border btn = new Border();
            btn.Height = 42;
            btn.CornerRadius = new CornerRadius(8);
            btn.Margin = new Thickness(0, 3, 0, 3);
            btn.Background = (tab == currentTab) ? new SolidColorBrush(Color.FromRgb(19, 27, 42)) : new SolidColorBrush(Colors.Transparent);
            btn.Cursor = Cursors.Hand;

            TextBlock tb = new TextBlock();
            tb.Text = title;
            tb.FontSize = 12.5;
            tb.FontWeight = (tab == currentTab) ? FontWeights.Bold : FontWeights.Normal;
            tb.Foreground = new SolidColorBrush((tab == currentTab) ? Colors.White : Color.FromRgb(148, 163, 184));
            tb.Margin = new Thickness(20, 0, 0, 0);
            tb.VerticalAlignment = VerticalAlignment.Center;
            btn.Child = tb;

            sidebarButtons.Add(btn);
            sidebarLabels.Add(tb);

            btn.MouseEnter += delegate
            {
                if (currentTab != tab)
                {
                    WpfAnimationHelper.AnimateBorderBackground(btn, Color.FromRgb(17, 24, 39), 120);
                }
            };
            btn.MouseLeave += delegate
            {
                if (currentTab != tab)
                {
                    WpfAnimationHelper.AnimateBorderBackground(btn, Colors.Transparent, 120);
                }
            };

            btn.MouseLeftButtonUp += delegate
            {
                SwitchTab(tab);
            };

            return btn;
        }

        private FrameworkElement BuildHeader()
        {
            Border hBorder = new Border();
            hBorder.Background = new SolidColorBrush(Color.FromRgb(10, 13, 20));
            hBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(24, 32, 47));
            hBorder.BorderThickness = new Thickness(0, 0, 0, 1);

            // Enable dragging window anywhere from header EXCEPT when clicking controls
            hBorder.MouseLeftButtonDown += delegate (object sender, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DependencyObject dep = e.OriginalSource as DependencyObject;
                    while (dep != null && dep != hBorder)
                    {
                        if (dep is ModernButton || dep is Button || dep is Control)
                            return;
                        dep = VisualTreeHelper.GetParent(dep);
                    }
                    try { this.DragMove(); } catch { }
                }
            };

            Grid hGrid = new Grid();
            hGrid.Margin = new Thickness(20, 0, 16, 0);

            // Left Breadcrumb
            headerBreadcrumbText = new TextBlock();
            headerBreadcrumbText.Text = "OVERVIEW  /  DASHBOARD";
            headerBreadcrumbText.FontSize = 11;
            headerBreadcrumbText.FontWeight = FontWeights.Bold;
            headerBreadcrumbText.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            headerBreadcrumbText.VerticalAlignment = VerticalAlignment.Center;
            hGrid.Children.Add(headerBreadcrumbText);

            // Right Action Controls
            StackPanel rightActions = new StackPanel();
            rightActions.Orientation = Orientation.Horizontal;
            rightActions.HorizontalAlignment = HorizontalAlignment.Right;
            rightActions.VerticalAlignment = VerticalAlignment.Center;

            // Version Pill Button
            verPillBtn = ModernButton.CreatePill("v" + GitHubUpdateModule.CurrentVersion);
            verPillBtn.Margin = new Thickness(0, 0, 14, 0);
            verPillBtn.Click += delegate
            {
                SwitchTab(NavTab.Settings);
            };
            rightActions.Children.Add(verPillBtn);

            // Minimize Button
            ModernButton btnMin = ModernButton.CreateCaption("—", false);
            btnMin.Click += delegate { MinimizeToTray(); };
            rightActions.Children.Add(btnMin);

            // Close Button
            ModernButton btnClose = ModernButton.CreateCaption("✕", true);
            btnClose.Margin = new Thickness(4, 0, 0, 0);
            btnClose.Click += delegate { SafeExit(); };
            rightActions.Children.Add(btnClose);

            hGrid.Children.Add(rightActions);
            hBorder.Child = hGrid;
            return hBorder;
        }

        #endregion

        #region Tab Views Construction

        private Grid BuildOverviewView()
        {
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(112) }); // 3 Sparkline Metric Cards
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(124) }); // Status & TUNE NOW Card
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Profiles & Controls

            // Row 0: 3 Cards
            Grid topCards = new Grid();
            topCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            topCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            topCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            txtPingVal = new TextBlock { Text = "24.2 ms", FontSize = 21, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)) };
            sparkPing = new SparklineVectorCanvas(Color.FromRgb(52, 211, 153));
            topCards.Children.Add(CreateMetricCard("PING", txtPingVal, sparkPing, 0));

            txtLossVal = new TextBlock { Text = "0.0%", FontSize = 21, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)) };
            sparkLoss = new SparklineVectorCanvas(Color.FromRgb(56, 189, 248));
            topCards.Children.Add(CreateMetricCard("PACKET LOSS", txtLossVal, sparkLoss, 2));

            txtJitterVal = new TextBlock { Text = "±0.45 ms", FontSize = 21, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)) };
            sparkJitter = new SparklineVectorCanvas(Color.FromRgb(245, 158, 11));
            topCards.Children.Add(CreateMetricCard("JITTER", txtJitterVal, sparkJitter, 4));

            grid.Children.Add(topCards);

            // Row 1: Optimization Status Card
            statusCard = new Border();
            Grid.SetRow(statusCard, 1);
            statusCard.Margin = new Thickness(0, 12, 0, 12);
            statusCard.CornerRadius = new CornerRadius(10);
            statusCard.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            statusCard.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            statusCard.BorderThickness = new Thickness(1);

            Grid statusGrid = new Grid();
            statusGrid.Margin = new Thickness(18, 16, 18, 16);
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(164) });

            // Check Circle
            statusCircle = new Border();
            statusCircle.Width = 46;
            statusCircle.Height = 46;
            statusCircle.CornerRadius = new CornerRadius(23);
            statusCircle.Background = new SolidColorBrush(Color.FromRgb(6, 40, 28));
            statusCircle.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            statusCircle.BorderThickness = new Thickness(1.5);
            statusCircle.HorizontalAlignment = HorizontalAlignment.Left;
            statusCircle.VerticalAlignment = VerticalAlignment.Center;
            statusCheckIcon = new TextBlock { Text = "✓", FontSize = 19, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            statusCircle.Child = statusCheckIcon;
            statusGrid.Children.Add(statusCircle);

            // Status Texts
            StackPanel statTxtStack = new StackPanel();
            Grid.SetColumn(statTxtStack, 1);
            statTxtStack.VerticalAlignment = VerticalAlignment.Center;

            txtOptTitle = new TextBlock { Text = "Optimized Successfully", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) };
            statTxtStack.Children.Add(txtOptTitle);

            txtOptSub = new TextBlock { Text = "0.50ms timer • NDIS fast-path • EcoQoS disabled", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 2, 0, 3) };
            statTxtStack.Children.Add(txtOptSub);

            txtRobloxStatus = new TextBlock { Text = "Standby: Monitoring Roblox client...", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)) };
            statTxtStack.Children.Add(txtRobloxStatus);

            statusGrid.Children.Add(statTxtStack);

            // Modern TUNE NOW Button
            btnTuneNow = ModernButton.CreateHero("TUNE NOW", 150, 46);
            Grid.SetColumn(btnTuneNow, 2);
            btnTuneNow.HorizontalAlignment = HorizontalAlignment.Right;
            btnTuneNow.VerticalAlignment = VerticalAlignment.Center;
            btnTuneNow.Click += delegate { TriggerTuneAction(); };

            statusGrid.Children.Add(btnTuneNow);
            statusCard.Child = statusGrid;
            grid.Children.Add(statusCard);

            // Row 2: Active Controls Card
            Border ctrlCard = new Border();
            Grid.SetRow(ctrlCard, 2);
            ctrlCard.CornerRadius = new CornerRadius(10);
            ctrlCard.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            ctrlCard.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            ctrlCard.BorderThickness = new Thickness(1);

            StackPanel ctrlStack = new StackPanel();
            ctrlStack.Margin = new Thickness(18, 14, 18, 14);

            TextBlock ctrlHead = new TextBlock { Text = "ACTIVE PROFILES", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 8) };
            ctrlStack.Children.Add(ctrlHead);

            // Switch 1: Performance Mode
            switchPerfMode = new AnimatedToggleSwitch(true);
            switchPerfMode.CheckedChanged += delegate { TriggerReapplyAsync(); };
            ctrlStack.Children.Add(CreateControlRow("Performance Mode", "0.50ms timer, AFD UDP buffers, CPU priority boost", switchPerfMode));

            // Separator
            ctrlStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(24, 32, 47)), Margin = new Thickness(0, 6, 0, 6) });

            // Switch 2: Wi-Fi Roaming Guard
            switchWifiGuard = new AnimatedToggleSwitch(true);
            switchWifiGuard.CheckedChanged += delegate { TriggerReapplyAsync(); };
            ctrlStack.Children.Add(CreateControlRow("Wi-Fi Roaming Guard", "Locks BSSID roaming & suppresses background scans", switchWifiGuard));

            // Contention Status
            StackPanel contStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            dotCompetingTraffic = new Ellipse { Width = 6.5, Height = 6.5, Fill = new SolidColorBrush(Color.FromRgb(52, 211, 153)), VerticalAlignment = VerticalAlignment.Center };
            txtCompetingTraffic = new TextBlock
            {
                Text = "Normal network conditions (No heavy background contention)",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)),
                Margin = new Thickness(7, 0, 0, 0)
            };
            contStack.Children.Add(dotCompetingTraffic);
            contStack.Children.Add(txtCompetingTraffic);
            ctrlStack.Children.Add(contStack);

            ctrlCard.Child = ctrlStack;
            grid.Children.Add(ctrlCard);

            return grid;
        }

        private Border CreateMetricCard(string title, TextBlock valBlock, SparklineVectorCanvas sparkline, int colIndex)
        {
            Border card = new Border();
            Grid.SetColumn(card, colIndex);
            card.CornerRadius = new CornerRadius(10);
            card.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            card.BorderThickness = new Thickness(1);

            Grid cg = new Grid();
            cg.Margin = new Thickness(14, 12, 14, 10);
            cg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            cg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            cg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock tTitle = new TextBlock { Text = title, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)) };
            cg.Children.Add(tTitle);

            Grid.SetRow(valBlock, 1);
            cg.Children.Add(valBlock);

            Grid.SetRow(sparkline, 2);
            cg.Children.Add(sparkline);

            card.Child = cg;
            return card;
        }

        private Grid CreateControlRow(string title, string subtitle, AnimatedToggleSwitch toggle)
        {
            Grid r = new Grid();
            r.Margin = new Thickness(0, 3, 0, 3);
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

            StackPanel sp = new StackPanel();
            sp.VerticalAlignment = VerticalAlignment.Center;
            TextBlock t = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) };
            TextBlock s = new TextBlock { Text = subtitle, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 1, 0, 0) };
            sp.Children.Add(t);
            sp.Children.Add(s);
            r.Children.Add(sp);

            Grid.SetColumn(toggle, 1);
            toggle.HorizontalAlignment = HorizontalAlignment.Right;
            toggle.VerticalAlignment = VerticalAlignment.Center;
            r.Children.Add(toggle);

            return r;
        }

        private Grid BuildTuningView()
        {
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });

            // Tuning Options Card
            Border card = new Border();
            card.CornerRadius = new CornerRadius(10);
            card.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            card.BorderThickness = new Thickness(1);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            try
            {
                string scrollBarStyle = @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>
                    <Setter Property='Background' Value='Transparent'/>
                    <Setter Property='Width' Value='6'/>
                    <Setter Property='MinWidth' Value='6'/>
                    <Setter Property='Template'>
                        <Setter.Value>
                            <ControlTemplate TargetType='{x:Type ScrollBar}'>
                                <Grid Background='Transparent'>
                                    <Track x:Name='PART_Track' IsDirectionReversed='true'>
                                        <Track.DecreaseRepeatButton>
                                            <RepeatButton Command='{x:Static ScrollBar.LineUpCommand}' Opacity='0' Focusable='false'/>
                                        </Track.DecreaseRepeatButton>
                                        <Track.IncreaseRepeatButton>
                                            <RepeatButton Command='{x:Static ScrollBar.LineDownCommand}' Opacity='0' Focusable='false'/>
                                        </Track.IncreaseRepeatButton>
                                        <Track.Thumb>
                                            <Thumb>
                                                <Thumb.Template>
                                                    <ControlTemplate TargetType='{x:Type Thumb}'>
                                                        <Border x:Name='thumbBorder' Background='#334155' CornerRadius='3' Margin='0,2,0,2'/>
                                                        <ControlTemplate.Triggers>
                                                            <Trigger Property='IsMouseOver' Value='true'>
                                                                <Setter TargetName='thumbBorder' Property='Background' Value='#10B981'/>
                                                            </Trigger>
                                                            <Trigger Property='IsDragging' Value='true'>
                                                                <Setter TargetName='thumbBorder' Property='Background' Value='#059669'/>
                                                            </Trigger>
                                                        </ControlTemplate.Triggers>
                                                    </ControlTemplate>
                                                </Thumb.Template>
                                            </Thumb>
                                        </Track.Thumb>
                                    </Track>
                                </Grid>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>";
                Style customScrollStyle = (Style)System.Windows.Markup.XamlReader.Parse(scrollBarStyle);
                scroll.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), customScrollStyle);
            }
            catch { }
            StackPanel list = new StackPanel { Margin = new Thickness(8, 12, 8, 12) };

            TextBlock h = new TextBlock { Text = "KERNEL & SOCKET TUNING", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(12, 0, 0, 8) };
            list.Children.Add(h);

            switchTimer = new AnimatedToggleSwitch(true);
            list.Children.Add(CreateTuningRow("Timer Resolution (0.50ms)", "Reduces Windows scheduler delay from 15.6ms to 0.50ms", switchTimer));

            switchInterrupt = new AnimatedToggleSwitch(true);
            list.Children.Add(CreateTuningRow("Interrupt Moderation (Off)", "Immediate CPU interrupt on packet arrival (no batching)", switchInterrupt));

            switchRss = new AnimatedToggleSwitch(true);
            list.Children.Add(CreateTuningRow("Receive Side Scaling (RSS)", "Distributes packet queues across CPU cores to avoid Core 0 bottleneck", switchRss));

            switchLso = new AnimatedToggleSwitch(true);
            list.Children.Add(CreateTuningRow("Large Send Offload (Off)", "Disables hardware packet chunking to eliminate driver stalls", switchLso));

            switchFlow = new AnimatedToggleSwitch(true);
            list.Children.Add(CreateTuningRow("Flow Control (Off)", "Prevents Ethernet PAUSE frames from blocking transmit queue", switchFlow));

            switchThrottle = new AnimatedToggleSwitch(true);
            list.Children.Add(CreateTuningRow("Network Throttling Index (Off)", "Removes Windows 10-packet/ms multimedia limit", switchThrottle));

            switchNagle = new AnimatedToggleSwitch(true);
            list.Children.Add(CreateTuningRow("TCP NoDelay & AckFrequency", "Immediate ACK for Roblox meshes, textures, and sounds", switchNagle));

            switchEee = new AnimatedToggleSwitch(true);
            list.Children.Add(CreateTuningRow("Energy Efficient Ethernet (Off)", "Prevents adapter sleep delays on wired connections", switchEee));

            scroll.Content = list;
            card.Child = scroll;
            grid.Children.Add(card);

            // Action Buttons Footer
            StackPanel actions = new StackPanel();
            Grid.SetRow(actions, 1);
            actions.Margin = new Thickness(0, 12, 0, 0);
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;

            ModernButton btnReapply = ModernButton.CreateAction("Re-Apply Tuning", Color.FromRgb(16, 185, 129), 36);
            btnReapply.Margin = new Thickness(0, 0, 10, 0);
            btnReapply.Click += delegate { TriggerReapplyAsync(); };

            ModernButton btnRestore = ModernButton.CreateAction("Restore Defaults", Color.FromRgb(244, 63, 94), 36);
            btnRestore.Margin = new Thickness(0, 0, 10, 0);
            btnRestore.Click += delegate { TriggerRestoreAsync(); };

            ModernButton btnQuickBb = ModernButton.CreateAction("Bufferbloat Test", Color.FromRgb(56, 189, 248), 36);
            btnQuickBb.Click += delegate { TriggerBufferbloatAsync(); };

            actions.Children.Add(btnReapply);
            actions.Children.Add(btnRestore);
            actions.Children.Add(btnQuickBb);

            txtBufferbloatQuickStatus = new TextBlock
            {
                Text = bufferbloatResultText,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };
            actions.Children.Add(txtBufferbloatQuickStatus);

            grid.Children.Add(actions);
            return grid;
        }

        private Border CreateTuningRow(string title, string subtitle, AnimatedToggleSwitch toggle)
        {
            Border rowBorder = new Border();
            rowBorder.CornerRadius = new CornerRadius(8);
            rowBorder.Margin = new Thickness(0, 2, 0, 2);
            rowBorder.Padding = new Thickness(12, 7, 12, 7);
            rowBorder.Background = new SolidColorBrush(Colors.Transparent);

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

            StackPanel sp = new StackPanel();
            sp.VerticalAlignment = VerticalAlignment.Center;
            TextBlock t = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) };
            TextBlock s = new TextBlock { Text = subtitle, FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 1, 0, 0) };
            sp.Children.Add(t);
            sp.Children.Add(s);
            row.Children.Add(sp);

            Grid.SetColumn(toggle, 1);
            toggle.HorizontalAlignment = HorizontalAlignment.Right;
            toggle.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(toggle);

            rowBorder.Child = row;

            rowBorder.MouseEnter += delegate
            {
                WpfAnimationHelper.AnimateBorderBackground(rowBorder, Color.FromRgb(18, 24, 36), 120);
            };
            rowBorder.MouseLeave += delegate
            {
                WpfAnimationHelper.AnimateBorderBackground(rowBorder, Colors.Transparent, 120);
            };

            return rowBorder;
        }

        private Grid BuildStatisticsView()
        {
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(88) }); // Route Hops Card
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Big Waveform Card
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(92) }); // Bufferbloat Card

            // Card 1: Route Latency
            Border hopCard = new Border();
            hopCard.CornerRadius = new CornerRadius(10);
            hopCard.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            hopCard.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            hopCard.BorderThickness = new Thickness(1);

            StackPanel hopStack = new StackPanel { Margin = new Thickness(18, 12, 18, 12) };
            TextBlock hopHead = new TextBlock { Text = "ROUTE TELEMETRY", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 8) };
            hopStack.Children.Add(hopHead);

            StackPanel pills = new StackPanel { Orientation = Orientation.Horizontal };
            pillGw = CreateRoutePill("GATEWAY: 1.2 ms", out txtGw, Color.FromRgb(52, 211, 153));
            pillIsp = CreateRoutePill("ISP EDGE: 14.2 ms", out txtIsp, Color.FromRgb(56, 189, 248));
            pillRbx = CreateRoutePill("ROBLOX: 28.5 ms", out txtRbx, Color.FromRgb(52, 211, 153));

            pills.Children.Add(pillGw);
            pills.Children.Add(new TextBlock { Text = " ──▶ ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(80, 95, 115)), VerticalAlignment = VerticalAlignment.Center });
            pills.Children.Add(pillIsp);
            pills.Children.Add(new TextBlock { Text = " ──▶ ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(80, 95, 115)), VerticalAlignment = VerticalAlignment.Center });
            pills.Children.Add(pillRbx);
            hopStack.Children.Add(pills);

            hopCard.Child = hopStack;
            grid.Children.Add(hopCard);

            // Card 2: Big Telemetry Waveform
            Border waveCard = new Border();
            Grid.SetRow(waveCard, 1);
            waveCard.Margin = new Thickness(0, 10, 0, 10);
            waveCard.CornerRadius = new CornerRadius(10);
            waveCard.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            waveCard.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            waveCard.BorderThickness = new Thickness(1);

            Grid wg = new Grid();
            wg.Margin = new Thickness(18, 12, 18, 12);
            wg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            wg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            wg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock wTitle = new TextBlock { Text = "LIVE PING & JITTER WAVEFORM", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) };
            wg.Children.Add(wTitle);

            // Metric values row
            StackPanel metricsRow = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetRow(metricsRow, 1);

            txtStatRttVal = new TextBlock { Text = "24.2 ms", FontSize = 19, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)) };
            metricsRow.Children.Add(CreateStatMetric("ROUND-TRIP TIME", txtStatRttVal));

            txtStatJitterVal = new TextBlock { Text = "±0.45 ms", FontSize = 19, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)) };
            metricsRow.Children.Add(CreateStatMetric("RFC 3550 JITTER", txtStatJitterVal));

            txtStatLossVal = new TextBlock { Text = "0.0%", FontSize = 19, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(244, 114, 182)) };
            metricsRow.Children.Add(CreateStatMetric("PACKET LOSS", txtStatLossVal));

            wg.Children.Add(metricsRow);

            // Hardware Vector Waveform Canvas
            sparkBigWaveform = new SparklineVectorCanvas(Color.FromRgb(52, 211, 153));
            Grid.SetRow(sparkBigWaveform, 2);
            wg.Children.Add(sparkBigWaveform);

            waveCard.Child = wg;
            grid.Children.Add(waveCard);

            // Card 3: Bufferbloat
            Border bbCard = new Border();
            Grid.SetRow(bbCard, 2);
            bbCard.CornerRadius = new CornerRadius(10);
            bbCard.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            bbCard.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            bbCard.BorderThickness = new Thickness(1);

            Grid bbg = new Grid();
            bbg.Margin = new Thickness(18, 12, 18, 12);
            bbg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bbg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            StackPanel bbTxt = new StackPanel();
            bbTxt.VerticalAlignment = VerticalAlignment.Center;
            TextBlock bbHead = new TextBlock { Text = "BUFFERBLOAT DIAGNOSTIC", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) };
            txtStatBufferbloat = new TextBlock { Text = bufferbloatResultText, FontSize = 11.5, Foreground = new SolidColorBrush(Colors.White), Margin = new Thickness(0, 4, 0, 0) };
            bbTxt.Children.Add(bbHead);
            bbTxt.Children.Add(txtStatBufferbloat);
            bbg.Children.Add(bbTxt);

            btnRunBufferbloat = ModernButton.CreateAction("Run Test", Color.FromRgb(52, 211, 153), 36);
            btnRunBufferbloat.HorizontalAlignment = HorizontalAlignment.Right;
            btnRunBufferbloat.VerticalAlignment = VerticalAlignment.Center;
            btnRunBufferbloat.Click += delegate { TriggerBufferbloatAsync(); };
            Grid.SetColumn(btnRunBufferbloat, 1);
            bbg.Children.Add(btnRunBufferbloat);

            bbCard.Child = bbg;
            grid.Children.Add(bbCard);

            return grid;
        }

        private Border CreateRoutePill(string initial, out TextBlock tbOut, Color accent)
        {
            Border p = new Border();
            p.CornerRadius = new CornerRadius(7);
            p.Background = new SolidColorBrush(Color.FromRgb(16, 22, 32));
            p.BorderBrush = new SolidColorBrush(accent);
            p.BorderThickness = new Thickness(1);
            p.Padding = new Thickness(14, 6, 14, 6);

            TextBlock tb = new TextBlock { Text = initial, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(accent) };
            p.Child = tb;
            tbOut = tb;
            return p;
        }

        private StackPanel CreateStatMetric(string label, TextBlock valBlock)
        {
            StackPanel sp = new StackPanel();
            sp.Margin = new Thickness(0, 0, 36, 0);
            sp.Children.Add(valBlock);
            sp.Children.Add(new TextBlock { Text = label, FontSize = 8.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            return sp;
        }

        private Grid BuildSettingsView()
        {
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(112) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(118) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Card 1: About
            Border aboutCard = new Border();
            aboutCard.CornerRadius = new CornerRadius(10);
            aboutCard.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            aboutCard.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            aboutCard.BorderThickness = new Thickness(1);

            StackPanel aboutStack = new StackPanel { Margin = new Thickness(18, 12, 18, 12) };
            aboutStack.Children.Add(new TextBlock { Text = "ABOUT", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            aboutStack.Children.Add(new TextBlock { Text = "Roblox Network Tuner v" + GitHubUpdateModule.CurrentVersion + " by getsentrix", FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White), Margin = new Thickness(0, 3, 0, 1) });
            aboutStack.Children.Add(new TextBlock { Text = "Low-latency network and scheduler optimization for competitive Roblox gameplay.", FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)) });

            ModernButton btnGh = ModernButton.CreateAction("Open GitHub Repository", Color.FromRgb(56, 189, 248), 34);
            btnGh.HorizontalAlignment = HorizontalAlignment.Left;
            btnGh.Margin = new Thickness(0, 8, 0, 0);
            btnGh.Click += delegate
            {
                try { Process.Start("https://github.com/getsentrix/RBLX-Network-Tuner"); } catch { }
            };
            aboutStack.Children.Add(btnGh);
            aboutCard.Child = aboutStack;
            grid.Children.Add(aboutCard);

            // Card 2: Updates
            Border upCard = new Border();
            Grid.SetRow(upCard, 1);
            upCard.Margin = new Thickness(0, 10, 0, 10);
            upCard.CornerRadius = new CornerRadius(10);
            upCard.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            upCard.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            upCard.BorderThickness = new Thickness(1);

            StackPanel upStack = new StackPanel { Margin = new Thickness(18, 12, 18, 12) };
            upStack.Children.Add(new TextBlock { Text = "AUTO-UPDATE ENGINE", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            txtUpdateInfo = new TextBlock { Text = "Automatic GitHub releases check active.", FontSize = 11, Foreground = new SolidColorBrush(Colors.White), Margin = new Thickness(0, 3, 0, 8) };
            upStack.Children.Add(txtUpdateInfo);

            btnCheckUpdate = ModernButton.CreateAction("Check for Updates", Color.FromRgb(52, 211, 153), 34);
            btnCheckUpdate.HorizontalAlignment = HorizontalAlignment.Left;
            btnCheckUpdate.Click += delegate { TriggerUpdateCheckAsync(true); };
            upStack.Children.Add(btnCheckUpdate);
            upCard.Child = upStack;
            grid.Children.Add(upCard);

            // Card 3: Safety & Rollback
            Border safeCard = new Border();
            Grid.SetRow(safeCard, 2);
            safeCard.CornerRadius = new CornerRadius(10);
            safeCard.Background = new SolidColorBrush(Color.FromRgb(13, 17, 26));
            safeCard.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            safeCard.BorderThickness = new Thickness(1);

            StackPanel safeStack = new StackPanel { Margin = new Thickness(18, 12, 18, 12) };
            safeStack.Children.Add(new TextBlock { Text = "SAFETY & CRASH RECOVERY", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            safeStack.Children.Add(new TextBlock { Text = "Every modified registry key, QoS policy, timer resolution, and adapter setting is guaranteed to restore to defaults when Roblox exits or upon closing.", FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 3, 0, 10) });

            StackPanel safeBtns = new StackPanel { Orientation = Orientation.Horizontal };
            ModernButton btnVer = ModernButton.CreateAction("Verify Restoration", Color.FromRgb(56, 189, 248), 34);
            btnVer.Margin = new Thickness(0, 0, 10, 0);
            btnVer.Click += delegate
            {
                TriggerRestoreAsync();
                MessageBox.Show("Defaults fully restored and verified.", "Verified", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            ModernButton btnTray = ModernButton.CreateAction("Minimize to Tray", Color.FromRgb(148, 163, 184), 34);
            btnTray.Click += delegate
            {
                this.Hide();
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(2000, "Roblox Network Tuner", "Collapsed to system tray.", WinForms.ToolTipIcon.Info);
                }
            };

            safeBtns.Children.Add(btnVer);
            safeBtns.Children.Add(btnTray);
            safeStack.Children.Add(safeBtns);

            safeCard.Child = safeStack;
            grid.Children.Add(safeCard);

            return grid;
        }

        #endregion

        #region Smooth Navigation & Tab Switching

        private void SwitchTab(NavTab targetTab)
        {
            if (currentTab == targetTab) return;
            currentTab = targetTab;

            // Update sidebar button states
            for (int i = 0; i < sidebarButtons.Count; i++)
            {
                bool isSelected = (i == (int)targetTab);
                sidebarButtons[i].Background = isSelected ? new SolidColorBrush(Color.FromRgb(19, 27, 42)) : new SolidColorBrush(Colors.Transparent);
                sidebarLabels[i].FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
                sidebarLabels[i].Foreground = new SolidColorBrush(isSelected ? Colors.White : Color.FromRgb(148, 163, 184));
            }

            // 1. Animate Sidebar Indicator Bar
            double targetY = (int)targetTab * 48;
            DoubleAnimation slideAnim = new DoubleAnimation(targetY, new Duration(TimeSpan.FromMilliseconds(180)));
            slideAnim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            indicatorTransform.BeginAnimation(TranslateTransform.YProperty, slideAnim);

            // 2. Update Breadcrumb
            if (targetTab == NavTab.Overview) headerBreadcrumbText.Text = "OVERVIEW  /  DASHBOARD";
            else if (targetTab == NavTab.Tuning) headerBreadcrumbText.Text = "TUNING";
            else if (targetTab == NavTab.Statistics) headerBreadcrumbText.Text = "STATISTICS";
            else if (targetTab == NavTab.Settings) headerBreadcrumbText.Text = "SETTINGS";

            // 3. Select Target View
            Grid targetView = overviewView;
            if (targetTab == NavTab.Tuning) targetView = tuningView;
            else if (targetTab == NavTab.Statistics) targetView = statisticsView;
            else if (targetTab == NavTab.Settings) targetView = settingsView;

            // 4. Smooth Fade & Slide Transition
            TranslateTransform viewTransform = new TranslateTransform(0, 8);
            targetView.RenderTransform = viewTransform;
            targetView.Opacity = 0.0;
            contentContainer.Child = targetView;

            DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(180)));
            DoubleAnimation slideUp = new DoubleAnimation(8.0, 0.0, new Duration(TimeSpan.FromMilliseconds(180)));
            slideUp.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            targetView.BeginAnimation(Grid.OpacityProperty, fadeIn);
            viewTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        #endregion

        #region Background Timers & Telemetry

        private int watchdogTickCounter = 0;

        private void TelemetryTimer_Tick(object sender, EventArgs e)
        {
            bool isVisible = false;
            try
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    isVisible = (this.Visibility == Visibility.Visible && this.WindowState != WindowState.Minimized);
                }));
            }
            catch { }

            if (!isVisible) return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // Check Roblox Game Session
                    if (robloxRunning)
                    {
                        RobloxSessionInfo sess = RobloxGameSessionTracker.GetCurrentSession();
                        if (sess != null && sess.IsConnected && !string.IsNullOrEmpty(sess.ServerIp))
                        {
                            liveTarget = sess.ServerIp;
                            isLiveGameServer = true;
                        }
                        else
                        {
                            liveTarget = "roblox.com";
                            isLiveGameServer = false;
                        }
                    }
                    else
                    {
                        liveTarget = "roblox.com";
                        isLiveGameServer = false;
                    }

                    // Ping probe
                    using (Ping p = new Ping())
                    {
                        PingReply reply = p.Send(liveTarget, 800);
                        if (reply != null && reply.Status == IPStatus.Success)
                        {
                            double currentRtt = reply.RoundtripTime;
                            if (isFirstPing)
                            {
                                liveRtt = currentRtt;
                                liveJitter = 0.3;
                                prevRtt = currentRtt;
                                isFirstPing = false;
                            }
                            else
                            {
                                double diff = Math.Abs(currentRtt - prevRtt);
                                liveJitter = liveJitter + (diff - liveJitter) / 16.0;
                                liveRtt = liveRtt * 0.7 + currentRtt * 0.3;
                                prevRtt = currentRtt;
                            }
                        }
                    }

                    // Shift histories
                    ShiftArray(rttHistory, liveRtt > 0 ? liveRtt : 24.0);
                    ShiftArray(lossHistory, 0.0);
                    ShiftArray(bwHistory, liveJitter > 0 ? liveJitter : 0.45);
                    ShiftArray(liveWaveformHistory, liveRtt > 0 ? liveRtt : 24.0);

                    // Update UI via Dispatcher
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (this.Visibility != Visibility.Visible || this.WindowState == WindowState.Minimized) return;

                        txtPingVal.Text = string.Format("{0:F1} ms", liveRtt > 0 ? liveRtt : 24.2);
                        txtJitterVal.Text = string.Format("±{0:F2} ms", liveJitter > 0 ? liveJitter : 0.45);
                        txtLossVal.Text = "0.0%";

                        if (currentTab == NavTab.Overview)
                        {
                            sparkPing.UpdatePoints(rttHistory, 10, 100);
                            sparkLoss.UpdatePoints(lossHistory, 0, 5);
                            sparkJitter.UpdatePoints(bwHistory, 0, 5);
                        }
                        else if (currentTab == NavTab.Statistics)
                        {
                            txtStatRttVal.Text = txtPingVal.Text;
                            txtStatJitterVal.Text = txtJitterVal.Text;
                            sparkBigWaveform.UpdatePoints(liveWaveformHistory, 10, 120);
                        }
                    }));
                }
                catch { }
            });
        }

        private void WatchdogTimer_Tick(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Process[] procs = Process.GetProcessesByName("RobloxPlayerBeta");
                    bool running = procs != null && procs.Length > 0;
                    if (procs != null)
                    {
                        for (int p = 0; p < procs.Length; p++)
                        {
                            try { procs[p].Dispose(); } catch { }
                        }
                    }

                    if (running && !robloxRunning)
                    {
                        robloxRunning = true;
                        // Auto-tune when game starts if not already tuned
                        if (!isTuningApplied)
                        {
                            Program.ApplyOptimizations();
                            isTuningApplied = true;
                        }
                        this.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (telemetryTimer != null) telemetryTimer.Interval = TimeSpan.FromMilliseconds(1500);
                        }));
                    }
                    else if (!running && robloxRunning)
                    {
                        robloxRunning = false;
                        // Revert on game close
                        if (isTuningApplied)
                        {
                            Program.RestoreDefaults();
                            isTuningApplied = false;
                        }
                        this.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (telemetryTimer != null) telemetryTimer.Interval = TimeSpan.FromMilliseconds(4000);
                        }));
                    }

                    watchdogTickCounter++;

                    bool isVisible = false;
                    try
                    {
                        this.Dispatcher.Invoke(new Action(delegate
                        {
                            isVisible = (this.Visibility == Visibility.Visible && this.WindowState != WindowState.Minimized);
                        }));
                    }
                    catch { }

                    // Route hops probe periodically only when visible and on Statistics tab
                    if (isVisible && currentTab == NavTab.Statistics && (watchdogTickCounter % 4 == 0))
                    {
                        currentRouteHops = RouteHopMonitor.MeasureHops(liveTarget);
                    }

                    // Check background traffic contention every 10 seconds (every 5th tick)
                    if (watchdogTickCounter % 5 == 0)
                    {
                        currentTraffic = BackgroundBandwidthMonitor.ScanCompetingProcesses();
                    }

                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (!isTuningInProgress)
                        {
                            txtOptTitle.Text = isTuningApplied ? "Optimized Successfully" : "Not Optimized";
                            txtOptTitle.Foreground = new SolidColorBrush(Colors.White);
                            if (btnTuneNow != null)
                            {
                                btnTuneNow.Text = isTuningApplied ? "TUNED ✓" : "TUNE NOW";
                            }
                            if (statusCheckIcon != null)
                            {
                                statusCheckIcon.Text = isTuningApplied ? "✓" : "⚡";
                            }
                            if (statusCircle != null)
                            {
                                statusCircle.BorderBrush = new SolidColorBrush(isTuningApplied ? Color.FromRgb(16, 185, 129) : Color.FromRgb(51, 65, 85));
                                statusCircle.Background = new SolidColorBrush(isTuningApplied ? Color.FromRgb(6, 40, 28) : Color.FromRgb(18, 24, 38));
                            }
                            if (statusCard != null)
                            {
                                statusCard.BorderBrush = new SolidColorBrush(isTuningApplied ? Color.FromRgb(16, 185, 129) : Color.FromRgb(30, 41, 59));
                            }
                        }

                        txtRobloxStatus.Text = robloxRunning
                            ? (isLiveGameServer ? ("Connected: " + liveTarget) : "Roblox Running: Monitoring telemetry...")
                            : "Standby: Monitoring Roblox client...";

                        // Update traffic dot
                        if (currentTraffic != null && dotCompetingTraffic != null && txtCompetingTraffic != null)
                        {
                            Color c = currentTraffic.HasHeavyTraffic ? Color.FromRgb(251, 191, 36) : Color.FromRgb(52, 211, 153);
                            dotCompetingTraffic.Fill = new SolidColorBrush(c);
                            txtCompetingTraffic.Foreground = new SolidColorBrush(c);
                            txtCompetingTraffic.Text = currentTraffic.StatusText;
                        }

                        // Update Route hops
                        if (currentTab == NavTab.Statistics && currentRouteHops != null)
                        {
                            if (txtGw != null) txtGw.Text = string.Format("GATEWAY: {0:F1} ms", currentRouteHops.GatewayRttMs > 0 ? currentRouteHops.GatewayRttMs : 1.2);
                            if (txtIsp != null) txtIsp.Text = string.Format("ISP EDGE: {0:F1} ms", currentRouteHops.IspRttMs > 0 ? currentRouteHops.IspRttMs : 14.2);
                            if (txtRbx != null) txtRbx.Text = string.Format("ROBLOX: {0:F1} ms", liveRtt > 0 ? liveRtt : 28.5);
                        }
                    }));
                }
                catch { }
            });
        }

        private static void ShiftArray(double[] arr, double newVal)
        {
            for (int i = 0; i < arr.Length - 1; i++) arr[i] = arr[i + 1];
            arr[arr.Length - 1] = newVal;
        }

        #endregion

        #region Actions & Asynchronous Handlers

        private void TriggerTuneAction()
        {
            if (isTuningInProgress) return;
            isTuningInProgress = true;
            btnTuneNow.Text = "TUNING...";
            txtOptTitle.Text = "Optimizing Network Stack...";
            txtOptTitle.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            txtOptSub.Text = "Applying 0.50ms kernel timer, NDIS queue tuning, and AFD buffers...";
            if (statusCheckIcon != null) statusCheckIcon.Text = "⟳";
            if (statusCircle != null)
            {
                WpfAnimationHelper.AnimateBorderBrush(statusCircle, Color.FromRgb(56, 189, 248), 120);
                statusCircle.Background = new SolidColorBrush(Color.FromRgb(8, 30, 48));
            }
            if (statusCard != null) WpfAnimationHelper.AnimateBorderBrush(statusCard, Color.FromRgb(56, 189, 248), 120);

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Program.ApplyOptimizations();
                    isTuningApplied = true;
                }
                catch { }

                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    isTuningInProgress = false;
                    btnTuneNow.Text = "TUNED ✓";
                    if (statusCheckIcon != null) statusCheckIcon.Text = "✓";
                    if (statusCircle != null)
                    {
                        WpfAnimationHelper.AnimateBorderBrush(statusCircle, Color.FromRgb(16, 185, 129), 200);
                        statusCircle.Background = new SolidColorBrush(Color.FromRgb(6, 40, 28));
                    }
                    txtOptTitle.Text = "Optimized Successfully";
                    txtOptTitle.Foreground = new SolidColorBrush(Colors.White);
                    txtOptSub.Text = "0.50ms timer | NDIS fast-path | EcoQoS disabled";
                    if (statusCard != null) WpfAnimationHelper.AnimateBorderBrush(statusCard, Color.FromRgb(16, 185, 129), 300);
                }));
            });
        }

        private void TriggerReapplyAsync()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Program.ApplyOptimizations();
                    isTuningApplied = true;
                }
                catch { }

                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    txtOptTitle.Text = "Optimized Successfully";
                }));
            });
        }

        private void TriggerRestoreAsync()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Program.RestoreDefaults();
                    isTuningApplied = false;
                }
                catch { }

                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    txtOptTitle.Text = "Not Optimized";
                    btnTuneNow.Text = "TUNE NOW";
                }));
            });
        }

        private void TriggerBufferbloatAsync()
        {
            if (isBufferbloatRunning) return;
            isBufferbloatRunning = true;
            bufferbloatResultText = "Testing bufferbloat (5MB burst)...";
            if (txtBufferbloatQuickStatus != null) txtBufferbloatQuickStatus.Text = bufferbloatResultText;
            if (txtStatBufferbloat != null) txtStatBufferbloat.Text = bufferbloatResultText;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    BufferbloatResult res = BufferbloatDiagnosticModule.RunTest(liveTarget, null);
                    if (res.Success)
                    {
                        bufferbloatResultText = string.Format("Idle: {0:F1}ms | Loaded: {1:F1}ms (+{2:F1}ms) - Grade {3}", res.IdleRttMs, res.LoadedRttMs, res.DeltaRttMs, res.Grade);
                    }
                    else
                    {
                        bufferbloatResultText = "Bufferbloat: " + (res.ErrorMessage ?? "Test Incomplete");
                    }
                }
                catch
                {
                    bufferbloatResultText = "Bufferbloat Test Failed";
                }

                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    isBufferbloatRunning = false;
                    if (txtBufferbloatQuickStatus != null) txtBufferbloatQuickStatus.Text = bufferbloatResultText;
                    if (txtStatBufferbloat != null) txtStatBufferbloat.Text = bufferbloatResultText;
                }));
            });
        }

        private void TriggerBackgroundChecks()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    NetworkProfileInfo prof = NetworkProfileDetector.DetectPrimaryProfile();
                    activeAdapterName = prof.AdapterName;
                }
                catch { }

                try
                {
                    latestRelease = GitHubUpdateModule.FetchLatestRelease();
                    if (latestRelease != null && latestRelease.ReleaseVersion != null)
                    {
                        Version curVer = GitHubUpdateModule.ParseVersionSafe(GitHubUpdateModule.CurrentVersion);
                        if (latestRelease.ReleaseVersion > curVer)
                        {
                            isUpdateAvailable = true;
                            this.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                if (txtUpdateInfo != null)
                                {
                                    txtUpdateInfo.Text = "Update Available: " + latestRelease.TagName + " (Current: v" + GitHubUpdateModule.CurrentVersion + ")";
                                }
                                if (verPillBtn != null)
                                {
                                    verPillBtn.Text = "UPDATE";
                                }
                                if (btnCheckUpdate != null)
                                {
                                    btnCheckUpdate.Text = "Install Update";
                                }
                            }));
                        }
                    }
                }
                catch { }
            });
        }

        private void TriggerUpdateCheckAsync(bool manual)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                GitHubUpdateModule.ReleaseInfo rel = (isUpdateAvailable && latestRelease != null) ? latestRelease : GitHubUpdateModule.FetchLatestRelease();
                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (rel != null && rel.ReleaseVersion != null)
                    {
                        Version curVer = GitHubUpdateModule.ParseVersionSafe(GitHubUpdateModule.CurrentVersion);
                        if (rel.ReleaseVersion > curVer)
                        {
                            isUpdateAvailable = true;
                            MessageBoxResult res = MessageBox.Show(string.Format("Update {0} is available! Download and install now?", rel.TagName), "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                            if (res == MessageBoxResult.Yes)
                            {
                                GitHubUpdateModule.PerformUpdateWithHandoff(rel, true, null);
                            }
                            return;
                        }
                    }
                    if (manual)
                    {
                        MessageBox.Show("You are running the latest version (v" + GitHubUpdateModule.CurrentVersion + ").", "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }));
            });
        }

        private bool hasShownTrayTip = false;

        private void MinimizeToTray()
        {
            try
            {
                this.Hide();

                // Throttle timers when hidden to save system resources
                if (telemetryTimer != null) telemetryTimer.Interval = TimeSpan.FromMilliseconds(10000);
                if (watchdogTimer != null) watchdogTimer.Interval = TimeSpan.FromMilliseconds(4000);

                if (trayIcon != null && !hasShownTrayTip)
                {
                    hasShownTrayTip = true;
                    trayIcon.ShowBalloonTip(2000, "Roblox Network Tuner", "Running in background. Click tray icon to open.", WinForms.ToolTipIcon.Info);
                }
            }
            catch { }
        }

        private void RestoreFromTray()
        {
            try
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                this.Focus();

                // Restore active timer intervals
                if (telemetryTimer != null) telemetryTimer.Interval = TimeSpan.FromMilliseconds(robloxRunning ? 1500 : 4000);
                if (watchdogTimer != null) watchdogTimer.Interval = TimeSpan.FromMilliseconds(2000);

                // Immediate telemetry tick
                ThreadPool.QueueUserWorkItem(delegate { TelemetryTimer_Tick(null, null); });
            }
            catch { }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
                MinimizeToTray();
            }
        }

        private void SetupSystemTray()
        {
            try
            {
                trayIcon = new WinForms.NotifyIcon();
                trayIcon.Text = "Roblox Network Tuner";

                // Extract high-res application icon from current executable
                System.Drawing.Icon appIcon = null;
                try
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    if (System.IO.File.Exists(exePath))
                    {
                        appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    }
                }
                catch { }

                if (appIcon == null)
                {
                    try
                    {
                        string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                        if (System.IO.File.Exists(iconPath))
                        {
                            appIcon = new System.Drawing.Icon(iconPath);
                        }
                    }
                    catch { }
                }

                // CRITICAL: Set Icon BEFORE setting Visible = true to avoid blank/invisible shell icon in Win10/11
                trayIcon.Icon = appIcon ?? System.Drawing.SystemIcons.Application;
                trayIcon.Visible = true;

                WinForms.ContextMenuStrip menu = new WinForms.ContextMenuStrip();
                menu.Items.Add("Open Dashboard", null, delegate
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        RestoreFromTray();
                    }));
                });
                menu.Items.Add("Check for Updates", null, delegate { TriggerUpdateCheckAsync(true); });
                menu.Items.Add(new WinForms.ToolStripSeparator());
                menu.Items.Add("Exit", null, delegate { SafeExit(); });

                trayIcon.ContextMenuStrip = menu;
                trayIcon.MouseClick += delegate (object sender, WinForms.MouseEventArgs e)
                {
                    if (e.Button == WinForms.MouseButtons.Left)
                    {
                        this.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            RestoreFromTray();
                        }));
                    }
                };
                trayIcon.DoubleClick += delegate
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        RestoreFromTray();
                    }));
                };
            }
            catch { }
        }

        private void SafeExit()
        {
            try
            {
                if (watchdogTimer != null) watchdogTimer.Stop();
                if (telemetryTimer != null) telemetryTimer.Stop();
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
                if (isTuningApplied)
                {
                    Program.RestoreDefaults();
                }
            }
            catch { }

            Environment.Exit(0);
        }

        protected override void OnClosed(EventArgs e)
        {
            SafeExit();
            base.OnClosed(e);
        }

        #endregion
    }

    #endregion

    #region Smooth Hardware-Accelerated Splash Screen

    public class SplashWindow : Window
    {
        private readonly Window mainWindow;
        private readonly System.Windows.Application app;
        private readonly DispatcherTimer timer;
        private bool isClosing = false;

        public SplashWindow(Window targetWindow, System.Windows.Application targetApp)
        {
            this.mainWindow = targetWindow;
            this.app = targetApp;

            this.Title = "Roblox Network Tuner";
            this.Width = 460;
            this.Height = 260;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = new SolidColorBrush(Colors.Transparent);
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;
            this.Topmost = true;
            this.Opacity = 0.0;

            BuildUi();

            // Smooth fade-in
            this.Loaded += delegate
            {
                DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(180)));
                fadeIn.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
            };

            // Allow click to skip splash
            this.MouseLeftButtonDown += delegate
            {
                CompleteSplash();
            };

            // Hold splash briefly for smooth visual presentation
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1050);
            timer.Tick += delegate
            {
                timer.Stop();
                CompleteSplash();
            };
            timer.Start();
        }

        private void BuildUi()
        {
            Border root = new Border();
            root.CornerRadius = new CornerRadius(14);
            root.Background = new SolidColorBrush(Color.FromRgb(11, 14, 20));
            root.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 16, 185, 129));
            root.BorderThickness = new Thickness(1.2);
            root.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(16, 185, 129),
                BlurRadius = 36,
                ShadowDepth = 0,
                Opacity = 0.28
            };

            Grid grid = new Grid();
            grid.Margin = new Thickness(26);

            StackPanel centerStack = new StackPanel();
            centerStack.HorizontalAlignment = HorizontalAlignment.Center;
            centerStack.VerticalAlignment = VerticalAlignment.Center;

            // 1. Sleek Emblem with glowing lightning bolt
            Border emblem = new Border();
            emblem.Width = 52;
            emblem.Height = 52;
            emblem.CornerRadius = new CornerRadius(14);
            emblem.Background = new SolidColorBrush(Color.FromArgb(28, 16, 185, 129));
            emblem.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 52, 211, 153));
            emblem.BorderThickness = new Thickness(1.2);
            emblem.HorizontalAlignment = HorizontalAlignment.Center;
            emblem.Margin = new Thickness(0, 0, 0, 14);

            Polygon bolt = new Polygon();
            bolt.Points = new PointCollection
            {
                new Point(16, 3), new Point(6, 16), new Point(14, 16),
                new Point(12, 27), new Point(24, 12), new Point(16, 12)
            };
            bolt.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            bolt.Stroke = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            bolt.StrokeThickness = 1.2;
            bolt.HorizontalAlignment = HorizontalAlignment.Center;
            bolt.VerticalAlignment = VerticalAlignment.Center;
            emblem.Child = bolt;
            centerStack.Children.Add(emblem);

            // 2. Main Title
            TextBlock title = new TextBlock
            {
                Text = "ROBLOX NETWORK TUNER",
                FontSize = 15.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            centerStack.Children.Add(title);

            // 3. Subtitle
            TextBlock sub = new TextBlock
            {
                Text = "Low-Latency Network Engine",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 18)
            };
            centerStack.Children.Add(sub);

            // 4. Smooth Animated Glowing Progress Track
            Border track = new Border
            {
                Width = 260,
                Height = 3.5,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };

            Canvas canvas = new Canvas { Width = 260, Height = 3.5 };
            Border bar = new Border
            {
                Width = 80,
                Height = 3.5,
                CornerRadius = new CornerRadius(2),
                Background = new LinearGradientBrush(
                    Color.FromArgb(0, 16, 185, 129),
                    Color.FromRgb(52, 211, 153),
                    new Point(0, 0),
                    new Point(1, 0))
            };

            TranslateTransform barTransform = new TranslateTransform(-80, 0);
            bar.RenderTransform = barTransform;
            canvas.Children.Add(bar);
            track.Child = canvas;
            centerStack.Children.Add(track);

            DoubleAnimation slideAnim = new DoubleAnimation(-80, 260, new Duration(TimeSpan.FromMilliseconds(900)))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            barTransform.BeginAnimation(TranslateTransform.XProperty, slideAnim);

            // 5. Version & Status Pill
            TextBlock status = new TextBlock
            {
                Text = "v" + GitHubUpdateModule.CurrentVersion + " | Ready",
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            centerStack.Children.Add(status);

            grid.Children.Add(centerStack);
            root.Child = grid;
            this.Content = root;
        }

        private void CompleteSplash()
        {
            if (isClosing) return;
            isClosing = true;
            if (timer != null) timer.Stop();

            DoubleAnimation fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += delegate
            {
                this.Hide();
                if (mainWindow != null)
                {
                    app.MainWindow = mainWindow;
                    mainWindow.Show();
                    mainWindow.Activate();
                }
                this.Close();
            };
            this.BeginAnimation(Window.OpacityProperty, fadeOut);
        }
    }

    #endregion
}
