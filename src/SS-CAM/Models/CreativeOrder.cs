using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SS_CAM.Models
{
    /// <summary>
    /// Represents a creative brief / order submitted from the SS-CAM Web Portal,
    /// Android Companion App, or Desktop interface.
    /// </summary>
    public class CreativeOrder
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Entity { get; set; }
        public string Priority { get; set; }
        public string Channel { get; set; }
        public string Format { get; set; }
        public string CustomSize { get; set; }
        public string Material { get; set; }
        public string MaterialType { get; set; }
        public string Copy { get; set; }
        public string TargetDate { get; set; }
        public string AttachmentNote { get; set; }
        public string Requester { get; set; }
        public string RequesterRole { get; set; }
        public string Status { get; set; }
        public string SubmittedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string AssignedTo { get; set; }
        public string ProjectId { get; set; }
        public string InternalNote { get; set; }

        public CreativeOrder()
        {
            Id = string.Format("ORD-{0:yyMMdd}-{1}", DateTime.UtcNow, new Random().Next(1000, 9999));
            Title = string.Empty;
            Entity = "SSH";
            Priority = "tier_1";
            Channel = "digital";
            Format = "1_1_feed";
            CustomSize = string.Empty;
            Material = string.Empty;
            MaterialType = string.Empty;
            Copy = string.Empty;
            TargetDate = DateTime.Now.AddDays(3).ToString("yyyy-MM-dd");
            AttachmentNote = string.Empty;
            Requester = "Staff";
            RequesterRole = "Team Member";
            Status = "pending";
            SubmittedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            AssignedTo = null;
            ProjectId = null;
            InternalNote = string.Empty;
        }

        // ─── Computed Properties for WPF UI ──────────────────────────────────────

        public string SafeTitle
        {
            get { return string.IsNullOrWhiteSpace(Title) ? "Untitled Request" : Title.Trim(); }
        }

        public string SafeEntity
        {
            get { return string.IsNullOrWhiteSpace(Entity) ? "SSH" : Entity.Trim().ToUpperInvariant(); }
        }

        public string EntityFullName
        {
            get
            {
                switch (SafeEntity)
                {
                    case "SSC": return "SuamiSihat Healthcare / Clinic";
                    case "SSH": return "SuamiSihat Holding";
                    case "SSE": return "SuamiSihat E-Commerce";
                    case "SSW": return "SuamiSihat Wellness";
                    case "SST": return "SuamiSihat Technology";
                    case "SS":  return "SuamiSihat Brand";
                    default:    return SafeEntity;
                }
            }
        }

        public Brush EntityBrush
        {
            get
            {
                switch (SafeEntity)
                {
                    case "SSC": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#06B6D4")); // Teal
                    case "SSH": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")); // Blue
                    case "SSE": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6")); // Purple
                    case "SSW": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Emerald
                    case "SST": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316")); // Orange
                    default:    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")); // Brand Blue
                }
            }
        }

        public string PriorityBadge
        {
            get
            {
                string p = (Priority ?? "").ToLowerInvariant();
                if (p.Contains("3") || p.Contains("urgent")) return "P3";
                if (p.Contains("2") || p.Contains("fast") || p.Contains("high")) return "P2";
                if (p.Contains("0") || p.Contains("low") || p.Contains("pipeline")) return "P0";
                return "P1";
            }
        }

        public string PriorityLabel
        {
            get
            {
                string p = (Priority ?? "").ToLowerInvariant();
                if (p.Contains("3") || p.Contains("urgent")) return "P3 (Urgent)";
                if (p.Contains("2") || p.Contains("fast") || p.Contains("high")) return "P2 (Fast-Track)";
                if (p.Contains("0") || p.Contains("low") || p.Contains("pipeline")) return "P0 (Low / Pipeline)";
                return "P1 (Standard)";
            }
        }

        public Brush PriorityBrush
        {
            get
            {
                string p = (Priority ?? "").ToLowerInvariant();
                if (p.Contains("3") || p.Contains("urgent"))
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")); // Red
                if (p.Contains("2") || p.Contains("fast") || p.Contains("high"))
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Amber
                if (p.Contains("0") || p.Contains("low") || p.Contains("pipeline"))
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")); // Slate / Low
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Green
            }
        }

        public string FormatLabel
        {
            get
            {
                string label;
                switch ((Format ?? "").ToLowerInvariant())
                {
                    case "9_16_video":          label = "9:16 Video / Reels"; break;
                    case "1_1_feed":            label = "1:1 Feed Post"; break;
                    case "4_5_portrait":        label = "4:5 Portrait Feed"; break;
                    case "16_9_landscape":      label = "16:9 Landscape HD"; break;
                    case "print_digital":       label = "Digital Banner / Web"; break;
                    case "custom_digital":      label = "Custom Screen"; break;
                    case "print_packaging_box": label = "Packaging Box & Sleeve"; break;
                    case "print_label":         label = "Bottle / Jar Label"; break;
                    case "print_posm":          label = "Print / POSM Poster"; break;
                    case "print_banner_rollup": label = "Roll-Up / Bunting"; break;
                    case "print_flyer":         label = "Flyer / Leaflet"; break;
                    case "custom_print":        label = "Custom Print"; break;
                    default:
                        label = string.IsNullOrWhiteSpace(Format) ? "Standard Asset" : Format.Replace('_', ' ');
                        break;
                }

                if (!string.IsNullOrWhiteSpace(CustomSize))
                {
                    label += string.Format(" ({0})", CustomSize.Trim());
                }

                string mat = !string.IsNullOrWhiteSpace(Material) ? Material : MaterialType;
                if (!string.IsNullOrWhiteSpace(mat))
                {
                    label += string.Format(" · {0}", mat.Replace('_', ' '));
                }

                return label;
            }
        }

        public string StatusLabel
        {
            get
            {
                switch ((Status ?? "").ToLowerInvariant())
                {
                    case "pending":      return "Pending Review";
                    case "in_progress":  return "In Progress";
                    case "for_approval": return "For Approval";
                    case "done":
                    case "completed":    return "Added to Backlog";
                    case "cancelled":    return "Cancelled";
                    default:             return Status ?? "Pending";
                }
            }
        }

        public Brush StatusBrush
        {
            get
            {
                switch ((Status ?? "").ToLowerInvariant())
                {
                    case "pending":      return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Amber
                    case "in_progress":  return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")); // Blue
                    case "for_approval": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6")); // Purple
                    case "done":
                    case "completed":    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Green
                    case "cancelled":    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")); // Gray
                    default:             return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                }
            }
        }

        public bool IsConverted
        {
            get { return !string.IsNullOrWhiteSpace(ProjectId); }
        }

        public Visibility ConvertedBannerVisibility
        {
            get { return IsConverted ? Visibility.Visible : Visibility.Collapsed; }
        }

        public string FormattedTargetDate
        {
            get
            {
                DateTime dt;
                if (DateTime.TryParse(TargetDate, out dt))
                    return dt.ToString("dd MMM yyyy (ddd)");
                return TargetDate ?? "No Deadline";
            }
        }

        public string FormattedSubmittedAt
        {
            get
            {
                DateTime dt;
                if (DateTime.TryParse(SubmittedAt, out dt))
                    return dt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
                return SubmittedAt ?? "";
            }
        }

        public string CopySnippet
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Copy)) return "No copy provided.";
                string clean = Copy.Replace('\r', ' ').Replace('\n', ' ').Trim();
                if (clean.Length > 120) return clean.Substring(0, 117) + "...";
                return clean;
            }
        }

        public bool IsOverdue
        {
            get
            {
                if (Status == "done" || Status == "completed" || Status == "cancelled")
                    return false;
                DateTime dt;
                if (DateTime.TryParse(TargetDate, out dt))
                    return dt.Date < DateTime.Today;
                return false;
            }
        }

        public int AttachmentCount { get; set; }
        public List<string> AttachmentFiles { get; set; }
    }

    /// <summary>
    /// Lightweight order item used in dropdowns, ProjectCreator linking, and attachment ingestion.
    /// </summary>
    public class CreativeOrderItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string SubBrand { get; set; }
        public string RequesterName { get; set; }
        public string RequesterEmail { get; set; }
        public string DeliverableType { get; set; }
        public string Priority { get; set; }
        public string Deadline { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string ProjectId { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public int AttachmentCount { get; set; }
        public List<string> AttachmentFiles { get; set; }

        public CreativeOrderItem()
        {
            AttachmentFiles = new List<string>();
        }

        public string DisplayText
        {
            get
            {
                string attachBadge = AttachmentCount > 0 ? string.Format(" [\uD83D\uDCCE {0} files]", AttachmentCount) : "";
                string brand = !string.IsNullOrWhiteSpace(SubBrand) ? string.Format("[{0}] ", SubBrand) : "";
                return string.Format("{0}{1} | {2}{3}", brand, Id, Title ?? "Untitled", attachBadge);
            }
        }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
