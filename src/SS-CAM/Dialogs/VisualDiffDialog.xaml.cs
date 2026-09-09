using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SS_CAM.Services;
using Wpf.Ui.Controls;

namespace SS_CAM.Dialogs
{
    public enum DiffMode
    {
        SplitVertical,
        SplitHorizontal,
        SideBySide,
        OpacityBlend,
        PixelDiff
    }

    public partial class VisualDiffDialog : FluentWindow
    {
        private readonly string _projectPath;
        private string _beforePath;
        private string _afterPath;
        private BitmapImage _beforeBitmap;
        private BitmapImage _afterBitmap;
        private BitmapSource _diffBitmap;
        private DiffMode _currentMode = DiffMode.SplitVertical;

        private double _zoom = 1.0;
        private bool _isPanning = false;
        private Point _panStartPoint;
        private Point _initialTranslate;
        private bool _isDraggingSplitter = false;
        private bool _isUpdatingCombos = false;

        public VisualDiffDialog(string projectPath = null, string initialBefore = null, string initialAfter = null)
        {
            InitializeComponent();
            _projectPath = projectPath;
            _beforePath = initialBefore;
            _afterPath = initialAfter;

            Loaded += OnDialogLoaded;
            KeyDown += OnDialogKeyDown;
        }

        private void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            PopulateAssetDropdowns();
            SetMode(DiffMode.SplitVertical);

            if (string.IsNullOrWhiteSpace(_beforePath) && CmbBeforeAsset.Items.Count > 0)
            {
                CmbBeforeAsset.SelectedIndex = 0;
            }

            if (string.IsNullOrWhiteSpace(_afterPath) && CmbAfterAsset.Items.Count > 1)
            {
                CmbAfterAsset.SelectedIndex = 1;
            }
            else if (string.IsNullOrWhiteSpace(_afterPath) && CmbAfterAsset.Items.Count > 0)
            {
                CmbAfterAsset.SelectedIndex = 0;
            }

            ReloadAssets();
        }

        private void PopulateAssetDropdowns()
        {
            _isUpdatingCombos = true;
            try
            {
                List<string> projectImages = new List<string>();
                if (!string.IsNullOrWhiteSpace(_projectPath) && Directory.Exists(_projectPath))
                {
                    projectImages = VisualDiffService.ScanAllProjectImages(_projectPath);
                }

                if (!string.IsNullOrWhiteSpace(_beforePath) && !projectImages.Contains(_beforePath, StringComparer.OrdinalIgnoreCase))
                {
                    projectImages.Insert(0, _beforePath);
                }

                if (!string.IsNullOrWhiteSpace(_afterPath) && !projectImages.Contains(_afterPath, StringComparer.OrdinalIgnoreCase))
                {
                    projectImages.Insert(0, _afterPath);
                }

                CmbBeforeAsset.Items.Clear();
                CmbAfterAsset.Items.Clear();

                foreach (string file in projectImages)
                {
                    string label = Path.GetFileName(file);
                    string dir = Path.GetDirectoryName(file);
                    if (!string.IsNullOrWhiteSpace(_projectPath) && dir.StartsWith(_projectPath, StringComparison.OrdinalIgnoreCase))
                    {
                        string sub = dir.Substring(_projectPath.Length).TrimStart('\\', '/');
                        if (!string.IsNullOrWhiteSpace(sub)) label = string.Format("{0} ({1})", label, sub);
                    }

                    CmbBeforeAsset.Items.Add(new ComboBoxItem { Content = label, Tag = file });
                    CmbAfterAsset.Items.Add(new ComboBoxItem { Content = label, Tag = file });
                }

                SelectComboItem(CmbBeforeAsset, _beforePath);
                SelectComboItem(CmbAfterAsset, _afterPath);
            }
            finally
            {
                _isUpdatingCombos = false;
            }
        }

        private void SelectComboItem(ComboBox cmb, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                ComboBoxItem cbi = cmb.Items[i] as ComboBoxItem;
                if (cbi != null && string.Equals(cbi.Tag as string, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }
        }

        private void ReloadAssets()
        {
            _beforeBitmap = LoadBitmap(_beforePath);
            _afterBitmap = LoadBitmap(_afterPath);
            _diffBitmap = null;

            ImgBefore.Source = _beforeBitmap;
            ImgAfter.Source = _afterBitmap;
            ImgSideBefore.Source = _beforeBitmap;
            ImgSideAfter.Source = _afterBitmap;

            string beforeName = !string.IsNullOrWhiteSpace(_beforePath) ? Path.GetFileName(_beforePath) : "None";
            string afterName = !string.IsNullOrWhiteSpace(_afterPath) ? Path.GetFileName(_afterPath) : "None";

            TxtBadgeBefore.Text = string.Format("BEFORE: {0}", beforeName);
            TxtBadgeAfter.Text = string.Format("AFTER: {0}", afterName);
            TxtSideBeforeTitle.Text = string.Format("BEFORE: {0}", beforeName);
            TxtSideAfterTitle.Text = string.Format("AFTER: {0}", afterName);

            UpdateMetadataDisplay();
            ApplyModeSettings();
            ResetZoomAndPan();
        }

        private BitmapImage LoadBitmap(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(filePath, UriKind.Absolute);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[VisualDiffDialog] LoadBitmap error: " + ex.Message);
                return null;
            }
        }

        private void UpdateMetadataDisplay()
        {
            VisualImageMetadata metaBefore = VisualDiffService.GetImageMetadata(_beforePath);
            VisualImageMetadata metaAfter = VisualDiffService.GetImageMetadata(_afterPath);

            if (metaBefore != null)
            {
                TxtMetaBefore.Text = string.Format("Before: {0} • {1} • {2}",
                    metaBefore.DimensionsString, metaBefore.FormattedFileSize, metaBefore.Format);
            }
            else
            {
                TxtMetaBefore.Text = "Before: Not selected";
            }

            if (metaAfter != null)
            {
                string sizeDiffStr = "";
                if (metaBefore != null && metaBefore.FileSizeBytes > 0)
                {
                    double diffPct = ((double)(metaAfter.FileSizeBytes - metaBefore.FileSizeBytes) / metaBefore.FileSizeBytes) * 100.0;
                    sizeDiffStr = diffPct >= 0 ? string.Format(" (+{0:0.0}%)", diffPct) : string.Format(" ({0:0.0}%)", diffPct);
                }

                TxtMetaAfter.Text = string.Format("After: {0} • {1}{2} • {3}",
                    metaAfter.DimensionsString, metaAfter.FormattedFileSize, sizeDiffStr, metaAfter.Format);
            }
            else
            {
                TxtMetaAfter.Text = "After: Not selected";
            }

            if (metaBefore != null && metaAfter != null)
            {
                if (metaBefore.PixelWidth == metaAfter.PixelWidth && metaBefore.PixelHeight == metaAfter.PixelHeight)
                {
                    TxtDimensionMatch.Text = "1:1 MATCH";
                    BadgeDimensionMatch.Background = (Brush)FindResource("SystemFillColorSuccessBrush");
                    BadgeDimensionMatch.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtDimensionMatch.Text = "SCALED";
                    BadgeDimensionMatch.Background = (Brush)FindResource("SystemFillColorCautionBrush");
                    BadgeDimensionMatch.Visibility = Visibility.Visible;
                }
            }
            else
            {
                BadgeDimensionMatch.Visibility = Visibility.Collapsed;
            }
        }

        #region Mode Switching
        private void SetMode(DiffMode mode)
        {
            _currentMode = mode;

            BtnModeSplitVertical.Appearance = mode == DiffMode.SplitVertical ? ControlAppearance.Primary : ControlAppearance.Secondary;
            BtnModeSplitHorizontal.Appearance = mode == DiffMode.SplitHorizontal ? ControlAppearance.Primary : ControlAppearance.Secondary;
            BtnModeSideBySide.Appearance = mode == DiffMode.SideBySide ? ControlAppearance.Primary : ControlAppearance.Secondary;
            BtnModeBlend.Appearance = mode == DiffMode.OpacityBlend ? ControlAppearance.Primary : ControlAppearance.Secondary;
            BtnModeDiff.Appearance = mode == DiffMode.PixelDiff ? ControlAppearance.Primary : ControlAppearance.Secondary;

            ApplyModeSettings();
        }

        private async void ApplyModeSettings()
        {
            if (_currentMode == DiffMode.SideBySide)
            {
                SingleViewportContainer.Visibility = Visibility.Collapsed;
                SideBySideContainer.Visibility = Visibility.Visible;
                DiffSlider.IsEnabled = false;
                TxtSliderLabel.Text = "Side by Side:";
                TxtSliderPercent.Text = "Sync";
                return;
            }

            SingleViewportContainer.Visibility = Visibility.Visible;
            SideBySideContainer.Visibility = Visibility.Collapsed;
            DiffSlider.IsEnabled = true;

            switch (_currentMode)
            {
                case DiffMode.SplitVertical:
                    TxtSliderLabel.Text = "Split Position:";
                    ImgDiff.Visibility = Visibility.Collapsed;
                    ImgAfter.Visibility = Visibility.Visible;
                    ImgAfter.Opacity = 1.0;
                    SplitterOverlayCanvas.Visibility = Visibility.Visible;
                    SplitterLineVertical.Visibility = Visibility.Visible;
                    SplitterHandleVertical.Visibility = Visibility.Visible;
                    SplitterLineHorizontal.Visibility = Visibility.Collapsed;
                    SplitterHandleHorizontal.Visibility = Visibility.Collapsed;
                    UpdateSplitPosition(DiffSlider.Value);
                    break;

                case DiffMode.SplitHorizontal:
                    TxtSliderLabel.Text = "Split Position:";
                    ImgDiff.Visibility = Visibility.Collapsed;
                    ImgAfter.Visibility = Visibility.Visible;
                    ImgAfter.Opacity = 1.0;
                    SplitterOverlayCanvas.Visibility = Visibility.Visible;
                    SplitterLineVertical.Visibility = Visibility.Collapsed;
                    SplitterHandleVertical.Visibility = Visibility.Collapsed;
                    SplitterLineHorizontal.Visibility = Visibility.Visible;
                    SplitterHandleHorizontal.Visibility = Visibility.Visible;
                    UpdateSplitPosition(DiffSlider.Value);
                    break;

                case DiffMode.OpacityBlend:
                    TxtSliderLabel.Text = "Opacity Blend:";
                    ImgDiff.Visibility = Visibility.Collapsed;
                    ImgAfter.Visibility = Visibility.Visible;
                    SplitterOverlayCanvas.Visibility = Visibility.Collapsed;
                    ImgAfter.Clip = null;
                    ImgAfter.Opacity = DiffSlider.Value / 100.0;
                    TxtSliderPercent.Text = string.Format("{0}%", (int)DiffSlider.Value);
                    break;

                case DiffMode.PixelDiff:
                    TxtSliderLabel.Text = "Delta Threshold:";
                    ImgAfter.Visibility = Visibility.Collapsed;
                    SplitterOverlayCanvas.Visibility = Visibility.Collapsed;
                    ImgDiff.Visibility = Visibility.Visible;

                    if (_diffBitmap == null && _beforeBitmap != null && _afterBitmap != null)
                    {
                        _diffBitmap = await Task.Run(() => VisualDiffService.GeneratePixelDiffBitmap(_beforeBitmap, _afterBitmap));
                        ImgDiff.Source = _diffBitmap;
                    }
                    TxtSliderPercent.Text = "Auto";
                    break;
            }
        }

        private void OnModeSplitVerticalClicked(object sender, RoutedEventArgs e) { SetMode(DiffMode.SplitVertical); }
        private void OnModeSplitHorizontalClicked(object sender, RoutedEventArgs e) { SetMode(DiffMode.SplitHorizontal); }
        private void OnModeSideBySideClicked(object sender, RoutedEventArgs e) { SetMode(DiffMode.SideBySide); }
        private void OnModeBlendClicked(object sender, RoutedEventArgs e) { SetMode(DiffMode.OpacityBlend); }
        private void OnModeDiffClicked(object sender, RoutedEventArgs e) { SetMode(DiffMode.PixelDiff); }
        #endregion

        #region Slider & Split Logic
        private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;

            if (_currentMode == DiffMode.OpacityBlend)
            {
                ImgAfter.Opacity = e.NewValue / 100.0;
                TxtSliderPercent.Text = string.Format("{0}%", (int)e.NewValue);
            }
            else if (_currentMode == DiffMode.SplitVertical || _currentMode == DiffMode.SplitHorizontal)
            {
                TxtSliderPercent.Text = string.Format("{0}%", (int)e.NewValue);
                UpdateSplitPosition(e.NewValue);
            }
        }

        private void UpdateSplitPosition(double percent)
        {
            double w = ImgBefore.ActualWidth;
            double h = ImgBefore.ActualHeight;

            if (w <= 0 || h <= 0)
            {
                if (_beforeBitmap != null)
                {
                    w = _beforeBitmap.PixelWidth;
                    h = _beforeBitmap.PixelHeight;
                }
                else return;
            }

            double ratio = Math.Max(0.0, Math.Min(1.0, percent / 100.0));

            if (_currentMode == DiffMode.SplitVertical)
            {
                double splitX = w * ratio;
                ImgAfterClip.Rect = new Rect(splitX, 0, Math.Max(0, w - splitX), h);

                SplitterLineVertical.X1 = splitX;
                SplitterLineVertical.Y1 = 0;
                SplitterLineVertical.X2 = splitX;
                SplitterLineVertical.Y2 = h;

                Canvas.SetLeft(SplitterHandleVertical, splitX - 18);
                Canvas.SetTop(SplitterHandleVertical, (h / 2.0) - 18);
            }
            else if (_currentMode == DiffMode.SplitHorizontal)
            {
                double splitY = h * ratio;
                ImgAfterClip.Rect = new Rect(0, splitY, w, Math.Max(0, h - splitY));

                SplitterLineHorizontal.X1 = 0;
                SplitterLineHorizontal.Y1 = splitY;
                SplitterLineHorizontal.X2 = w;
                SplitterLineHorizontal.Y2 = splitY;

                Canvas.SetLeft(SplitterHandleHorizontal, (w / 2.0) - 18);
                Canvas.SetTop(SplitterHandleHorizontal, splitY - 18);
            }
        }
        #endregion

        #region Asset Selection & Swapping
        private void OnBeforeAssetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingCombos) return;
            ComboBoxItem cbi = CmbBeforeAsset.SelectedItem as ComboBoxItem;
            string path = cbi != null ? cbi.Tag as string : null;
            if (!string.IsNullOrEmpty(path))
            {
                _beforePath = path;
                ReloadAssets();
            }
        }

        private void OnAfterAssetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingCombos) return;
            ComboBoxItem cbi = CmbAfterAsset.SelectedItem as ComboBoxItem;
            string path = cbi != null ? cbi.Tag as string : null;
            if (!string.IsNullOrEmpty(path))
            {
                _afterPath = path;
                ReloadAssets();
            }
        }

        private void OnBrowseBeforeClicked(object sender, RoutedEventArgs e)
        {
            string file = BrowseImageFile("Select 'Before' Image");
            if (!string.IsNullOrWhiteSpace(file))
            {
                _beforePath = file;
                PopulateAssetDropdowns();
                ReloadAssets();
            }
        }

        private void OnBrowseAfterClicked(object sender, RoutedEventArgs e)
        {
            string file = BrowseImageFile("Select 'After' Image");
            if (!string.IsNullOrWhiteSpace(file))
            {
                _afterPath = file;
                PopulateAssetDropdowns();
                ReloadAssets();
            }
        }

        private string BrowseImageFile(string title)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = title,
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All Files (*.*)|*.*",
                InitialDirectory = !string.IsNullOrWhiteSpace(_projectPath) && Directory.Exists(_projectPath) ? _projectPath : null
            };

            return ofd.ShowDialog() == true ? ofd.FileName : null;
        }

        private void OnSwapClicked(object sender, RoutedEventArgs e)
        {
            string temp = _beforePath;
            _beforePath = _afterPath;
            _afterPath = temp;

            _isUpdatingCombos = true;
            try
            {
                SelectComboItem(CmbBeforeAsset, _beforePath);
                SelectComboItem(CmbAfterAsset, _afterPath);
            }
            finally
            {
                _isUpdatingCombos = false;
            }

            ReloadAssets();
        }
        #endregion

        #region Viewport Navigation (Zoom & Pan)
        private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.15 : 0.85;
            ApplyZoom(_zoom * factor);
            e.Handled = true;
        }

        private void ApplyZoom(double targetZoom)
        {
            _zoom = Math.Max(0.1, Math.Min(10.0, targetZoom));
            CanvasScale.ScaleX = _zoom;
            CanvasScale.ScaleY = _zoom;

            SideBeforeScale.ScaleX = _zoom;
            SideBeforeScale.ScaleY = _zoom;
            SideAfterScale.ScaleX = _zoom;
            SideAfterScale.ScaleY = _zoom;

            TxtZoomLevel.Text = string.Format("{0}%", (int)Math.Round(_zoom * 100));
        }

        private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                (_currentMode == DiffMode.SplitVertical || _currentMode == DiffMode.SplitHorizontal))
            {
                _isDraggingSplitter = true;
                ViewportOuterGrid.CaptureMouse();
                UpdateSplitFromMouse(e.GetPosition(SingleViewportContainer));
                e.Handled = true;
                return;
            }

            if (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed ||
                (_currentMode == DiffMode.SideBySide && e.LeftButton == MouseButtonState.Pressed))
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(ViewportOuterGrid);
                _initialTranslate = new Point(CanvasTranslate.X, CanvasTranslate.Y);
                ViewportOuterGrid.CaptureMouse();
                ViewportOuterGrid.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
        }

        private void OnViewportMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSplitter)
            {
                UpdateSplitFromMouse(e.GetPosition(SingleViewportContainer));
                e.Handled = true;
                return;
            }

            if (_isPanning)
            {
                Point current = e.GetPosition(ViewportOuterGrid);
                Vector delta = current - _panStartPoint;

                CanvasTranslate.X = _initialTranslate.X + delta.X;
                CanvasTranslate.Y = _initialTranslate.Y + delta.Y;

                SideBeforeTranslate.X = CanvasTranslate.X;
                SideBeforeTranslate.Y = CanvasTranslate.Y;
                SideAfterTranslate.X = CanvasTranslate.X;
                SideAfterTranslate.Y = CanvasTranslate.Y;

                e.Handled = true;
            }
        }

        private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSplitter)
            {
                _isDraggingSplitter = false;
                ViewportOuterGrid.ReleaseMouseCapture();
                e.Handled = true;
            }

            if (_isPanning)
            {
                _isPanning = false;
                ViewportOuterGrid.ReleaseMouseCapture();
                ViewportOuterGrid.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        private void UpdateSplitFromMouse(Point p)
        {
            double w = ImgBefore.ActualWidth;
            double h = ImgBefore.ActualHeight;

            if (w <= 0 || h <= 0) return;

            if (_currentMode == DiffMode.SplitVertical)
            {
                double pct = Math.Max(0.0, Math.Min(100.0, (p.X / w) * 100.0));
                DiffSlider.Value = pct;
            }
            else if (_currentMode == DiffMode.SplitHorizontal)
            {
                double pct = Math.Max(0.0, Math.Min(100.0, (p.Y / h) * 100.0));
                DiffSlider.Value = pct;
            }
        }

        private void ResetZoomAndPan()
        {
            _zoom = 1.0;
            CanvasScale.ScaleX = 1.0;
            CanvasScale.ScaleY = 1.0;
            CanvasTranslate.X = 0;
            CanvasTranslate.Y = 0;

            SideBeforeScale.ScaleX = 1.0;
            SideBeforeScale.ScaleY = 1.0;
            SideBeforeTranslate.X = 0;
            SideBeforeTranslate.Y = 0;

            SideAfterScale.ScaleX = 1.0;
            SideAfterScale.ScaleY = 1.0;
            SideAfterTranslate.X = 0;
            SideAfterTranslate.Y = 0;

            TxtZoomLevel.Text = "100%";
        }

        private void OnZoomOutClicked(object sender, RoutedEventArgs e) { ApplyZoom(_zoom * 0.85); }
        private void OnZoomInClicked(object sender, RoutedEventArgs e) { ApplyZoom(_zoom * 1.15); }
        private void OnZoomFitClicked(object sender, RoutedEventArgs e) { ResetZoomAndPan(); }
        private void OnZoom100Clicked(object sender, RoutedEventArgs e) { ApplyZoom(1.0); }
        #endregion

        #region Keyboard & Close
        private void OnDialogKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                DiffSlider.Value = Math.Max(0, DiffSlider.Value - 2);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                DiffSlider.Value = Math.Min(100, DiffSlider.Value + 2);
                e.Handled = true;
            }
            else if (e.Key == Key.Space)
            {
                OnSwapClicked(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion
    }
}
