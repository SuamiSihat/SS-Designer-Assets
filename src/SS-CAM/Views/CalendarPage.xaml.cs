using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SS_CAM.Models;
using SS_CAM.Services;
using SS_CAM.Utilities;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SS_CAM.Views
{
    public class CalendarDayEventItem
    {
        public string Project { get; set; }
        public string FullPath { get; set; }
        public string Priority { get; set; }
        public string PriorityColor { get; set; }
        public string EventType { get; set; }  // "Deadline" or "Started"
        public string Status { get; set; }
        public string DesignerDisplay { get; set; }

        public string PriorityBadgeText
        {
            get
            {
                string p = (Priority ?? "").ToLowerInvariant().Trim();
                if (p == "urgent") return "P3";
                if (p == "high") return "P2";
                if (p == "medium" || p == "standard") return "P1";
                return "";
            }
        }

        public System.Windows.Visibility PriorityBadgeVisibility
        {
            get
            {
                return string.IsNullOrEmpty(PriorityBadgeText) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
        }
    }

    public partial class CalendarPage : Page
    {
        private string _workspaceRoot = "";
        private int _currentYear = DateTime.Today.Year;
        private int _currentMonth = DateTime.Today.Month;
        private List<ProjectStatusItem> _allProjects = new List<ProjectStatusItem>();
        private bool _isPopulatingFilter = false;
        private bool _isGanttView = false;
        private ProjectStatusItem _drawerEditingProject = null;
        private string _drawerEditingSubtaskId = null;
        private bool _isUpdatingDrawerDates = false;

        private bool _isDraggingGanttEdge = false;
        private bool _dragIsLeftEdge = false;
        private ProjectStatusItem _dragGanttProject = null;
        private Border _dragGanttBar = null;
        private Grid _dragGanttRowGrid = null;
        private int _dragDaysInMonth = 30;
        private DateTime _dragActiveMonth = DateTime.Today;
        private int _dragOriginalStartDay = 1;
        private int _dragOriginalEndDay = 1;
        private int _dragCurrentStartDay = 1;
        private int _dragCurrentEndDay = 1;
        private FrameworkElement _dragCapturedHandle = null;

        public CalendarPage()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var profile = UserProfileService.LoadProfile();
                _workspaceRoot = profile != null ? profile.WorkspaceRoot : "";
                PopulateDesignerFilter();
                LoadProjects();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] OnPageLoaded error: " + ex.Message);
            }
        }

        private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scroller = (sender as ScrollViewer) ?? PageScrollViewer;
            if (scroller != null)
            {
                scroller.ScrollToVerticalOffset(scroller.VerticalOffset - (e.Delta / 2.0));
                e.Handled = true;
            }
        }

        private void PopulateDesignerFilter()
        {
            try
            {
                _isPopulatingFilter = true;
                string currentSelection = DesignerFilter.SelectedItem != null ? DesignerFilter.SelectedItem.ToString() : "All Designers";

                HashSet<string> designerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                List<StaffDirectoryItem> staffList = null;
                try { staffList = UserProfileService.GetStaffDirectory(_workspaceRoot); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[CalendarPage] GetStaffDirectory: " + ex.Message); }

                if (!string.IsNullOrWhiteSpace(_workspaceRoot))
                {
                    List<DesignerFolderChoice> designers = WorkspaceScanner.GetDesignerFolders(_workspaceRoot);
                    if (designers != null)
                    {
                        foreach (DesignerFolderChoice d in designers)
                        {
                            if (d != null && !string.IsNullOrWhiteSpace(d.Name))
                                designerSet.Add(d.Name);
                        }
                    }
                }

                foreach (ProjectStatusItem p in _allProjects)
                {
                    if (p != null && !string.IsNullOrWhiteSpace(p.Designer))
                    {
                        if (!Regex.IsMatch(p.Designer, @"^\d{4}$") && !Regex.IsMatch(p.Designer, @"^\d{6}") &&
                            !p.Designer.StartsWith("#") && !p.Designer.StartsWith("_"))
                        {
                            if (staffList != null)
                            {
                                var matched = staffList.Find(s => string.Equals(s.Name, p.Designer, StringComparison.OrdinalIgnoreCase) ||
                                                                  string.Equals(s.StaffId, p.Designer, StringComparison.OrdinalIgnoreCase));
                                if (matched != null && !WorkloadSlaService.IsDesignerOrAdminRole(matched.Role, matched.Department))
                                {
                                    continue; // Exclude manager role
                                }
                            }
                            designerSet.Add(p.Designer);
                        }
                    }
                }

                DesignerFilter.Items.Clear();
                DesignerFilter.Items.Add("All Designers");
                foreach (string d in designerSet)
                {
                    DesignerFilter.Items.Add(d);
                }

                int selectedIdx = 0;
                for (int i = 0; i < DesignerFilter.Items.Count; i++)
                {
                    if (string.Equals(DesignerFilter.Items[i].ToString(), currentSelection, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIdx = i;
                        break;
                    }
                }
                DesignerFilter.SelectedIndex = selectedIdx;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] PopulateFilter error: " + ex.Message);
            }
            finally
            {
                _isPopulatingFilter = false;
            }
        }

        private async System.Threading.Tasks.Task LoadProjectsAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
                {
                    _allProjects.Clear();
                    PopulateDesignerFilter();
                    RenderCalendarGrid();
                    return;
                }

                string root = _workspaceRoot;
                List<ProjectStatusItem> items = await System.Threading.Tasks.Task.Run(() =>
                {
                    List<ProjectStatusItem> list = new List<ProjectStatusItem>();
                    try
                    {
                        List<DesignerFolderItem> folders = WorkspaceScanner.ListDesignerFolders(root, "", "", 500);
                        if (folders != null)
                        {
                            foreach (DesignerFolderItem folder in folders)
                            {
                                if (folder != null && !string.IsNullOrEmpty(folder.FullPath))
                                {
                                    ProjectStatusItem item = FrontmatterService.ReadStatus(folder.FullPath);
                                    if (item != null)
                                    {
                                        if (string.IsNullOrWhiteSpace(item.Designer) && !string.IsNullOrWhiteSpace(folder.Designer))
                                        {
                                            item.Designer = folder.Designer;
                                        }
                                        list.Add(item);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[CalendarPage] Background LoadProjects error: " + ex.Message);
                    }
                    return list;
                });

                _allProjects.Clear();
                _allProjects.AddRange(items);

                PopulateDesignerFilter();
                UpdateMetrics();
                RenderCalendarGrid();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] LoadProjects error: " + ex.Message);
                RenderCalendarGrid();
            }
        }

        private async void LoadProjects()
        {
            await LoadProjectsAsync();
        }

        private List<ProjectStatusItem> GetFilteredProjects()
        {
            List<ProjectStatusItem> filtered = new List<ProjectStatusItem>();
            string designerFilter = DesignerFilter.SelectedItem != null ? DesignerFilter.SelectedItem.ToString() : "All Designers";
            string statusFilter = "";
            if (StatusFilter != null && StatusFilter.SelectedItem is ComboBoxItem)
            {
                string sel = ((ComboBoxItem)StatusFilter.SelectedItem).Content.ToString();
                if (sel != "All Statuses") statusFilter = sel;
            }
            string searchQuery = TxtSearchQuery != null ? TxtSearchQuery.Text.Trim().ToLowerInvariant() : "";

            foreach (ProjectStatusItem p in _allProjects)
            {
                if (p == null) continue;
                if (designerFilter != "All Designers" && !string.IsNullOrWhiteSpace(designerFilter))
                {
                    bool match = false;
                    if (!string.IsNullOrWhiteSpace(p.Designer))
                    {
                        if (string.Equals(p.Designer, designerFilter, StringComparison.OrdinalIgnoreCase) ||
                            p.Designer.IndexOf(designerFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            designerFilter.IndexOf(p.Designer, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            match = true;
                        }
                    }
                    if (!match && !string.IsNullOrWhiteSpace(p.FullPath))
                    {
                        if (p.FullPath.IndexOf(designerFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            match = true;
                        }
                    }
                    if (!match) continue;
                }
                if (!string.IsNullOrEmpty(statusFilter) &&
                    !string.Equals(p.Status, statusFilter, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    string projName = (p.Project ?? "").ToLowerInvariant();
                    string clientName = (p.Client ?? "").ToLowerInvariant();
                    string designerName = (p.Designer ?? "").ToLowerInvariant();
                    if (!projName.Contains(searchQuery) && !clientName.Contains(searchQuery) && !designerName.Contains(searchQuery)) continue;
                }
                filtered.Add(p);
            }
            return filtered;
        }

        private void UpdateMetrics()
        {
            try
            {
                List<ProjectStatusItem> filtered = GetFilteredProjects();
                int total = filtered.Count;
                int deadlinesThisMonth = 0;
                int startedThisMonth = 0;
                int overdueCount = 0;

                DateTime firstDayOfMonth = new DateTime(_currentYear, _currentMonth, 1);
                DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

                foreach (ProjectStatusItem p in filtered)
                {
                    if (p == null) continue;

                    // Deadlines this month
                    DateTime dtDeadline;
                    if (!string.IsNullOrWhiteSpace(p.Deadline) && DateTime.TryParse(p.Deadline, out dtDeadline))
                    {
                        if (dtDeadline >= firstDayOfMonth && dtDeadline <= lastDayOfMonth)
                        {
                            deadlinesThisMonth++;
                        }
                        string st = (p.Status ?? "").ToLowerInvariant();
                        if (dtDeadline < DateTime.Today && st != "done" && st != "approved" && st != "completed")
                        {
                            overdueCount++;
                        }
                    }

                    // Started this month
                    DateTime dtCreated = p.ParsedCreatedDate;
                    if (dtCreated >= firstDayOfMonth && dtCreated <= lastDayOfMonth)
                    {
                        startedThisMonth++;
                    }
                }

                if (MetricDeadlinesMonth != null) MetricDeadlinesMonth.Text = deadlinesThisMonth.ToString();
                if (MetricStartedMonth != null) MetricStartedMonth.Text = startedThisMonth.ToString();
                if (MetricOverdue != null) MetricOverdue.Text = overdueCount.ToString();
                if (MetricTotalProjects != null) MetricTotalProjects.Text = total.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] UpdateMetrics error: " + ex.Message);
            }
        }

        private void RenderCalendarGrid()
        {
            try
            {
                if (TxtCurrentMonthTitle != null)
                {
                    DateTime activeMonthDate = new DateTime(_currentYear, _currentMonth, 1);
                    List<MalaysiaHolidayItem> monthHolidays = MalaysiaHolidayService.GetHolidaysForMonth(_currentYear, _currentMonth);
                    if (monthHolidays != null && monthHolidays.Count > 0)
                    {
                        TxtCurrentMonthTitle.Text = string.Format("{0}  (🇲🇾 {1} Public Holiday{2})",
                            activeMonthDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                            monthHolidays.Count,
                            monthHolidays.Count > 1 ? "s" : "");
                    }
                    else
                    {
                        TxtCurrentMonthTitle.Text = activeMonthDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
                    }
                }

                if (CalendarGrid == null) return;
                CalendarGrid.Children.Clear();

                DateTime firstOfMonth = new DateTime(_currentYear, _currentMonth, 1);
                int dayOfWeek = (int)firstOfMonth.DayOfWeek; // 0 = Sunday
                int daysInMonth = DateTime.DaysInMonth(_currentYear, _currentMonth);

                List<ProjectStatusItem> filtered = GetFilteredProjects();

                // Map events by date (key: YYYY-MM-DD)
                Dictionary<string, List<CalendarDayEventItem>> eventMap = new Dictionary<string, List<CalendarDayEventItem>>(StringComparer.OrdinalIgnoreCase);

                foreach (ProjectStatusItem p in filtered)
                {
                    if (p == null) continue;

                    // 1. Deadline event
                    DateTime dtDeadline;
                    if (!string.IsNullOrWhiteSpace(p.Deadline) && DateTime.TryParse(p.Deadline, out dtDeadline))
                    {
                        string key = dtDeadline.ToString("yyyy-MM-dd");
                        if (!eventMap.ContainsKey(key)) eventMap[key] = new List<CalendarDayEventItem>();
                        eventMap[key].Add(new CalendarDayEventItem
                        {
                            Project = p.Project,
                            FullPath = p.FullPath,
                            Priority = p.Priority ?? "medium",
                            PriorityColor = p.PriorityColor,
                            EventType = "Deadline",
                            Status = p.Status ?? "in-progress",
                            DesignerDisplay = string.Format("Designer: {0} | Client: {1}", p.Designer ?? "N/A", p.Client ?? "SS")
                        });
                    }

                    // 2. Creation date event
                    string createdKey = p.CreatedDateDisplay;
                    if (!string.IsNullOrWhiteSpace(createdKey) && createdKey != "N/A")
                    {
                        if (!eventMap.ContainsKey(createdKey)) eventMap[createdKey] = new List<CalendarDayEventItem>();
                        eventMap[createdKey].Add(new CalendarDayEventItem
                        {
                            Project = p.Project,
                            FullPath = p.FullPath,
                            Priority = p.Priority ?? "medium",
                            PriorityColor = p.PriorityColor,
                            EventType = "Started",
                            Status = p.Status ?? "in-progress",
                            DesignerDisplay = string.Format("Designer: {0} | Client: {1}", p.Designer ?? "N/A", p.Client ?? "SS")
                        });
                    }
                }

                // Render 35 cells (5 weeks) or 42 cells (6 weeks)
                int totalCells = (dayOfWeek + daysInMonth > 35) ? 42 : 35;
                DateTime startDate = firstOfMonth.AddDays(-dayOfWeek);

                for (int i = 0; i < totalCells; i++)
                {
                    DateTime cellDate = startDate.AddDays(i);
                    bool isCurrentMonth = cellDate.Month == _currentMonth;
                    bool isToday = cellDate.Date == DateTime.Today;
                    string dateKey = cellDate.ToString("yyyy-MM-dd");

                    List<CalendarDayEventItem> dayEvents = eventMap.ContainsKey(dateKey) ? eventMap[dateKey] : new List<CalendarDayEventItem>();

                    UIElement cellUI = CreateDayCellUI(cellDate, isCurrentMonth, isToday, dayEvents);
                    CalendarGrid.Children.Add(cellUI);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] RenderCalendarGrid error: " + ex.Message);
            }
        }

        private UIElement CreateDayCellUI(DateTime date, bool isCurrentMonth, bool isToday, List<CalendarDayEventItem> events)
        {
            CardAction card = new CardAction
            {
                MinHeight = 135,
                Margin = new Thickness(0, 0, 4, 4),
                Padding = new Thickness(6),
                Cursor = Cursors.Hand,
                Tag = date,
                Opacity = isCurrentMonth ? 1.0 : 0.4
            };

            card.Click += OnDayCardClicked;
            card.PreviewMouseWheel += OnScrollViewerPreviewMouseWheel;

            Grid cellGrid = new Grid();
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top Bar: Day Number + Today Badge
            StackPanel topPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            
            Border numBadge = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 2, 6, 2),
                Background = isToday ? (Brush)Application.Current.Resources["FluentBrand80"] : Brushes.Transparent
            };

            TextBlock txtNum = new TextBlock
            {
                Text = date.Day.ToString(),
                FontSize = 11,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = isToday ? Brushes.White : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            };
            numBadge.Child = txtNum;
            topPanel.Children.Add(numBadge);

            if (date.DayOfWeek == DayOfWeek.Friday)
            {
                TextBlock friBadge = new TextBlock
                {
                    Text = " Solat",
                    FontSize = 9,
                    Foreground = (Brush)Application.Current.Resources["FluentBrand80"],
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                };
                topPanel.Children.Add(friBadge);
            }

            // Malaysia Public Holiday Tag
            MalaysiaHolidayItem holiday = MalaysiaHolidayService.GetHoliday(date);
            if (holiday != null)
            {
                Border holBadge = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(4, 0, 0, 0),
                    Background = new SolidColorBrush(Color.FromArgb(40, 239, 68, 68)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(120, 239, 68, 68)),
                    BorderThickness = new Thickness(1),
                    ToolTip = "Malaysia Public Holiday: " + holiday.Name
                };
                TextBlock holTxt = new TextBlock
                {
                    Text = "🇲🇾 " + holiday.ShortName,
                    FontSize = 8.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                holBadge.Child = holTxt;
                topPanel.Children.Add(holBadge);
            }

            Grid.SetRow(topPanel, 0);
            cellGrid.Children.Add(topPanel);

            // Stack of Event Chips
            StackPanel eventStack = new StackPanel();
            Grid.SetRow(eventStack, 1);

            // Prepend Holiday Chip if applicable
            if (holiday != null)
            {
                Border holChip = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(0, 0, 0, 3),
                    Background = new SolidColorBrush(Color.FromArgb(30, 239, 68, 68)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(90, 239, 68, 68)),
                    BorderThickness = new Thickness(1),
                    ToolTip = "Malaysia Public Holiday: " + holiday.Name
                };
                StackPanel holPanel = new StackPanel { Orientation = Orientation.Horizontal };
                TextBlock holIcon = new TextBlock
                {
                    Text = "🇲🇾",
                    FontSize = 9.5,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                TextBlock holTitle = new TextBlock
                {
                    Text = holiday.Name,
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                holPanel.Children.Add(holIcon);
                holPanel.Children.Add(holTitle);
                holChip.Child = holPanel;
                eventStack.Children.Add(holChip);
            }

            int displayCount = Math.Min(holiday != null ? 2 : 3, events.Count);
            for (int e = 0; e < displayCount; e++)
            {
                CalendarDayEventItem item = events[e];
                Border chip = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(0, 0, 0, 3),
                    Background = (item.EventType == "Deadline" && date.Date < DateTime.Today && (item.Status ?? "").ToLower() != "done")
                        ? (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
                        : (item.EventType == "Deadline" && date.Date == DateTime.Today)
                            ? (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
                            : (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                    ContextMenu = CreateQuickStatusContextMenu(item.FullPath, item.Status)
                };

                StackPanel chipPanel = new StackPanel { Orientation = Orientation.Horizontal };

                Border dot = new Border
                {
                    Width = 6,
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = GetPriorityBrush(item.PriorityColor)
                };
                chipPanel.Children.Add(dot);

                TextBlock txtEvent = new TextBlock
                {
                    Text = string.Format("{0}: {1}", item.EventType == "Deadline" ? "Due" : "Start", item.Project),
                    FontSize = 9.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = (item.EventType == "Deadline" && date.Date < DateTime.Today && (item.Status ?? "").ToLower() != "done")
                        ? Brushes.White
                        : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                    VerticalAlignment = VerticalAlignment.Center
                };
                chipPanel.Children.Add(txtEvent);

                chip.Child = chipPanel;
                eventStack.Children.Add(chip);
            }

            int maxDisplay = holiday != null ? 2 : 3;
            if (events.Count > maxDisplay)
            {
                TextBlock txtMore = new TextBlock
                {
                    Text = string.Format("+{0} more", events.Count - maxDisplay),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    Margin = new Thickness(2, 2, 0, 0)
                };
                eventStack.Children.Add(txtMore);
            }

            cellGrid.Children.Add(eventStack);
            card.Content = cellGrid;
            return card;
        }

        private static SolidColorBrush GetPriorityBrush(string hex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] GetPriorityBrush error: " + ex.Message);
            }
            return new SolidColorBrush(Color.FromRgb(33, 161, 247));
        }

        private void OnDayCardClicked(object sender, RoutedEventArgs e)
        {
            CardAction card = sender as CardAction;
            if (card == null || card.Tag == null) return;

            DateTime cellDate = (DateTime)card.Tag;
            string dateKey = cellDate.ToString("yyyy-MM-dd");

            List<ProjectStatusItem> filtered = GetFilteredProjects();
            List<CalendarDayEventItem> dayEvents = new List<CalendarDayEventItem>();

            // Prepend Malaysia Public Holiday if applicable
            MalaysiaHolidayItem holiday = MalaysiaHolidayService.GetHoliday(cellDate);
            if (holiday != null)
            {
                dayEvents.Add(new CalendarDayEventItem
                {
                    Project = "🇲🇾 " + holiday.Name,
                    FullPath = "",
                    Priority = "Holiday",
                    PriorityColor = "#EF4444",
                    EventType = "Public Holiday",
                    DesignerDisplay = holiday.Description
                });
            }

            foreach (ProjectStatusItem p in filtered)
            {
                if (p == null) continue;

                DateTime dtDead;
                if (!string.IsNullOrWhiteSpace(p.Deadline) &&
                    DateTime.TryParse(p.Deadline, out dtDead) &&
                    dtDead.Date == cellDate.Date)
                {
                    dayEvents.Add(new CalendarDayEventItem
                    {
                        Project = p.Project,
                        FullPath = p.FullPath,
                        Priority = p.Priority ?? "medium",
                        PriorityColor = p.PriorityColor,
                        EventType = "Deadline",
                        DesignerDisplay = string.Format("Designer: {0} | Client: {1}", p.Designer ?? "N/A", p.Client ?? "SS")
                    });
                }

                if (p.ParsedCreatedDate.Date == cellDate.Date)
                {
                    dayEvents.Add(new CalendarDayEventItem
                    {
                        Project = p.Project,
                        FullPath = p.FullPath,
                        Priority = p.Priority ?? "medium",
                        PriorityColor = p.PriorityColor,
                        EventType = "Started",
                        DesignerDisplay = string.Format("Designer: {0} | Client: {1}", p.Designer ?? "N/A", p.Client ?? "SS")
                    });
                }
            }

            DayDetailTitle.Text = cellDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
            DayDetailSubtitle.Text = string.Format("{0} scheduled item(s)", dayEvents.Count);
            DayDetailItemsList.ItemsSource = dayEvents;
            DayDetailPanel.Visibility = Visibility.Visible;
        }

        private void OnCloseDayDetail(object sender, RoutedEventArgs e)
        {
            DayDetailPanel.Visibility = Visibility.Collapsed;
        }

        private void OnOpenProjectFolderClicked(object sender, RoutedEventArgs e)
        {
            FrameworkElement el = sender as FrameworkElement;
            if (el == null || el.Tag == null) return;
            string path = el.Tag.ToString();

            try
            {
                if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] OpenFolder error: " + ex.Message);
            }
        }

        private void OnPrevMonthClicked(object sender, RoutedEventArgs e)
        {
            _currentMonth--;
            if (_currentMonth < 1)
            {
                _currentMonth = 12;
                _currentYear--;
            }
            UpdateMetrics();
            if (_isGanttView) RenderGanttTimeline(); else RenderCalendarGrid();
        }

        private void OnNextMonthClicked(object sender, RoutedEventArgs e)
        {
            _currentMonth++;
            if (_currentMonth > 12)
            {
                _currentMonth = 1;
                _currentYear++;
            }
            UpdateMetrics();
            if (_isGanttView) RenderGanttTimeline(); else RenderCalendarGrid();
        }

        private void OnTodayClicked(object sender, RoutedEventArgs e)
        {
            _currentYear = DateTime.Today.Year;
            _currentMonth = DateTime.Today.Month;
            UpdateMetrics();
            if (_isGanttView) RenderGanttTimeline(); else RenderCalendarGrid();
        }

        private void OnSearchQueryChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded || _isPopulatingFilter) return;
            UpdateMetrics();
            if (_isGanttView) RenderGanttTimeline(); else RenderCalendarGrid();
        }

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _isPopulatingFilter) return;
            UpdateMetrics();
            if (_isGanttView) RenderGanttTimeline(); else RenderCalendarGrid();
        }

        private void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            LoadProjects();
        }

        private System.Windows.Controls.ContextMenu CreateQuickStatusContextMenu(string projectPath, string currentStatus)
        {
            System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu();
            string[] statuses = new[] { "backlog", "in-progress", "review", "done", "on-hold" };

            System.Windows.Controls.MenuItem header = new System.Windows.Controls.MenuItem
            {
                Header = "Change Status to:",
                IsEnabled = false,
                FontWeight = FontWeights.Bold
            };
            menu.Items.Add(header);
            menu.Items.Add(new System.Windows.Controls.Separator());

            foreach (string st in statuses)
            {
                System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem
                {
                    Header = st,
                    IsChecked = string.Equals(st, currentStatus, StringComparison.OrdinalIgnoreCase),
                    Tag = new Tuple<string, string>(projectPath, st)
                };
                item.Click += OnQuickStatusMenuClicked;
                menu.Items.Add(item);
            }

            return menu;
        }

        private void OnQuickStatusMenuClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Controls.MenuItem item = sender as System.Windows.Controls.MenuItem;
                if (item == null || item.Tag == null) return;

                Tuple<string, string> data = item.Tag as Tuple<string, string>;
                if (data == null) return;

                string projectPath = data.Item1;
                string newStatus = data.Item2;

                if (!string.IsNullOrWhiteSpace(projectPath) && Directory.Exists(projectPath))
                {
                    ProjectStatusItem statusItem = FrontmatterService.ReadStatus(projectPath);
                    if (statusItem != null)
                    {
                        statusItem.Status = newStatus;
                        FrontmatterService.WriteStatus(statusItem);
                        LoadProjects();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] Quick status menu error: " + ex.Message);
            }
        }

        private void OnViewGridClicked(object sender, RoutedEventArgs e)
        {
            _isGanttView = false;
            if (BtnViewGrid != null) BtnViewGrid.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            if (BtnViewGantt != null) BtnViewGantt.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            if (CalendarGridContainer != null) CalendarGridContainer.Visibility = Visibility.Visible;
            if (GanttContainer != null) GanttContainer.Visibility = Visibility.Collapsed;
            RenderCalendarGrid();
        }

        private void OnViewGanttClicked(object sender, RoutedEventArgs e)
        {
            _isGanttView = true;
            if (BtnViewGrid != null) BtnViewGrid.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            if (BtnViewGantt != null) BtnViewGantt.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            if (CalendarGridContainer != null) CalendarGridContainer.Visibility = Visibility.Collapsed;
            if (GanttContainer != null) GanttContainer.Visibility = Visibility.Visible;
            RenderGanttTimeline();
        }

        private static bool TryParseProjectDates(ProjectStatusItem p, out DateTime startDt, out DateTime endDt)
        {
            startDt = DateTime.MinValue;
            endDt = DateTime.MinValue;
            if (p == null) return false;

            // 1. Parse Start Date
            if (!string.IsNullOrWhiteSpace(p.CreatedDate))
            {
                string clean = p.CreatedDate.Trim().Trim('"', '\'');
                if (clean.Contains("T")) clean = clean.Substring(0, clean.IndexOf("T"));
                DateTime dt;
                if (DateTime.TryParse(clean, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) ||
                    DateTime.TryParse(clean, out dt))
                {
                    startDt = dt.Date;
                }
            }
            if (startDt == DateTime.MinValue)
            {
                startDt = p.ParsedCreatedDate.Date;
            }

            // 2. Parse Deadline Date
            if (!string.IsNullOrWhiteSpace(p.Deadline))
            {
                string clean = p.Deadline.Trim().Trim('"', '\'');
                if (clean.Contains("T")) clean = clean.Substring(0, clean.IndexOf("T"));
                DateTime dt;
                if (DateTime.TryParse(clean, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) ||
                    DateTime.TryParse(clean, out dt))
                {
                    endDt = dt.Date;
                }
            }

            // If deadline is missing, default to startDt
            if (endDt == DateTime.MinValue)
            {
                endDt = startDt;
            }

            if (endDt < startDt)
            {
                endDt = startDt;
            }

            return true;
        }

        private void RenderGanttTimeline()
        {
            try
            {
                if (GanttHeaderGrid == null || GanttRowsStack == null) return;

                DateTime activeMonthDate = new DateTime(_currentYear, _currentMonth, 1);

                if (TxtCurrentMonthTitle != null)
                {
                    List<MalaysiaHolidayItem> monthHolidays = MalaysiaHolidayService.GetHolidaysForMonth(_currentYear, _currentMonth);
                    if (monthHolidays != null && monthHolidays.Count > 0)
                    {
                        TxtCurrentMonthTitle.Text = string.Format("{0}  (🇲🇾 {1} Public Holiday{2})",
                            activeMonthDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                            monthHolidays.Count,
                            monthHolidays.Count > 1 ? "s" : "");
                    }
                    else
                    {
                        TxtCurrentMonthTitle.Text = activeMonthDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
                    }
                }

                GanttHeaderGrid.Children.Clear();
                GanttHeaderGrid.ColumnDefinitions.Clear();
                GanttRowsStack.Children.Clear();

                int daysInMonth = DateTime.DaysInMonth(_currentYear, _currentMonth);

                // ─── 1. Setup Background Grid Columns ───
                if (GanttBackgroundGrid != null)
                {
                    GanttBackgroundGrid.Children.Clear();
                    GanttBackgroundGrid.ColumnDefinitions.Clear();
                    GanttBackgroundGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        GanttBackgroundGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    }
                }

                // ─── 2. Setup Today Overlay Grid Columns ───
                if (GanttTodayRowsGrid != null)
                {
                    GanttTodayRowsGrid.Children.Clear();
                    GanttTodayRowsGrid.ColumnDefinitions.Clear();
                    GanttTodayRowsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        GanttTodayRowsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    }
                }

                // ─── 3. Setup Header Column 0 (Project Name) ───
                GanttHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                TextBlock titleHeader = new TextBlock
                {
                    Text = "PROJECT NAME",
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                Grid.SetColumn(titleHeader, 0);
                GanttHeaderGrid.Children.Add(titleHeader);

                // ─── 4. Build Days Header & Background Fills (Columns 1 to daysInMonth) ───
                for (int day = 1; day <= daysInMonth; day++)
                {
                    GanttHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    DateTime dt = new DateTime(_currentYear, _currentMonth, day);
                    bool isToday = (dt.Date == DateTime.Today);
                    MalaysiaHolidayItem holiday = MalaysiaHolidayService.GetHoliday(dt);
                    bool isWeekend = (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday);
                    bool isSunday = (dt.DayOfWeek == DayOfWeek.Sunday);
                    bool isSaturday = (dt.DayOfWeek == DayOfWeek.Saturday);
                    string dayLetter = MalaysiaHolidayService.GetDayLetter(dt);
                    string dayNumberStr = day.ToString();

                    // Background Grid Fills & Column Lines
                    if (GanttBackgroundGrid != null)
                    {
                        Grid colGrid = new Grid
                        {
                            VerticalAlignment = VerticalAlignment.Stretch,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };

                        // Vertical subtle separator border line for each day
                        Border rightLine = new Border
                        {
                            BorderBrush = (Brush)Application.Current.FindResource("CardStrokeColorDefaultBrush"),
                            BorderThickness = new Thickness(0, 0, 1, 0),
                            Opacity = 0.35,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch
                        };
                        colGrid.Children.Add(rightLine);

                        // Saturday & Sunday Weekend Shading Fill
                        if (isWeekend)
                        {
                            Border weekendFill = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(20, 100, 116, 139)),
                                BorderBrush = new SolidColorBrush(Color.FromArgb(35, 100, 116, 139)),
                                BorderThickness = new Thickness(1, 0, 1, 0),
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                VerticalAlignment = VerticalAlignment.Stretch
                            };
                            colGrid.Children.Add(weekendFill);
                        }

                        // Public Holiday Shading Fill (e.g. Malaysia Day)
                        if (holiday != null)
                        {
                            Border holidayFill = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(32, 239, 68, 68)),
                                BorderBrush = new SolidColorBrush(Color.FromArgb(75, 239, 68, 68)),
                                BorderThickness = new Thickness(1, 0, 1, 0),
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                VerticalAlignment = VerticalAlignment.Stretch
                            };
                            colGrid.Children.Add(holidayFill);
                        }

                        Grid.SetColumn(colGrid, day);
                        GanttBackgroundGrid.Children.Add(colGrid);
                    }

                    // Header Cell UI with Day Letter & Number
                    string tooltipDateText = string.Format("{0:dddd, d MMMM yyyy}", dt);
                    if (holiday != null)
                    {
                        tooltipDateText += "\n🇲🇾 " + holiday.Name + " (Public Holiday - Creative Off-Day)";
                    }
                    else if (isSunday)
                    {
                        tooltipDateText += "\nSunday (Weekend Off-Day - Non-Working Day)";
                    }
                    else if (isSaturday)
                    {
                        tooltipDateText += "\nSaturday (Weekend Off-Day - Non-Working Day)";
                    }
                    else
                    {
                        tooltipDateText += "\nWorking Day";
                    }

                    if (isToday)
                    {
                        Border todayBadge = new Border
                        {
                            Background = (Brush)Application.Current.FindResource("FluentBrand80"),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(2, 2, 2, 2),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(1, 0, 1, 0),
                            ToolTip = tooltipDateText + " [TODAY]"
                        };
                        StackPanel todayStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
                        TextBlock todayDayName = new TextBlock
                        {
                            Text = dayLetter,
                            FontSize = 8,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                        TextBlock todayNum = new TextBlock
                        {
                            Text = dayNumberStr,
                            FontSize = 10,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                        todayStack.Children.Add(todayDayName);
                        todayStack.Children.Add(todayNum);
                        todayBadge.Child = todayStack;
                        Grid.SetColumn(todayBadge, day);
                        GanttHeaderGrid.Children.Add(todayBadge);
                    }
                    else
                    {
                        Border cellBorder = new Border
                        {
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(1, 2, 1, 2),
                            Margin = new Thickness(1, 0, 1, 0),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Center,
                            ToolTip = tooltipDateText
                        };

                        if (holiday != null)
                        {
                            cellBorder.Background = new SolidColorBrush(Color.FromArgb(35, 239, 68, 68));
                            cellBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 239, 68, 68));
                            cellBorder.BorderThickness = new Thickness(1);
                        }
                        else if (isWeekend)
                        {
                            cellBorder.Background = new SolidColorBrush(Color.FromArgb(20, 100, 116, 139));
                            cellBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 100, 116, 139));
                            cellBorder.BorderThickness = new Thickness(1);
                        }

                        StackPanel cellStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };

                        // Day Name (Mon, Tue, Wed, Thu, Fri, Sat, Sun)
                        TextBlock txtDayLetter = new TextBlock
                        {
                            Text = dayLetter,
                            FontSize = 8,
                            FontWeight = (holiday != null) ? FontWeights.Bold : (isWeekend ? FontWeights.SemiBold : FontWeights.Normal),
                            HorizontalAlignment = HorizontalAlignment.Center
                        };

                        if (holiday != null)
                        {
                            txtDayLetter.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red only on public holidays
                        }
                        else if (isWeekend)
                        {
                            txtDayLetter.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // Slate for Saturday & Sunday weekend
                        }
                        else if (dt.DayOfWeek == DayOfWeek.Friday)
                        {
                            txtDayLetter.Foreground = (Brush)Application.Current.FindResource("FluentBrand80"); // Friday accent
                        }
                        else
                        {
                            txtDayLetter.Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush");
                        }

                        // Day Number (1..31)
                        TextBlock txtDayNum = new TextBlock
                        {
                            Text = dayNumberStr,
                            FontSize = 10,
                            FontWeight = (holiday != null) ? FontWeights.Bold : FontWeights.Normal,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };

                        if (holiday != null)
                        {
                            txtDayNum.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red only on public holidays
                        }
                        else
                        {
                            txtDayNum.Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush");
                        }

                        cellStack.Children.Add(txtDayLetter);
                        cellStack.Children.Add(txtDayNum);
                        cellBorder.Child = cellStack;

                        Grid.SetColumn(cellBorder, day);
                        GanttHeaderGrid.Children.Add(cellBorder);
                    }
                }

                // ─── 5. Add Today Vertical Indicator Line across Gantt rows ───
                if (_currentYear == DateTime.Today.Year && _currentMonth == DateTime.Today.Month)
                {
                    int todayDay = DateTime.Today.Day;
                    if (GanttTodayRowsGrid != null && todayDay >= 1 && todayDay <= daysInMonth)
                    {
                        Border todayLine = new Border
                        {
                            Width = 2,
                            Background = (Brush)Application.Current.FindResource("FluentBrand80"),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            Opacity = 0.9,
                            IsHitTestVisible = false
                        };
                        Grid.SetColumn(todayLine, todayDay);
                        GanttTodayRowsGrid.Children.Add(todayLine);
                    }
                }

                // ─── 6. Filter and Render Project Rows ───
                List<ProjectStatusItem> filtered = GetFilteredProjects();

                if (filtered.Count == 0)
                {
                    TextBlock emptyMsg = new TextBlock
                    {
                        Text = "No projects active for this period.",
                        Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
                        FontSize = 12,
                        Margin = new Thickness(0, 16, 0, 16),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    GanttRowsStack.Children.Add(emptyMsg);
                    return;
                }

                DateTime monthStart = new DateTime(_currentYear, _currentMonth, 1);
                DateTime monthEnd = new DateTime(_currentYear, _currentMonth, daysInMonth);
                int renderedCount = 0;

                foreach (ProjectStatusItem p in filtered)
                {
                    if (p == null) continue;

                    DateTime startDt;
                    DateTime endDt;
                    if (!TryParseProjectDates(p, out startDt, out endDt)) continue;

                    // 1. Skip if project has NO overlap with active month view
                    if (endDt < monthStart || startDt > monthEnd) continue;

                    // 2. Clamp strictly to current month view boundaries
                    DateTime effectiveStart = (startDt < monthStart) ? monthStart : startDt;
                    DateTime effectiveEnd = (endDt > monthEnd) ? monthEnd : endDt;

                    int startDay = effectiveStart.Day;
                    int endDay = effectiveEnd.Day;
                    if (startDay > endDay) startDay = endDay;

                    int colSpan = Math.Max(1, (endDay - startDay) + 1);

                    // 3. Detect Off-Day Conflicts (Deadline falls on Saturday, Sunday, or Public Holiday)
                    bool isDeadlineOffDay = MalaysiaHolidayService.IsOffDay(endDt);
                    string offDayReason = isDeadlineOffDay ? MalaysiaHolidayService.GetOffDayReason(endDt) : null;

                    Border rowBorder = new Border
                    {
                        BorderBrush = (Brush)Application.Current.FindResource("CardStrokeColorDefaultBrush"),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Padding = new Thickness(0, 6, 0, 6)
                    };

                    Grid rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    }

                    // Project Title Label Column
                    StackPanel nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
                    nameStack.ToolTip = "Click to inspect & edit in right drawer";
                    ProjectStatusItem projRef = p;
                    nameStack.MouseLeftButtonDown += (s, ev) =>
                    {
                        ev.Handled = true;
                        OpenProjectDetailDrawer(projRef);
                    };

                    TextBlock pName = new TextBlock
                    {
                        Text = p.Project ?? "Untitled",
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)Application.Current.FindResource("TextFillColorPrimaryBrush"),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    TextBlock pSub = new TextBlock
                    {
                        Text = isDeadlineOffDay
                            ? string.Format("{0} • {1} • ⚠️ Due on {2}!", p.Designer ?? "Unknown", p.Status ?? "backlog", offDayReason)
                            : string.Format("{0} • {1}", p.Designer ?? "Unknown", p.Status ?? "backlog"),
                        FontSize = 9,
                        FontWeight = isDeadlineOffDay ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = isDeadlineOffDay
                            ? new SolidColorBrush(Color.FromRgb(220, 38, 38)) // Red warning text
                            : (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    nameStack.Children.Add(pName);
                    nameStack.Children.Add(pSub);

                    // Subtask indicator badge in left column
                    if (p.TotalSubtasksCount > 0)
                    {
                        StackPanel subtaskRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                        bool allDone = (p.CompletedSubtasksCount == p.TotalSubtasksCount);
                        Border subtaskBadge = new Border
                        {
                            Background = allDone
                                ? (Brush)Application.Current.FindResource("SystemFillColorSuccessBrush")
                                : (Brush)Application.Current.FindResource("TextControlBackground"),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(4, 1, 4, 1)
                        };
                        TextBlock txtSubtaskBadge = new TextBlock
                        {
                            Text = string.Format("✓ {0}/{1} • {2:0.#} pts", p.CompletedSubtasksCount, p.TotalSubtasksCount, p.TotalWeight),
                            FontSize = 8.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = allDone ? Brushes.White : (Brush)Application.Current.FindResource("FluentBrand80")
                        };
                        subtaskBadge.Child = txtSubtaskBadge;
                        subtaskRow.Children.Add(subtaskBadge);
                        nameStack.Children.Add(subtaskRow);
                    }

                    Grid.SetColumn(nameStack, 0);
                    rowGrid.Children.Add(nameStack);

                    // Timeline bar element
                    Brush barBrush = GetStatusBrush(p.Status);
                    Border bar = new Border
                    {
                        Background = barBrush,
                        CornerRadius = new CornerRadius(4),
                        Height = 20,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(1, 0, 1, 0)
                    };

                    if (isDeadlineOffDay)
                    {
                        bar.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red border for conflict
                        bar.BorderThickness = new Thickness(1.5);
                    }

                    string warningToolTip = isDeadlineOffDay
                        ? string.Format("\n\n⚠️ SCHEDULE CONFLICT: Project deadline falls on an OFF-DAY ({0})!\nCreative deliverables must not be scheduled on weekends or public holidays. Please reschedule to a working day.", offDayReason)
                        : "";

                    string subtaskSummary = "";
                    if (p.Subtasks != null && p.Subtasks.Count > 0)
                    {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        sb.AppendLine(string.Format("\n\nDeliverables & Subtasks ({0}/{1} Done • {2:0.#} pts):", p.CompletedSubtasksCount, p.TotalSubtasksCount, p.TotalWeight));
                        foreach (var st in p.Subtasks)
                        {
                            string mark = st.IsCompleted ? "✓" : ((st.Status ?? "").ToLowerInvariant().Contains("progress") ? "⏳" : "○");
                            sb.AppendLine(string.Format("  {0} [{1}] {2} ({3}, {4})", mark, st.StatusDisplay, st.Name, st.Specs, st.WeightDisplay));
                        }
                        subtaskSummary = sb.ToString();
                    }

                    bar.ToolTip = string.Format("Project: {0}\nDesigner: {1}\nStatus: {2}\nStart: {3}\nDeadline: {4}{5}{6}\n\n👉 Click center to inspect in drawer\n↔ Drag left edge to adjust Start Date\n↔ Drag right edge to adjust Deadline",
                        p.Project, p.Designer, p.Status, p.CreatedDateDisplay, p.DeadlineDisplay, warningToolTip, subtaskSummary);

                    DockPanel barContent = new DockPanel { LastChildFill = true, Margin = new Thickness(2, 0, 2, 0) };

                    if (isDeadlineOffDay)
                    {
                        Border warnBadge = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(3, 0, 3, 0),
                            Margin = new Thickness(4, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        DockPanel.SetDock(warnBadge, Dock.Right);
                        TextBlock warnTxt = new TextBlock
                        {
                            Text = "⚠️ Off-Day",
                            FontSize = 8,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        warnBadge.Child = warnTxt;
                        barContent.Children.Add(warnBadge);
                    }

                    // Subtask pill badge on Gantt timeline bar
                    if (p.TotalSubtasksCount > 0)
                    {
                        Border subtaskPill = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(4, 0, 4, 0),
                            Margin = new Thickness(3, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        DockPanel.SetDock(subtaskPill, Dock.Right);
                        TextBlock txtPill = new TextBlock
                        {
                            Text = string.Format("✓ {0}/{1}", p.CompletedSubtasksCount, p.TotalSubtasksCount),
                            FontSize = 8,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        subtaskPill.Child = txtPill;
                        barContent.Children.Add(subtaskPill);
                    }

                    TextBlock barText = new TextBlock
                    {
                        Text = p.Project,
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    barContent.Children.Add(barText);

                    // Center clickable region (opens drawer)
                    Border centerRegion = new Border
                    {
                        Background = Brushes.Transparent,
                        Cursor = Cursors.Hand,
                        Child = barContent
                    };
                    centerRegion.MouseLeftButtonDown += (s, ev) =>
                    {
                        ev.Handled = true;
                        OpenProjectDetailDrawer(projRef);
                    };

                    // Left drag-to-resize handle (Start Date)
                    Border leftGripper = new Border
                    {
                        Width = 2,
                        Height = 10,
                        CornerRadius = new CornerRadius(1),
                        Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false
                    };
                    Border leftHandle = new Border
                    {
                        Width = 7,
                        Background = Brushes.Transparent,
                        Cursor = Cursors.SizeWE,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Child = leftGripper,
                        ToolTip = "Drag to adjust Start Date"
                    };
                    leftHandle.MouseEnter += (s, e) => leftGripper.Background = Brushes.White;
                    leftHandle.MouseLeave += (s, e) => leftGripper.Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
                    leftHandle.MouseLeftButtonDown += (s, ev) =>
                    {
                        ev.Handled = true;
                        StartGanttBarDrag(projRef, bar, rowGrid, daysInMonth, activeMonthDate, true, startDay, endDay, ev, leftHandle);
                    };

                    // Right drag-to-resize handle (Deadline)
                    Border rightGripper = new Border
                    {
                        Width = 2,
                        Height = 10,
                        CornerRadius = new CornerRadius(1),
                        Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false
                    };
                    Border rightHandle = new Border
                    {
                        Width = 7,
                        Background = Brushes.Transparent,
                        Cursor = Cursors.SizeWE,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Child = rightGripper,
                        ToolTip = "Drag to adjust Deadline (End Date)"
                    };
                    rightHandle.MouseEnter += (s, e) => rightGripper.Background = Brushes.White;
                    rightHandle.MouseLeave += (s, e) => rightGripper.Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
                    rightHandle.MouseLeftButtonDown += (s, ev) =>
                    {
                        ev.Handled = true;
                        StartGanttBarDrag(projRef, bar, rowGrid, daysInMonth, activeMonthDate, false, startDay, endDay, ev, rightHandle);
                    };

                    Grid barGrid = new Grid();
                    barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
                    barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });

                    Grid.SetColumn(leftHandle, 0);
                    Grid.SetColumn(centerRegion, 1);
                    Grid.SetColumn(rightHandle, 2);

                    barGrid.Children.Add(leftHandle);
                    barGrid.Children.Add(centerRegion);
                    barGrid.Children.Add(rightHandle);

                    bar.Child = barGrid;

                    Grid.SetColumn(bar, startDay);
                    Grid.SetColumnSpan(bar, colSpan);
                    rowGrid.Children.Add(bar);

                    rowBorder.Child = rowGrid;
                    GanttRowsStack.Children.Add(rowBorder);
                    renderedCount++;
                }

                if (renderedCount == 0)
                {
                    TextBlock emptyMsg = new TextBlock
                    {
                        Text = string.Format("No projects active during {0}.", activeMonthDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture)),
                        Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
                        FontSize = 12,
                        Margin = new Thickness(0, 16, 0, 16),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    GanttRowsStack.Children.Add(emptyMsg);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] RenderGanttTimeline error: " + ex.Message);
            }
        }

        #region Gantt Timeline Drag-to-Resize

        private void StartGanttBarDrag(ProjectStatusItem project, Border bar, Grid rowGrid, int daysInMonth, DateTime activeMonthDate, bool isLeftEdge, int startDay, int endDay, MouseButtonEventArgs ev, FrameworkElement handle)
        {
            if (project == null || bar == null || rowGrid == null || handle == null) return;
            try
            {
                _isDraggingGanttEdge = true;
                _dragIsLeftEdge = isLeftEdge;
                _dragGanttProject = project;
                _dragGanttBar = bar;
                _dragGanttRowGrid = rowGrid;
                _dragDaysInMonth = daysInMonth;
                _dragActiveMonth = activeMonthDate;
                _dragOriginalStartDay = startDay;
                _dragOriginalEndDay = endDay;
                _dragCurrentStartDay = startDay;
                _dragCurrentEndDay = endDay;
                _dragCapturedHandle = handle;

                handle.CaptureMouse();

                MouseEventHandler moveHandler = null;
                MouseButtonEventHandler upHandler = null;

                moveHandler = (s, moveEv) =>
                {
                    if (!_isDraggingGanttEdge) return;
                    Point pt = moveEv.GetPosition(_dragGanttRowGrid);
                    double col0Width = 160.0;
                    double availableWidth = _dragGanttRowGrid.ActualWidth - col0Width;
                    if (availableWidth <= 0) return;

                    double dayWidth = availableWidth / _dragDaysInMonth;
                    int targetDay = (int)Math.Floor((pt.X - col0Width) / dayWidth) + 1;
                    targetDay = Math.Max(1, Math.Min(_dragDaysInMonth, targetDay));

                    if (_dragIsLeftEdge)
                    {
                        targetDay = Math.Min(targetDay, _dragCurrentEndDay);
                        if (targetDay != _dragCurrentStartDay)
                        {
                            _dragCurrentStartDay = targetDay;
                            int newColSpan = Math.Max(1, (_dragCurrentEndDay - _dragCurrentStartDay) + 1);
                            Grid.SetColumn(_dragGanttBar, _dragCurrentStartDay);
                            Grid.SetColumnSpan(_dragGanttBar, newColSpan);

                            DateTime newStart = new DateTime(_dragActiveMonth.Year, _dragActiveMonth.Month, _dragCurrentStartDay);
                            int dur = Math.Max(1, (_dragGanttProject.ParsedDeadline.Date - newStart.Date).Days + 1);
                            _dragGanttBar.ToolTip = string.Format("Adjusting Start Date ➔ {0:dd MMM yyyy} (Duration: {1}d)", newStart, dur);
                        }
                    }
                    else
                    {
                        targetDay = Math.Max(_dragCurrentStartDay, targetDay);
                        if (targetDay != _dragCurrentEndDay)
                        {
                            _dragCurrentEndDay = targetDay;
                            int newColSpan = Math.Max(1, (_dragCurrentEndDay - _dragCurrentStartDay) + 1);
                            Grid.SetColumnSpan(_dragGanttBar, newColSpan);

                            DateTime newDeadline = new DateTime(_dragActiveMonth.Year, _dragActiveMonth.Month, _dragCurrentEndDay);
                            bool isConflict = MalaysiaHolidayService.IsOffDay(newDeadline);
                            string offReason = isConflict ? MalaysiaHolidayService.GetOffDayReason(newDeadline) : null;
                            if (isConflict)
                            {
                                _dragGanttBar.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                                _dragGanttBar.BorderThickness = new Thickness(2);
                                _dragGanttBar.ToolTip = string.Format("Adjusting Deadline ➔ {0:dd MMM yyyy} ⚠️ OFF-DAY ({1})!\nRelease to set, or drag to next working day.", newDeadline, offReason);
                            }
                            else
                            {
                                _dragGanttBar.BorderBrush = Brushes.Transparent;
                                _dragGanttBar.BorderThickness = new Thickness(0);
                                int dur = (_dragCurrentEndDay - _dragCurrentStartDay) + 1;
                                _dragGanttBar.ToolTip = string.Format("Adjusting Deadline ➔ {0:dd MMM yyyy} ({1} days)", newDeadline, dur);
                            }
                        }
                    }
                };

                upHandler = (s, upEv) =>
                {
                    if (!_isDraggingGanttEdge) return;
                    _isDraggingGanttEdge = false;
                    _dragCapturedHandle.ReleaseMouseCapture();

                    handle.MouseMove -= moveHandler;
                    handle.MouseLeftButtonUp -= upHandler;

                    bool dateChanged = false;
                    if (_dragIsLeftEdge && _dragCurrentStartDay != _dragOriginalStartDay)
                    {
                        DateTime newStart = new DateTime(_dragActiveMonth.Year, _dragActiveMonth.Month, _dragCurrentStartDay);
                        _dragGanttProject.CreatedDate = newStart.ToString("yyyy-MM-dd");
                        DateTime dl = _dragGanttProject.ParsedDeadline;
                        if (dl >= newStart)
                        {
                            _dragGanttProject.Duration = string.Format("{0}d", (dl.Date - newStart.Date).Days + 1);
                        }
                        dateChanged = true;
                    }
                    else if (!_dragIsLeftEdge && _dragCurrentEndDay != _dragOriginalEndDay)
                    {
                        DateTime newDeadline = new DateTime(_dragActiveMonth.Year, _dragActiveMonth.Month, _dragCurrentEndDay);
                        _dragGanttProject.Deadline = newDeadline.ToString("yyyy-MM-dd");
                        DateTime st = _dragGanttProject.ParsedCreatedDate;
                        if (newDeadline >= st)
                        {
                            _dragGanttProject.Duration = string.Format("{0}d", (newDeadline.Date - st.Date).Days + 1);
                        }
                        dateChanged = true;
                    }

                    if (dateChanged)
                    {
                        try
                        {
                            FrontmatterService.WriteStatus(_dragGanttProject);
                            if (_drawerEditingProject != null && string.Equals(_drawerEditingProject.FullPath, _dragGanttProject.FullPath, StringComparison.OrdinalIgnoreCase))
                            {
                                OpenProjectDetailDrawer(_dragGanttProject);
                            }
                            RenderGanttTimeline();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[CalendarPage] DragEnd save error: " + ex.Message);
                        }
                    }
                };

                handle.MouseMove += moveHandler;
                handle.MouseLeftButtonUp += upHandler;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] StartGanttBarDrag error: " + ex.Message);
            }
        }

        #endregion

        #region Project Detail Drawer & Deliverables Management

        public void OpenProjectDetailDrawer(ProjectStatusItem project)
        {
            if (project == null) return;
            _drawerEditingProject = project;

            try
            {
                if (DayDetailPanel != null) DayDetailPanel.Visibility = Visibility.Collapsed;

                if (DrawerJobId != null)
                {
                    string idText = !string.IsNullOrWhiteSpace(project.Project) ? project.Project : "PROJECT";
                    if (idText.Length > 18) idText = idText.Substring(0, 18);
                    DrawerJobId.Text = idText;
                }

                UpdateDrawerStatusBadge(project.Status);

                if (DrawerTitle != null) DrawerTitle.Text = project.Project ?? "Untitled Project";
                if (DrawerSubtitle != null)
                {
                    DrawerSubtitle.Text = string.Format("{0} • {1}",
                        !string.IsNullOrWhiteSpace(project.Designer) ? project.Designer : "Unassigned",
                        project.FullPath ?? "");
                }

                if (DrawerDesigner != null)
                {
                    string currentDesigner = project.Designer ?? "";
                    DrawerDesigner.Items.Clear();
                    if (DesignerFilter != null)
                    {
                        foreach (var it in DesignerFilter.Items)
                        {
                            string d = it != null ? it.ToString() : "";
                            if (!string.IsNullOrWhiteSpace(d) && !d.Equals("All Designers", StringComparison.OrdinalIgnoreCase))
                            {
                                DrawerDesigner.Items.Add(d);
                            }
                        }
                    }
                    DrawerDesigner.Text = currentDesigner;
                }

                if (DrawerStatus != null) SelectComboItemByContent(DrawerStatus, project.Status);
                if (DrawerPriority != null) SelectComboItemByContent(DrawerPriority, project.Priority);

                _isUpdatingDrawerDates = true;
                DateTime dtStart;
                if (!string.IsNullOrWhiteSpace(project.CreatedDate) && DateTime.TryParse(project.CreatedDate, out dtStart))
                {
                    DrawerStartDate.SelectedDate = dtStart.Date;
                }
                else
                {
                    DrawerStartDate.SelectedDate = project.ParsedCreatedDate.Date;
                }

                DateTime dtDeadline;
                if (!string.IsNullOrWhiteSpace(project.Deadline) && DateTime.TryParse(project.Deadline, out dtDeadline))
                {
                    DrawerDeadline.SelectedDate = dtDeadline.Date;
                }
                else
                {
                    DrawerDeadline.SelectedDate = DrawerStartDate.SelectedDate;
                }

                if (DrawerDuration != null)
                {
                    DrawerDuration.Text = !string.IsNullOrWhiteSpace(project.Duration) ? project.Duration : "";
                }
                _isUpdatingDrawerDates = false;

                if (DrawerRevision != null)
                {
                    DrawerRevision.Text = project.Revision.ToString();
                }

                if (DrawerSaveStatus != null)
                {
                    DrawerSaveStatus.Text = "";
                }

                UpdateDrawerConflictBanner();

                if (DrawerSubtaskEditorCard != null) DrawerSubtaskEditorCard.Visibility = Visibility.Collapsed;
                _drawerEditingSubtaskId = null;
                PopulateDrawerSubtasks(project);

                if (ProjectDetailDrawer != null)
                {
                    ProjectDetailDrawer.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] OpenProjectDetailDrawer error: " + ex.Message);
            }
        }

        private void OnCloseProjectDetailDrawer(object sender, RoutedEventArgs e)
        {
            if (ProjectDetailDrawer != null)
            {
                ProjectDetailDrawer.Visibility = Visibility.Collapsed;
            }
            _drawerEditingProject = null;
        }

        private void OnOpenDrawerFromDayDetailClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                FrameworkElement el = sender as FrameworkElement;
                if (el == null || el.Tag == null) return;
                string path = el.Tag.ToString();
                if (string.IsNullOrWhiteSpace(path)) return;

                var proj = _allProjects.Find(p => string.Equals(p.FullPath, path, StringComparison.OrdinalIgnoreCase));
                if (proj != null)
                {
                    OpenProjectDetailDrawer(proj);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] OnOpenDrawerFromDayDetailClicked error: " + ex.Message);
            }
        }

        private void UpdateDrawerConflictBanner()
        {
            if (DrawerConflictBanner == null) return;
            if (DrawerDeadline != null && DrawerDeadline.SelectedDate.HasValue)
            {
                DateTime dl = DrawerDeadline.SelectedDate.Value.Date;
                bool isOff = MalaysiaHolidayService.IsOffDay(dl);
                if (isOff)
                {
                    string reason = MalaysiaHolidayService.GetOffDayReason(dl);
                    if (DrawerConflictText != null)
                    {
                        DrawerConflictText.Text = string.Format("⚠️ Deadline ({0:dd MMM yyyy}) lands on an OFF-DAY ({1}). Creative deliverables must not be scheduled on weekends or public holidays.", dl, reason);
                    }
                    DrawerConflictBanner.Visibility = Visibility.Visible;
                }
                else
                {
                    DrawerConflictBanner.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                DrawerConflictBanner.Visibility = Visibility.Collapsed;
            }
        }

        private void OnDrawerFixConflictClicked(object sender, RoutedEventArgs e)
        {
            if (DrawerDeadline == null || !DrawerDeadline.SelectedDate.HasValue) return;
            DateTime cur = DrawerDeadline.SelectedDate.Value.Date;
            DateTime next = cur.AddDays(1);
            int safety = 0;
            while (MalaysiaHolidayService.IsOffDay(next) && safety < 14)
            {
                next = next.AddDays(1);
                safety++;
            }
            DrawerDeadline.SelectedDate = next;
        }

        private void OnDrawerDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingDrawerDates) return;
            UpdateDrawerConflictBanner();

            if (DrawerStartDate != null && DrawerDeadline != null &&
                DrawerStartDate.SelectedDate.HasValue && DrawerDeadline.SelectedDate.HasValue)
            {
                DateTime s = DrawerStartDate.SelectedDate.Value.Date;
                DateTime d = DrawerDeadline.SelectedDate.Value.Date;
                if (d >= s)
                {
                    int days = (d - s).Days + 1;
                    _isUpdatingDrawerDates = true;
                    if (DrawerDuration != null) DrawerDuration.Text = string.Format("{0}d", days);
                    _isUpdatingDrawerDates = false;
                }
            }
        }

        private void OnDrawerDurationChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingDrawerDates) return;
            if (DrawerStartDate == null || !DrawerStartDate.SelectedDate.HasValue) return;
            if (DrawerDuration == null) return;

            string text = DrawerDuration.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            var match = Regex.Match(text, @"^\d+");
            if (match.Success)
            {
                int days;
                if (int.TryParse(match.Value, out days) && days > 0)
                {
                    _isUpdatingDrawerDates = true;
                    DateTime s = DrawerStartDate.SelectedDate.Value.Date;
                    DrawerDeadline.SelectedDate = s.AddDays(days - 1);
                    _isUpdatingDrawerDates = false;
                    UpdateDrawerConflictBanner();
                }
            }
        }

        private void OnDrawerAddDaysClicked(object sender, RoutedEventArgs e)
        {
            FrameworkElement btn = sender as FrameworkElement;
            if (btn == null || btn.Tag == null) return;
            int addDays;
            if (!int.TryParse(btn.Tag.ToString(), out addDays)) return;

            DateTime baseDate = DrawerDeadline != null && DrawerDeadline.SelectedDate.HasValue
                ? DrawerDeadline.SelectedDate.Value.Date
                : (DrawerStartDate != null && DrawerStartDate.SelectedDate.HasValue ? DrawerStartDate.SelectedDate.Value.Date : DateTime.Today);

            if (DrawerDeadline != null)
            {
                DrawerDeadline.SelectedDate = baseDate.AddDays(addDays);
            }
        }

        private void OnDrawerStatusSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DrawerStatus != null && DrawerStatus.SelectedItem is ComboBoxItem)
            {
                string status = ((ComboBoxItem)DrawerStatus.SelectedItem).Content.ToString();
                UpdateDrawerStatusBadge(status);
            }
        }

        private void UpdateDrawerStatusBadge(string status)
        {
            if (DrawerStatusBadgeText == null || DrawerStatusBadge == null) return;
            string st = (status ?? "backlog").ToLowerInvariant().Trim();
            DrawerStatusBadgeText.Text = st.ToUpperInvariant();
            DrawerStatusBadge.Background = GetStatusBrush(st);
            DrawerStatusBadgeText.Foreground = Brushes.White;
        }

        private void OnDrawerRevisionDecrementClicked(object sender, RoutedEventArgs e)
        {
            if (DrawerRevision == null) return;
            int val;
            if (!int.TryParse(DrawerRevision.Text, out val)) val = 0;
            if (val > 0) val--;
            DrawerRevision.Text = val.ToString();
        }

        private void OnDrawerRevisionIncrementClicked(object sender, RoutedEventArgs e)
        {
            if (DrawerRevision == null) return;
            int val;
            if (!int.TryParse(DrawerRevision.Text, out val)) val = 0;
            val++;
            DrawerRevision.Text = val.ToString();
        }

        private void OnDrawerSaveClicked(object sender, RoutedEventArgs e)
        {
            if (_drawerEditingProject == null) return;

            try
            {
                if (DrawerStatus != null && DrawerStatus.SelectedItem is ComboBoxItem)
                    _drawerEditingProject.Status = ((ComboBoxItem)DrawerStatus.SelectedItem).Content.ToString();

                if (DrawerPriority != null && DrawerPriority.SelectedItem is ComboBoxItem)
                    _drawerEditingProject.Priority = ((ComboBoxItem)DrawerPriority.SelectedItem).Content.ToString();

                if (DrawerDeadline != null && DrawerDeadline.SelectedDate.HasValue)
                    _drawerEditingProject.Deadline = DrawerDeadline.SelectedDate.Value.ToString("yyyy-MM-dd");

                if (DrawerStartDate != null && DrawerStartDate.SelectedDate.HasValue)
                    _drawerEditingProject.CreatedDate = DrawerStartDate.SelectedDate.Value.ToString("yyyy-MM-dd");

                if (DrawerDuration != null)
                    _drawerEditingProject.Duration = DrawerDuration.Text.Trim();

                if (DrawerDesigner != null)
                    _drawerEditingProject.Designer = DrawerDesigner.Text.Trim();

                int revVal;
                if (DrawerRevision != null && int.TryParse(DrawerRevision.Text, out revVal))
                    _drawerEditingProject.Revision = revVal;

                // Write to README.md
                FrontmatterService.WriteStatus(_drawerEditingProject);

                if (DrawerSaveStatus != null)
                {
                    DrawerSaveStatus.Text = "Saved to README.md \u2713";
                }

                NotificationService.ShowSuccess(
                    "Project Updated",
                    string.Format("Saved timeline & status for '{0}'.", _drawerEditingProject.Project),
                    _drawerEditingProject.FullPath);

                // Update item in _allProjects
                int idx = _allProjects.FindIndex(p => string.Equals(p.FullPath, _drawerEditingProject.FullPath, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    _allProjects[idx] = _drawerEditingProject;
                }

                UpdateMetrics();
                if (_isGanttView) RenderGanttTimeline(); else RenderCalendarGrid();
            }
            catch (Exception ex)
            {
                if (DrawerSaveStatus != null)
                {
                    DrawerSaveStatus.Text = "Error: " + ex.Message;
                }
                Debug.WriteLine("[CalendarPage] OnDrawerSaveClicked error: " + ex.Message);
            }
        }

        private void OnDrawerOpenFolderClicked(object sender, RoutedEventArgs e)
        {
            if (_drawerEditingProject != null && Directory.Exists(_drawerEditingProject.FullPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _drawerEditingProject.FullPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[CalendarPage] OpenFolder error: " + ex.Message);
                }
            }
        }

        private void OnDrawerOpenSourceClicked(object sender, RoutedEventArgs e)
        {
            if (_drawerEditingProject != null && Directory.Exists(_drawerEditingProject.FullPath))
            {
                string srcDir = Path.Combine(_drawerEditingProject.FullPath, "02_SOURCE_FILES");
                string targetFile = null;
                if (Directory.Exists(srcDir))
                {
                    string[] files = Directory.GetFiles(srcDir);
                    foreach (string f in files)
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext == ".afdesign" || ext == ".psd" || ext == ".ai" || ext == ".afphoto" || ext == ".afpub")
                        {
                            targetFile = f;
                            break;
                        }
                    }
                }

                if (targetFile != null && File.Exists(targetFile))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = targetFile, UseShellExecute = true });
                        NotificationService.ShowInfo("Launching Source File", Path.GetFileName(targetFile));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[CalendarPage] OpenSource error: " + ex.Message);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("No working source file (.afdesign, .psd, .ai) found in 02_SOURCE_FILES.", "Source File Not Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
        }

        private void OnDrawerOpenCanvaClicked(object sender, RoutedEventArgs e)
        {
            if (_drawerEditingProject != null && !string.IsNullOrWhiteSpace(_drawerEditingProject.CanvaUrl))
            {
                try
                {
                    string url = _drawerEditingProject.CanvaUrl.Trim();
                    if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        url = "https://" + url;
                    }
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[CalendarPage] OpenCanva error: " + ex.Message);
                }
            }
            else
            {
                NotificationService.ShowInfo("No Canva URL", "This project does not have a Canva URL specified in README.md.");
            }
        }

        private async void OnDrawerHandoverZipClicked(object sender, RoutedEventArgs e)
        {
            if (_drawerEditingProject == null || !Directory.Exists(_drawerEditingProject.FullPath)) return;

            Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Creative Handover Package (ZIP)",
                Filter = "ZIP Archive (*.zip)|*.zip",
                FileName = string.Format("{0}_Handover.zip", _drawerEditingProject.Project),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (sfd.ShowDialog() == true)
            {
                ExportPackageOptions options = new ExportPackageOptions
                {
                    IncludeDeliverables = true,
                    IncludeCopywriting = true,
                    IncludeBriefMarkdown = true,
                    IncludeHtmlSummary = true,
                    IncludeWipMockups = false
                };

                ExportPackageResult res = await ExportPackagingService.CreateHandoverPackageAsync(_drawerEditingProject.FullPath, sfd.FileName, options);
                if (res != null && res.Success)
                {
                    NotificationService.ShowSuccess("Handover Exported", string.Format("Packaged {0} files into {1}", res.FileCount, Path.GetFileName(res.ZipFilePath)));
                }
                else
                {
                    System.Windows.MessageBox.Show(res != null ? res.ErrorMessage : "Packaging failed", "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void PopulateDrawerSubtasks(ProjectStatusItem item)
        {
            if (item == null) return;
            try
            {
                if (DrawerSubtasksList != null)
                {
                    DrawerSubtasksList.ItemsSource = null;
                    DrawerSubtasksList.ItemsSource = item.Subtasks;
                }
                if (DrawerSubtasksProgressBar != null)
                {
                    DrawerSubtasksProgressBar.Value = item.SubtaskProgressPercent;
                }
                if (DrawerSubtasksProgress != null)
                {
                    DrawerSubtasksProgress.Text = string.Format("({0}/{1} Done • {2:0.#} pts)",
                        item.CompletedSubtasksCount, item.TotalSubtasksCount, item.TotalWeight);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CalendarPage] PopulateDrawerSubtasks error: " + ex.Message);
            }
        }

        private void OnDrawerSubtaskStatusToggleClicked(object sender, RoutedEventArgs e)
        {
            FrameworkElement btn = sender as FrameworkElement;
            string subtaskId = btn != null ? btn.Tag as string : null;
            if (string.IsNullOrWhiteSpace(subtaskId) || _drawerEditingProject == null || _drawerEditingProject.Subtasks == null) return;

            var st = _drawerEditingProject.Subtasks.Find(s => string.Equals(s.Id, subtaskId, StringComparison.OrdinalIgnoreCase));
            if (st == null) return;

            string currentStatus = (st.Status ?? "draft").ToLowerInvariant().Trim();
            if (currentStatus == "draft") st.Status = "in-progress";
            else if (currentStatus == "in-progress" || currentStatus == "progress" || currentStatus == "review" || currentStatus == "revision") st.Status = "done";
            else st.Status = "draft";

            FrontmatterService.WriteStatus(_drawerEditingProject);
            PopulateDrawerSubtasks(_drawerEditingProject);
            if (_isGanttView) RenderGanttTimeline();
        }

        private void OnDrawerAddSubtaskClicked(object sender, RoutedEventArgs e)
        {
            if (_drawerEditingProject == null) return;
            if (_drawerEditingProject.Subtasks == null) _drawerEditingProject.Subtasks = new List<ProjectSubtaskItem>();

            int nextNum = _drawerEditingProject.Subtasks.Count + 1;
            bool isVideo = (!string.IsNullOrEmpty(_drawerEditingProject.Project) && _drawerEditingProject.Project.IndexOf("V_", StringComparison.OrdinalIgnoreCase) >= 0);
            string id = string.Format("{0}{1:D2}", isVideo ? "V" : "KV", nextNum);
            string name = isVideo ? (nextNum == 1 ? "Master Story Cut 60s" : string.Format("Hook Variation {0} 15s", (char)('A' + nextNum - 2))) : string.Format("Key Visual {0}", nextNum);
            double weight = isVideo ? (nextNum == 1 ? 2.0 : 0.4) : (nextNum == 1 ? 1.0 : 0.2);

            _drawerEditingSubtaskId = null;
            if (DrawerSubtaskEditorTitle != null) DrawerSubtaskEditorTitle.Text = "Add Deliverable / Subtask";
            if (DrawerSubtaskId != null) DrawerSubtaskId.Text = id;
            if (DrawerSubtaskName != null) DrawerSubtaskName.Text = name;
            if (DrawerSubtaskSpecs != null) DrawerSubtaskSpecs.Text = isVideo ? "9:16, 1080x1920" : "1:1, 1080x1080";
            if (DrawerSubtaskType != null) DrawerSubtaskType.Text = isVideo ? (nextNum == 1 ? "master_video" : "hook_variation") : (nextNum == 1 ? "key_visual" : "resize");
            if (DrawerSubtaskWeight != null) DrawerSubtaskWeight.Text = weight.ToString("0.#");
            if (DrawerSubtaskStatus != null) DrawerSubtaskStatus.SelectedIndex = 0;

            if (DrawerSubtaskEditorCard != null)
            {
                DrawerSubtaskEditorCard.Visibility = Visibility.Visible;
                if (DrawerSubtaskName != null) DrawerSubtaskName.Focus();
            }
        }

        private void OnDrawerEditSubtaskClicked(object sender, RoutedEventArgs e)
        {
            FrameworkElement btn = sender as FrameworkElement;
            string subtaskId = btn != null ? btn.Tag as string : null;
            if (string.IsNullOrWhiteSpace(subtaskId) || _drawerEditingProject == null || _drawerEditingProject.Subtasks == null) return;

            var st = _drawerEditingProject.Subtasks.Find(s => string.Equals(s.Id, subtaskId, StringComparison.OrdinalIgnoreCase));
            if (st == null) return;

            _drawerEditingSubtaskId = st.Id;
            if (DrawerSubtaskEditorTitle != null) DrawerSubtaskEditorTitle.Text = string.Format("Edit Deliverable: {0}", st.Id);
            if (DrawerSubtaskId != null) DrawerSubtaskId.Text = st.Id ?? "";
            if (DrawerSubtaskName != null) DrawerSubtaskName.Text = st.Name ?? "";
            if (DrawerSubtaskSpecs != null) DrawerSubtaskSpecs.Text = st.Specs ?? "";
            if (DrawerSubtaskType != null) DrawerSubtaskType.Text = st.Type ?? "";
            if (DrawerSubtaskWeight != null) DrawerSubtaskWeight.Text = st.Weight.ToString("0.#");

            if (DrawerSubtaskStatus != null)
            {
                string norm = (st.Status ?? "draft").ToLowerInvariant().Trim();
                if (norm == "done" || norm == "approved") DrawerSubtaskStatus.SelectedIndex = 2;
                else if (norm == "in-progress" || norm == "progress" || norm == "review" || norm == "revision") DrawerSubtaskStatus.SelectedIndex = 1;
                else DrawerSubtaskStatus.SelectedIndex = 0;
            }

            if (DrawerSubtaskEditorCard != null)
            {
                DrawerSubtaskEditorCard.Visibility = Visibility.Visible;
                if (DrawerSubtaskName != null) DrawerSubtaskName.Focus();
            }
        }

        private void OnDrawerCancelSubtaskEditClicked(object sender, RoutedEventArgs e)
        {
            _drawerEditingSubtaskId = null;
            if (DrawerSubtaskEditorCard != null)
            {
                DrawerSubtaskEditorCard.Visibility = Visibility.Collapsed;
            }
        }

        private void OnDrawerSaveSubtaskEditClicked(object sender, RoutedEventArgs e)
        {
            if (_drawerEditingProject == null) return;
            if (_drawerEditingProject.Subtasks == null) _drawerEditingProject.Subtasks = new List<ProjectSubtaskItem>();

            string id = DrawerSubtaskId != null ? DrawerSubtaskId.Text.Trim() : "";
            string name = DrawerSubtaskName != null ? DrawerSubtaskName.Text.Trim() : "";
            string specs = DrawerSubtaskSpecs != null ? DrawerSubtaskSpecs.Text.Trim() : "";
            string type = DrawerSubtaskType != null ? DrawerSubtaskType.Text.Trim() : "";
            string weightRaw = DrawerSubtaskWeight != null ? DrawerSubtaskWeight.Text.Trim() : "";

            if (string.IsNullOrWhiteSpace(id)) id = string.Format("ST{0:D2}", _drawerEditingProject.Subtasks.Count + 1);
            if (string.IsNullOrWhiteSpace(name)) name = "Untitled Deliverable";
            if (string.IsNullOrWhiteSpace(specs)) specs = "1080x1080";
            if (string.IsNullOrWhiteSpace(type)) type = "artwork";

            if (specs.Contains("(") && specs.IndexOf("(") > 3)
            {
                specs = specs.Substring(0, specs.IndexOf("(")).Trim();
            }

            double weight = 1.0;
            if (!string.IsNullOrWhiteSpace(weightRaw))
            {
                string match = Regex.Match(weightRaw, @"\d+(\.\d+)?").Value;
                if (!double.TryParse(match, NumberStyles.Any, CultureInfo.InvariantCulture, out weight))
                {
                    weight = 1.0;
                }
            }

            string status = "draft";
            if (DrawerSubtaskStatus != null)
            {
                if (DrawerSubtaskStatus.SelectedIndex == 1) status = "in-progress";
                else if (DrawerSubtaskStatus.SelectedIndex == 2) status = "done";
                else status = "draft";
            }

            if (!string.IsNullOrWhiteSpace(_drawerEditingSubtaskId))
            {
                var existing = _drawerEditingProject.Subtasks.Find(s => string.Equals(s.Id, _drawerEditingSubtaskId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Id = id;
                    existing.Name = name;
                    existing.Specs = specs;
                    existing.Type = type;
                    existing.Weight = weight;
                    existing.Status = status;
                }
                NotificationService.ShowSuccess("Deliverable Updated", string.Format("Updated '{0}' ({1:0.#} pts)", name, weight), _drawerEditingProject.FullPath);
            }
            else
            {
                var newItem = new ProjectSubtaskItem
                {
                    Id = id,
                    Name = name,
                    Specs = specs,
                    Type = type,
                    Weight = weight,
                    Status = status,
                    AssignedDesigner = _drawerEditingProject.Designer ?? ""
                };
                _drawerEditingProject.Subtasks.Add(newItem);
                NotificationService.ShowSuccess("Deliverable Added", string.Format("Added '{0}' ({1:0.#} pts)", name, weight), _drawerEditingProject.FullPath);
            }

            _drawerEditingSubtaskId = null;
            if (DrawerSubtaskEditorCard != null) DrawerSubtaskEditorCard.Visibility = Visibility.Collapsed;

            FrontmatterService.WriteStatus(_drawerEditingProject);
            PopulateDrawerSubtasks(_drawerEditingProject);
            if (_isGanttView) RenderGanttTimeline();
        }

        private void OnDrawerRemoveSubtaskClicked(object sender, RoutedEventArgs e)
        {
            FrameworkElement btn = sender as FrameworkElement;
            string subtaskId = btn != null ? btn.Tag as string : null;
            if (string.IsNullOrWhiteSpace(subtaskId) || _drawerEditingProject == null || _drawerEditingProject.Subtasks == null) return;

            var st = _drawerEditingProject.Subtasks.Find(s => string.Equals(s.Id, subtaskId, StringComparison.OrdinalIgnoreCase));
            if (st != null)
            {
                if (string.Equals(_drawerEditingSubtaskId, st.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _drawerEditingSubtaskId = null;
                    if (DrawerSubtaskEditorCard != null) DrawerSubtaskEditorCard.Visibility = Visibility.Collapsed;
                }
                _drawerEditingProject.Subtasks.Remove(st);
                FrontmatterService.WriteStatus(_drawerEditingProject);
                PopulateDrawerSubtasks(_drawerEditingProject);
                if (_isGanttView) RenderGanttTimeline();
            }
        }

        private static void SelectComboItemByContent(ComboBox cmb, string value)
        {
            if (cmb == null) return;
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (string.Equals(item.Content.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        #endregion

        private Brush GetStatusBrush(string status)
        {
            string st = (status ?? "").ToLowerInvariant();
            switch (st)
            {
                case "in-progress":
                    return (Brush)Application.Current.FindResource("FluentBrand80");
                case "review":
                    return (Brush)Application.Current.FindResource("SystemFillColorCautionBrush");
                case "done":
                    return (Brush)Application.Current.FindResource("SystemFillColorSuccessBrush");
                case "on-hold":
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                default: // backlog
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            }
        }
    }
}
