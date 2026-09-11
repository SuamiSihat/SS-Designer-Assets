using System;
using System.Collections.Generic;

namespace SS_CAM.Models
{
    public class ProjectStatusItem
    {
        public string Project { get; set; }       // folder name
        public string FullPath { get; set; }
        public string Status { get; set; }        // backlog|in-progress|review|done|on-hold
        public string Designer { get; set; }
        public string Client { get; set; }
        public string Deadline { get; set; }
        public string CreatedDate { get; set; }   // YYYY-MM-DD
        public string Priority { get; set; }      // low|medium|high|urgent
        public int Revision { get; set; }
        public List<string> Tags { get; set; }
        public bool HasFrontmatter { get; set; }

        public List<ProjectSubtaskItem> Subtasks { get; set; }
        public double CategoryWeight { get; set; }

        public string Duration { get; set; }

        public System.Windows.Visibility DurationVisibility
        {
            get
            {
                return string.IsNullOrWhiteSpace(Duration) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
        }

        public string CanvaUrl { get; set; }

        public bool HasCanvaUrl
        {
            get { return !string.IsNullOrWhiteSpace(CanvaUrl); }
        }

        public System.Windows.Visibility CanvaBadgeVisibility
        {
            get
            {
                return HasCanvaUrl ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        public ProjectStatusItem()
        {
            Status = "backlog";
            Priority = "medium";
            Revision = 0;
            Tags = new List<string>();
            Subtasks = new List<ProjectSubtaskItem>();
            CategoryWeight = 1.0;
            HasFrontmatter = false;
            CreatedDate = "";
            Duration = "";
            CanvaUrl = "";
        }

        public int TotalSubtasksCount
        {
            get { return Subtasks != null ? Subtasks.Count : 0; }
        }

        public int CompletedSubtasksCount
        {
            get { return Subtasks != null ? Subtasks.FindAll(s => s.IsCompleted).Count : 0; }
        }

        public double TotalWeight
        {
            get
            {
                if (Subtasks != null && Subtasks.Count > 0)
                {
                    double sum = 0;
                    for (int i = 0; i < Subtasks.Count; i++) sum += Subtasks[i].Weight;
                    return Math.Round(sum, 1);
                }
                if (CategoryWeight > 0) return Math.Round(CategoryWeight, 1);
                if (!string.IsNullOrEmpty(Project))
                {
                    if (Project.IndexOf("V_", StringComparison.OrdinalIgnoreCase) >= 0) return 2.0;
                    if (Project.IndexOf("P_", StringComparison.OrdinalIgnoreCase) >= 0) return 2.5;
                    if (Project.IndexOf("W_", StringComparison.OrdinalIgnoreCase) >= 0) return 1.5;
                }
                return 1.0;
            }
        }

        public double ActiveWeight
        {
            get
            {
                if (Subtasks != null && Subtasks.Count > 0)
                {
                    double sum = 0;
                    for (int i = 0; i < Subtasks.Count; i++)
                    {
                        if (!Subtasks[i].IsCompleted) sum += Subtasks[i].Weight;
                    }
                    return Math.Round(sum, 1);
                }
                return IsCompletedStatus ? 0.0 : TotalWeight;
            }
        }

        public double SubtaskProgressPercent
        {
            get
            {
                if (TotalSubtasksCount == 0) return IsCompletedStatus ? 100.0 : 0.0;
                return Math.Min(100.0, Math.Round(((double)CompletedSubtasksCount / TotalSubtasksCount) * 100.0, 0));
            }
        }

        public string SubtaskProgressDisplay
        {
            get
            {
                if (TotalSubtasksCount == 0) return string.Format("{0:0.#} pts", TotalWeight);
                return string.Format("{0}/{1} Done • {2:0.#} pts", CompletedSubtasksCount, TotalSubtasksCount, TotalWeight);
            }
        }

        public System.Windows.Visibility SubtaskProgressVisibility
        {
            get
            {
                return TotalSubtasksCount > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        public bool IsHighLoad
        {
            get { return ActiveWeight >= 4.0 || TotalWeight >= 5.0; }
        }

        public DateTime ParsedCreatedDate
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CreatedDate))
                {
                    string clean = CreatedDate.Trim().Trim('"', '\'');
                    DateTime dt;
                    if (DateTime.TryParse(clean, out dt)) return dt;
                }
                return DateTime.Today;
            }
        }

        public DateTime ParsedDeadline
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Deadline))
                {
                    string clean = Deadline.Trim().Trim('"', '\'');
                    if (clean.Contains("T")) clean = clean.Substring(0, clean.IndexOf("T"));
                    DateTime dt;
                    if (DateTime.TryParse(clean, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt) ||
                        DateTime.TryParse(clean, out dt))
                    {
                        return dt.Date;
                    }
                }
                return ParsedCreatedDate.Date;
            }
        }

        public int AgeInDays
        {
            get
            {
                DateTime start = ParsedCreatedDate;
                int days = (int)(DateTime.Today - start.Date).TotalDays;
                return days >= 0 ? days : 0;
            }
        }

        public string AgeDisplay
        {
            get
            {
                int age = AgeInDays;
                if (age == 0) return "Started today";
                if (age == 1) return "1d in queue";
                return string.Format("{0}d in queue", age);
            }
        }

        public string AgeBadgeColor
        {
            get
            {
                if (IsCompletedStatus) return "#64748B";
                int age = AgeInDays;
                if (age > 60) return "#EF4444";
                if (age > 30) return "#F59E0B";
                return "#64748B";
            }
        }

        public string CreatedDateDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CreatedDate)) return "N/A";
                string clean = CreatedDate.Trim().Trim('"', '\'');
                DateTime dt;
                if (DateTime.TryParse(clean, out dt))
                    return dt.ToString("yyyy-MM-dd");
                return clean;
            }
        }

        public string StatusDisplay
        {
            get
            {
                string s = (Status ?? "").Trim().Trim('"', '\'').ToLowerInvariant();
                if (s == "in-progress" || s == "in_progress" || s == "inprogress" || s == "in progress") return "In Progress";
                if (s == "on-hold" || s == "on_hold" || s == "onhold" || s == "on hold") return "On Hold";
                if (s == "revision" || s == "revision_required" || s == "revision-required") return "Revision Required";
                if (s == "approved") return "Approved";
                if (s == "review" || s == "in-review" || s == "in_review") return "Review Queue";
                if (s == "done" || s == "completed") return "Done";
                if (s == "backlog") return "Backlog";
                if (string.IsNullOrWhiteSpace(Status)) return "Untracked";
                string clean = Status.Trim();
                return char.ToUpper(clean[0]) + clean.Substring(1);
            }
        }

        public string StatusBadgeColor
        {
            get
            {
                string s = (Status ?? "").Trim().Trim('"', '\'').ToLowerInvariant();
                if (s == "done" || s == "approved" || s == "completed") return "#10B981";
                if (s == "review" || s == "in-review" || s == "in_review") return "#F59E0B";
                if (s == "revision" || s == "revision_required" || s == "revision-required") return "#D97706";
                if (s == "in-progress" || s == "in_progress" || s == "inprogress" || s == "in progress") return "#0078D4";
                if (s == "on-hold" || s == "on_hold" || s == "onhold" || s == "on hold") return "#64748B";
                return "#8B5CF6";
            }
        }

        public string PriorityBadgeText
        {
            get
            {
                string p = (Priority ?? "").ToLowerInvariant().Trim();
                if (p == "urgent") return "P3";
                if (p == "high") return "P2";
                if (p == "medium" || p == "standard") return "P1";
                return ""; // Low has no badge
            }
        }

        public System.Windows.Visibility PriorityBadgeVisibility
        {
            get
            {
                return string.IsNullOrEmpty(PriorityBadgeText) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
        }

        public string PriorityColor
        {
            get
            {
                string p = (Priority ?? "").ToLowerInvariant().Trim();
                if (p == "urgent") return "#EF4444"; // P3 - Red
                if (p == "high") return "#F59E0B";   // P2 - Orange/Amber
                if (p == "medium" || p == "standard") return "#0078D4"; // P1 - Fluent Blue
                return "#64748B";
            }
        }

        public bool IsCompletedStatus
        {
            get
            {
                string s = (Status ?? "").Trim().Trim('"', '\'').ToLowerInvariant();
                return s == "done" || s == "approved" || s == "completed";
            }
        }

        public string DeadlineDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Deadline)) return "";
                string cleanDeadline = (Deadline ?? "").Trim().Trim('"', '\'');
                if (string.IsNullOrWhiteSpace(cleanDeadline)) return "";

                if (IsCompletedStatus)
                {
                    DateTime dtCompleted;
                    if (DateTime.TryParse(cleanDeadline, out dtCompleted))
                    {
                        return dtCompleted.ToString("yyyy-MM-dd");
                    }
                    return cleanDeadline;
                }
                DateTime dt;
                if (DateTime.TryParse(cleanDeadline, out dt))
                {
                    int days = (int)(dt - DateTime.Today).TotalDays;
                    if (days < 0) return string.Format("Overdue {0}d", Math.Abs(days));
                    if (days == 0) return "Due Today";
                    return string.Format("Due in {0}d", days);
                }
                return cleanDeadline;
            }
        }

        public bool IsOverdue
        {
            get
            {
                if (IsCompletedStatus) return false;
                if (string.IsNullOrWhiteSpace(Deadline)) return false;
                string cleanDeadline = (Deadline ?? "").Trim().Trim('"', '\'');
                if (string.IsNullOrWhiteSpace(cleanDeadline)) return false;

                DateTime dt;
                if (DateTime.TryParse(cleanDeadline, out dt))
                {
                    return (dt - DateTime.Today).TotalDays < 0;
                }
                return false;
            }
        }

        public string DeadlineBadgeBackground
        {
            get
            {
                if (IsCompletedStatus) return "Transparent";
                if (IsOverdue) return "#EF4444"; // Solid Red
                return "Transparent";
            }
        }

        public string DeadlineBadgeForeground
        {
            get
            {
                if (IsCompletedStatus) return "#10B981"; // Emerald green for finished projects
                if (IsOverdue) return "#FFFFFF"; // White text on Red
                return DeadlineColor;
            }
        }

        public string DeadlineColor
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Deadline)) return "#64748B";
                if (IsCompletedStatus) return "#10B981"; // Emerald green for finished projects
                string cleanDeadline = (Deadline ?? "").Trim().Trim('"', '\'');
                DateTime dt;
                if (DateTime.TryParse(cleanDeadline, out dt))
                {
                    int days = (int)(dt - DateTime.Today).TotalDays;
                    if (days < 0) return "#EF4444";
                    if (days <= 3) return "#F59E0B";
                    return "#10B981";
                }
                return "#64748B";
            }
        }

        public string DesignerColor
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Designer)) return "#0078D4";
                int hash = Math.Abs(Designer.GetHashCode());
                string[] palette = new[] { "#0078D4", "#106EBE", "#043388", "#21A1F7", "#059669", "#D97706", "#7C3AED" };
                return palette[hash % palette.Length];
            }
        }

        public string DesignerInitials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Designer)) return "S";
                string d = Designer.Trim();
                if (d.Length <= 2) return d.ToUpper();
                string[] parts = d.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
                return d.Substring(0, 1).ToUpper();
            }
        }
    }

    public class ProjectSubtaskItem
    {
        public string Id { get; set; }           // e.g. "V01", "KV01"
        public string Name { get; set; }         // e.g. "Master 60s Cut", "Feed 1080x1080"
        public string Type { get; set; }         // master_video | hook_variation | key_visual | resize | cutdown
        public double Weight { get; set; }       // e.g. 2.0, 0.4, 1.0, 0.2
        public string Status { get; set; }       // draft | in-progress | review | approved
        public string Specs { get; set; }        // "1080x1920, 60s"
        public string AssignedDesigner { get; set; }

        public ProjectSubtaskItem()
        {
            Id = "";
            Name = "";
            Type = "standard";
            Weight = 1.0;
            Status = "draft";
            Specs = "";
            AssignedDesigner = "";
        }

        public bool IsCompleted
        {
            get
            {
                string s = (Status ?? "").ToLowerInvariant().Trim();
                return s == "approved" || s == "done" || s == "completed";
            }
        }

        public string StatusBadgeColor
        {
            get
            {
                string s = (Status ?? "").ToLowerInvariant().Trim();
                if (s == "approved" || s == "done") return "#10B981";
                if (s == "in-progress" || s == "progress") return "#0078D4";
                if (s == "review") return "#F59E0B";
                if (s == "revision") return "#D97706";
                return "#64748B";
            }
        }

        public string StatusDisplay
        {
            get
            {
                string s = (Status ?? "").ToLowerInvariant().Trim();
                if (s == "done" || s == "approved") return "Done";
                if (s == "in-progress" || s == "progress") return "In Progress";
                if (s == "draft") return "Draft";
                if (s == "review") return "Review";
                if (s == "revision") return "Revision";
                if (string.IsNullOrWhiteSpace(s)) return "Draft";
                return char.ToUpper(s[0]) + s.Substring(1);
            }
        }

        public string WeightDisplay
        {
            get
            {
                return string.Format("{0:0.#} pt{1}", Weight, Weight == 1.0 ? "" : "s");
            }
        }
    }
}
