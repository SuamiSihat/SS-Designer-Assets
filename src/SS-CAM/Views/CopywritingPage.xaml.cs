using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SS_CAM.Models;
using SS_CAM.Services;
using SS_CAM.Utilities;

namespace SS_CAM.Views
{
    public partial class CopywritingPage : Page
    {
        private string workspaceRoot = string.Empty;
        private List<ProjectItemInfo> discoveredProjects = new List<ProjectItemInfo>();
        private ProjectItemInfo selectedProject = null;
        private bool isInternalChange = false;
        private int currentViewMode = 0; // 0 = PreviewLive, 1 = Split, 2 = EditMode, 3 = MockupView
        private int currentSubMode = 0; // 0 = Doc, 1 = WhatsApp, 2 = MetaAd, 3 = Both
        private bool isMetaTextExpanded = false;
        private DispatcherTimer liveSimulationDebounceTimer;
        private bool isRightPaneCollapsed = false;
        private GridLength rightPaneSavedWidth = new GridLength(320);

        public class ProjectItemInfo
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public string ProjectId { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        public CopywritingPage()
        {
            InitializeComponent();

            liveSimulationDebounceTimer = new DispatcherTimer();
            liveSimulationDebounceTimer.Interval = TimeSpan.FromMilliseconds(150);
            liveSimulationDebounceTimer.Tick += OnLiveSimulationTimerTick;

            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            UserProfile profile = UserProfileService.LoadProfile();
            if (profile != null && !string.IsNullOrWhiteSpace(profile.WorkspaceRoot))
            {
                workspaceRoot = profile.WorkspaceRoot;
            }

            WorkspaceWatcherService.Instance.WorkspaceChanged += OnWorkspaceChanged;
            await LoadProjectsAsync();
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (liveSimulationDebounceTimer != null)
                {
                    liveSimulationDebounceTimer.Stop();
                }
                WorkspaceWatcherService.Instance.WorkspaceChanged -= OnWorkspaceChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[CopywritingPage] PageUnload error: " + ex.Message);
            }
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangedEventArgs e)
        {
            if (e.ChangeType == WorkspaceChangeType.ProjectCopywriting)
            {
                Dispatcher.Invoke(delegate
                {
                    try
                    {
                        if (selectedProject != null && string.Equals(e.ProjectPath, selectedProject.FullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            string content = CopywritingDesktopService.LoadCopywriting(selectedProject.FullPath, selectedProject.ProjectId, workspaceRoot, selectedProject.Name);
                            CopyScriptEditor.Text = content;
                            if (RenderedCopyViewer != null)
                            {
                                RenderedCopyViewer.Document = MarkdownHelper.ToFlowDocument(content);
                            }
                            UpdateMetrics(content);
                            UpdateLiveSimulation(content);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[CopywritingPage] OnWorkspaceChanged error: " + ex.Message);
                    }
                });
            }
        }

        private async Task LoadProjectsAsync()
        {
            if (LoadingOverlay != null)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
            }

            try
            {
                discoveredProjects.Clear();
                ProjectSelectorCmb.Items.Clear();

                if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
                {
                    TxtFilePath.Text = "Workspace root not found or not configured in Settings.";
                    SetStatusBadge("Not Configured", false);
                    return;
                }

                SetStatusBadge("Scanning...", false);
                List<ProjectItemInfo> projects = await Task.Factory.StartNew(delegate
                {
                    List<ProjectItemInfo> list = new List<ProjectItemInfo>();
                    try
                    {
                        Queue<string> queue = new Queue<string>();
                        queue.Enqueue(workspaceRoot);
                        Regex pattern = new Regex(@"^\d{6}_\d[A-Z0-9]*", RegexOptions.IgnoreCase);

                        while (queue.Count > 0)
                        {
                            string current = queue.Dequeue();
                            string[] subdirs;
                            try { subdirs = Directory.GetDirectories(current); }
                            catch (Exception ex) { Debug.WriteLine("[CopywritingPage] GetDirectories: " + ex.Message); continue; }

                            foreach (string sub in subdirs)
                            {
                                string dirName = Path.GetFileName(sub);
                                if (string.IsNullOrEmpty(dirName) ||
                                    dirName.StartsWith(".") ||
                                    dirName.StartsWith("_") ||
                                    dirName.StartsWith("#") ||
                                    dirName.StartsWith("@") ||
                                    dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                                    dirName.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                if (pattern.IsMatch(dirName))
                                {
                                    ProjectItemInfo info = new ProjectItemInfo();
                                    info.Name = dirName;
                                    info.FullPath = sub;
                                    info.ProjectId = dirName;
                                    list.Add(info);
                                }
                                else
                                {
                                    queue.Enqueue(sub);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[CopywritingPage] Scan error: " + ex.Message);
                    }

                    // Sort descending (newest projects first)
                    list.Sort(delegate (ProjectItemInfo a, ProjectItemInfo b)
                    {
                        return string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase);
                    });

                    return list;
                });

                discoveredProjects = projects;
                foreach (ProjectItemInfo p in discoveredProjects)
                {
                    ProjectSelectorCmb.Items.Add(p);
                }

                if (ProjectSelectorCmb.Items.Count > 0)
                {
                    ProjectSelectorCmb.SelectedIndex = 0;
                }
                else
                {
                    TxtFilePath.Text = "No project vaults discovered in workspace.";
                    SetStatusBadge("Idle", false);
                }
            }
            finally
            {
                if (LoadingOverlay != null)
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OnProjectSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedProject = ProjectSelectorCmb.SelectedItem as ProjectItemInfo;
            if (selectedProject == null)
            {
                TxtFilePath.Text = "No project selected";
                CopyScriptEditor.Text = string.Empty;
                if (RenderedCopyViewer != null)
                {
                    RenderedCopyViewer.Document = MarkdownHelper.ToFlowDocument(string.Empty);
                }
                UpdateMetrics(string.Empty);
                UpdateLiveSimulation(string.Empty);
                UpdateProjectProperties(null);
                SetStatusBadge("No Project", false);
                return;
            }

            string filePath = CopywritingDesktopService.GetCopyFilePath(selectedProject.FullPath, selectedProject.ProjectId, workspaceRoot);
            TxtFilePath.Text = filePath ?? "Unknown path";

            string content = CopywritingDesktopService.LoadCopywriting(selectedProject.FullPath, selectedProject.ProjectId, workspaceRoot, selectedProject.Name);

            isInternalChange = true;
            CopyScriptEditor.Text = content;
            isInternalChange = false;

            UpdateMetrics(content);
            UpdateLiveSimulation(content);
            UpdateProjectProperties(selectedProject);
            SetStatusBadge("NAS Synced", true);

            // Default to Preview Live Markdown mode (Default View)
            ApplyViewMode(0);
        }

        private void OnModePreviewClicked(object sender, RoutedEventArgs e)
        {
            ApplyViewMode(0);
        }

        private void OnModeSplitClicked(object sender, RoutedEventArgs e)
        {
            ApplyViewMode(1);
        }

        private void OnModeEditClicked(object sender, RoutedEventArgs e)
        {
            ApplyViewMode(2);
        }

        private void OnModeMockupClicked(object sender, RoutedEventArgs e)
        {
            ApplyViewMode(3);
        }

        private void ApplyViewMode(int mode)
        {
            currentViewMode = mode;

            if (mode == 0) // 1. Preview Live Markdown (Default Full-Width View)
            {
                ColEditor.Width = new GridLength(1, GridUnitType.Star);
                ColSplitter.Width = new GridLength(0, GridUnitType.Pixel);
                ColLivePreview.Width = new GridLength(0, GridUnitType.Pixel);

                CopyScriptEditor.Visibility = Visibility.Collapsed;
                RenderedCopyViewer.Visibility = Visibility.Visible;
                LiveGridSplitter.Visibility = Visibility.Collapsed;
                RightPaneContainer.Visibility = Visibility.Collapsed;

                if (RenderedCopyViewer != null && CopyScriptEditor != null)
                {
                    RenderedCopyViewer.Document = MarkdownHelper.ToFlowDocument(CopyScriptEditor.Text ?? string.Empty);
                }

                TxtCanvasHeader.Text = "Rendered Markdown Document (Preview Live)";
                TxtEditorShortcutHint.Visibility = Visibility.Collapsed;

                BtnModePreview.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                BtnModeSplit.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                BtnModeEdit.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                BtnModeMockup.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            }
            else if (mode == 1) // 2. Split View: Raw Editor (Left) + Sub-Mode Right Pane Side-by-Side
            {
                ColEditor.Width = new GridLength(1, GridUnitType.Star);
                ColSplitter.Width = new GridLength(12, GridUnitType.Pixel);
                ColLivePreview.Width = new GridLength(1, GridUnitType.Star);

                CopyScriptEditor.Visibility = Visibility.Visible;
                RenderedCopyViewer.Visibility = Visibility.Collapsed;
                LiveGridSplitter.Visibility = Visibility.Visible;
                RightPaneContainer.Visibility = Visibility.Visible;

                ApplySubMode(currentSubMode);

                TxtCanvasHeader.Text = "Copywriting Studio — Side-by-Side Split View";
                TxtEditorShortcutHint.Visibility = Visibility.Visible;

                BtnModePreview.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                BtnModeSplit.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                BtnModeEdit.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                BtnModeMockup.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            }
            else if (mode == 2) // 3. Edit Mode: Full Width Monospace Markdown Editor
            {
                ColEditor.Width = new GridLength(1, GridUnitType.Star);
                ColSplitter.Width = new GridLength(0, GridUnitType.Pixel);
                ColLivePreview.Width = new GridLength(0, GridUnitType.Pixel);

                CopyScriptEditor.Visibility = Visibility.Visible;
                RenderedCopyViewer.Visibility = Visibility.Collapsed;
                LiveGridSplitter.Visibility = Visibility.Collapsed;
                RightPaneContainer.Visibility = Visibility.Collapsed;

                TxtCanvasHeader.Text = "Markdown Copy Editor (COPY.md)";
                TxtEditorShortcutHint.Visibility = Visibility.Visible;

                BtnModePreview.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                BtnModeSplit.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                BtnModeEdit.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                BtnModeMockup.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;

                CopyScriptEditor.Focus();
            }
            else // mode == 3: 4. Mockup View: WhatsApp & Meta Ads Live Simulation
            {
                ColEditor.Width = new GridLength(0, GridUnitType.Pixel);
                ColSplitter.Width = new GridLength(0, GridUnitType.Pixel);
                ColLivePreview.Width = new GridLength(1, GridUnitType.Star);

                CopyScriptEditor.Visibility = Visibility.Collapsed;
                RenderedCopyViewer.Visibility = Visibility.Collapsed;
                LiveGridSplitter.Visibility = Visibility.Collapsed;
                RightPaneContainer.Visibility = Visibility.Visible;

                if (currentSubMode == 0)
                {
                    currentSubMode = 3; // Default to Both mockups in Mockup View
                }
                ApplySubMode(currentSubMode);

                TxtCanvasHeader.Text = "WhatsApp Broadcast & Meta Ad Live Mockups";
                TxtEditorShortcutHint.Visibility = Visibility.Collapsed;

                BtnModePreview.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                BtnModeSplit.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                BtnModeEdit.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                BtnModeMockup.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            }
        }

        private void OnSubModeDocClicked(object sender, RoutedEventArgs e)
        {
            ApplySubMode(0);
        }

        private void OnSubModeWhatsAppClicked(object sender, RoutedEventArgs e)
        {
            ApplySubMode(1);
        }

        private void OnSubModeMetaAdClicked(object sender, RoutedEventArgs e)
        {
            ApplySubMode(2);
        }

        private void OnSubModeBothClicked(object sender, RoutedEventArgs e)
        {
            ApplySubMode(3);
        }

        private void ApplySubMode(int subMode)
        {
            currentSubMode = subMode;

            if (BtnSubModeDoc != null)
                BtnSubModeDoc.Appearance = (subMode == 0) ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
            if (BtnSubModeWhatsApp != null)
                BtnSubModeWhatsApp.Appearance = (subMode == 1) ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
            if (BtnSubModeMetaAd != null)
                BtnSubModeMetaAd.Appearance = (subMode == 2) ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
            if (BtnSubModeBoth != null)
                BtnSubModeBoth.Appearance = (subMode == 3) ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;

            string currentText = CopyScriptEditor != null ? CopyScriptEditor.Text ?? string.Empty : string.Empty;

            if (subMode == 0) // 1. Rendered Markdown Document
            {
                if (RenderedCopyViewerSplit != null)
                {
                    RenderedCopyViewerSplit.Visibility = Visibility.Visible;
                    RenderedCopyViewerSplit.Document = MarkdownHelper.ToFlowDocument(currentText);
                }
                if (LivePreviewPanel != null) LivePreviewPanel.Visibility = Visibility.Collapsed;
                if (TxtSubPreviewTitle != null) TxtSubPreviewTitle.Text = "Rendered Markdown Document";
            }
            else if (subMode == 1) // 2. WhatsApp Broadcast only
            {
                if (RenderedCopyViewerSplit != null) RenderedCopyViewerSplit.Visibility = Visibility.Collapsed;
                if (LivePreviewPanel != null) LivePreviewPanel.Visibility = Visibility.Visible;
                if (CardWhatsAppPreview != null) CardWhatsAppPreview.Visibility = Visibility.Visible;
                if (CardMetaAdPreview != null) CardMetaAdPreview.Visibility = Visibility.Collapsed;
                if (ColMockupWhatsApp != null) ColMockupWhatsApp.Width = new GridLength(1, GridUnitType.Star);
                if (ColMockupDivider != null) ColMockupDivider.Width = new GridLength(0);
                if (ColMockupMetaAd != null) ColMockupMetaAd.Width = new GridLength(0);
                if (TxtSubPreviewTitle != null) TxtSubPreviewTitle.Text = "WhatsApp Broadcast Simulation";
                UpdateLiveSimulation(currentText);
            }
            else if (subMode == 2) // 3. Meta Ad only
            {
                if (RenderedCopyViewerSplit != null) RenderedCopyViewerSplit.Visibility = Visibility.Collapsed;
                if (LivePreviewPanel != null) LivePreviewPanel.Visibility = Visibility.Visible;
                if (CardWhatsAppPreview != null) CardWhatsAppPreview.Visibility = Visibility.Collapsed;
                if (CardMetaAdPreview != null) CardMetaAdPreview.Visibility = Visibility.Visible;
                if (ColMockupWhatsApp != null) ColMockupWhatsApp.Width = new GridLength(0);
                if (ColMockupDivider != null) ColMockupDivider.Width = new GridLength(0);
                if (ColMockupMetaAd != null) ColMockupMetaAd.Width = new GridLength(1, GridUnitType.Star);
                if (TxtSubPreviewTitle != null) TxtSubPreviewTitle.Text = "Meta Ads Sponsored Post Simulation";
                UpdateLiveSimulation(currentText);
            }
            else // subMode == 3: Both mockups side-by-side
            {
                if (RenderedCopyViewerSplit != null) RenderedCopyViewerSplit.Visibility = Visibility.Collapsed;
                if (LivePreviewPanel != null) LivePreviewPanel.Visibility = Visibility.Visible;
                if (CardWhatsAppPreview != null) CardWhatsAppPreview.Visibility = Visibility.Visible;
                if (CardMetaAdPreview != null) CardMetaAdPreview.Visibility = Visibility.Visible;
                if (ColMockupWhatsApp != null) ColMockupWhatsApp.Width = new GridLength(1, GridUnitType.Star);
                if (ColMockupDivider != null) ColMockupDivider.Width = new GridLength(14);
                if (ColMockupMetaAd != null) ColMockupMetaAd.Width = new GridLength(1, GridUnitType.Star);
                if (TxtSubPreviewTitle != null) TxtSubPreviewTitle.Text = "Combined WhatsApp & Meta Ad Preview";
                UpdateLiveSimulation(currentText);
            }
        }

        private void OnCopyScriptTextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInternalChange) return;

            string currentText = CopyScriptEditor.Text ?? string.Empty;
            UpdateMetrics(currentText);

            if (liveSimulationDebounceTimer != null)
            {
                liveSimulationDebounceTimer.Stop();
                liveSimulationDebounceTimer.Start();
            }
            else
            {
                UpdateLiveSimulation(currentText);
            }

            SetStatusBadge("Unsaved Changes", false);
        }

        private void OnLiveSimulationTimerTick(object sender, EventArgs e)
        {
            if (liveSimulationDebounceTimer != null)
            {
                liveSimulationDebounceTimer.Stop();
            }

            string currentText = CopyScriptEditor != null ? CopyScriptEditor.Text ?? string.Empty : string.Empty;

            if (currentViewMode == 0 && RenderedCopyViewer != null)
            {
                RenderedCopyViewer.Document = MarkdownHelper.ToFlowDocument(currentText);
            }
            else if (currentViewMode == 1)
            {
                if (currentSubMode == 0 && RenderedCopyViewerSplit != null)
                {
                    RenderedCopyViewerSplit.Document = MarkdownHelper.ToFlowDocument(currentText);
                }
                else
                {
                    UpdateLiveSimulation(currentText);
                }
            }
            else if (currentViewMode == 3)
            {
                UpdateLiveSimulation(currentText);
            }
        }

        private void UpdateLiveSimulation(string content)
        {
            if (TxtWhatsAppLiveContent != null)
            {
                BuildWhatsAppInlines(TxtWhatsAppLiveContent, content);
            }

            UpdateWhatsAppLinkPreview(content);
            UpdateMetaAdSimulation(content);

            if (TxtWhatsAppTimestamp != null)
            {
                TxtWhatsAppTimestamp.Text = DateTime.Now.ToString("h:mm tt");
            }
        }

        private void BuildWhatsAppInlines(TextBlock targetBlock, string rawContent)
        {
            if (targetBlock == null) return;
            targetBlock.Inlines.Clear();

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                targetBlock.Inlines.Add(new Run("Drafting your copy in the editor on the left will render formatted WhatsApp inlines here in real time..."));
                return;
            }

            string formatted = CopywritingDesktopService.FormatForWhatsApp(rawContent);

            Regex tokenRegex = new Regex(@"(https?://[^\s)""'>]+|wa\.me/[^\s)""'>]+)|\*([^*\r\n]+)\*|_([^_\r\n]+)_|~([^~\r\n]+)~|`([^`\r\n]+)`", RegexOptions.Compiled);

            int lastIdx = 0;
            foreach (Match m in tokenRegex.Matches(formatted))
            {
                if (m.Index > lastIdx)
                {
                    targetBlock.Inlines.Add(new Run(formatted.Substring(lastIdx, m.Index - lastIdx)));
                }

                if (m.Groups[1].Success) // URL
                {
                    string urlText = m.Groups[1].Value;
                    Run r = new Run(urlText)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(2, 126, 181)),
                        TextDecorations = TextDecorations.Underline
                    };
                    targetBlock.Inlines.Add(r);
                }
                else if (m.Groups[2].Success) // Bold *text*
                {
                    Bold b = new Bold(new Run(m.Groups[2].Value));
                    targetBlock.Inlines.Add(b);
                }
                else if (m.Groups[3].Success) // Italic _text_
                {
                    Italic it = new Italic(new Run(m.Groups[3].Value));
                    targetBlock.Inlines.Add(it);
                }
                else if (m.Groups[4].Success) // Strikethrough ~text~
                {
                    Run r = new Run(m.Groups[4].Value)
                    {
                        TextDecorations = TextDecorations.Strikethrough
                    };
                    targetBlock.Inlines.Add(r);
                }
                else if (m.Groups[5].Success) // Monospace code `text`
                {
                    Run r = new Run(m.Groups[5].Value)
                    {
                        FontFamily = new FontFamily("Consolas, monospace"),
                        Background = new SolidColorBrush(Color.FromRgb(235, 237, 240))
                    };
                    targetBlock.Inlines.Add(r);
                }

                lastIdx = m.Index + m.Length;
            }

            if (lastIdx < formatted.Length)
            {
                targetBlock.Inlines.Add(new Run(formatted.Substring(lastIdx)));
            }
        }

        private void UpdateWhatsAppLinkPreview(string markdown)
        {
            string url = CopywritingDesktopService.ExtractFirstUrl(markdown);
            if (!string.IsNullOrWhiteSpace(url) && WhatsAppLinkPreviewCard != null)
            {
                WhatsAppLinkPreviewCard.Visibility = Visibility.Visible;
                try
                {
                    Uri uri = new Uri(url);
                    if (TxtWhatsAppPreviewDomain != null) TxtWhatsAppPreviewDomain.Text = uri.Host.ToUpperInvariant();
                }
                catch
                {
                    if (TxtWhatsAppPreviewDomain != null) TxtWhatsAppPreviewDomain.Text = "SUAMISIHAT.CLINIC";
                }

                if (TxtWhatsAppPreviewUrl != null) TxtWhatsAppPreviewUrl.Text = url;
            }
            else if (WhatsAppLinkPreviewCard != null)
            {
                WhatsAppLinkPreviewCard.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateMetaAdSimulation(string markdown)
        {
            string headline, cta, primaryText;
            CopywritingDesktopService.ExtractHeadlineAndCta(markdown, out headline, out cta, out primaryText);

            if (TxtMetaHeadline != null)
            {
                TxtMetaHeadline.Text = !string.IsNullOrWhiteSpace(headline) ? headline : "SuamiSihat — Formulasi Tenaga & Vitaliti Maskulin Premium";
            }

            if (TxtMetaCtaButton != null)
            {
                TxtMetaCtaButton.Text = !string.IsNullOrWhiteSpace(cta) ? cta : "Send Message";
            }

            string detectedUrl = CopywritingDesktopService.ExtractFirstUrl(markdown);
            if (!string.IsNullOrWhiteSpace(detectedUrl))
            {
                try
                {
                    Uri uri = new Uri(detectedUrl);
                    if (TxtMetaDomain != null) TxtMetaDomain.Text = uri.Host.ToUpperInvariant();
                }
                catch
                {
                    if (TxtMetaDomain != null) TxtMetaDomain.Text = "SUAMISIHAT.CLINIC";
                }
            }
            else
            {
                if (TxtMetaDomain != null) TxtMetaDomain.Text = "SUAMISIHAT.CLINIC";
            }

            if (TxtMetaAdLiveContent != null)
            {
                if (string.IsNullOrWhiteSpace(primaryText))
                {
                    TxtMetaAdLiveContent.Text = "Primary ad copy will appear here formatted for Facebook & Instagram feed ads.";
                    if (BtnMetaSeeMore != null) BtnMetaSeeMore.Visibility = Visibility.Collapsed;
                }
                else if (primaryText.Length > 180 && !isMetaTextExpanded)
                {
                    TxtMetaAdLiveContent.Text = primaryText.Substring(0, 180).TrimEnd() + "...";
                    if (BtnMetaSeeMore != null)
                    {
                        BtnMetaSeeMore.Visibility = Visibility.Visible;
                        if (TxtMetaSeeMoreLabel != null) TxtMetaSeeMoreLabel.Text = "... See more";
                    }
                }
                else
                {
                    TxtMetaAdLiveContent.Text = primaryText;
                    if (BtnMetaSeeMore != null)
                    {
                        if (primaryText.Length > 180)
                        {
                            BtnMetaSeeMore.Visibility = Visibility.Visible;
                            if (TxtMetaSeeMoreLabel != null) TxtMetaSeeMoreLabel.Text = "See less";
                        }
                        else
                        {
                            BtnMetaSeeMore.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        private void OnMetaSeeMoreClicked(object sender, RoutedEventArgs e)
        {
            isMetaTextExpanded = !isMetaTextExpanded;
            UpdateMetaAdSimulation(CopyScriptEditor != null ? CopyScriptEditor.Text ?? string.Empty : string.Empty);
        }

        private void SetStatusBadge(string statusText, bool isSuccess)
        {
            if (TxtSaveStatus != null)
            {
                TxtSaveStatus.Text = statusText;
            }

            if (StatusBadgeDot != null)
            {
                if (isSuccess)
                {
                    StatusBadgeDot.Fill = (Brush)FindResource("SystemFillColorSuccessBrush");
                }
                else if (string.Equals(statusText, "Unsaved Changes", StringComparison.OrdinalIgnoreCase))
                {
                    StatusBadgeDot.Fill = (Brush)FindResource("SystemFillColorCautionBrush");
                }
                else
                {
                    StatusBadgeDot.Fill = (Brush)FindResource("TextFillColorSecondaryBrush");
                }
            }
        }

        private void UpdateMetrics(string content)
        {
            int words, chars, lines, readingSec, speakingSec;
            CopywritingDesktopService.ComputeMetrics(content, out words, out chars, out lines, out readingSec, out speakingSec);

            TxtMetricWords.Text = string.Format("{0} words", words);
            TxtMetricChars.Text = string.Format("{0} chars", chars);
            TxtMetricLines.Text = string.Format("{0} lines", lines);

            if (readingSec < 60)
            {
                TxtMetricReadingTime.Text = string.Format("~{0} sec", readingSec);
            }
            else
            {
                int min = readingSec / 60;
                int sec = readingSec % 60;
                TxtMetricReadingTime.Text = string.Format("~{0}m {1}s", min, sec);
            }

            if (speakingSec < 60)
            {
                TxtMetricSpeakingTime.Text = string.Format("~{0} sec", speakingSec);
            }
            else
            {
                int sMin = speakingSec / 60;
                int sSec = speakingSec % 60;
                TxtMetricSpeakingTime.Text = string.Format("~{0}m {1}s", sMin, sSec);
            }
        }

        private void OnSaveScriptClicked(object sender, RoutedEventArgs e)
        {
            SaveCurrentScript();
        }

        private void SaveCurrentScript()
        {
            if (selectedProject == null)
            {
                MessageBox.Show("Please select a project first.", "No Project Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string text = CopyScriptEditor.Text;
            bool success = CopywritingDesktopService.SaveCopywriting(selectedProject.FullPath, selectedProject.ProjectId, workspaceRoot, text);

            if (success)
            {
                // Save local revision snapshot
                CopywritingDesktopService.SaveSnapshot(selectedProject.FullPath, selectedProject.ProjectId, workspaceRoot, text);

                SetStatusBadge(string.Format("Saved {0}", DateTime.Now.ToString("HH:mm:ss")), true);
                if (RenderedCopyViewer != null)
                {
                    RenderedCopyViewer.Document = MarkdownHelper.ToFlowDocument(text);
                }
                NotificationService.ShowSuccess("Script Saved", "Script saved to 03_COPYWRITING/COPY.md on NAS.");
            }
            else
            {
                SetStatusBadge("Save Failed", false);
                MessageBox.Show("Failed to write COPY.md to NAS. Please check directory permissions.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCopyClipboardClicked(object sender, RoutedEventArgs e)
        {
            string text = CopyScriptEditor.Text;
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("No copy text to copy.", "Copywriting Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                ClipboardService.SetText(text);
                SetStatusBadge("Markdown Copied", true);
                NotificationService.ShowInfo("Clipboard", "Full Markdown copy script copied to clipboard!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CopywritingPage] Copy error: " + ex.Message);
            }
        }

        private void OnCopyWhatsAppClicked(object sender, RoutedEventArgs e)
        {
            string content = CopyScriptEditor != null ? CopyScriptEditor.Text : string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show("No copy text to format for WhatsApp.", "Copywriting Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string waFormatted = CopywritingDesktopService.FormatForWhatsApp(content);
                ClipboardService.SetText(waFormatted);
                SetStatusBadge("WhatsApp Copy Copied", true);
                NotificationService.ShowSuccess("WhatsApp Copy Copied", "Copy formatted with *bold*, emojis, and clean line breaks!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CopywritingPage] Copy WhatsApp error: " + ex.Message);
            }
        }

        private void OnCopyMetaAdsClicked(object sender, RoutedEventArgs e)
        {
            string content = CopyScriptEditor != null ? CopyScriptEditor.Text : string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show("No copy text to format for Meta Ads.", "Copywriting Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string metaFormatted = CopywritingDesktopService.FormatForMetaAds(content);
                ClipboardService.SetText(metaFormatted);
                SetStatusBadge("Meta Ads Copy Copied", true);
                NotificationService.ShowSuccess("Meta Ads Copy Copied", "Primary text, Headline, and CTA structured for Ads Manager!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CopywritingPage] Copy Meta Ads error: " + ex.Message);
            }
        }

        private void OnCopyPlainTextClicked(object sender, RoutedEventArgs e)
        {
            string text = CopyScriptEditor.Text;
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("No copy text to copy.", "Copywriting Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string plain = CopywritingDesktopService.StripMarkdownToPlainText(text);
                ClipboardService.SetText(plain);
                SetStatusBadge("Clean Text Copied", true);
                NotificationService.ShowSuccess("Plain Text Copied", "Clean copy without markdown symbols copied for Ads & WhatsApp!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CopywritingPage] Copy plain text error: " + ex.Message);
            }
        }

        // Framework Inserts
        private void OnInsertTikTokClicked(object sender, RoutedEventArgs e)
        {
            InsertPresetFramework("tiktok_3hooks");
        }

        private void OnInsertMetaPasClicked(object sender, RoutedEventArgs e)
        {
            InsertPresetFramework("meta_pas");
        }

        private void OnInsertWhatsAppClicked(object sender, RoutedEventArgs e)
        {
            InsertPresetFramework("whatsapp_broadcast");
        }

        private void OnInsertNeubrutalistClicked(object sender, RoutedEventArgs e)
        {
            InsertPresetFramework("neubrutalist_hook");
        }

        private void OnInsertRetroClicked(object sender, RoutedEventArgs e)
        {
            InsertPresetFramework("retro_story");
        }

        private void OnInsertClaimsClicked(object sender, RoutedEventArgs e)
        {
            InsertPresetFramework("claims");
        }

        // Quick Snippets Inserts
        private void OnSnippetWhatsappLinkClicked(object sender, RoutedEventArgs e)
        {
            InsertSnippetText(Environment.NewLine + "👉 *Klik link untuk WhatsApp Direct:* https://suamisihat.clinic/wsap" + Environment.NewLine);
        }

        private void OnSnippetPromoVoucherClicked(object sender, RoutedEventArgs e)
        {
            InsertSnippetText(Environment.NewLine + "🎟️ *KOD VOUCHER EKSKLUSIF:* `SSPROMO50` (Diskaun RM50 untuk 20 pelanggan terawal)" + Environment.NewLine);
        }

        private void OnSnippetKkmDisclaimerClicked(object sender, RoutedEventArgs e)
        {
            InsertSnippetText(Environment.NewLine + "> ⚠️ *Penafian Kesihatan:* Produk & rawatan ini berdaftar di bawah kawalan pihak berkuasa kesihatan. Kesan mungkin berbeza mengikut individu. Sila rujuk pakar perubatan kami untuk konsultasi penuh." + Environment.NewLine);
        }

        private void OnSnippetHalalGuaranteeClicked(object sender, RoutedEventArgs e)
        {
            InsertSnippetText(Environment.NewLine + "🛡️ *100% DIJAMIN ASLI & HALAL:* Diproses mengikut piawaian GMP & mendapat kelulusan persijilan Halal rasmi." + Environment.NewLine);
        }

        private void OnSnippetUrgencyTimerClicked(object sender, RoutedEventArgs e)
        {
            InsertSnippetText(Environment.NewLine + "⏳ *TAWARAN TERHAD HARI INI SAHAJA!* Slot konsultasi percuma terhad kepada 15 individu terawal." + Environment.NewLine);
        }

        private void InsertPresetFramework(string presetKey)
        {
            string projectTitle = selectedProject != null ? selectedProject.Name : "Project";
            string snippet = CopywritingDesktopService.GetPresetTemplate(presetKey, projectTitle);
            InsertSnippetText(Environment.NewLine + snippet + Environment.NewLine);
        }

        private void InsertSnippetText(string snippet)
        {
            if (currentViewMode == 1)
            {
                ApplyViewMode(0); // Return to split view if currently viewing FlowDocument
            }

            int caret = CopyScriptEditor.CaretIndex;
            string current = CopyScriptEditor.Text ?? string.Empty;

            if (caret >= 0 && caret <= current.Length)
            {
                string updated = current.Insert(caret, snippet);
                CopyScriptEditor.Text = updated;
                CopyScriptEditor.CaretIndex = caret + snippet.Length;
            }
            else
            {
                CopyScriptEditor.Text = current + snippet;
                CopyScriptEditor.CaretIndex = CopyScriptEditor.Text.Length;
            }

            CopyScriptEditor.Focus();
            SetStatusBadge("Unsaved Changes", false);
        }

        private void OnResetTemplateClicked(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to replace current content with the standard brand copywriting template?", "Reset Template", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                string projectTitle = selectedProject != null ? selectedProject.Name : "Project";
                string def = CopywritingDesktopService.GetDefaultTemplate(projectTitle);
                CopyScriptEditor.Text = def;
                if (RenderedCopyViewer != null)
                {
                    RenderedCopyViewer.Document = MarkdownHelper.ToFlowDocument(def);
                }
                UpdateLiveSimulation(def);
                SetStatusBadge("Template Reset (Unsaved)", false);
            }
        }

        private void OnOpenFolderClicked(object sender, RoutedEventArgs e)
        {
            if (selectedProject != null && Directory.Exists(selectedProject.FullPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = selectedProject.FullPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[CopywritingPage] Open folder error: " + ex.Message);
                }
            }
        }

        private async void OnReloadProjectsClicked(object sender, RoutedEventArgs e)
        {
            await LoadProjectsAsync();
        }

        private void OnEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                SaveCurrentScript();
            }
        }

        private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scroller = (sender as ScrollViewer) ?? PageScrollViewer;
            if (scroller != null)
            {
                scroller.ScrollToVerticalOffset(scroller.VerticalOffset - (e.Delta / 2.0));
                e.Handled = true;
            }
        }

        #region Right Property Pane & Sidebar Handlers

        private void UpdateProjectProperties(ProjectItemInfo proj)
        {
            if (TxtPropDesigner == null) return;

            if (proj == null || string.IsNullOrWhiteSpace(proj.FullPath) || !Directory.Exists(proj.FullPath))
            {
                TxtPropDesigner.Text = "—";
                if (TxtPropDesignerInitial != null) TxtPropDesignerInitial.Text = "—";
                TxtPropReviewer.Text = "Hasan · Manager";
                TxtPropBrandPill.Text = "SS";
                TxtPropBrandName.Text = "SuamiSihat";
                TxtPropPriority.Text = "MEDIUM";
                SetPriorityBadge("medium");
                TxtPropDeadline.Text = "—";
                TxtPropDeliverablesCount.Text = "0 files";
                TxtPropApprovalStatus.Text = "No approval records yet.";
                return;
            }

            try
            {
                ProjectStatusItem status = FrontmatterService.ReadStatus(proj.FullPath);

                // 1. Assignee (Designer)
                string designer = !string.IsNullOrWhiteSpace(status.Designer) ? status.Designer : "Harussani";
                TxtPropDesigner.Text = designer;
                if (TxtPropDesignerInitial != null)
                {
                    TxtPropDesignerInitial.Text = designer.Length > 0 ? designer.Substring(0, 1).ToUpperInvariant() : "D";
                }

                // 2. Reviewer
                TxtPropReviewer.Text = "Hasan · Manager";

                // 3. Corporate Brand / Subsidiary
                string brandCode = !string.IsNullOrWhiteSpace(status.Client) ? status.Client : "SS";
                if (brandCode == "SS" && proj.Name != null && proj.Name.Contains("_"))
                {
                    string[] parts = proj.Name.Split('_');
                    if (parts.Length >= 3 && parts[2].StartsWith("SS", StringComparison.OrdinalIgnoreCase))
                    {
                        brandCode = parts[2].ToUpperInvariant();
                    }
                }
                TxtPropBrandPill.Text = brandCode;
                TxtPropBrandName.Text = GetSubsidiaryFullName(brandCode);

                // 4. Priority Level
                string priority = !string.IsNullOrWhiteSpace(status.Priority) ? status.Priority : "medium";
                TxtPropPriority.Text = priority.ToUpperInvariant();
                SetPriorityBadge(priority);

                // 5. Campaign Deadline
                string deadline = !string.IsNullOrWhiteSpace(status.Deadline) ? status.Deadline : "—";
                TxtPropDeadline.Text = deadline;

                // 6. Deliverables Storage
                string delivDir = Path.Combine(proj.FullPath, "05_DELIVERABLES");
                int fileCount = 0;
                if (Directory.Exists(delivDir))
                {
                    fileCount = Directory.GetFiles(delivDir, "*.*", SearchOption.AllDirectories).Length;
                }
                TxtPropDeliverablesCount.Text = string.Format("{0} file{1}", fileCount, fileCount == 1 ? "" : "s");

                // 7. Recent Approvals & Sign-Offs
                if (status.Revision > 0)
                {
                    TxtPropApprovalStatus.Text = string.Format("Revision {0} • In Review", status.Revision);
                }
                else if (string.Equals(status.Status, "approved", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(status.Status, "done", StringComparison.OrdinalIgnoreCase))
                {
                    TxtPropApprovalStatus.Text = "Approved & Signed-Off";
                }
                else
                {
                    TxtPropApprovalStatus.Text = "No approval records yet.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CopywritingPage] UpdateProjectProperties error: " + ex.Message);
            }
        }

        private string GetSubsidiaryFullName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "SuamiSihat Creative Production";
            switch (code.ToUpperInvariant())
            {
                case "SSH": return "SuamiSihat Holding Sdn Bhd";
                case "SSC": return "SuamiSihat Clinic / Healthcare";
                case "SSW": return "SuamiSihat Wellness Sdn Bhd";
                case "SSE": return "SuamiSihat Ecommerce Sdn Bhd";
                case "SST": return "SuamiSihat Technology Sdn Bhd";
                default: return "SuamiSihat Creative Production";
            }
        }

        private void SetPriorityBadge(string priority)
        {
            if (BadgePropPriority == null || TxtPropPriority == null) return;
            switch (priority.ToLowerInvariant())
            {
                case "urgent":
                case "critical":
                    BadgePropPriority.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                    BadgePropPriority.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 165, 165));
                    TxtPropPriority.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    break;
                case "high":
                    BadgePropPriority.Background = new SolidColorBrush(Color.FromRgb(255, 237, 213));
                    BadgePropPriority.BorderBrush = new SolidColorBrush(Color.FromRgb(253, 186, 116));
                    TxtPropPriority.Foreground = new SolidColorBrush(Color.FromRgb(194, 65, 12));
                    break;
                case "low":
                    BadgePropPriority.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    BadgePropPriority.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                    TxtPropPriority.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    break;
                default: // medium
                    BadgePropPriority.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                    BadgePropPriority.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 211, 77));
                    TxtPropPriority.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                    break;
            }
        }

        private void OnRightTabPropertiesClicked(object sender, RoutedEventArgs e)
        {
            if (BtnRightTabProperties != null) BtnRightTabProperties.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            if (BtnRightTabSnippets != null) BtnRightTabSnippets.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            if (PanePropertiesContent != null) PanePropertiesContent.Visibility = Visibility.Visible;
            if (PaneSnippetsContent != null) PaneSnippetsContent.Visibility = Visibility.Collapsed;
        }

        private void OnRightTabSnippetsClicked(object sender, RoutedEventArgs e)
        {
            if (BtnRightTabProperties != null) BtnRightTabProperties.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            if (BtnRightTabSnippets != null) BtnRightTabSnippets.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            if (PanePropertiesContent != null) PanePropertiesContent.Visibility = Visibility.Collapsed;
            if (PaneSnippetsContent != null) PaneSnippetsContent.Visibility = Visibility.Visible;
        }

        private void OnToggleRightPaneClicked(object sender, RoutedEventArgs e)
        {
            if (ColRightPane == null) return;

            if (isRightPaneCollapsed)
            {
                ColRightPane.Width = rightPaneSavedWidth;
                if (ColRightSplitter != null) ColRightSplitter.Width = GridLength.Auto;
                if (RightPropertyPane != null) RightPropertyPane.Visibility = Visibility.Visible;
                if (RightPaneGridSplitter != null) RightPaneGridSplitter.Visibility = Visibility.Visible;
                isRightPaneCollapsed = false;
                if (BtnToggleRightPane != null) BtnToggleRightPane.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            }
            else
            {
                if (ColRightPane.ActualWidth > 100)
                {
                    rightPaneSavedWidth = ColRightPane.Width;
                }
                ColRightPane.Width = new GridLength(0);
                if (ColRightSplitter != null) ColRightSplitter.Width = new GridLength(0);
                if (RightPropertyPane != null) RightPropertyPane.Visibility = Visibility.Collapsed;
                if (RightPaneGridSplitter != null) RightPaneGridSplitter.Visibility = Visibility.Collapsed;
                isRightPaneCollapsed = true;
                if (BtnToggleRightPane != null) BtnToggleRightPane.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            }
        }

        private void OnOpenDeliverablesFolderClicked(object sender, MouseButtonEventArgs e)
        {
            if (selectedProject == null || string.IsNullOrWhiteSpace(selectedProject.FullPath)) return;
            try
            {
                string delivDir = Path.Combine(selectedProject.FullPath, "05_DELIVERABLES");
                if (!Directory.Exists(delivDir))
                {
                    Directory.CreateDirectory(delivDir);
                }
                Process.Start(new ProcessStartInfo("explorer.exe", delivDir) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CopywritingPage] Open deliverables folder error: " + ex.Message);
            }
        }

        #endregion
    }
}

