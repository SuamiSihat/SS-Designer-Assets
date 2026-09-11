using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using SS_CAM.Models;

namespace SS_CAM.Services
{
    /// <summary>
    /// Parses and writes YAML-style frontmatter blocks from/to project README.md files.
    /// Frontmatter is delimited by lines containing only "---".
    /// </summary>
    public static class FrontmatterService
    {
        private const string Delimiter = "---";

        /// <summary>
        /// Reads the README.md in the given project folder and returns a populated ProjectStatusItem.
        /// Returns an item with HasFrontmatter=false if no frontmatter is found.
        /// </summary>
        public static ProjectStatusItem ReadStatus(string projectFolderPath)
        {
            ProjectStatusItem item = new ProjectStatusItem
            {
                Project = Path.GetFileName(projectFolderPath),
                FullPath = projectFolderPath
            };

            string readmePath = Path.Combine(projectFolderPath, "README.md");
            if (!File.Exists(readmePath)) return item;

            try
            {
                string[] lines = File.ReadAllLines(readmePath, Encoding.UTF8);
                Dictionary<string, string> fm = ParseFrontmatter(lines);
                if (fm == null) return item;

                item.HasFrontmatter = true;
                item.Status = GetValue(fm, "status", "backlog");
                item.Designer = GetValue(fm, "designer", "");
                item.Client = GetValue(fm, "client", "");
                item.Deadline = GetValue(fm, "deadline", "");
                item.CreatedDate = GetValue(fm, "created", "");
                item.Priority = GetValue(fm, "priority", "medium");
                item.Duration = GetValue(fm, "duration", "");
                item.Revision = ParseInt(GetValue(fm, "revision", "0"));

                if (string.IsNullOrWhiteSpace(item.CreatedDate))
                {
                    item.CreatedDate = InferCreatedDate(projectFolderPath, item.Project);
                }

                string tagsRaw = GetValue(fm, "tags", "");
                item.Tags = ParseTags(tagsRaw);
                string catWeightStr = GetValue(fm, "category_weight", GetValue(fm, "weight", ""));
                double cw;
                if (double.TryParse(catWeightStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cw) && cw > 0)
                {
                    item.CategoryWeight = cw;
                }
                item.Subtasks = ParseSubtasks(lines);
                item.CanvaUrl = GetValue(fm, "canva_url", "");
                if (string.IsNullOrWhiteSpace(item.CanvaUrl))
                {
                    string sourceFolder = Path.Combine(projectFolderPath, "02_SOURCE");
                    string shortcutPath = Path.Combine(sourceFolder, "Open_In_Canva.url");
                    if (File.Exists(shortcutPath))
                    {
                        try
                        {
                            foreach (string line in File.ReadAllLines(shortcutPath))
                            {
                                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                                {
                                    item.CanvaUrl = line.Substring(4).Trim();
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[FrontmatterService] ReadStatus Open_In_Canva.url error: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[FrontmatterService] ReadStatus failed for '{0}': {1}", projectFolderPath, ex.Message));
            }

            if (string.IsNullOrWhiteSpace(item.CreatedDate))
            {
                item.CreatedDate = InferCreatedDate(projectFolderPath, item.Project);
            }

            return item;
        }

        /// <summary>
        /// Reads and returns the body content (notes below frontmatter) of the project's README.md.
        /// </summary>
        public static string ReadBody(string projectFolderPath)
        {
            if (string.IsNullOrWhiteSpace(projectFolderPath)) return "";
            string readmePath = Path.Combine(projectFolderPath, "README.md");
            if (!File.Exists(readmePath)) return "";

            try
            {
                string[] lines = File.ReadAllLines(readmePath, Encoding.UTF8);
                return ExtractBody(lines);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[FrontmatterService] ReadBody failed for '{0}': {1}", projectFolderPath, ex.Message));
                return "";
            }
        }

        /// <summary>
        /// Writes updated frontmatter fields back to the project's README.md.
        /// The body content below the frontmatter is preserved verbatim.
        /// </summary>
        public static void WriteStatus(ProjectStatusItem item)
        {
            WriteStatusAndBody(item, null);
        }

        /// <summary>
        /// Writes updated frontmatter fields and body content back to the project's README.md.
        /// </summary>
        public static void WriteStatusAndBody(ProjectStatusItem item, string newBody)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FullPath)) return;
            string readmePath = Path.Combine(item.FullPath, "README.md");

            string body = newBody;
            if (body == null && File.Exists(readmePath))
            {
                string[] lines = File.ReadAllLines(readmePath, Encoding.UTF8);
                body = ExtractBody(lines);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Delimiter);
            sb.AppendLine(string.Format("status: {0}", item.Status ?? "backlog"));
            sb.AppendLine(string.Format("designer: {0}", item.Designer ?? ""));
            sb.AppendLine(string.Format("client: {0}", item.Client ?? ""));
            sb.AppendLine(string.Format("deadline: {0}", item.Deadline ?? ""));
            sb.AppendLine(string.Format("created: {0}", item.CreatedDate ?? ""));
            sb.AppendLine(string.Format("priority: {0}", item.Priority ?? "medium"));
            if (!string.IsNullOrWhiteSpace(item.Duration))
                sb.AppendLine(string.Format("duration: {0}", item.Duration));
            if (!string.IsNullOrWhiteSpace(item.CanvaUrl))
                sb.AppendLine(string.Format("canva_url: {0}", item.CanvaUrl));
            if (item.CategoryWeight > 0)
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "category_weight: {0:0.#}", item.CategoryWeight));
            if (item.Tags != null && item.Tags.Count > 0)
                sb.AppendLine(string.Format("tags: [{0}]", string.Join(", ", item.Tags.ToArray())));
            else
                sb.AppendLine("tags: []");
            sb.AppendLine(string.Format("revision: {0}", item.Revision));

            if (item.Subtasks != null && item.Subtasks.Count > 0)
            {
                sb.AppendLine("subtasks:");
                foreach (var st in item.Subtasks)
                {
                    sb.AppendLine(string.Format("  - id: \"{0}\"", (st.Id ?? "").Replace("\"", "\\\"")));
                    sb.AppendLine(string.Format("    name: \"{0}\"", (st.Name ?? "").Replace("\"", "\\\"")));
                    sb.AppendLine(string.Format("    type: {0}", string.IsNullOrWhiteSpace(st.Type) ? "standard" : st.Type));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "    weight: {0:0.#}", st.Weight > 0 ? st.Weight : 1.0));
                    sb.AppendLine(string.Format("    status: {0}", string.IsNullOrWhiteSpace(st.Status) ? "in-progress" : st.Status));
                    if (!string.IsNullOrWhiteSpace(st.Specs))
                        sb.AppendLine(string.Format("    specs: \"{0}\"", st.Specs.Replace("\"", "\\\"")));
                    if (!string.IsNullOrWhiteSpace(st.AssignedDesigner))
                        sb.AppendLine(string.Format("    designer: {0}", st.AssignedDesigner));
                }
            }

            sb.AppendLine(Delimiter);

            if (!string.IsNullOrWhiteSpace(body))
            {
                sb.AppendLine();
                sb.Append(body.TrimStart('\r', '\n'));
            }

            try { File.WriteAllText(readmePath, sb.ToString(), Encoding.UTF8); }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[FrontmatterService] WriteStatusAndBody failed for '{0}': {1}", item.FullPath, ex.Message));
            }
        }

        /// <summary>
        /// Generates the default frontmatter block for a new project.
        /// </summary>
        public static string BuildDefaultFrontmatter(string designerStaffId, string client, string deadline = null, string categoryPreset = null, string orderId = null, string canvaUrl = null, List<ProjectSubtaskItem> subtasks = null, double categoryWeight = 1.0)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Delimiter);
            sb.AppendLine("status: backlog");
            sb.AppendLine(string.Format("designer: {0}", designerStaffId ?? ""));
            sb.AppendLine(string.Format("client: {0}", client ?? ""));
            sb.AppendLine(string.Format("deadline: {0}", deadline ?? ""));
            sb.AppendLine(string.Format("created: {0}", DateTime.Today.ToString("yyyy-MM-dd")));
            sb.AppendLine("priority: medium");
            sb.AppendLine("duration: ");
            if (categoryWeight > 0)
            {
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "category_weight: {0:0.#}", categoryWeight));
            }
            if (!string.IsNullOrWhiteSpace(categoryPreset))
            {
                sb.AppendLine(string.Format("category_preset: \"{0}\"", categoryPreset.Replace("\"", "\\\"")));
            }
            if (!string.IsNullOrWhiteSpace(orderId))
            {
                sb.AppendLine(string.Format("order_id: {0}", orderId.Trim()));
            }
            if (!string.IsNullOrWhiteSpace(canvaUrl))
            {
                sb.AppendLine(string.Format("canva_url: {0}", canvaUrl.Trim()));
            }
            sb.AppendLine("tags: []");
            sb.AppendLine("revision: 0");

            if (subtasks != null && subtasks.Count > 0)
            {
                sb.AppendLine("subtasks:");
                foreach (var st in subtasks)
                {
                    sb.AppendLine(string.Format("  - id: \"{0}\"", (st.Id ?? "").Replace("\"", "\\\"")));
                    sb.AppendLine(string.Format("    name: \"{0}\"", (st.Name ?? "").Replace("\"", "\\\"")));
                    sb.AppendLine(string.Format("    type: {0}", string.IsNullOrWhiteSpace(st.Type) ? "standard" : st.Type));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "    weight: {0:0.#}", st.Weight > 0 ? st.Weight : 1.0));
                    sb.AppendLine(string.Format("    status: {0}", string.IsNullOrWhiteSpace(st.Status) ? "in-progress" : st.Status));
                    if (!string.IsNullOrWhiteSpace(st.Specs))
                        sb.AppendLine(string.Format("    specs: \"{0}\"", st.Specs.Replace("\"", "\\\"")));
                    if (!string.IsNullOrWhiteSpace(st.AssignedDesigner))
                        sb.AppendLine(string.Format("    designer: {0}", st.AssignedDesigner));
                }
            }

            sb.AppendLine(Delimiter);
            return sb.ToString();
        }

        private static string InferCreatedDate(string projectFolderPath, string folderName)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(folderName) && folderName.Length >= 6)
                {
                    string yearStr = folderName.Substring(0, 4);
                    string monthStr = folderName.Substring(4, 2);
                    int y, m;
                    if (int.TryParse(yearStr, out y) && int.TryParse(monthStr, out m) && y >= 2000 && y <= 2100 && m >= 1 && m <= 12)
                    {
                        return new DateTime(y, m, 1).ToString("yyyy-MM-dd");
                    }
                }
                if (!string.IsNullOrWhiteSpace(projectFolderPath) && Directory.Exists(projectFolderPath))
                {
                    return Directory.GetCreationTime(projectFolderPath).ToString("yyyy-MM-dd");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[FrontmatterService] InferCreatedDate error: " + ex.Message);
            }
            return DateTime.Today.ToString("yyyy-MM-dd");
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private static Dictionary<string, string> ParseFrontmatter(string[] lines)
        {
            if (lines == null || lines.Length == 0) return null;
            if (lines[0].Trim() != Delimiter) return null;

            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim() == Delimiter) return result;

                string rawLine = lines[i];
                if (string.IsNullOrWhiteSpace(rawLine)) continue;

                // In YAML frontmatter, top-level keys must start at column 0 (no leading whitespace and not list items)
                if (char.IsWhiteSpace(rawLine[0]) || rawLine.TrimStart().StartsWith("-"))
                {
                    continue;
                }

                int colon = rawLine.IndexOf(':');
                if (colon > 0)
                {
                    string key = rawLine.Substring(0, colon).Trim();
                    string val = rawLine.Substring(colon + 1).Trim();
                    if (val.Length >= 2 && ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'"))))
                    {
                        val = val.Substring(1, val.Length - 2).Trim();
                    }
                    result[key] = val;
                }
            }
            return result; // no closing delimiter found — return whatever was parsed
        }

        private static string ExtractBody(string[] lines)
        {
            if (lines == null || lines.Length == 0) return "";
            if (lines[0].Trim() != Delimiter)
                return string.Join(Environment.NewLine, lines);

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim() == Delimiter)
                {
                    List<string> body = new List<string>();
                    for (int j = i + 1; j < lines.Length; j++) body.Add(lines[j]);
                    return string.Join(Environment.NewLine, body.ToArray()).TrimStart('\r', '\n');
                }
            }
            return "";
        }

        private static string GetValue(Dictionary<string, string> fm, string key, string def)
        {
            string val;
            return fm.TryGetValue(key, out val) && !string.IsNullOrWhiteSpace(val) ? val : def;
        }

        private static int ParseInt(string s)
        {
            int v;
            return int.TryParse(s, out v) ? v : 0;
        }

        private static List<string> ParseTags(string raw)
        {
            List<string> tags = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return tags;
            raw = raw.Trim('[', ']');
            foreach (string t in raw.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string tag = t.Trim();
                if (!string.IsNullOrWhiteSpace(tag)) tags.Add(tag);
            }
            return tags;
        }

        public static bool UpdateSubtaskStatus(string projectFolderPath, string subtaskId, string newStatus)
        {
            if (string.IsNullOrWhiteSpace(projectFolderPath) || string.IsNullOrWhiteSpace(subtaskId)) return false;
            try
            {
                ProjectStatusItem item = ReadStatus(projectFolderPath);
                if (item == null || item.Subtasks == null || item.Subtasks.Count == 0) return false;

                var matched = item.Subtasks.Find(s => string.Equals(s.Id, subtaskId, StringComparison.OrdinalIgnoreCase));
                if (matched == null) return false;

                matched.Status = newStatus.ToLowerInvariant().Trim();

                // If all subtasks are approved, optionally promote project status
                bool allDone = true;
                foreach (var st in item.Subtasks)
                {
                    if (!st.IsCompleted) { allDone = false; break; }
                }

                if (allDone && (item.Status == "in-progress" || item.Status == "review"))
                {
                    item.Status = "approved";
                }

                WriteStatus(item);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[FrontmatterService] UpdateSubtaskStatus error: " + ex.Message);
                return false;
            }
        }

        private static List<ProjectSubtaskItem> ParseSubtasks(string[] lines)
        {
            List<ProjectSubtaskItem> list = new List<ProjectSubtaskItem>();
            if (lines == null || lines.Length == 0) return list;

            int startIndex = -1;
            int endIndex = lines.Length;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == Delimiter)
                {
                    if (startIndex == -1) startIndex = i;
                    else { endIndex = i; break; }
                }
            }

            if (startIndex == -1) return list;

            bool inSubtasks = false;
            ProjectSubtaskItem current = null;

            for (int i = startIndex + 1; i < endIndex; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (trimmed.StartsWith("subtasks:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("deliverables:", StringComparison.OrdinalIgnoreCase))
                {
                    inSubtasks = true;
                    continue;
                }

                if (inSubtasks)
                {
                    if (!string.IsNullOrWhiteSpace(line) && !char.IsWhiteSpace(line[0]))
                    {
                        if (current != null) { list.Add(current); current = null; }
                        inSubtasks = false;
                        continue;
                    }

                    if (trimmed.StartsWith("-", StringComparison.Ordinal))
                    {
                        if (current != null) list.Add(current);
                        current = new ProjectSubtaskItem();

                        string afterDash = trimmed.Substring(1).Trim();
                        if (!string.IsNullOrWhiteSpace(afterDash))
                        {
                            ParseSubtaskProperty(current, afterDash);
                        }
                    }
                    else if (current != null && !string.IsNullOrWhiteSpace(trimmed))
                    {
                        ParseSubtaskProperty(current, trimmed);
                    }
                }
            }

            if (current != null) list.Add(current);
            return list;
        }

        private static void ParseSubtaskProperty(ProjectSubtaskItem item, string propLine)
        {
            int colon = propLine.IndexOf(':');
            if (colon <= 0) return;

            string key = propLine.Substring(0, colon).Trim().ToLowerInvariant();
            string val = propLine.Substring(colon + 1).Trim();
            if (val.Length >= 2 && ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'"))))
            {
                val = val.Substring(1, val.Length - 2).Trim();
            }

            if (key == "id") item.Id = val;
            else if (key == "name" || key == "title") item.Name = val;
            else if (key == "type" || key == "category") item.Type = val;
            else if (key == "weight" || key == "points")
            {
                double w;
                if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out w))
                {
                    item.Weight = w;
                }
            }
            else if (key == "status") item.Status = val.ToLowerInvariant();
            else if (key == "specs" || key == "dimensions" || key == "format") item.Specs = val;
            else if (key == "designer" || key == "assigned") item.AssignedDesigner = val;
        }
    }
}
