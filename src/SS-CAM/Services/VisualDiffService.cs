using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SS_CAM.Services
{
    public class VisualImageMetadata
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }
        public string DimensionsString
        {
            get { return string.Format("{0} × {1}", PixelWidth, PixelHeight); }
        }
        public long FileSizeBytes { get; set; }
        public string FormattedFileSize
        {
            get
            {
                if (FileSizeBytes < 1024) return FileSizeBytes + " B";
                if (FileSizeBytes < 1024 * 1024) return string.Format("{0:0.0} KB", FileSizeBytes / 1024.0);
                return string.Format("{0:0.0} MB", FileSizeBytes / (1024.0 * 1024.0));
            }
        }
        public string Format { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class RevisionPair
    {
        public string BaseName { get; set; }
        public string BeforePath { get; set; }
        public string AfterPath { get; set; }
        public string BeforeLabel { get; set; }
        public string AfterLabel { get; set; }

        public override string ToString()
        {
            return string.Format("{0} → {1}", BeforeLabel, AfterLabel);
        }
    }

    public static class VisualDiffService
    {
        private static readonly string[] ImageExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

        private static readonly Regex RevisionRegex = new Regex(
            @"(?i)(?:[_\-\s]|^)(?:v|rev|revision|r)[\-_]?(\d+)(?:[_\-\s\.]|$)",
            RegexOptions.Compiled);

        private static readonly Regex SemanticSuffixRegex = new Regex(
            @"(?i)(?:[_\-\s])(before|after|old|new|draft|final|approved|revised)(?:[_\-\s\.]|$)",
            RegexOptions.Compiled);

        public static VisualImageMetadata GetImageMetadata(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                FileInfo fi = new FileInfo(filePath);
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(
                        fs,
                        BitmapCreateOptions.DelayCreation,
                        BitmapCacheOption.None);

                    if (decoder.Frames.Count > 0)
                    {
                        BitmapFrame frame = decoder.Frames[0];
                        return new VisualImageMetadata
                        {
                            FilePath = filePath,
                            FileName = fi.Name,
                            PixelWidth = frame.PixelWidth,
                            PixelHeight = frame.PixelHeight,
                            FileSizeBytes = fi.Length,
                            Format = fi.Extension.TrimStart('.').ToUpperInvariant(),
                            LastModified = fi.LastWriteTime
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[VisualDiffService] GetImageMetadata error: " + ex.Message);
            }

            return null;
        }

        public static List<string> ScanAllProjectImages(string projectPath)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
                return results;

            try
            {
                foreach (string ext in ImageExtensions)
                {
                    foreach (string file in Directory.GetFiles(projectPath, "*" + ext, SearchOption.AllDirectories))
                    {
                        results.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[VisualDiffService] ScanAllProjectImages: " + ex.Message);
            }

            return results.OrderBy(f => Path.GetFileName(f)).ToList();
        }

        public static List<RevisionPair> DetectRevisionPairs(string projectPath, string activeFile = null)
        {
            List<RevisionPair> pairs = new List<RevisionPair>();
            List<string> allFiles = ScanAllProjectImages(projectPath);
            if (allFiles.Count < 2)
                return pairs;

            var grouped = new Dictionary<string, List<Tuple<int, string, string>>>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in allFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                Match m = RevisionRegex.Match(fileName);
                if (m.Success)
                {
                    int revNum = 0;
                    if (m.Groups.Count > 1 && int.TryParse(m.Groups[1].Value, out revNum))
                    {
                        string baseKey = RevisionRegex.Replace(fileName, "").Trim('_', '-', ' ');
                        if (!grouped.ContainsKey(baseKey))
                            grouped[baseKey] = new List<Tuple<int, string, string>>();

                        grouped[baseKey].Add(Tuple.Create(revNum, file, Path.GetFileName(file)));
                    }
                }
                else
                {
                    Match mSem = SemanticSuffixRegex.Match(fileName);
                    if (mSem.Success)
                    {
                        string tag = mSem.Groups[1].Value.ToLowerInvariant();
                        int order = (tag == "before" || tag == "old" || tag == "draft") ? 1 : 2;
                        string baseKey = SemanticSuffixRegex.Replace(fileName, "").Trim('_', '-', ' ');
                        if (!grouped.ContainsKey(baseKey))
                            grouped[baseKey] = new List<Tuple<int, string, string>>();

                        grouped[baseKey].Add(Tuple.Create(order, file, Path.GetFileName(file)));
                    }
                }
            }

            foreach (var kvp in grouped)
            {
                var list = kvp.Value.OrderBy(x => x.Item1).ToList();
                if (list.Count >= 2)
                {
                    for (int i = 0; i < list.Count - 1; i++)
                    {
                        pairs.Add(new RevisionPair
                        {
                            BaseName = kvp.Key,
                            BeforePath = list[i].Item2,
                            AfterPath = list[i + 1].Item2,
                            BeforeLabel = list[i].Item3,
                            AfterLabel = list[i + 1].Item3
                        });
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(activeFile))
            {
                var matched = pairs.FirstOrDefault(p =>
                    string.Equals(p.BeforePath, activeFile, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.AfterPath, activeFile, StringComparison.OrdinalIgnoreCase));

                if (matched != null)
                {
                    pairs.Remove(matched);
                    pairs.Insert(0, matched);
                }
            }

            return pairs;
        }

        public static BitmapSource GeneratePixelDiffBitmap(BitmapSource before, BitmapSource after)
        {
            if (before == null || after == null)
                return null;

            try
            {
                int targetWidth = before.PixelWidth;
                int targetHeight = before.PixelHeight;

                BitmapSource normBefore = before;
                if (normBefore.Format != PixelFormats.Bgra32)
                {
                    normBefore = new FormatConvertedBitmap(normBefore, PixelFormats.Bgra32, null, 0);
                }

                BitmapSource normAfter = after;
                if (normAfter.PixelWidth != targetWidth || normAfter.PixelHeight != targetHeight)
                {
                    double scaleX = (double)targetWidth / normAfter.PixelWidth;
                    double scaleY = (double)targetHeight / normAfter.PixelHeight;
                    normAfter = new TransformedBitmap(normAfter, new ScaleTransform(scaleX, scaleY));
                }

                if (normAfter.Format != PixelFormats.Bgra32)
                {
                    normAfter = new FormatConvertedBitmap(normAfter, PixelFormats.Bgra32, null, 0);
                }

                int stride = targetWidth * 4;
                int totalBytes = stride * targetHeight;

                byte[] beforePixels = new byte[totalBytes];
                byte[] afterPixels = new byte[totalBytes];
                byte[] diffPixels = new byte[totalBytes];

                normBefore.CopyPixels(beforePixels, stride, 0);
                normAfter.CopyPixels(afterPixels, stride, 0);

                for (int i = 0; i < totalBytes; i += 4)
                {
                    byte b1 = beforePixels[i];
                    byte g1 = beforePixels[i + 1];
                    byte r1 = beforePixels[i + 2];
                    byte a1 = beforePixels[i + 3];

                    byte b2 = afterPixels[i];
                    byte g2 = afterPixels[i + 1];
                    byte r2 = afterPixels[i + 2];
                    byte a2 = afterPixels[i + 3];

                    int deltaB = Math.Abs(b1 - b2);
                    int deltaG = Math.Abs(g1 - g2);
                    int deltaR = Math.Abs(r1 - r2);
                    int deltaA = Math.Abs(a1 - a2);

                    int totalDelta = deltaR + deltaG + deltaB + deltaA;

                    if (totalDelta > 16)
                    {
                        diffPixels[i] = 160;     // Blue
                        diffPixels[i + 1] = 20;   // Green
                        diffPixels[i + 2] = 255;  // Red (Bright Magenta/Fuchsia highlight)
                        diffPixels[i + 3] = 255;  // Alpha
                    }
                    else
                    {
                        byte gray = (byte)((r1 * 0.299 + g1 * 0.587 + b1 * 0.114) * 0.35);
                        diffPixels[i] = gray;
                        diffPixels[i + 1] = gray;
                        diffPixels[i + 2] = gray;
                        diffPixels[i + 3] = 255;
                    }
                }

                WriteableBitmap wb = new WriteableBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Bgra32, null);
                wb.WritePixels(new Int32Rect(0, 0, targetWidth, targetHeight), diffPixels, stride, 0);
                wb.Freeze();
                return wb;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[VisualDiffService] GeneratePixelDiffBitmap: " + ex.Message);
                return null;
            }
        }
    }
}
