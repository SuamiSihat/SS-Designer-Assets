using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using SS_CAM.Models;
using SS_CAM.Services;
using SS_CAM.Views;

namespace SS_CAM
{
    public partial class MainWindow : FluentWindow
    {
        private class AnimShapeItem
        {
            public Ellipse Shape { get; set; }
            public double VX { get; set; }
            public double VY { get; set; }
            public double Diameter { get; set; }
        }

        private DispatcherTimer headerAnimTimer;
        private List<AnimShapeItem> animItems;
        private UserProfile currentProfile;
        private List<DesignerFolderItem> _popoverCachedProjects;
        private bool _popoverUpdating = false;

        public MainWindow()
        {
            InitializeComponent();
            ThemeService.ThemeChanged += OnThemeModeChanged;
            NotificationService.OnNotificationReceived += OnNotificationReceived;
            NotificationService.OnHistoryUpdated += OnNotificationHistoryUpdated;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                App.LogTrace("MainWindow: OnLoaded started");
                Title = "SuamiSihat Creative Assets Management " + Services.AppVersion.DisplayVersion;
                WindowState = WindowState.Normal;
                Width = 1280;
                Height = 820;
                Activate();

                // 1. Initialize Header Geometric Loop Animation
                try { InitHeaderAnimation(); App.LogTrace("MainWindow: InitHeaderAnimation done"); }
                catch (Exception ex) { App.LogTrace("MainWindow: InitHeaderAnimation error: " + ex.Message); }

                // 2. Play Intro Sound Effect on App Launch
                try { AudioFeedbackService.PlayIntroSound(); App.LogTrace("MainWindow: PlayIntroSound done"); }
                catch (Exception ex) { App.LogTrace("MainWindow: PlayIntroSound error: " + ex.Message); }

                // 3. Load Designer Profile & Check First-Run Setup
                try
                {
                    RefreshProfileUI();
                    CheckFirstRunProfileSetup();
                    App.LogTrace("MainWindow: ProfileSetup done");
                }
                catch (Exception ex) { App.LogTrace("MainWindow: Profile setup error: " + ex.Message); }

                // 4. Initialize Real-Time Footer Status Bar Timer & NAS Online Check
                try
                {
                    InitNasHealthCheck();
                    InitUpdateCheck();
                    InitRadioStatusListeners();
                    InitVisualizerListeners();
                    App.LogTrace("MainWindow: Listeners done");
                }
                catch (Exception ex) { App.LogTrace("MainWindow: Listeners error: " + ex.Message); }

                // 5. Apply Theme on Launch (loads saved theme from ThemeService)
                try { ThemeService.ApplyTheme(ThemeService.CurrentTheme); App.LogTrace("MainWindow: ApplyTheme done"); }
                catch (Exception ex) { App.LogTrace("MainWindow: ApplyTheme error: " + ex.Message); }

                // 6. Start Background NAS File Watcher
                try
                {
                    if (currentProfile != null && !string.IsNullOrWhiteSpace(currentProfile.WorkspaceRoot))
                    {
                        WorkspaceWatcherService.Instance.Start(currentProfile.WorkspaceRoot);
                        App.LogTrace("MainWindow: WorkspaceWatcher started");
                    }
                }
                catch (Exception ex) { App.LogTrace("MainWindow: WorkspaceWatcher error: " + ex.Message); }

                // 6b. Initialize Live Work Session Tracker
                try
                {
                    InitWorkSessionTracker();
                    App.LogTrace("MainWindow: WorkSessionTracker initialized");
                }
                catch (Exception ex) { App.LogTrace("MainWindow: WorkSessionTracker error: " + ex.Message); }

                // 7. Navigate to Dashboard on startup
                try { RootNavigation.Navigate(typeof(DashboardPage)); App.LogTrace("MainWindow: Navigated to DashboardPage"); }
                catch (Exception ex) { App.LogTrace("MainWindow: Navigate Dashboard error: " + ex.Message); }

                // 8. Trigger Welcome Toast Notification
                try
                {
                    string displayName = (currentProfile != null && !string.IsNullOrWhiteSpace(currentProfile.DesignerName)) ? currentProfile.DesignerName : "designer";
                    NotificationService.ShowSuccess("SuamiSihat CAM Ready", "Workstation initialized. Welcome back, " + displayName + "!");
                    App.LogTrace("MainWindow: Welcome notification done");
                }
                catch (Exception ex) { App.LogTrace("MainWindow: Welcome notification error: " + ex.Message); }

                App.LogTrace("MainWindow: OnLoaded finished completely");
            }
            catch (Exception fatalEx)
            {
                App.LogTrace("MainWindow: Fatal OnLoaded exception: " + fatalEx);
            }
        }

        // ─── Global Mouse Wheel Scroll Handler ────────────────────────────────────
        // Fixes WPF mouse-wheel-only-by-dragging-scrollbar issue.
        // Walks visual tree from source element upward to find the nearest
        // ScrollViewer with scrollable content and performs LineDown/LineUp scrolling.
        private void OnGlobalPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (e.Handled) return;
                var element = e.OriginalSource as DependencyObject;
                while (element != null)
                {
                    var sv = element as ScrollViewer;
                    if (sv != null)
                    {
                        bool canScrollV = sv.ScrollableHeight > 0;
                        bool canScrollH = sv.ScrollableWidth > 0;

                        if (canScrollV || canScrollH)
                        {
                            int steps = Math.Max(1, Math.Min(8, Math.Abs(e.Delta) / 30));
                            if (canScrollV)
                            {
                                if (e.Delta < 0) for (int i = 0; i < steps; i++) sv.LineDown();
                                else for (int i = 0; i < steps; i++) sv.LineUp();
                            }
                            else if (canScrollH)
                            {
                                if (e.Delta < 0) for (int i = 0; i < steps; i++) sv.LineRight();
                                else for (int i = 0; i < steps; i++) sv.LineLeft();
                            }
                            e.Handled = true;
                            return;
                        }
                    }
                    element = VisualTreeHelper.GetParent(element);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] OnGlobalPreviewMouseWheel: " + ex.Message); }
        }

        private void InitRadioStatusListeners()
        {
            var radio = RadioStreamService.Instance;
            radio.PlaybackStateChanged += OnRadioPlaybackStateChanged;
            radio.StationChanged += OnRadioStationChanged;
            radio.StreamTitleChanged += OnRadioStreamTitleChanged;

            UpdateRadioStatusUI();
            InitializeRadioSpectrumAnimator();
        }

        private void OnRadioPlaybackStateChanged(RadioPlaybackState state)
        {
            UpdateRadioStatusUI();
        }

        private void OnRadioStationChanged(RadioStation station)
        {
            UpdateRadioStatusUI();
        }

        private void OnRadioStreamTitleChanged(string title)
        {
            UpdateRadioStatusUI();
        }

        private void UpdateRadioStatusUI()
        {
            try
            {
                var radio = RadioStreamService.Instance;
                if (radio == null) return;

                bool isPlaying = (radio.State == RadioPlaybackState.Playing);
                bool isBuffering = (radio.State == RadioPlaybackState.Buffering);
                bool isPaused = (radio.State == RadioPlaybackState.Paused);

                string stationName = radio.CurrentStation != null ? radio.CurrentStation.Name : "Radio Stream";
                string streamTitle = radio.LocalProxy != null ? radio.LocalProxy.CurrentStreamTitle : null;
                string nowPlayingTitle = !string.IsNullOrWhiteSpace(streamTitle) ? streamTitle.Trim() : stationName;

                // Option B: Footer Mini-Widget Status - displays Now Playing Title only
                if (StatusRadioText != null)
                {
                    if (isPlaying)
                        StatusRadioText.Text = nowPlayingTitle;
                    else if (isBuffering)
                        StatusRadioText.Text = "Connecting...";
                    else if (isPaused)
                        StatusRadioText.Text = "Paused";
                    else
                        StatusRadioText.Text = "Ready";
                }

                if (FooterRadioPanel != null)
                {
                    if (isPlaying)
                    {
                        if (!string.IsNullOrWhiteSpace(streamTitle) && !string.Equals(streamTitle.Trim(), stationName, StringComparison.OrdinalIgnoreCase))
                            FooterRadioPanel.ToolTip = "Now Playing: " + streamTitle.Trim() + "\nStation: " + stationName + "\nClick to open audio controls";
                        else
                            FooterRadioPanel.ToolTip = "Now Playing: " + stationName + "\nClick to open audio controls";
                    }
                    else
                    {
                        FooterRadioPanel.ToolTip = "Studio Audio (Click to open controls)";
                    }
                }

                if (StatusRadioIcon != null)
                {
                    if (isPlaying)
                    {
                        StatusRadioIcon.Text = "\uE767";
                        var brandBrush = TryFindResource("FluentBrand80") as Brush;
                        if (brandBrush != null) StatusRadioIcon.Foreground = brandBrush;
                    }
                    else if (isBuffering)
                    {
                        StatusRadioIcon.Text = "\uE823";
                        var secBrush = TryFindResource("TextFillColorSecondaryBrush") as Brush;
                        if (secBrush != null) StatusRadioIcon.Foreground = secBrush;
                    }
                    else
                    {
                        StatusRadioIcon.Text = "\uE767";
                        var defaultBrush = TryFindResource("SidebarTextSecondaryBrush") as Brush;
                        if (defaultBrush != null) StatusRadioIcon.Foreground = defaultBrush;
                    }
                }

                // Option B: Flyout Popover Status
                if (TxtFlyoutStation != null)
                {
                    TxtFlyoutStation.Text = stationName;
                }
                if (TxtFlyoutStatus != null)
                {
                    if (isPlaying)
                    {
                        TxtFlyoutStatus.Text = !string.IsNullOrWhiteSpace(streamTitle) ? "♫ " + streamTitle.Trim() : "Streaming: " + stationName;
                    }
                    else if (isBuffering)
                    {
                        TxtFlyoutStatus.Text = "Connecting to stream...";
                    }
                    else if (isPaused)
                    {
                        TxtFlyoutStatus.Text = "Playback paused";
                    }
                    else
                    {
                        TxtFlyoutStatus.Text = "Ready to stream";
                    }
                }
                if (TxtFlyoutPlayIcon != null)
                {
                    if (isPlaying)
                        TxtFlyoutPlayIcon.Text = "\uE769"; // Pause icon
                    else if (isBuffering)
                        TxtFlyoutPlayIcon.Text = "\uE823"; // Buffering
                    else
                        TxtFlyoutPlayIcon.Text = "\uE768"; // Play icon
                }
                if (SliderFlyoutVolume != null)
                {
                    double volPercent = radio.Volume * 100.0;
                    if (Math.Abs(SliderFlyoutVolume.Value - volPercent) > 1.0)
                    {
                        SliderFlyoutVolume.Value = volPercent;
                    }
                }

                if (BottomRadioPlayerBar != null)
                {
                    // Reclaim vertical canvas space: keep bottom bar collapsed by default
                    BottomRadioPlayerBar.Visibility = Visibility.Collapsed;
                }
                
                string emoji = radio.CurrentStation != null ? radio.CurrentStation.IconEmoji : "📻";
                
                if (TxtBottomRadioTitle != null) TxtBottomRadioTitle.Text = stationName;

                // Load station cover image if available
                if (radio.CurrentStation != null && ImgBottomRadioCover != null)
                {
                    string coverPath = radio.CurrentStation.HasLocalCover ? radio.CurrentStation.LocalCoverPath : radio.CurrentStation.CoverImageUrl;
                    if (!string.IsNullOrWhiteSpace(coverPath))
                    {
                        try
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(coverPath, UriKind.RelativeOrAbsolute);
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            ImgBottomRadioCover.Source = bitmap;
                            ImgBottomRadioCover.Visibility = Visibility.Visible;
                            if (TxtBottomRadioEmoji != null) TxtBottomRadioEmoji.Visibility = Visibility.Collapsed;
                        }
                        catch
                        {
                            ImgBottomRadioCover.Visibility = Visibility.Collapsed;
                            if (TxtBottomRadioEmoji != null) TxtBottomRadioEmoji.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        ImgBottomRadioCover.Visibility = Visibility.Collapsed;
                        if (TxtBottomRadioEmoji != null) TxtBottomRadioEmoji.Visibility = Visibility.Visible;
                    }
                }
                
                if (radio.State == RadioPlaybackState.Playing)
                {
                    if (TxtBottomRadioPlayIcon != null) TxtBottomRadioPlayIcon.Text = "\uE769";
                    string trackText = !string.IsNullOrEmpty(streamTitle) ? streamTitle : "Live Audio Stream";
                    if (TxtBottomRadioTrack != null) TxtBottomRadioTrack.Text = trackText;
                }
                else if (radio.State == RadioPlaybackState.Buffering)
                {
                    if (TxtBottomRadioPlayIcon != null) TxtBottomRadioPlayIcon.Text = "\uE823";
                    if (TxtBottomRadioTrack != null) TxtBottomRadioTrack.Text = "Connecting & Buffering Stream...";
                }
                else
                {
                    if (TxtBottomRadioPlayIcon != null) TxtBottomRadioPlayIcon.Text = "\uE768";
                    if (TxtBottomRadioTrack != null) TxtBottomRadioTrack.Text = "Paused";
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] UpdateRadioStatusUI: " + ex.Message); }
        }

        private void OnBottomRadioVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                RadioStreamService.Instance.Volume = e.NewValue / 100.0;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] OnBottomRadioVolumeChanged: " + ex.Message); }
        }

        private void OnStatusRadioPlayToggle(object sender, RoutedEventArgs e)
        {
            RadioStreamService.Instance.TogglePlayPause();
            e.Handled = true;
        }


        private void OnFooterRadioClicked(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (RadioFlyoutPopup != null)
                {
                    RadioFlyoutPopup.IsOpen = !RadioFlyoutPopup.IsOpen;
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] OnFooterRadioClicked: " + ex.Message);
            }
        }

        private void OnFlyoutOpenFullRadioPage(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RadioFlyoutPopup != null)
                {
                    RadioFlyoutPopup.IsOpen = false;
                }
                RootNavigation.Navigate(typeof(RadioPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] OnFlyoutOpenFullRadioPage: " + ex.Message);
            }
        }

        private void OnFlyoutPlayToggleClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                RadioStreamService.Instance.TogglePlayPause();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] OnFlyoutPlayToggleClicked: " + ex.Message);
            }
        }

        private void OnFlyoutVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                RadioStreamService.Instance.Volume = e.NewValue / 100.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] OnFlyoutVolumeChanged: " + ex.Message);
            }
        }

        private void OnTitleBarNavToggleClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RootNavigation != null)
                {
                    RootNavigation.IsPaneOpen = !RootNavigation.IsPaneOpen;
                    ApplyPaneCollapseState(RootNavigation.IsPaneOpen);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] OnTitleBarNavToggleClicked: " + ex.Message); }
        }

        private void OnNavigationPaneOpened(NavigationView sender, RoutedEventArgs args)
        {
            ApplyPaneCollapseState(true);
        }

        private void OnNavigationPaneClosed(NavigationView sender, RoutedEventArgs args)
        {
            ApplyPaneCollapseState(false);
        }

        private void ApplyPaneCollapseState(bool isPaneOpen)
        {
            try
            {
                if (StatusNasText != null) StatusNasText.Visibility = isPaneOpen ? Visibility.Visible : Visibility.Collapsed;
                if (StatusTimerText != null) StatusTimerText.Visibility = isPaneOpen ? Visibility.Visible : Visibility.Collapsed;
                if (StatusRadioText != null) StatusRadioText.Visibility = isPaneOpen ? Visibility.Visible : Visibility.Collapsed;
                if (StatusThemeText != null) StatusThemeText.Visibility = isPaneOpen ? Visibility.Visible : Visibility.Collapsed;

                if (FooterNasPanel != null)
                {
                    FooterNasPanel.Margin = isPaneOpen ? new Thickness(16, 4, 16, 4) : new Thickness(0, 6, 0, 6);
                    FooterNasPanel.HorizontalAlignment = isPaneOpen ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                }
                if (NasStatusDot != null)
                {
                    NasStatusDot.Margin = isPaneOpen ? new Thickness(0, 0, 8, 0) : new Thickness(0);
                }

                if (FooterTimerPanel != null)
                {
                    FooterTimerPanel.Margin = isPaneOpen ? new Thickness(16, 4, 16, 4) : new Thickness(0, 6, 0, 6);
                    FooterTimerPanel.HorizontalAlignment = isPaneOpen ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                }
                if (StatusTimerIcon != null)
                {
                    StatusTimerIcon.Margin = isPaneOpen ? new Thickness(0, 0, 7, 0) : new Thickness(0);
                }

                if (FooterRadioPanel != null)
                {
                    FooterRadioPanel.Margin = isPaneOpen ? new Thickness(16, 4, 16, 4) : new Thickness(0, 6, 0, 6);
                    FooterRadioPanel.HorizontalAlignment = isPaneOpen ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                }
                if (StatusRadioIcon != null)
                {
                    StatusRadioIcon.Margin = isPaneOpen ? new Thickness(0, 0, 7, 0) : new Thickness(0);
                }

                if (FooterThemePanel != null)
                {
                    FooterThemePanel.Margin = isPaneOpen ? new Thickness(16, 4, 16, 4) : new Thickness(0, 6, 0, 6);
                    FooterThemePanel.HorizontalAlignment = isPaneOpen ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                }
                if (StatusThemeIcon != null)
                {
                    StatusThemeIcon.Margin = isPaneOpen ? new Thickness(0, 0, 7, 0) : new Thickness(0);
                }

                if (BottomRadioVolumePanel != null)
                {
                    BottomRadioVolumePanel.Visibility = isPaneOpen ? Visibility.Visible : Visibility.Collapsed;
                }

                if (BottomRadioNowPlayingLabel != null)
                {
                    BottomRadioNowPlayingLabel.Visibility = isPaneOpen ? Visibility.Visible : Visibility.Collapsed;
                }

                if (BottomRadioPlayerBar != null)
                {
                    BottomRadioPlayerBar.Padding = isPaneOpen ? new Thickness(16, 10, 16, 10) : new Thickness(12, 6, 12, 6);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] ApplyPaneCollapseState: " + ex.Message);
            }
        }

        private void OnTitleBarDragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] DragMove: " + ex.Message); }
            }
        }

        private void OnVersionBadgeClicked(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (RootNavigation != null)
                {
                    RootNavigation.Navigate(typeof(WorkstationHealthPage));
                }
                e.Handled = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] OnVersionBadgeClicked: " + ex.Message); }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ThemeService.ThemeChanged -= OnThemeModeChanged;
            NotificationService.OnNotificationReceived -= OnNotificationReceived;
            NotificationService.OnHistoryUpdated -= OnNotificationHistoryUpdated;

            if (headerAnimTimer != null)
            {
                headerAnimTimer.Stop();
            }
            
            if (nasCheckTimer != null)
            {
                nasCheckTimer.Stop();
            }

            WorkspaceWatcherService.Instance.Stop();
        }

        public void NavigateTo(Type pageType, object dummy = null)
{
    RootNavigation.Navigate(pageType);
}

        public void RefreshProfileUI()
        {
            currentProfile = UserProfileService.LoadProfile();

            // Update sidebar persona name & department
            if (SidebarPersonaName != null)
                SidebarPersonaName.Text = currentProfile.DesignerName ?? "Designer";
            if (SidebarPersonaDept != null)
                SidebarPersonaDept.Text = currentProfile.Department ?? "Creative Department";

            // Initials from first letter of designer name
            if (SidebarAvatarInitials != null)
            {
                string name = currentProfile.DesignerName ?? "D";
                SidebarAvatarInitials.Text = name.Length > 0 ? name[0].ToString().ToUpper() : "D";
            }

            // Update avatar photo if set
            if (!string.IsNullOrWhiteSpace(currentProfile.AvatarPath))
            {
                try
                {
                    BitmapImage bmp = new BitmapImage();
                    if (currentProfile.AvatarPath.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    {
                        int comma = currentProfile.AvatarPath.IndexOf(',');
                        if (comma >= 0)
                        {
                            byte[] bytes = Convert.FromBase64String(currentProfile.AvatarPath.Substring(comma + 1));
                            using (var ms = new System.IO.MemoryStream(bytes))
                            {
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                            }
                            if (SidebarAvatarImage != null)
                            {
                                SidebarAvatarImage.Source = bmp;
                                SidebarAvatarImage.Visibility = System.Windows.Visibility.Visible;
                            }
                            if (SidebarAvatarInitials != null)
                                SidebarAvatarInitials.Visibility = System.Windows.Visibility.Collapsed;
                        }
                    }
                    else if (File.Exists(currentProfile.AvatarPath))
                    {
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(currentProfile.AvatarPath, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        if (SidebarAvatarImage != null)
                        {
                            SidebarAvatarImage.Source = bmp;
                            SidebarAvatarImage.Visibility = System.Windows.Visibility.Visible;
                        }
                        if (SidebarAvatarInitials != null)
                            SidebarAvatarInitials.Visibility = System.Windows.Visibility.Collapsed;
                    }
                    else
                    {
                        if (SidebarAvatarImage != null)
                            SidebarAvatarImage.Visibility = System.Windows.Visibility.Collapsed;
                        if (SidebarAvatarInitials != null)
                            SidebarAvatarInitials.Visibility = System.Windows.Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] LoadSidebarAvatar: " + ex.Message);
                    if (SidebarAvatarImage != null) SidebarAvatarImage.Visibility = System.Windows.Visibility.Collapsed;
                    if (SidebarAvatarInitials != null) SidebarAvatarInitials.Visibility = System.Windows.Visibility.Visible;
                }
            }
            else
            {
                if (SidebarAvatarImage != null)
                    SidebarAvatarImage.Visibility = System.Windows.Visibility.Collapsed;
                if (SidebarAvatarInitials != null)
                    SidebarAvatarInitials.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void CheckFirstRunProfileSetup()
        {
            try
            {
                var profile = UserProfileService.LoadProfile();
                if (profile == null || !profile.IsConfigured || string.IsNullOrWhiteSpace(profile.DesignerName) || string.IsNullOrWhiteSpace(profile.WorkspaceRoot) || !Directory.Exists(profile.WorkspaceRoot))
                {
                    Dialogs.FirstRunSetupDialog setup = new Dialogs.FirstRunSetupDialog(profile);
                    if (setup.ShowDialog() == true)
                    {
                        RefreshProfileUI();
                        NotificationService.ShowSuccess("Profile Configured", "Workstation setup complete. Welcome to SS-CAM!");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] CheckFirstRunProfileSetup error: " + ex.Message);
            }
        }

        private List<Rectangle> visBars;
        private List<Ellipse> visGlowDots;
        private double[] visCurrentHeights;
        private int visTickCount = 0;
        private bool wasPlayingLastTick = false;

        private void InitHeaderAnimation()
        {
            if (HeaderCanvas == null || HeaderCanvas.Visibility != Visibility.Visible)
                return;

            animItems = new List<AnimShapeItem>();
            visBars = new List<Rectangle>();
            visGlowDots = new List<Ellipse>();
            visCurrentHeights = new double[48];

            var shapeData = new[]
            {
                new { X = 60.0, Y = 10.0, VX = 0.45, VY = 0.20, D = 72.0, O = 0.09 },
                new { X = 210.0, Y = -18.0, VX = -0.28, VY = 0.28, D = 44.0, O = 0.07 },
                new { X = 390.0, Y = 28.0, VX = 0.22, VY = -0.18, D = 90.0, O = 0.06 },
                new { X = 540.0, Y = 5.0, VX = -0.32, VY = 0.22, D = 56.0, O = 0.08 },
                new { X = 720.0, Y = 32.0, VX = 0.28, VY = -0.22, D = 38.0, O = 0.07 },
                new { X = 860.0, Y = -8.0, VX = -0.20, VY = 0.25, D = 78.0, O = 0.055 },
                new { X = 1000.0, Y = 22.0, VX = 0.36, VY = -0.14, D = 50.0, O = 0.08 },
                new { X = 1120.0, Y = 12.0, VX = -0.24, VY = 0.17, D = 64.0, O = 0.06 }
            };

            foreach (var d in shapeData)
            {
                Ellipse e = new Ellipse
                {
                    Width = d.D,
                    Height = d.D,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.4,
                    Opacity = d.O,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(e, d.X);
                Canvas.SetTop(e, d.Y);
                HeaderCanvas.Children.Add(e);

                animItems.Add(new AnimShapeItem
                {
                    Shape = e,
                    VX = d.VX,
                    VY = d.VY,
                    Diameter = d.D
                });
            }

            // Create 48 Audio Visual Synthesizer Spectrum Bars across bottom of HeaderCanvas
            int barCount = 48;
            string[] ncsColors = new[] { "#21A1F7", "#3B82F6", "#EC4899", "#8B5CF6", "#06B6D4" };

            for (int i = 0; i < barCount; i++)
            {
                string colorHex = ncsColors[i % ncsColors.Length];
                Rectangle bar = new Rectangle
                {
                    Width = 3.5,
                    Height = 2,
                    RadiusX = 1.75,
                    RadiusY = 1.75,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                    Opacity = 0.0
                };
                HeaderCanvas.Children.Add(bar);
                visBars.Add(bar);

                // Top Radiant Glow Dot for Visual Synthesizer Tip
                Ellipse dot = new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = Brushes.White,
                    Opacity = 0.0
                };
                HeaderCanvas.Children.Add(dot);
                visGlowDots.Add(dot);
            }

            headerAnimTimer = new DispatcherTimer();
            headerAnimTimer.Interval = TimeSpan.FromMilliseconds(33);
            headerAnimTimer.Tick += (s, ev) =>
            {
                visTickCount++;
                double cw = HeaderCanvas.ActualWidth;
                double ch = HeaderCanvas.ActualHeight;
                if (cw <= 0 || ch <= 0) return;

                bool isPlaying = RadioStreamService.Instance.State == RadioPlaybackState.Playing;

                // Dynamic header state tracking (sidebar background can be updated here in future)
                if (isPlaying != wasPlayingLastTick)
                {
                    wasPlayingLastTick = isPlaying;
                    if (HeaderSubtitle != null) HeaderSubtitle.Visibility = Visibility.Collapsed;
                }

                // Ambient floating circles
                foreach (var item in animItems)
                {
                    double x = Canvas.GetLeft(item.Shape) + item.VX;
                    double y = Canvas.GetTop(item.Shape) + item.VY;
                    double d = item.Diameter;

                    if (x > cw) x = -d;
                    else if (x < -d) x = cw;

                    if (y > ch) y = -d;
                    else if (y < -d) y = ch;

                    Canvas.SetLeft(item.Shape, x);
                    Canvas.SetTop(item.Shape, y);
                    item.Shape.Opacity = isPlaying ? 0.03 : 0.08;
                }

                // Fluid Visual Synthesizer Bar & Radiant Glow Tip Animation Engine
                double barSpacing = cw / barCount;

                for (int i = 0; i < visBars.Count; i++)
                {
                    Rectangle bar = visBars[i];
                    Ellipse dot = visGlowDots[i];
                    double targetHeight = 2;

                    if (isPlaying)
                    {
                        // Multi-Harmonic Synthesizer Spectrum Math (Bass Sub-pulse + Mid Frequency + Treble Shimmer)
                        double bass = Math.Sin(visTickCount * 0.18 + i * 0.12) * 0.45;
                        double mid = Math.Cos(visTickCount * 0.28 - i * 0.32) * 0.35;
                        double treble = Math.Sin(visTickCount * 0.45 + i * 0.65) * 0.20;
                        double bellEnvelope = Math.Sin((double)i / barCount * Math.PI);

                        double rawVal = Math.Abs(bass + mid + treble) * (0.35 + 0.65 * bellEnvelope);
                        targetHeight = Math.Min(34.0, Math.Max(3.0, 4.0 + 30.0 * rawVal));

                        bar.Opacity = 0.85;
                        dot.Opacity = 0.95;
                    }
                    else
                    {
                        targetHeight = 0;
                        bar.Opacity = 0.0;
                        dot.Opacity = 0.0;
                    }

                    // Damped Spring Smooth Interpolation (Prevents freezing & jitter)
                    visCurrentHeights[i] = visCurrentHeights[i] * 0.75 + targetHeight * 0.25;

                    bar.Height = visCurrentHeights[i];
                    double xPos = i * barSpacing + 3;
                    double yPos = ch - visCurrentHeights[i] - 1;

                    Canvas.SetLeft(bar, xPos);
                    Canvas.SetTop(bar, yPos);

                    Canvas.SetLeft(dot, xPos - 0.25);
                    Canvas.SetTop(dot, Math.Max(0, yPos - 4));
                }
            };
            headerAnimTimer.Start();
        }


        
        

        

        

        

        

        

        

        

        private void OnStatusThemeToggle(object sender, MouseButtonEventArgs e)
        {
            // Cycle through themes: Falconia -> Metamorphosis -> Catppuccin -> RosePine -> Nord -> Falconia
            AppTheme nextTheme;
            if (ThemeService.CurrentTheme == AppTheme.Falconia)
                nextTheme = AppTheme.Metamorphosis;
            else if (ThemeService.CurrentTheme == AppTheme.Metamorphosis)
                nextTheme = AppTheme.Catppuccin;
            else if (ThemeService.CurrentTheme == AppTheme.Catppuccin)
                nextTheme = AppTheme.RosePine;
            else if (ThemeService.CurrentTheme == AppTheme.RosePine)
                nextTheme = AppTheme.Nord;
            else
                nextTheme = AppTheme.Falconia;

            ThemeService.ApplyTheme(nextTheme);
        }

        private void OnThemeModeChanged(AppTheme theme)
        {
            ThemeColors c = ThemeService.GetColors(theme);

            // -- NavigationView pane background --
            // RootNavigation.Background targets the control root, not the pane panel in WPF-UI 3.x.
            // The correct pane-specific keys must be set in RootNavigation.Resources.
            if (RootNavigation != null)
            {
                var sidebarBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.SidebarBg));
                RootNavigation.Background = sidebarBrush;
                // WPF-UI 3.x NavigationView pane resource keys:
                //   NavigationViewExpandedPaneBackground = Left (expanded) pane
                //   NavigationViewDefaultPaneBackground  = LeftCompact / LeftMinimal pane
                RootNavigation.Resources["NavigationViewExpandedPaneBackground"] = sidebarBrush;
                RootNavigation.Resources["NavigationViewDefaultPaneBackground"]  = sidebarBrush;
            }

            // -- TitleBar strip & caption buttons background + foreground: match theme --
            var titleBgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.SidebarBg));
            var titleFgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.TitleBarForeground));

            if (TitleBarStrip != null)
                TitleBarStrip.Background = titleBgBrush;

            if (TitleBarAppTitle != null)
                TitleBarAppTitle.Foreground = titleFgBrush;

            if (TitleBarNavToggleIcon != null)
                TitleBarNavToggleIcon.Foreground = titleFgBrush;

            if (AppTitleBar != null)
            {
                AppTitleBar.Background = Brushes.Transparent;
                AppTitleBar.Foreground = titleFgBrush;
            }

            // -- PaneHeader: search box container --
            if (SidebarSearchBorder != null)
                SidebarSearchBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.SearchBg));

            // -- PaneHeader: search text foreground --
            if (SidebarSearchBox != null)
            {
                SidebarSearchBox.Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.SearchText));
                SidebarSearchBox.CaretBrush   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.SearchText));
            }

            // -- PaneFooter: horizontal divider --
            if (SidebarDivider != null)
                SidebarDivider.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.SidebarBorder));

            // -- PaneFooter: user profile card background --
            if (SidebarUserCard != null)
                SidebarUserCard.Background = Brushes.Transparent;

            // -- PaneFooter: avatar ring background --
            if (SidebarAvatarRing != null)
                SidebarAvatarRing.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.FooterCardBg));

            // -- PaneFooter: persona name + department text colors --
            if (SidebarPersonaName != null)
                SidebarPersonaName.Foreground = titleFgBrush;
            if (SidebarPersonaDept != null)
                SidebarPersonaDept.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.UserCardSub));

            // -- Update theme name label in sidebar footer --
            if (StatusThemeText != null)
            {
                string themeName = (theme == AppTheme.Metamorphosis) ? "Metamorphosis" : (theme == AppTheme.Catppuccin ? "Catppuccin" : (theme == AppTheme.RosePine ? "Rosé Pine" : (theme == AppTheme.Nord ? "Nord Light" : "SuamiSihat Light")));
                StatusThemeText.Text = themeName;
            }

            // -- Nav item foreground --
            // Metamorphosis, Catppuccin & RosePine: dark sidebar in Dark WPF-UI mode — force nav text white.
            // Falconia & Nord: light sidebar in Light WPF-UI mode — clear override (WPF-UI default = dark).
            bool forceWhiteNavText = (theme == AppTheme.Metamorphosis || theme == AppTheme.Catppuccin || theme == AppTheme.RosePine);
            var navFg = forceWhiteNavText
                ? new SolidColorBrush(Colors.White)
                : null;

            if (RootNavigation != null)
            {
                foreach (object item in RootNavigation.MenuItems)
                {
                    var navItem = item as Wpf.Ui.Controls.NavigationViewItem;
                    if (navItem != null)
                    {
                        if (navFg != null) navItem.Foreground = navFg;
                        else navItem.ClearValue(Wpf.Ui.Controls.NavigationViewItem.ForegroundProperty);
                    }
                }
                foreach (object item in RootNavigation.FooterMenuItems)
                {
                    var navItem2 = item as Wpf.Ui.Controls.NavigationViewItem;
                    if (navItem2 != null)
                    {
                        if (navFg != null) navItem2.Foreground = navFg;
                        else navItem2.ClearValue(Wpf.Ui.Controls.NavigationViewItem.ForegroundProperty);
                    }
                }
            }
        }

        private void OnFooterTimerClicked(object sender, MouseButtonEventArgs e)
        {
            RootNavigation.Navigate(typeof(WellbeingPage));
        }

        private DispatcherTimer nasCheckTimer;

        private void InitNasHealthCheck()
        {
            nasCheckTimer = new DispatcherTimer();
            nasCheckTimer.Interval = TimeSpan.FromSeconds(30);
            nasCheckTimer.Tick += (s, e) => TriggerNasHealthCheck();
            nasCheckTimer.Start();
            TriggerNasHealthCheck();
        }

        private void TriggerNasHealthCheck()
        {
            if (StatusNasText != null)
            {
                StatusNasText.Text = "SSNAS Checking...";
                StatusNasText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            }
            if (NasStatusDot != null)
                NasStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

            CheckNasOnlineAsync((isOnline, statusText) =>
            {
                if (StatusNasText != null)
                {
                    StatusNasText.Text = statusText;
                    StatusNasText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isOnline ? "#10B981" : "#EF4444"));
                }
                if (NasStatusDot != null)
                    NasStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isOnline ? "#10B981" : "#EF4444"));
            });
        }

        private void CheckNasOnlineAsync(Action<bool, string> callback)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                bool isOnline = false;
                try
                {
                    System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://suamisihat.myds.me");
                    request.Timeout = 4000;
                    request.ReadWriteTimeout = 4000;
                    request.Method = "HEAD";

                    using (System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse())
                    {
                        isOnline = (response.StatusCode == System.Net.HttpStatusCode.OK ||
                                    response.StatusCode == System.Net.HttpStatusCode.Moved ||
                                    response.StatusCode == System.Net.HttpStatusCode.Redirect ||
                                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                                    response.StatusCode == System.Net.HttpStatusCode.Forbidden);
                    }
                }
                catch (System.Net.WebException ex)
                {
                    if (ex.Response != null)
                    {
                        isOnline = true;
                    }
                    else
                    {
                        isOnline = false;
                    }
                }
                catch
                {
                    isOnline = false;
                }

                string statusText = isOnline ? "SSNAS Online" : "SSNAS Offline";

                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (callback != null)
                        {
                            callback(isOnline, statusText);
                        }
                    }));
                }
            });
        }

        private void OnCheckNasStatusClicked(object sender, MouseButtonEventArgs e)
        {
            // If NAS is online, open the NAS web interface; always re-check status
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://suamisihat.myds.me/",
                    UseShellExecute = true
                });
            }
            catch { /* fall through to status check */ }
            TriggerNasHealthCheck();
        }

        // ─── Auto-Update Check ────────────────────────────────────────────────
        // Hosts a version.json at: https://suamisihat.myds.me/ss-cam/version.json
        // Format:
        // {
        //   "version": "2.1.0",
        //   "releaseNotes": "Radio & Focus Stream Player.",
        //   "downloadUrl": "https://suamisihat.myds.me/ss-cam/SS-CAM-v2.1.0.exe"
        // }

        private string CurrentVersion
        {
            get { return SS_CAM.Services.AppVersion.VersionString; }
        }
        private const string VersionCheckUrl = "https://suamisihat.myds.me/ss-cam/version.json";
        private string _updateDownloadUrl = "";

        private void InitUpdateCheck()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                System.Threading.Thread.Sleep(3000); // slight delay so app loads first
                CheckForUpdate();
            });
        }

        private void CheckForUpdate()
        {
            try
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (s, c, ch, e) => true;
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(VersionCheckUrl);
                req.Timeout = 5000;
                req.Method = "GET";
                req.Accept = "application/json";

                using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
                using (System.IO.StreamReader reader = new System.IO.StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    string latestVersion = ExtractJsonString(json, "version");
                    string releaseNotes = ExtractJsonString(json, "releaseNotes");
                    string downloadUrl = ExtractJsonString(json, "downloadUrl");

                    if (!string.IsNullOrWhiteSpace(latestVersion) && IsNewerVersion(latestVersion, CurrentVersion))
                    {
                        _updateDownloadUrl = downloadUrl;
                        string notes = string.IsNullOrWhiteSpace(releaseNotes) ? "" : " – " + releaseNotes;
                        string msg = string.Format("SS-CAM v{0} is available{1}", latestVersion, notes);

                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                ShowUpdateBanner(msg);
                            }));
                        }
                    }
                }
            }
            catch { /* silent – no connection or NAS offline, skip update check */ }
        }

        private void ShowUpdateBanner(string message)
        {
            if (UpdateBanner != null && UpdateBannerText != null)
            {
                UpdateBannerText.Text = message;
                UpdateBanner.Visibility = Visibility.Visible;
            }
        }

        private void OnDownloadUpdateClicked(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_updateDownloadUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _updateDownloadUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] OpenUpdateUrl: " + ex.Message); }
            }
        }

        private void OnDismissUpdateBanner(object sender, RoutedEventArgs e)
        {
            if (UpdateBanner != null)
            {
                UpdateBanner.Visibility = Visibility.Collapsed;
            }
        }

        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                Version vLatest = new Version(latest);
                Version vCurrent = new Version(current);
                return vLatest.CompareTo(vCurrent) > 0;
            }
            catch { return false; }
        }

        private string ExtractJsonString(string json, string key)
        {
            // Lightweight JSON string extractor without Newtonsoft dependency for resilience
            string search = string.Format("\"{0}\"", key);
            int keyIdx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0) return "";
            int colon = json.IndexOf(':', keyIdx + search.Length);
            if (colon < 0) return "";
            int open = json.IndexOf('"', colon + 1);
            if (open < 0) return "";
            int close = json.IndexOf('"', open + 1);
            if (close < 0) return "";
            return json.Substring(open + 1, close - open - 1);
        }

        // ─── Global Keyboard Shortcuts & Command Palette (Ctrl + K) ─────────
        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                if (CommandPaletteOverlay != null && CommandPaletteOverlay.Visibility == Visibility.Visible)
                {
                    CloseCommandPalette();
                }
                else
                {
                    OpenCommandPalette("All");
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (CommandPaletteOverlay != null && CommandPaletteOverlay.Visibility == Visibility.Visible)
                {
                    e.Handled = true;
                    CloseCommandPalette();
                }
                else if (WorkTimerPopoverOverlay != null && WorkTimerPopoverOverlay.Visibility == Visibility.Visible)
                {
                    e.Handled = true;
                    WorkTimerPopoverOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private string GetWorkspaceRoot()
        {
            if (currentProfile != null && !string.IsNullOrWhiteSpace(currentProfile.WorkspaceRoot))
            {
                return currentProfile.WorkspaceRoot;
            }
            try
            {
                currentProfile = UserProfileService.LoadProfile();
                if (currentProfile != null && !string.IsNullOrWhiteSpace(currentProfile.WorkspaceRoot))
                {
                    return currentProfile.WorkspaceRoot;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] GetWorkspaceRoot: " + ex.Message);
            }
            return string.Empty;
        }

        // ─── Live Work Progress Tracker & Status Indicator ───────────────────────
        private DispatcherTimer _workSessionTimer;

        private void InitWorkSessionTracker()
        {
            try
            {
                _workSessionTimer = new DispatcherTimer();
                _workSessionTimer.Interval = TimeSpan.FromSeconds(1);
                _workSessionTimer.Tick += (s, ev) =>
                {
                    WorkSessionTrackerService.Instance.Tick();
                    UpdateWorkStatusUI();
                };
                _workSessionTimer.Start();

                WorkSessionTrackerService.Instance.StateChanged += (s, ev) =>
                {
                    if (Dispatcher.CheckAccess()) UpdateWorkStatusUI();
                    else Dispatcher.BeginInvoke(new Action(UpdateWorkStatusUI));
                };

                UpdateWorkStatusUI();
                LiveTaskSyncService.Instance.Initialize(GetWorkspaceRoot());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitWorkSessionTracker: " + ex.Message);
            }
        }

        private void UpdateWorkStatusUI()
        {
            try
            {
                var tracker = WorkSessionTrackerService.Instance;
                string timeText = tracker.FormattedTime;
                string projectName = !string.IsNullOrWhiteSpace(tracker.ActiveProjectId) ? tracker.ActiveProjectId : "No Active Project";

                if (TxtWorkStatusTimer != null) TxtWorkStatusTimer.Text = timeText;
                if (TxtPopoverBigTimer != null) TxtPopoverBigTimer.Text = timeText;
                if (TxtWorkStatusProject != null) TxtWorkStatusProject.Text = projectName;

                Color dotColor;
                string stateIcon;
                string statusText;
                string toggleText;
                string toggleIcon;

                switch (tracker.State)
                {
                    case WorkSessionState.Running:
                        dotColor = (Color)ColorConverter.ConvertFromString("#10B981"); // Success Green
                        stateIcon = "\uE769"; // Pause glyph
                        statusText = string.Format("Active • Working on {0}", tracker.ActiveClient);
                        toggleText = "Pause Timer";
                        toggleIcon = "\uE769";
                        break;
                    case WorkSessionState.Paused:
                        dotColor = (Color)ColorConverter.ConvertFromString("#F59E0B"); // Caution Amber
                        stateIcon = "\uE768"; // Play glyph
                        statusText = "Paused";
                        toggleText = "Resume Timer";
                        toggleIcon = "\uE768";
                        break;
                    case WorkSessionState.Idle:
                    default:
                        dotColor = (Color)ColorConverter.ConvertFromString("#94A3B8"); // Muted Slate
                        stateIcon = "\uE916"; // Timer glyph
                        statusText = "Idle / Ready";
                        toggleText = "Start Timer";
                        toggleIcon = "\uE768";
                        break;
                }

                SolidColorBrush dotBrush = new SolidColorBrush(dotColor);
                if (WorkStatusDot != null) WorkStatusDot.Fill = dotBrush;
                if (PopoverStatusDot != null) PopoverStatusDot.Fill = dotBrush;
                if (TxtWorkStatusStateIcon != null) TxtWorkStatusStateIcon.Text = stateIcon;
                if (TxtPopoverStatusState != null) TxtPopoverStatusState.Text = statusText;
                if (TxtPopoverToggleText != null) TxtPopoverToggleText.Text = toggleText;
                if (TxtPopoverToggleIcon != null) TxtPopoverToggleIcon.Text = toggleIcon;

                // Sidebar footer timer synchronization - status only
                if (StatusTimerText != null)
                {
                    if (tracker.State == WorkSessionState.Running)
                        StatusTimerText.Text = tracker.FormattedShortTime;
                    else if (tracker.State == WorkSessionState.Paused)
                        StatusTimerText.Text = tracker.FormattedShortTime + " (Paused)";
                    else
                        StatusTimerText.Text = "Ready";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] UpdateWorkStatusUI: " + ex.Message);
            }
        }

        private void OnWorkStatusPillClicked(object sender, MouseButtonEventArgs e)
        {
            if (WorkTimerPopoverOverlay == null) return;

            if (WorkTimerPopoverOverlay.Visibility == Visibility.Visible)
            {
                WorkTimerPopoverOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                PopulatePopoverProjects();
                if (TxtPopoverSessionNotes != null)
                    TxtPopoverSessionNotes.Text = WorkSessionTrackerService.Instance.SessionNotes ?? string.Empty;
                WorkTimerPopoverOverlay.Visibility = Visibility.Visible;
            }
        }

        private void OnWorkTimerPopoverBackdropClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == WorkTimerPopoverOverlay)
            {
                WorkTimerPopoverOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void OnCloseWorkTimerPopoverClicked(object sender, RoutedEventArgs e)
        {
            if (WorkTimerPopoverOverlay != null)
                WorkTimerPopoverOverlay.Visibility = Visibility.Collapsed;
        }

        private void PopulatePopoverProjects()
        {
            try
            {
                _popoverUpdating = true;
                string root = GetWorkspaceRoot();
                _popoverCachedProjects = new List<DesignerFolderItem>();

                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                {
                    var projects = WorkspaceScanner.ListDesignerFolders(root, string.Empty, string.Empty, 100);
                    if (projects != null)
                    {
                        _popoverCachedProjects = projects;
                    }
                }

                // Populate Designer Filter
                if (CmbPopoverDesignerFilter != null)
                {
                    string previousSelection = CmbPopoverDesignerFilter.SelectedItem != null 
                        ? CmbPopoverDesignerFilter.SelectedItem.ToString() 
                        : null;

                    CmbPopoverDesignerFilter.Items.Clear();
                    CmbPopoverDesignerFilter.Items.Add("All Designers");

                    HashSet<string> designerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Add from staff directory
                    if (!string.IsNullOrWhiteSpace(root))
                    {
                        var staffList = WorkspaceScanner.GetDesignerFolders(root);
                        if (staffList != null)
                        {
                            foreach (var s in staffList)
                            {
                                if (s != null && !string.IsNullOrWhiteSpace(s.Name) && designerSet.Add(s.Name.Trim()))
                                {
                                    CmbPopoverDesignerFilter.Items.Add(s.Name.Trim());
                                }
                            }
                        }
                    }

                    // Add any designers detected from projects that weren't in directory
                    if (_popoverCachedProjects != null)
                    {
                        foreach (var p in _popoverCachedProjects)
                        {
                            if (!string.IsNullOrWhiteSpace(p.Designer) && 
                                !string.Equals(p.Designer, "Shared", StringComparison.OrdinalIgnoreCase) && 
                                designerSet.Add(p.Designer.Trim()))
                            {
                                CmbPopoverDesignerFilter.Items.Add(p.Designer.Trim());
                            }
                        }
                    }

                    // Determine default selected designer:
                    // 1. Previous selection if still valid
                    // 2. Currently logged-in profile designer
                    // 3. "All Designers"
                    int selectedIdx = 0;
                    if (!string.IsNullOrEmpty(previousSelection))
                    {
                        for (int i = 0; i < CmbPopoverDesignerFilter.Items.Count; i++)
                        {
                            if (string.Equals(CmbPopoverDesignerFilter.Items[i].ToString(), previousSelection, StringComparison.OrdinalIgnoreCase))
                            {
                                selectedIdx = i;
                                break;
                            }
                        }
                    }
                    else if (currentProfile != null && !string.IsNullOrWhiteSpace(currentProfile.DesignerName))
                    {
                        for (int i = 0; i < CmbPopoverDesignerFilter.Items.Count; i++)
                        {
                            string item = CmbPopoverDesignerFilter.Items[i].ToString();
                            if (string.Equals(item, currentProfile.DesignerName, StringComparison.OrdinalIgnoreCase) ||
                                item.IndexOf(currentProfile.DesignerName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                currentProfile.DesignerName.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                selectedIdx = i;
                                break;
                            }
                        }
                    }

                    CmbPopoverDesignerFilter.SelectedIndex = selectedIdx;
                }

                ApplyPopoverProjectFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] PopulatePopoverProjects: " + ex.Message);
            }
            finally
            {
                _popoverUpdating = false;
            }
        }

        private void OnPopoverDesignerFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_popoverUpdating) return;

            try
            {
                _popoverUpdating = true;
                ApplyPopoverProjectFilter();
            }
            finally
            {
                _popoverUpdating = false;
            }
        }

        private void ApplyPopoverProjectFilter()
        {
            try
            {
                if (CmbPopoverProjectPicker == null) return;
                CmbPopoverProjectPicker.Items.Clear();

                string selectedDesigner = "All Designers";
                if (CmbPopoverDesignerFilter != null && CmbPopoverDesignerFilter.SelectedItem != null)
                {
                    selectedDesigner = CmbPopoverDesignerFilter.SelectedItem.ToString();
                }

                if (_popoverCachedProjects != null && _popoverCachedProjects.Count > 0)
                {
                    foreach (var p in _popoverCachedProjects)
                    {
                        if (p == null || string.IsNullOrWhiteSpace(p.Project)) continue;

                        if (selectedDesigner != "All Designers" && !string.IsNullOrWhiteSpace(selectedDesigner))
                        {
                            bool match = false;
                            if (!string.IsNullOrWhiteSpace(p.Designer))
                            {
                                if (string.Equals(p.Designer, selectedDesigner, StringComparison.OrdinalIgnoreCase) ||
                                    p.Designer.IndexOf(selectedDesigner, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    selectedDesigner.IndexOf(p.Designer, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    match = true;
                                }
                            }
                            if (!match && !string.IsNullOrWhiteSpace(p.FullPath))
                            {
                                if (p.FullPath.IndexOf(selectedDesigner, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    match = true;
                                }
                            }
                            if (!match) continue;
                        }

                        CmbPopoverProjectPicker.Items.Add(p.Project);
                    }
                }

                string activeId = WorkSessionTrackerService.Instance.ActiveProjectId;
                if (!string.IsNullOrEmpty(activeId))
                {
                    int idx = CmbPopoverProjectPicker.Items.IndexOf(activeId);
                    if (idx >= 0)
                    {
                        CmbPopoverProjectPicker.SelectedIndex = idx;
                    }
                    else
                    {
                        CmbPopoverProjectPicker.Items.Insert(0, activeId);
                        CmbPopoverProjectPicker.SelectedIndex = 0;
                    }
                }
                else if (CmbPopoverProjectPicker.Items.Count > 0)
                {
                    CmbPopoverProjectPicker.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] ApplyPopoverProjectFilter: " + ex.Message);
            }
        }

        private void OnPopoverProjectPickerChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_popoverUpdating) return;
            if (CmbPopoverProjectPicker == null || CmbPopoverProjectPicker.SelectedItem == null) return;

            string selected = CmbPopoverProjectPicker.SelectedItem.ToString();
            var tracker = WorkSessionTrackerService.Instance;
            if (!string.IsNullOrEmpty(selected) && !string.Equals(selected, tracker.ActiveProjectId, StringComparison.OrdinalIgnoreCase))
            {
                tracker.SwitchProject(selected, selected, "SS", string.Empty);
                UpdateWorkStatusUI();
            }
        }

        private void OnPopoverSessionNotesChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtPopoverSessionNotes != null)
            {
                WorkSessionTrackerService.Instance.SessionNotes = TxtPopoverSessionNotes.Text;
            }
        }

        private void OnPopoverToggleTimerClicked(object sender, RoutedEventArgs e)
        {
            var tracker = WorkSessionTrackerService.Instance;
            if (tracker.State == WorkSessionState.Running)
            {
                tracker.Pause();
            }
            else if (tracker.State == WorkSessionState.Paused)
            {
                tracker.Resume();
            }
            else
            {
                string project = (CmbPopoverProjectPicker != null && CmbPopoverProjectPicker.SelectedItem != null)
                    ? CmbPopoverProjectPicker.SelectedItem.ToString()
                    : "Creative Work Session";
                tracker.StartOrResume(project, project, "SS", string.Empty);
            }
            UpdateWorkStatusUI();
        }

        private void OnPopoverResetTimerClicked(object sender, RoutedEventArgs e)
        {
            WorkSessionTrackerService.Instance.StopAndReset();
            UpdateWorkStatusUI();
        }

        private void OnPopoverJumpToProjectClicked(object sender, RoutedEventArgs e)
        {
            if (WorkTimerPopoverOverlay != null)
                WorkTimerPopoverOverlay.Visibility = Visibility.Collapsed;

            if (RootNavigation != null)
            {
                RootNavigation.Navigate(typeof(SearchCopyPage));
            }
        }

        // ─── Global Command Palette (Ctrl + K) ──────────────────────────────────
        private string _currentPaletteCategory = "All";

        private void OnSearchTriggerClicked(object sender, MouseButtonEventArgs e)
        {
            OpenCommandPalette("All");
        }

        private void OnSearchBoxGotFocus(object sender, RoutedEventArgs e)
        {
            OpenCommandPalette("All");
        }

        private void OnSearchBoxLostFocus(object sender, RoutedEventArgs e)
        {
            // Handled via Command Palette
        }

        private void OnSearchBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            // Handled via Command Palette
        }

        private void OpenCommandPalette(string category)
        {
            try
            {
                _currentPaletteCategory = string.IsNullOrWhiteSpace(category) ? "All" : category;
                UpdateCategoryChipAppearance();

                if (CommandPaletteOverlay != null)
                {
                    CommandPaletteOverlay.Visibility = Visibility.Visible;
                }

                if (CommandPaletteSearchBox != null)
                {
                    CommandPaletteSearchBox.Text = string.Empty;
                    CommandPaletteSearchBox.Focus();
                }

                PerformCommandPaletteSearch(string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] OpenCommandPalette: " + ex.Message);
            }
        }

        private void CloseCommandPalette()
        {
            if (CommandPaletteOverlay != null)
            {
                CommandPaletteOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void OnCommandPaletteBackdropClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == CommandPaletteOverlay)
            {
                CloseCommandPalette();
            }
        }

        private void OnCloseCommandPaletteClicked(object sender, RoutedEventArgs e)
        {
            CloseCommandPalette();
        }

        private void OnCommandPaletteCategoryChipClicked(object sender, RoutedEventArgs e)
        {
            FrameworkElement btn = sender as FrameworkElement;
            if (btn != null && btn.Tag != null)
            {
                _currentPaletteCategory = btn.Tag.ToString();
                UpdateCategoryChipAppearance();
                PerformCommandPaletteSearch(CommandPaletteSearchBox != null ? CommandPaletteSearchBox.Text : string.Empty);
            }
        }

        private void UpdateCategoryChipAppearance()
        {
            SetChipAppearance(ChipCategoryAll, "All");
            SetChipAppearance(ChipCategoryModules, "Navigation");
            SetChipAppearance(ChipCategoryProjects, "Projects");
            SetChipAppearance(ChipCategoryColors, "Brand Colors");
            SetChipAppearance(ChipCategoryCopy, "Copywriting");
            SetChipAppearance(ChipCategoryActions, "Actions");
        }

        private void SetChipAppearance(Wpf.Ui.Controls.Button btn, string category)
        {
            if (btn == null) return;
            bool isActive = string.Equals(_currentPaletteCategory, category, StringComparison.OrdinalIgnoreCase);
            btn.Appearance = isActive ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
        }

        private void OnCommandPaletteSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (CommandPaletteSearchBox != null)
            {
                PerformCommandPaletteSearch(CommandPaletteSearchBox.Text);
            }
        }

        private void PerformCommandPaletteSearch(string query)
        {
            try
            {
                string root = GetWorkspaceRoot();
                List<CommandPaletteItem> results = CommandPaletteService.Search(query, _currentPaletteCategory, root, 35);

                if (CommandPaletteResultsList != null)
                {
                    CommandPaletteResultsList.ItemsSource = results;
                    if (results.Count > 0)
                    {
                        CommandPaletteResultsList.SelectedIndex = 0;
                    }
                }

                if (TxtCommandPaletteCount != null)
                {
                    TxtCommandPaletteCount.Text = string.Format("{0} result{1}", results.Count, results.Count == 1 ? "" : "s");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] PerformCommandPaletteSearch: " + ex.Message);
            }
        }

        private void OnCommandPaletteSearchPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (CommandPaletteResultsList == null) return;

            if (e.Key == Key.Down)
            {
                e.Handled = true;
                int current = CommandPaletteResultsList.SelectedIndex;
                if (current < CommandPaletteResultsList.Items.Count - 1)
                {
                    CommandPaletteResultsList.SelectedIndex = current + 1;
                    CommandPaletteResultsList.ScrollIntoView(CommandPaletteResultsList.SelectedItem);
                }
            }
            else if (e.Key == Key.Up)
            {
                e.Handled = true;
                int current = CommandPaletteResultsList.SelectedIndex;
                if (current > 0)
                {
                    CommandPaletteResultsList.SelectedIndex = current - 1;
                    CommandPaletteResultsList.ScrollIntoView(CommandPaletteResultsList.SelectedItem);
                }
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteCommandPaletteItem(CommandPaletteResultsList.SelectedItem as CommandPaletteItem);
            }
        }

        private void OnCommandPaletteListPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteCommandPaletteItem(CommandPaletteResultsList.SelectedItem as CommandPaletteItem);
            }
        }

        private void OnCommandPaletteItemDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            ExecuteCommandPaletteItem(CommandPaletteResultsList.SelectedItem as CommandPaletteItem);
        }

        private void ExecuteCommandPaletteItem(CommandPaletteItem item)
        {
            if (item == null) return;

            CloseCommandPalette();

            try
            {
                if (item.TargetPageType != null)
                {
                    if (RootNavigation != null)
                    {
                        RootNavigation.Navigate(item.TargetPageType);
                    }
                }
                else if (string.Equals(item.Category, "Projects", StringComparison.OrdinalIgnoreCase))
                {
                    // Start or resume work session on this project and jump to Search & Copy
                    WorkSessionTrackerService.Instance.StartOrResume(item.Title, item.Title, "SS", item.Payload);
                    UpdateWorkStatusUI();

                    if (RootNavigation != null)
                    {
                        RootNavigation.Navigate(typeof(SearchCopyPage));
                    }
                    NotificationService.ShowSuccess("Working on Project", string.Format("Started work session timer for '{0}'.", item.Title));
                }
                else if (string.Equals(item.Category, "Brand Colors", StringComparison.OrdinalIgnoreCase))
                {
                    ClipboardService.SetText(item.Payload);
                    NotificationService.ShowSuccess("Copied Brand Color", string.Format("Copied {0} ({1}) to clipboard.", item.Title, item.Payload));
                }
                else if (string.Equals(item.Category, "Copywriting", StringComparison.OrdinalIgnoreCase))
                {
                    ClipboardService.SetText(item.Payload);
                    NotificationService.ShowSuccess("Copied Copywriting Snippet", string.Format("Copied {0} to clipboard.", item.Title));
                }
                else if (string.Equals(item.Category, "Actions", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteActionCommand(item.Payload);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] ExecuteCommandPaletteItem: " + ex.Message);
            }
        }

        private void ExecuteActionCommand(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return;

            if (actionId == "ACTION_TIMER_TOGGLE")
            {
                var tracker = WorkSessionTrackerService.Instance;
                if (tracker.State == WorkSessionState.Running)
                {
                    tracker.Pause();
                    NotificationService.ShowInfo("Work Timer Paused", string.Format("Paused session at {0}.", tracker.FormattedTime));
                }
                else if (tracker.State == WorkSessionState.Paused)
                {
                    tracker.Resume();
                    NotificationService.ShowSuccess("Work Timer Resumed", string.Format("Resumed tracking for '{0}'.", tracker.ActiveProjectName));
                }
                else
                {
                    tracker.StartOrResume("Creative Work Session", "Creative Work Session", "SS", string.Empty);
                    NotificationService.ShowSuccess("Work Timer Started", "Started new creative work session.");
                }
                UpdateWorkStatusUI();
            }
            else if (actionId == "ACTION_THEME_TOGGLE")
            {
                OnStatusThemeToggle(null, null);
                NotificationService.ShowInfo("Theme Switched", string.Format("Theme switched to {0}.", ThemeService.CurrentTheme));
            }
            else if (actionId == "ACTION_RADIO_TOGGLE")
            {
                OnStatusRadioPlayToggle(null, null);
            }
            else if (actionId == "ACTION_RESCAN_NAS")
            {
                TriggerNasHealthCheck();
                string root = GetWorkspaceRoot();
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                {
                    WorkspaceScanner.ScanAsync(root);
                    NotificationService.ShowSuccess("Workspace Rescanned", "Initiated background scan of Synology NAS vaults.");
                }
                else
                {
                    NotificationService.ShowWarning("Workspace Offline", "Synology NAS workspace path is not currently accessible.");
                }
            }
            else if (actionId == "ACTION_OPEN_WORKSPACE")
            {
                string root = GetWorkspaceRoot();
                if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = root,
                        UseShellExecute = true
                    });
                }
                else
                {
                    NotificationService.ShowWarning("Folder Not Found", "Workspace directory not configured or offline.");
                }
            }
        }

        private void FilterNavItems(string query)
        {
            foreach (object item in RootNavigation.MenuItems)
            {
                var navItem = item as Wpf.Ui.Controls.NavigationViewItem;
                if (navItem == null) continue;
                string label = navItem.Content != null ? navItem.Content.ToString() : "";
                bool matches = string.IsNullOrEmpty(query) ||
                               label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                navItem.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ─── Profile Card & Settings Navigation ───────────────────────────────
        private void OnSettingsNavItemClicked(object sender, RoutedEventArgs e)
        {
            if (RootNavigation != null)
            {
                RootNavigation.Navigate(typeof(SettingsPage));
            }
        }

        private void OnProfileCardClicked(object sender, MouseButtonEventArgs e)
        {
            if (RootNavigation != null)
            {
                RootNavigation.Navigate(typeof(SettingsPage));
            }
        }

        // ─── Persistent Bottom Radio Controls & Dynamic 60 FPS Background Spectrum Visualizer ───
        private System.Windows.Threading.DispatcherTimer _spectrumTimer;
        private static readonly Random _specRand = new Random();

        private void InitializeRadioSpectrumAnimator()
        {
            try
            {
                _spectrumTimer = new System.Windows.Threading.DispatcherTimer();
                _spectrumTimer.Interval = TimeSpan.FromMilliseconds(16); // 60 FPS real-time
                _spectrumTimer.Tick += UpdateSpectrumVisualizer;
                _spectrumTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeRadioSpectrumAnimator: " + ex.Message);
            }
        }

        private void UpdateSpectrumVisualizer(object sender, EventArgs e)
        {
            try
            {
                var service = RadioStreamService.Instance;
                bool isPlaying = service != null && service.State == RadioPlaybackState.Playing;
                double[] specData = (service != null && service.LocalProxy != null) ? service.LocalProxy.CurrentSpectrumData : null;

                if (BottomRadioSpectrumPanel == null || SpecWaveLinePath == null) return;

                double actualW = BottomRadioSpectrumPanel.ActualWidth;
                if (actualW < 100) actualW = 1000;
                double baseH = 40.0;

                int pointCount = 48;
                double stepX = actualW / (pointCount - 1);
                Point[] pts = new Point[pointCount];

                double phase = Environment.TickCount * 0.009;

                for (int i = 0; i < pointCount; i++)
                {
                    double normX = (double)i / (pointCount - 1);
                    double y = baseH - 4.0;

                    if (isPlaying)
                    {
                        double audioAmp = 0.5;
                        if (specData != null && specData.Length > (i % 24))
                        {
                            audioAmp = specData[i % 24];
                        }

                        // Organic liquid wavelength equation combining 3 harmonic sine waves + live audio amplitude
                        double wave1 = Math.Sin(normX * 10.0 + phase) * 11.0;
                        double wave2 = Math.Cos(normX * 20.0 - phase * 1.6) * 7.0;
                        double wave3 = Math.Sin(normX * 32.0 + phase * 2.3) * 4.0;

                        double totalH = (wave1 + wave2 + wave3) * (0.35 + audioAmp * 1.25);
                        y = Math.Max(4.0, Math.Min(baseH - 2.0, baseH - 18.0 - totalH));
                    }

                    pts[i] = new Point(i * stepX, y);
                }

                // Build line stroke geometry
                StreamGeometry strokeGeom = new StreamGeometry();
                using (StreamGeometryContext ctx = strokeGeom.Open())
                {
                    ctx.BeginFigure(pts[0], false, false);
                    ctx.PolyLineTo(pts, true, true);
                }
                strokeGeom.Freeze();
                SpecWaveLinePath.Data = strokeGeom;

                // Build gradient fill geometry under wave
                if (SpecWaveFillPath != null)
                {
                    StreamGeometry fillGeom = new StreamGeometry();
                    using (StreamGeometryContext ctx = fillGeom.Open())
                    {
                        ctx.BeginFigure(pts[0], true, true);
                        ctx.PolyLineTo(pts, true, true);
                        ctx.LineTo(new Point(actualW, baseH), true, false);
                        ctx.LineTo(new Point(0, baseH), true, false);
                    }
                    fillGeom.Freeze();
                    SpecWaveFillPath.Data = fillGeom;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] DrawSpectrumWave: " + ex.Message); }
        }

        private void InitVisualizerListeners()
        {
            try
            {
                var viz = VisualizerService.Instance;
                if (viz != null)
                {
                    viz.VisualizerModeChanged += (s, mode) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() => UpdateBottomVisualizerUI(mode)));
                    };
                    UpdateBottomVisualizerUI(viz.CurrentMode);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] InitVisualizerListeners: " + ex.Message); }
        }

        private void UpdateBottomVisualizerUI(VisualizerMode mode)
        {
            // Lock to Hero Mesh
        }

        private void OnNotificationReceived(object sender, NotificationEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(() => OnNotificationReceived(sender, e)));
                return;
            }

            CreateToastCard(e.Title, e.Message, e.Type, e.DurationMs);
            UpdateNotificationBadge();
        }

        private void OnNotificationHistoryUpdated(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(() => OnNotificationHistoryUpdated(sender, e)));
                return;
            }

            UpdateNotificationBadge();
            if (NotificationDrawer != null && NotificationDrawer.Visibility == Visibility.Visible)
            {
                RenderNotificationList();
            }
        }

        private void UpdateNotificationBadge()
        {
            try
            {
                int count = NotificationService.UnreadCount;
                if (NotificationBadgeBorder != null && TxtNotificationBadgeCount != null)
                {
                    if (count > 0)
                    {
                        TxtNotificationBadgeCount.Text = count > 99 ? "99+" : count.ToString();
                        NotificationBadgeBorder.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        NotificationBadgeBorder.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateNotificationBadge error: " + ex.Message);
            }
        }

        private void OnNotificationBellClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (NotificationDrawer == null) return;
                if (NotificationDrawer.Visibility == Visibility.Visible)
                {
                    NotificationDrawer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NotificationDrawer.Visibility = Visibility.Visible;
                    RenderNotificationList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("OnNotificationBellClicked error: " + ex.Message);
            }
        }

        private void OnCloseNotificationDrawerClicked(object sender, RoutedEventArgs e)
        {
            if (NotificationDrawer != null)
                NotificationDrawer.Visibility = Visibility.Collapsed;
        }

        private void OnMarkAllNotificationsReadClicked(object sender, RoutedEventArgs e)
        {
            NotificationService.MarkAllAsRead();
        }

        private void OnClearAllNotificationsClicked(object sender, RoutedEventArgs e)
        {
            NotificationService.ClearAll();
        }

        private void RenderNotificationList()
        {
            try
            {
                if (NotificationListStack == null) return;
                NotificationListStack.Children.Clear();

                var history = NotificationService.History;
                if (history == null || history.Count == 0)
                {
                    Wpf.Ui.Controls.TextBlock emptyText = new Wpf.Ui.Controls.TextBlock
                    {
                        Text = "No notifications in history.",
                        Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
                        FontSize = 12,
                        Margin = new Thickness(0, 16, 0, 16),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    NotificationListStack.Children.Add(emptyText);
                    return;
                }

                foreach (var item in history)
                {
                    if (item == null) continue;

                    Border card = new Border
                    {
                        Background = item.IsRead 
                            ? (Brush)Application.Current.FindResource("CardBackgroundFillColorSecondaryBrush")
                            : (Brush)Application.Current.FindResource("CardBackgroundFillColorDefaultBrush"),
                        BorderBrush = (Brush)Application.Current.FindResource("CardStrokeColorDefaultBrush"),
                        BorderThickness = new Thickness(item.IsRead ? 1 : 2, 1, 1, 1),
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(0, 0, 0, 8),
                        Padding = new Thickness(10, 8, 10, 8)
                    };

                    Grid grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    Wpf.Ui.Controls.SymbolIcon symbol = new Wpf.Ui.Controls.SymbolIcon
                    {
                        Symbol = (Wpf.Ui.Controls.SymbolRegular)Enum.Parse(typeof(Wpf.Ui.Controls.SymbolRegular), item.IconSymbol),
                        FontSize = 16,
                        Foreground = (Brush)Application.Current.FindResource(item.TypeColorResource),
                        Margin = new Thickness(0, 2, 8, 0),
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    Grid.SetColumn(symbol, 0);
                    grid.Children.Add(symbol);

                    StackPanel contentStack = new StackPanel();

                    Grid headerGrid = new Grid();
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    Wpf.Ui.Controls.TextBlock titleText = new Wpf.Ui.Controls.TextBlock
                    {
                        Text = item.Title ?? "",
                        FontWeight = item.IsRead ? FontWeights.Normal : FontWeights.Bold,
                        FontSize = 12,
                        Foreground = (Brush)Application.Current.FindResource("TextFillColorPrimaryBrush")
                    };
                    Grid.SetColumn(titleText, 0);
                    headerGrid.Children.Add(titleText);

                    Wpf.Ui.Controls.TextBlock timeText = new Wpf.Ui.Controls.TextBlock
                    {
                        Text = item.TimeAgo,
                        FontSize = 10,
                        Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush")
                    };
                    Grid.SetColumn(timeText, 1);
                    headerGrid.Children.Add(timeText);

                    contentStack.Children.Add(headerGrid);

                    Wpf.Ui.Controls.TextBlock msgText = new Wpf.Ui.Controls.TextBlock
                    {
                        Text = item.Message ?? "",
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
                        Margin = new Thickness(0, 2, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    contentStack.Children.Add(msgText);

                    Grid.SetColumn(contentStack, 1);
                    grid.Children.Add(contentStack);

                    card.Child = grid;
                    string itemId = item.Id;
                    card.Cursor = Cursors.Hand;
                    card.MouseLeftButtonDown += (s, ev) =>
                    {
                        NotificationService.MarkAsRead(itemId);
                    };

                    NotificationListStack.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RenderNotificationList error: " + ex.Message);
            }
        }

        private void CreateToastCard(string title, string message, NotificationType type, int durationMs)
        {
            try
            {
                string iconText = "\uE946";
                string accentHex = "#3B82F6"; // Info Blue
                string bgHex = "#1E293B";

                switch (type)
                {
                    case NotificationType.Success:
                        iconText = "\uE73E"; // CheckMark
                        accentHex = "#10B981"; // Emerald Green
                        break;
                    case NotificationType.Warning:
                        iconText = "\uE7BA"; // Warning
                        accentHex = "#F59E0B"; // Amber Warning
                        break;
                    case NotificationType.Error:
                        iconText = "\uEA39"; // Error Badge
                        accentHex = "#EF4444"; // Crimson Red
                        break;
                    case NotificationType.Info:
                    default:
                        iconText = "\uE946"; // Info Icon
                        accentHex = "#3B82F6";
                        break;
                }

                Color accentColor = (Color)ColorConverter.ConvertFromString(accentHex);
                Color bgColor = (Color)ColorConverter.ConvertFromString(bgHex);

                Border toastCard = new Border
                {
                    Background = new SolidColorBrush(bgColor),
                    BorderBrush = new SolidColorBrush(accentColor),
                    BorderThickness = new Thickness(1.5, 0, 0, 0),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(12, 10, 12, 10),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 16,
                        ShadowDepth = 4,
                        Direction = 270,
                        Color = Colors.Black,
                        Opacity = 0.4
                    }
                };

                Grid toastGrid = new Grid();
                toastGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toastGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                toastGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Icon
                Wpf.Ui.Controls.TextBlock iconBlock = new Wpf.Ui.Controls.TextBlock
                {
                    Text = iconText,
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    Foreground = new SolidColorBrush(accentColor),
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 2, 10, 0)
                };
                Grid.SetColumn(iconBlock, 0);

                // Text Stack
                StackPanel textStack = new StackPanel();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    textStack.Children.Add(new Wpf.Ui.Controls.TextBlock
                    {
                        Text = title,
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        Foreground = Brushes.White,
                        Margin = new Thickness(0, 0, 0, 2)
                    });
                }
                textStack.Children.Add(new Wpf.Ui.Controls.TextBlock
                {
                    Text = message ?? string.Empty,
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    TextWrapping = TextWrapping.Wrap
                });
                Grid.SetColumn(textStack, 1);

                // Close Button
                Wpf.Ui.Controls.Button closeBtn = new Wpf.Ui.Controls.Button
                {
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                    Width = 22,
                    Height = 22,
                    Padding = new Thickness(0),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Content = new Wpf.Ui.Controls.TextBlock
                    {
                        Text = "\uE711",
                        FontFamily = new FontFamily("Segoe Fluent Icons"),
                        FontSize = 10,
                        Foreground = Brushes.White
                    }
                };
                Grid.SetColumn(closeBtn, 2);

                toastGrid.Children.Add(iconBlock);
                toastGrid.Children.Add(textStack);
                toastGrid.Children.Add(closeBtn);
                toastCard.Child = toastGrid;

                // Auto Dismiss Timer
                System.Windows.Threading.DispatcherTimer dismissTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(durationMs)
                };

                Action dismissAction = () =>
                {
                    dismissTimer.Stop();
                    DoubleAnimation fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(250)));
                    fadeOut.Completed += (s, e) =>
                    {
                        ToastContainer.Children.Remove(toastCard);
                    };
                    toastCard.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };

                dismissTimer.Tick += (s, e) => dismissAction();
                closeBtn.Click += (s, e) => dismissAction();

                // Entry Fade Animation
                DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(250)));
                toastCard.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                ToastContainer.Children.Add(toastCard);
                dismissTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Toast error: " + ex.Message);
            }
        }

        private void OnBottomRadioBarClicked(object sender, MouseButtonEventArgs e)
        {
            try
            {
                RootNavigation.Navigate(typeof(RadioPage));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[MainWindow] OnBottomRadioBarClicked: " + ex.Message); }
        }
    }
}











