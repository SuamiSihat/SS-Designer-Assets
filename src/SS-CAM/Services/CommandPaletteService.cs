using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SS_CAM.Models;
using SS_CAM.Views;

namespace SS_CAM.Services
{
    public class CommandPaletteItem
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Category { get; set; } // "Navigation", "Projects", "Brand Colors", "Copywriting", "Actions"
        public string IconGlyph { get; set; }
        public string BadgeText { get; set; }
        public string Payload { get; set; }
        public string SwatchHex { get; set; }
        public Type TargetPageType { get; set; }
        public Action ExecuteAction { get; set; }

        public bool HasSwatch
        {
            get { return !string.IsNullOrWhiteSpace(SwatchHex); }
        }

        public System.Windows.Visibility SwatchVisibility
        {
            get { return !string.IsNullOrWhiteSpace(SwatchHex) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; }
        }

        public System.Windows.Visibility IconVisibility
        {
            get { return string.IsNullOrWhiteSpace(SwatchHex) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; }
        }
    }

    /// <summary>
    /// Indexer and search provider for the global Command Palette (Ctrl + K).
    /// Provides unified multi-category search across Navigation, Projects,
    /// Brand Color Tokens, Copywriting Snippets, and Studio Actions.
    /// Compatible with C# 5.0 (.NET Framework 4.8).
    /// </summary>
    public static class CommandPaletteService
    {
        private static List<CommandPaletteItem> _staticItems;

        static CommandPaletteService()
        {
            InitializeStaticItems();
        }

        private static void InitializeStaticItems()
        {
            _staticItems = new List<CommandPaletteItem>();

            // 1. Navigation Modules (15 Modules)
            AddNav("Dashboard", "Overview, KPI analytics & quick actions", typeof(DashboardPage), "\uE80F");
            AddNav("Project Creator", "Scaffold new campaign vaults & canvas files", typeof(ProjectCreatorPage), "\uED25");
            AddNav("Order Requests", "Creative orders intake queue & assignment", typeof(OrderRequestsPage), "\uF158");
            AddNav("Project Catalog", "Browse workspace projects, assets, briefs & visual diff (Search & Copy)", typeof(SearchCopyPage), "\uE721");
            AddNav("Copywriting Studio", "Live markdown editor, WhatsApp & Meta Ad previews", typeof(CopywritingPage), "\uE70F");
            AddNav("Brand Assets", "Master brand system color matrix & sub-brands hub", typeof(BrandAssetsPage), "\uE8F1");
            AddNav("Task Manager", "ClickUp-style Kanban board, task status & handovers", typeof(TaskManagerPage), "\uE73E");
            AddNav("Project Timeline", "Visual project timeline, Gantt chart & delivery deadlines", typeof(CalendarPage), "\uE787");
            AddNav("Studio Notes", "Team scratchpad, quick ideas & Markdown notes", typeof(QuickNotePage), "\uE70B");
            AddNav("Creative Wellbeing", "Biometric radar, focus heatmap & hydration tracker", typeof(WellbeingPage), "\uEB51");
            AddNav("Waktu Solat", "Accurate prayer times & Islamic calendar", typeof(WaktuSolatPage), "\uE823");
            AddNav("Radio Player", "Curated lofi, synthwave & live audio visualizer", typeof(RadioPage), "\uE8D6");
            AddNav("QR Code Studio", "Vector QR generator with custom logo embedding", typeof(QrCodePage), "\uED14");
            AddNav("Workstation Health", "System storage, memory & network latency audits", typeof(WorkstationHealthPage), "\uE7BA");
            AddNav("Settings & Profile", "User profile, theme switcher & NAS configuration", typeof(SettingsPage), "\uE713");

            // 2. Brand Colors (Master Brand System v3.5.1)
            AddColor("SuamiSihat Prussian Blue", "#022057", "--ss-prussian-blue", "Primary Corporate Deep Navy (RAL 5004 / Pantone 533 C)");
            AddColor("SuamiSihat Brand Blue", "#043388", "--ss-blue", "Mid Navy Authority Blue (RAL 5002 / Pantone 287 C)");
            AddColor("SuamiSihat Azure", "#21A1F7", "--ss-azure", "Vibrant Brand Accent (RAL 5012 / Pantone 299 C)");
            AddColor("SuamiSihat Malibu", "#6DC6EC", "--ss-malibu", "Cyan Tint & Interactive Hover (RAL 5015 / Pantone 2915 C)");
            AddColor("SuamiSihat Lion", "#BD9A73", "--ss-lion", "Premium Warm Gold Accent (Pantone 7503 C)");
            AddColor("SuamiSihat Fawn", "#CCAC8D", "--ss-fawn", "Soft Sand Muted Secondary (Pantone 7502 C)");
            AddColor("SuamiSihat Arylide", "#E5D15C", "--ss-arylide", "Warm Caution Yellow (RAL 1018)");
            AddColor("SuamiSihat Banana", "#FCE53D", "--ss-banana", "High-Voltage Electric Alert (RAL 1016)");
            AddColor("SSH Holding Gold", "#D97706", "--ss-holding-gold", "SuamiSihat Holdings Corporate Gold");
            AddColor("SSC Clinic Cyan", "#0EA5E9", "--ss-clinic-cyan", "SuamiSihat Clinic Medical Cyan");
            AddColor("SSW Wellness Teal", "#0D9488", "--ss-wellness-teal", "SuamiSihat Wellness Botanical Teal");
            AddColor("SSE Enterprise Navy", "#1E3A8A", "--ss-enterprise-navy", "SuamiSihat Enterprise Executive Navy");
            AddColor("SST Technology Indigo", "#4F46E5", "--ss-tech-indigo", "SuamiSihat Tech Innovation Indigo");

            // 3. Copywriting Hooks & CTAs
            AddCopy("Hook: Tenaga Lelaki Sejati", "Rahsia Tenaga Lelaki Sejati Kini Terbongkar — 100% Asli Tanpa Kompromi.", "Direct Authority Hook");
            AddCopy("Hook: Social Proof & Urgency", "Lebih 15,000+ Pelanggan Berpuas Hati — Formula Terbukti Berkesan, Stok Terhad!", "Social Proof Hook");
            AddCopy("Hook: Masalah Keletihan", "Dah Cuba Macam-Macam Tapi Masih Cepat Penat? Ini Jawapan Yang Anda Cari.", "Pain Point Problem Hook");
            AddCopy("Hook: Prestasi Maksimum", "Kembalikan Prestasi, Fokus & Keyakinan Puncak Anda Seperti Zaman Muda.", "Aspirational Hook");
            AddCopy("CTA: Tempah Sekarang (Free Post)", "[ Tempah Sekarang — Penghantaran Percuma Seluruh Malaysia ]", "Direct Response CTA");
            AddCopy("CTA: WhatsApp Konsultasi", "[ WhatsApp Pakar Kesihatan Kami Untuk Konsultasi Percuma Tanpa Syarat ]", "Consultation CTA");
            AddCopy("CTA: Tawaran Eksklusif", "[ Dapatkan Tawaran Eksklusif Hari Ini Sementara Stok Masih Ada ]", "Scarcity CTA");
            AddCopy("CTA: Kombo Nilai Jimat", "[ Beli 2 Percuma 1 — Promosi Terhad Pelanggan Baharu ]", "Value Offer CTA");
        }

        private static void AddNav(string title, string subtitle, Type pageType, string icon)
        {
            _staticItems.Add(new CommandPaletteItem
            {
                Title = title,
                Subtitle = subtitle,
                Category = "Navigation",
                IconGlyph = icon,
                BadgeText = "MODULE",
                TargetPageType = pageType
            });
        }

        private static void AddColor(string name, string hex, string token, string desc)
        {
            _staticItems.Add(new CommandPaletteItem
            {
                Title = name,
                Subtitle = string.Format("{0} • {1} • {2}", hex, token, desc),
                Category = "Brand Colors",
                IconGlyph = "\uE790",
                BadgeText = hex,
                Payload = hex,
                SwatchHex = hex
            });
        }

        private static void AddCopy(string title, string text, string desc)
        {
            _staticItems.Add(new CommandPaletteItem
            {
                Title = title,
                Subtitle = string.Format("\"{0}\" — {1}", text, desc),
                Category = "Copywriting",
                IconGlyph = "\uE70F",
                BadgeText = "SNIPPET",
                Payload = text
            });
        }

        /// <summary>
        /// Searches across all indexed categories including dynamic projects discovered from NAS workspace.
        /// </summary>
        public static List<CommandPaletteItem> Search(string query, string categoryFilter, string workspaceRoot, int maxResults)
        {
            List<CommandPaletteItem> results = new List<CommandPaletteItem>();
            string q = (query ?? string.Empty).Trim();
            bool hasQuery = !string.IsNullOrEmpty(q);

            // 1. Dynamic Studio Actions (Always evaluated live)
            List<CommandPaletteItem> actions = GetStudioActions();
            foreach (var act in actions)
            {
                if (MatchFilter(act, q, categoryFilter))
                {
                    results.Add(act);
                }
            }

            // 2. Filter static items (Navigation, Brand Colors, Copywriting)
            foreach (var item in _staticItems)
            {
                if (MatchFilter(item, q, categoryFilter))
                {
                    results.Add(item);
                }
            }

            // 3. Dynamic Projects from Workspace
            if (string.Equals(categoryFilter, "All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(categoryFilter, "Projects", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(workspaceRoot) && Directory.Exists(workspaceRoot))
                {
                    try
                    {
                        int projectLimit = hasQuery ? 12 : 5;
                        List<DesignerFolderItem> projects = WorkspaceScanner.ListDesignerFolders(workspaceRoot, string.Empty, q, projectLimit);
                        if (projects != null)
                        {
                            foreach (var p in projects)
                            {
                                results.Add(new CommandPaletteItem
                                {
                                    Title = p.Project,
                                    Subtitle = string.Format("Designer: {0} • Modified: {1} • {2}", p.Designer, p.Modified, p.FormattedSize),
                                    Category = "Projects",
                                    IconGlyph = "\uED25",
                                    BadgeText = "PROJECT",
                                    Payload = p.FullPath
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[CommandPaletteService] Search projects: " + ex.Message);
                    }
                }
            }

            // Rank results: exact match > startsWith > contains
            if (hasQuery)
            {
                results.Sort(delegate(CommandPaletteItem a, CommandPaletteItem b)
                {
                    int scoreA = CalculateScore(a, q);
                    int scoreB = CalculateScore(b, q);
                    return scoreB.CompareTo(scoreA); // Descending score
                });
            }

            if (results.Count > maxResults)
            {
                return results.Take(maxResults).ToList();
            }
            return results;
        }

        private static List<CommandPaletteItem> GetStudioActions()
        {
            List<CommandPaletteItem> list = new List<CommandPaletteItem>();

            // Work session toggle
            var timerService = WorkSessionTrackerService.Instance;
            string timerActionTitle = timerService.State == WorkSessionState.Running ? "Pause Work Timer" : "Start / Resume Work Timer";
            string timerActionSub = timerService.State == WorkSessionState.Running 
                ? string.Format("Currently tracking '{0}' ({1})", timerService.ActiveProjectName, timerService.FormattedTime)
                : "Record creative work duration on active project";

            list.Add(new CommandPaletteItem
            {
                Title = timerActionTitle,
                Subtitle = timerActionSub,
                Category = "Actions",
                IconGlyph = "\uE916",
                BadgeText = "TIMER",
                Payload = "ACTION_TIMER_TOGGLE"
            });

            list.Add(new CommandPaletteItem
            {
                Title = "Toggle Application Theme",
                Subtitle = "Cycle theme: Falconia, Metamorphosis, Catppuccin, Rosé Pine, Nord",
                Category = "Actions",
                IconGlyph = "\uE790",
                BadgeText = "THEME",
                Payload = "ACTION_THEME_TOGGLE"
            });

            list.Add(new CommandPaletteItem
            {
                Title = "Play / Pause Radio Stream",
                Subtitle = "Toggle live background Lo-Fi / Synthwave radio stream",
                Category = "Actions",
                IconGlyph = "\uE8D6",
                BadgeText = "RADIO",
                Payload = "ACTION_RADIO_TOGGLE"
            });

            list.Add(new CommandPaletteItem
            {
                Title = "Rescan Synology NAS Workspace",
                Subtitle = "Scan local and network vaults for new deliverables and briefs",
                Category = "Actions",
                IconGlyph = "\uE72C",
                BadgeText = "SCAN",
                Payload = "ACTION_RESCAN_NAS"
            });

            list.Add(new CommandPaletteItem
            {
                Title = "Open Workspace Root in Explorer",
                Subtitle = "Launch Windows Explorer at Synology NAS / Local vault folder",
                Category = "Actions",
                IconGlyph = "\uE838",
                BadgeText = "EXPLORER",
                Payload = "ACTION_OPEN_WORKSPACE"
            });

            return list;
        }

        private static bool MatchFilter(CommandPaletteItem item, string query, string categoryFilter)
        {
            if (!string.IsNullOrEmpty(categoryFilter) && !string.Equals(categoryFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(item.Category, categoryFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (string.IsNullOrEmpty(query)) return true;

            return (item.Title != null && item.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (item.Subtitle != null && item.Subtitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (item.Payload != null && item.Payload.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (item.Category != null && item.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int CalculateScore(CommandPaletteItem item, string q)
        {
            int score = 0;
            if (string.Equals(item.Title, q, StringComparison.OrdinalIgnoreCase)) score += 100;
            else if (item.Title != null && item.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase)) score += 60;
            else if (item.Title != null && item.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) score += 30;

            if (item.BadgeText != null && item.BadgeText.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) score += 20;
            if (item.Payload != null && item.Payload.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) score += 15;
            if (item.Subtitle != null && item.Subtitle.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) score += 10;

            return score;
        }
    }
}
