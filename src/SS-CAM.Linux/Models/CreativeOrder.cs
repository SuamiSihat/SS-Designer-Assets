using System;

namespace SS_CAM.Linux.Models
{
    public class CreativeOrder
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Entity { get; set; } = "SSH";
        public string Priority { get; set; } = "tier_1";
        public string Channel { get; set; } = "digital";
        public string Format { get; set; } = "1_1_feed";
        public string CustomSize { get; set; } = "";
        public string Material { get; set; } = "";
        public string MaterialType { get; set; } = "";
        public string Copy { get; set; } = "";
        public string TargetDate { get; set; } = "";
        public string AttachmentNote { get; set; } = "";
        public string Requester { get; set; } = "Staff";
        public string RequesterRole { get; set; } = "";
        public string Status { get; set; } = "pending";
        public string SubmittedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string? AssignedTo { get; set; } = null;
        public string? ProjectId { get; set; } = null;
        public string InternalNote { get; set; } = "";

        public string SafeTitle => string.IsNullOrWhiteSpace(Title) ? "Untitled Request" : Title.Trim();
        public string SafeEntity => string.IsNullOrWhiteSpace(Entity) ? "SSH" : Entity.Trim().ToUpperInvariant();

        public string EntityFullName => SafeEntity switch
        {
            "SSC" => "SuamiSihat Healthcare / Clinic",
            "SSH" => "SuamiSihat Holding",
            "SSE" => "SuamiSihat E-Commerce",
            "SSW" => "SuamiSihat Wellness",
            "SST" => "SuamiSihat Technology",
            _     => "SuamiSihat Brand"
        };

        public string EntityColor => SafeEntity switch
        {
            "SSC" => "#06B6D4",
            "SSH" => "#3B82F6",
            "SSE" => "#8B5CF6",
            "SSW" => "#10B981",
            "SST" => "#F97316",
            _     => "#2563EB"
        };

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

        public string PriorityLabel => PriorityBadge switch
        {
            "P3" => "P3 (Urgent)",
            "P2" => "P2 (Fast-Track)",
            "P0" => "P0 (Low / Pipeline)",
            _    => "P1 (Standard)"
        };

        public string PriorityColor => PriorityBadge switch
        {
            "P3" => "#EF4444",
            "P2" => "#F59E0B",
            "P0" => "#64748B",
            _    => "#10B981"
        };

        public string FormatLabel
        {
            get
            {
                string label = (Format ?? "").ToLowerInvariant() switch
                {
                    "9_16_video"          => "9:16 Video / Reels",
                    "1_1_feed"            => "1:1 Feed Post",
                    "4_5_portrait"        => "4:5 Portrait Feed",
                    "16_9_landscape"      => "16:9 Landscape HD",
                    "print_digital"       => "Digital Banner / Web",
                    "custom_digital"      => "Custom Screen",
                    "print_packaging_box" => "Packaging Box & Sleeve",
                    "print_label"         => "Bottle / Jar Label",
                    "print_posm"          => "Print / POSM Poster",
                    "print_banner_rollup" => "Roll-Up / Bunting",
                    "print_flyer"         => "Flyer / Leaflet",
                    "custom_print"        => "Custom Print",
                    _ => string.IsNullOrWhiteSpace(Format) ? "Standard Asset" : Format.Replace('_', ' ')
                };

                if (!string.IsNullOrWhiteSpace(CustomSize))
                    label += $" ({CustomSize.Trim()})";

                string mat = !string.IsNullOrWhiteSpace(Material) ? Material : MaterialType;
                if (!string.IsNullOrWhiteSpace(mat))
                    label += $" · {mat.Replace('_', ' ')}";

                return label;
            }
        }

        public string StatusLabel => (Status ?? "").ToLowerInvariant() switch
        {
            "pending"      => "Pending Review",
            "in_progress"  => "In Progress",
            "for_approval" => "For Approval",
            "done" or "completed" => "Added to Backlog",
            "cancelled"    => "Cancelled",
            _ => Status ?? "Pending"
        };

        public string StatusColor => (Status ?? "").ToLowerInvariant() switch
        {
            "pending"      => "#F59E0B",
            "in_progress"  => "#3B82F6",
            "for_approval" => "#8B5CF6",
            "done" or "completed" => "#10B981",
            "cancelled"    => "#64748B",
            _ => "#94A3B8"
        };

        public bool IsConverted => !string.IsNullOrWhiteSpace(ProjectId);

        public string FormattedTargetDate
        {
            get
            {
                if (DateTime.TryParse(TargetDate, out var dt))
                    return dt.ToString("dd MMM yyyy (ddd)");
                return TargetDate ?? "No Deadline";
            }
        }

        public string CopySnippet
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Copy)) return "No copy provided.";
                string clean = Copy.Replace('\r', ' ').Replace('\n', ' ').Trim();
                return clean.Length > 120 ? clean.Substring(0, 117) + "..." : clean;
            }
        }
    }
}
