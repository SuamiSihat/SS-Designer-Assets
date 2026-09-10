using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using SS_CAM.Utilities;

namespace SS_CAM.Services
{
    public enum WorkSessionState
    {
        Idle,
        Running,
        Paused
    }

    public class WorkSessionCheckpoint
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Client { get; set; }
        public string ProjectPath { get; set; }
        public int ElapsedSeconds { get; set; }
        public string State { get; set; }
        public DateTime LastUpdated { get; set; }
        public string SessionNotes { get; set; }
    }

    /// <summary>
    /// Manages real-time designer work session tracking for active projects.
    /// Tracks elapsed work time, active project metadata, state transitions,
    /// and provides crash-resilient local persistence.
    /// Compatible with C# 5.0 (.NET Framework 4.8).
    /// </summary>
    public class WorkSessionTrackerService
    {
        private static readonly Lazy<WorkSessionTrackerService> _instance =
            new Lazy<WorkSessionTrackerService>(new Func<WorkSessionTrackerService>(() => new WorkSessionTrackerService()));

        public static WorkSessionTrackerService Instance
        {
            get { return _instance.Value; }
        }

        private readonly Stopwatch _stopwatch;
        private readonly string _checkpointPath;

        public WorkSessionState State { get; private set; }
        public string ActiveProjectId { get; private set; }
        public string ActiveProjectName { get; private set; }
        public string ActiveClient { get; private set; }
        public string ActiveProjectPath { get; private set; }
        public int ElapsedSeconds { get; private set; }
        public DateTime SessionStart { get; private set; }
        public string SessionNotes { get; set; }

        public event EventHandler StateChanged;
        public event EventHandler SecondTicked;

        public WorkSessionTrackerService()
        {
            _stopwatch = new Stopwatch();
            State = WorkSessionState.Idle;
            ActiveProjectId = string.Empty;
            ActiveProjectName = "No Active Project";
            ActiveClient = "SS";
            ActiveProjectPath = string.Empty;
            SessionNotes = string.Empty;
            ElapsedSeconds = 0;

            try
            {
                _checkpointPath = Path.Combine(AppPaths.AppDataFolder, "work_session.json");
                LoadCheckpoint();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[WorkSessionTrackerService] Ctor checkpoint init: " + ex.Message);
            }
        }

        public string FormattedTime
        {
            get
            {
                int totalSecs = ElapsedSeconds + (_stopwatch.IsRunning ? (int)_stopwatch.Elapsed.TotalSeconds : 0);
                TimeSpan ts = TimeSpan.FromSeconds(totalSecs);
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            }
        }

        public string FormattedShortTime
        {
            get
            {
                int totalSecs = ElapsedSeconds + (_stopwatch.IsRunning ? (int)_stopwatch.Elapsed.TotalSeconds : 0);
                TimeSpan ts = TimeSpan.FromSeconds(totalSecs);
                if (ts.TotalHours >= 1)
                {
                    return string.Format("{0}h {1:D2}m", (int)ts.TotalHours, ts.Minutes);
                }
                return string.Format("{0}m {1:D2}s", ts.Minutes, ts.Seconds);
            }
        }

        public void StartOrResume(string projectId, string projectName, string client, string projectPath)
        {
            if (State == WorkSessionState.Running && string.Equals(ActiveProjectId, projectId, StringComparison.OrdinalIgnoreCase))
            {
                return; // Already actively working on this project
            }

            // If switching to another project, snapshot current elapsed
            if (!string.IsNullOrEmpty(ActiveProjectId) && !string.Equals(ActiveProjectId, projectId, StringComparison.OrdinalIgnoreCase))
            {
                SwitchProject(projectId, projectName, client, projectPath);
                return;
            }

            ActiveProjectId = projectId ?? string.Empty;
            ActiveProjectName = projectName ?? (projectId ?? "Active Project");
            ActiveClient = string.IsNullOrWhiteSpace(client) ? "SS" : client;
            ActiveProjectPath = projectPath ?? string.Empty;

            if (State == WorkSessionState.Idle)
            {
                ElapsedSeconds = 0;
                SessionStart = DateTime.Now;
            }

            State = WorkSessionState.Running;
            _stopwatch.Restart();
            SaveCheckpoint();
            RaiseStateChanged();
        }

        public void Pause()
        {
            if (State != WorkSessionState.Running) return;

            _stopwatch.Stop();
            ElapsedSeconds += (int)_stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Reset();

            State = WorkSessionState.Paused;
            SaveCheckpoint();
            RaiseStateChanged();
        }

        public void Resume()
        {
            if (State != WorkSessionState.Paused) return;

            State = WorkSessionState.Running;
            _stopwatch.Restart();
            SaveCheckpoint();
            RaiseStateChanged();
        }

        public void StopAndReset()
        {
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
                ElapsedSeconds += (int)_stopwatch.Elapsed.TotalSeconds;
            }
            _stopwatch.Reset();

            State = WorkSessionState.Idle;
            ActiveProjectId = string.Empty;
            ActiveProjectName = "No Active Project";
            ActiveClient = "SS";
            ActiveProjectPath = string.Empty;
            ElapsedSeconds = 0;
            SessionNotes = string.Empty;

            SaveCheckpoint();
            RaiseStateChanged();
        }

        public void SwitchProject(string newProjectId, string newProjectName, string newClient, string newProjectPath)
        {
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
                ElapsedSeconds += (int)_stopwatch.Elapsed.TotalSeconds;
            }
            _stopwatch.Reset();

            ActiveProjectId = newProjectId ?? string.Empty;
            ActiveProjectName = newProjectName ?? (newProjectId ?? "Active Project");
            ActiveClient = string.IsNullOrWhiteSpace(newClient) ? "SS" : newClient;
            ActiveProjectPath = newProjectPath ?? string.Empty;
            ElapsedSeconds = 0;
            SessionStart = DateTime.Now;
            SessionNotes = string.Empty;

            State = WorkSessionState.Running;
            _stopwatch.Restart();
            SaveCheckpoint();
            RaiseStateChanged();
        }

        public void Tick()
        {
            if (State == WorkSessionState.Running)
            {
                if (SecondTicked != null)
                {
                    try { SecondTicked(this, EventArgs.Empty); }
                    catch (Exception ex) { Debug.WriteLine("[WorkSessionTrackerService] SecondTicked: " + ex.Message); }
                }

                // Checkpoint every 60 seconds
                int currentLiveSecs = ElapsedSeconds + (int)_stopwatch.Elapsed.TotalSeconds;
                if (currentLiveSecs > 0 && currentLiveSecs % 60 == 0)
                {
                    SaveCheckpoint();
                }
                else if (currentLiveSecs > 0 && currentLiveSecs % 15 == 0)
                {
                    try
                    {
                        LiveTaskSyncService.Instance.BroadcastSession(ActiveProjectId, ActiveProjectName, ActiveClient, State, currentLiveSecs, SessionNotes);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[WorkSessionTrackerService] Broadcast heartbeat: " + ex.Message);
                    }
                }
            }
        }

        public void SaveCheckpoint()
        {
            try
            {
                int liveSecs = ElapsedSeconds + (_stopwatch.IsRunning ? (int)_stopwatch.Elapsed.TotalSeconds : 0);

                // Broadcast to shared team live tasks ledger
                try
                {
                    LiveTaskSyncService.Instance.BroadcastSession(ActiveProjectId, ActiveProjectName, ActiveClient, State, liveSecs, SessionNotes);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[WorkSessionTrackerService] BroadcastSession: " + ex.Message);
                }

                if (string.IsNullOrWhiteSpace(_checkpointPath)) return;

                WorkSessionCheckpoint checkpoint = new WorkSessionCheckpoint
                {
                    ProjectId = ActiveProjectId,
                    ProjectName = ActiveProjectName,
                    Client = ActiveClient,
                    ProjectPath = ActiveProjectPath,
                    ElapsedSeconds = liveSecs,
                    State = State.ToString(),
                    LastUpdated = DateTime.Now,
                    SessionNotes = SessionNotes
                };

                string dir = Path.GetDirectoryName(_checkpointPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonConvert.SerializeObject(checkpoint, Formatting.Indented);
                File.WriteAllText(_checkpointPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[WorkSessionTrackerService] SaveCheckpoint: " + ex.Message);
            }
        }

        public void LoadCheckpoint()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_checkpointPath) || !File.Exists(_checkpointPath)) return;

                string json = File.ReadAllText(_checkpointPath);
                if (string.IsNullOrWhiteSpace(json)) return;

                WorkSessionCheckpoint cp = JsonConvert.DeserializeObject<WorkSessionCheckpoint>(json);
                if (cp != null && !string.IsNullOrWhiteSpace(cp.ProjectId))
                {
                    // Check if checkpoint is from today (within last 16 hours)
                    TimeSpan age = DateTime.Now - cp.LastUpdated;
                    if (age.TotalHours < 16)
                    {
                        ActiveProjectId = cp.ProjectId;
                        ActiveProjectName = cp.ProjectName;
                        ActiveClient = cp.Client;
                        ActiveProjectPath = cp.ProjectPath;
                        ElapsedSeconds = cp.ElapsedSeconds;
                        SessionNotes = cp.SessionNotes ?? string.Empty;

                        // Restore as Paused so the designer can choose to resume
                        State = WorkSessionState.Paused;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[WorkSessionTrackerService] LoadCheckpoint: " + ex.Message);
            }
        }

        private void RaiseStateChanged()
        {
            if (StateChanged != null)
            {
                try { StateChanged(this, EventArgs.Empty); }
                catch (Exception ex) { Debug.WriteLine("[WorkSessionTrackerService] StateChanged: " + ex.Message); }
            }
        }
    }
}
