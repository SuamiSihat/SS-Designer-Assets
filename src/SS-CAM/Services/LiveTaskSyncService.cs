using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;
using SS_CAM.Utilities;

namespace SS_CAM.Services
{
    public class LiveTaskEntry
    {
        public string StaffId { get; set; }
        public string DesignerName { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Client { get; set; }
        public string State { get; set; } // "Running", "Paused", "Idle"
        public DateTime StartedAt { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public int ElapsedSeconds { get; set; }
        public string SessionNotes { get; set; }
        public string MachineName { get; set; }

        public LiveTaskEntry()
        {
            StaffId = string.Empty;
            DesignerName = "Designer";
            ProjectId = string.Empty;
            ProjectName = "Active Task";
            Client = "SS";
            State = "Idle";
            StartedAt = DateTime.Now;
            LastHeartbeat = DateTime.Now;
            ElapsedSeconds = 0;
            SessionNotes = string.Empty;
            MachineName = Environment.MachineName;
        }

        [JsonIgnore]
        public bool IsRunning
        {
            get { return string.Equals(State, "Running", StringComparison.OrdinalIgnoreCase); }
        }

        [JsonIgnore]
        public bool IsPaused
        {
            get { return string.Equals(State, "Paused", StringComparison.OrdinalIgnoreCase); }
        }

        [JsonIgnore]
        public string FormattedTime
        {
            get
            {
                TimeSpan ts = TimeSpan.FromSeconds(ElapsedSeconds);
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            }
        }

        [JsonIgnore]
        public string FormattedShortDuration
        {
            get
            {
                TimeSpan ts = TimeSpan.FromSeconds(ElapsedSeconds);
                if (ts.TotalHours >= 1)
                {
                    return string.Format("{0}h {1:D2}m", (int)ts.TotalHours, ts.Minutes);
                }
                return string.Format("{0}m {1:D2}s", ts.Minutes, ts.Seconds);
            }
        }

        [JsonIgnore]
        public string StatusLabel
        {
            get
            {
                if (IsRunning) return "Working Now";
                if (IsPaused) return "Paused";
                return "Idle";
            }
        }

        [JsonIgnore]
        public Brush StatusBrush
        {
            get
            {
                if (IsRunning) return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10B981 Emerald
                if (IsPaused) return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // #F59E0B Amber
                return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94A3B8 Slate
            }
        }

        [JsonIgnore]
        public string DisplayAuthor
        {
            get
            {
                if (!string.IsNullOrEmpty(StaffId) && !string.IsNullOrEmpty(DesignerName))
                {
                    return string.Format("{0} ({1})", DesignerName, StaffId);
                }
                return !string.IsNullOrEmpty(DesignerName) ? DesignerName : "Creative";
            }
        }

        [JsonIgnore]
        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DesignerName)) return "SS";
                string[] parts = DesignerName.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
                }
                return DesignerName.Substring(0, Math.Min(2, DesignerName.Length)).ToUpperInvariant();
            }
        }
    }

    /// <summary>
    /// Synchronizes active studio tasks in real-time across the design team via
    /// &lt;WorkspaceRoot&gt;\_Team\live_tasks.json on the Synology NAS.
    /// Dispatches instant desktop toast notifications when a designer starts or resumes work.
    /// Compatible with C# 5.0 (.NET Framework 4.8).
    /// </summary>
    public class LiveTaskSyncService
    {
        private static readonly Lazy<LiveTaskSyncService> _instance =
            new Lazy<LiveTaskSyncService>(new Func<LiveTaskSyncService>(() => new LiveTaskSyncService()));

        public static LiveTaskSyncService Instance
        {
            get { return _instance.Value; }
        }

        private const string TeamFolder = "_Team";
        private const string LiveTasksFile = "live_tasks.json";

        private readonly object _fileLock = new object();
        private readonly Dictionary<string, string> _previousStates;
        private DispatcherTimer _pollTimer;
        private string _activeWorkspaceRoot;
        private List<LiveTaskEntry> _cachedEntries;
        private bool _isInitialized;

        public event EventHandler LiveTasksChanged;

        public LiveTaskSyncService()
        {
            _previousStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _cachedEntries = new List<LiveTaskEntry>();
            _isInitialized = false;
        }

        public void Initialize(string workspaceRoot)
        {
            if (_isInitialized && string.Equals(_activeWorkspaceRoot, workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _activeWorkspaceRoot = workspaceRoot;
            _isInitialized = true;

            // Seed initial read
            RefreshLiveTasks();

            if (_pollTimer == null)
            {
                _pollTimer = new DispatcherTimer();
                _pollTimer.Interval = TimeSpan.FromSeconds(4);
                _pollTimer.Tick += OnPollTick;
                _pollTimer.Start();
            }
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            RefreshLiveTasks();
        }

        private string GetLiveTasksPath()
        {
            if (!string.IsNullOrWhiteSpace(_activeWorkspaceRoot) && Directory.Exists(_activeWorkspaceRoot))
            {
                string teamDir = Path.Combine(_activeWorkspaceRoot, TeamFolder);
                try
                {
                    if (!Directory.Exists(teamDir))
                    {
                        Directory.CreateDirectory(teamDir);
                    }
                    return Path.Combine(teamDir, LiveTasksFile);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[LiveTaskSyncService] GetLiveTasksPath NAS: " + ex.Message);
                }
            }

            // Fallback to LocalAppData
            string localDir = Path.Combine(AppPaths.AppDataFolder, TeamFolder);
            if (!Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }
            return Path.Combine(localDir, LiveTasksFile);
        }

        /// <summary>
        /// Reads all active live tasks from NAS / local fallback.
        /// </summary>
        public List<LiveTaskEntry> GetLiveTasks()
        {
            lock (_fileLock)
            {
                return new List<LiveTaskEntry>(_cachedEntries);
            }
        }

        /// <summary>
        /// Polls live_tasks.json, updates in-memory cache, and dispatches notifications on newly active tasks.
        /// </summary>
        public void RefreshLiveTasks()
        {
            try
            {
                string path = GetLiveTasksPath();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return;
                }

                string json;
                lock (_fileLock)
                {
                    json = File.ReadAllText(path, Encoding.UTF8);
                }

                if (string.IsNullOrWhiteSpace(json)) return;

                List<LiveTaskEntry> list = JsonConvert.DeserializeObject<List<LiveTaskEntry>>(json);
                if (list == null) return;

                // Determine current user to avoid self-notifying
                string myStaffId = string.Empty;
                try
                {
                    var profile = UserProfileService.LoadProfile();
                    if (profile != null) myStaffId = profile.StaffId ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[LiveTaskSyncService] LoadProfile: " + ex.Message);
                }

                // Filter out stale tasks older than 16 hours
                DateTime threshold = DateTime.Now.AddHours(-16);
                List<LiveTaskEntry> activeList = new List<LiveTaskEntry>();

                foreach (var entry in list)
                {
                    if (entry.LastHeartbeat < threshold) continue;
                    activeList.Add(entry);

                    // Check for state transitions from other team members
                    string key = string.Format("{0}_{1}", entry.StaffId, entry.ProjectId);
                    string prevState = null;
                    _previousStates.TryGetValue(key, out prevState);

                    if (entry.IsRunning && !string.Equals(prevState, "Running", StringComparison.OrdinalIgnoreCase))
                    {
                        // Another designer just began or resumed working on this task!
                        if (!string.IsNullOrEmpty(entry.StaffId) && !string.Equals(entry.StaffId, myStaffId, StringComparison.OrdinalIgnoreCase))
                        {
                            string notifTitle = "Team Designer Active";
                            string notifMsg = string.Format("{0} has started working on '{1}'", entry.DesignerName, entry.ProjectName);
                            NotificationService.Show(notifTitle, notifMsg, NotificationType.Info, 5000);
                        }
                    }

                    _previousStates[key] = entry.State ?? "Idle";
                }

                lock (_fileLock)
                {
                    _cachedEntries = activeList;
                }

                if (LiveTasksChanged != null)
                {
                    try { LiveTasksChanged(this, EventArgs.Empty); }
                    catch (Exception ex) { Debug.WriteLine("[LiveTaskSyncService] LiveTasksChanged: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LiveTaskSyncService] RefreshLiveTasks error: " + ex.Message);
            }
        }

        /// <summary>
        /// Convenience overload to broadcast an explicit LiveTaskEntry (used in unit testing and external sync adapters).
        /// </summary>
        public void BroadcastSession(LiveTaskEntry entry)
        {
            if (entry == null) return;
            WorkSessionState state = WorkSessionState.Idle;
            if (entry.IsRunning) state = WorkSessionState.Running;
            else if (entry.IsPaused) state = WorkSessionState.Paused;

            try
            {
                string path = GetLiveTasksPath();
                if (string.IsNullOrWhiteSpace(path)) return;

                lock (_fileLock)
                {
                    List<LiveTaskEntry> list = new List<LiveTaskEntry>();
                    if (File.Exists(path))
                    {
                        try
                        {
                            string existingJson = File.ReadAllText(path, Encoding.UTF8);
                            if (!string.IsNullOrWhiteSpace(existingJson))
                            {
                                list = JsonConvert.DeserializeObject<List<LiveTaskEntry>>(existingJson) ?? new List<LiveTaskEntry>();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[LiveTaskSyncService] Read existing for broadcast: " + ex.Message);
                        }
                    }

                    list.RemoveAll(delegate(LiveTaskEntry item)
                    {
                        return string.Equals(item.StaffId, entry.StaffId, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(item.MachineName, entry.MachineName, StringComparison.OrdinalIgnoreCase);
                    });

                    if (state != WorkSessionState.Idle && !string.IsNullOrEmpty(entry.ProjectId))
                    {
                        list.Insert(0, entry);
                    }

                    string outJson = JsonConvert.SerializeObject(list, Formatting.Indented);
                    File.WriteAllText(path, outJson, Encoding.UTF8);
                    _cachedEntries = list;
                }

                if (LiveTasksChanged != null)
                {
                    try { LiveTasksChanged(this, EventArgs.Empty); }
                    catch (Exception ex) { Debug.WriteLine("[LiveTaskSyncService] Local LiveTasksChanged: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LiveTaskSyncService] BroadcastSession(entry) error: " + ex.Message);
            }
        }

        /// <summary>
        /// Broadcasts current workstation work session state to shared NAS ledger.
        /// </summary>
        public void BroadcastSession(string projectId, string projectName, string client, WorkSessionState state, int elapsedSeconds, string notes)
        {
            try
            {
                string staffId = "0001D";
                string designerName = "Designer";

                try
                {
                    var profile = UserProfileService.LoadProfile();
                    if (profile != null)
                    {
                        if (!string.IsNullOrWhiteSpace(profile.StaffId)) staffId = profile.StaffId.Trim();
                        if (!string.IsNullOrWhiteSpace(profile.DesignerName)) designerName = profile.DesignerName.Trim();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[LiveTaskSyncService] LoadProfile for broadcast: " + ex.Message);
                }

                string path = GetLiveTasksPath();
                if (string.IsNullOrWhiteSpace(path)) return;

                lock (_fileLock)
                {
                    List<LiveTaskEntry> list = new List<LiveTaskEntry>();
                    if (File.Exists(path))
                    {
                        try
                        {
                            string existingJson = File.ReadAllText(path, Encoding.UTF8);
                            if (!string.IsNullOrWhiteSpace(existingJson))
                            {
                                list = JsonConvert.DeserializeObject<List<LiveTaskEntry>>(existingJson) ?? new List<LiveTaskEntry>();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[LiveTaskSyncService] Read existing for broadcast: " + ex.Message);
                        }
                    }

                    // Remove existing entry for this staffId or machine
                    list.RemoveAll(delegate(LiveTaskEntry item)
                    {
                        return string.Equals(item.StaffId, staffId, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(item.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
                    });

                    // If not idle, record the active session
                    if (state != WorkSessionState.Idle && !string.IsNullOrEmpty(projectId))
                    {
                        LiveTaskEntry newEntry = new LiveTaskEntry
                        {
                            StaffId = staffId,
                            DesignerName = designerName,
                            ProjectId = projectId,
                            ProjectName = projectName ?? projectId,
                            Client = string.IsNullOrWhiteSpace(client) ? "SS" : client,
                            State = state.ToString(),
                            StartedAt = DateTime.Now.AddSeconds(-elapsedSeconds),
                            LastHeartbeat = DateTime.Now,
                            ElapsedSeconds = elapsedSeconds,
                            SessionNotes = notes ?? string.Empty,
                            MachineName = Environment.MachineName
                        };
                        list.Insert(0, newEntry);
                    }

                    string outJson = JsonConvert.SerializeObject(list, Formatting.Indented);
                    File.WriteAllText(path, outJson, Encoding.UTF8);
                    _cachedEntries = list;
                }

                // Notify local subscribers
                if (LiveTasksChanged != null)
                {
                    try { LiveTasksChanged(this, EventArgs.Empty); }
                    catch (Exception ex) { Debug.WriteLine("[LiveTaskSyncService] Local LiveTasksChanged: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LiveTaskSyncService] BroadcastSession error: " + ex.Message);
            }
        }
    }
}
