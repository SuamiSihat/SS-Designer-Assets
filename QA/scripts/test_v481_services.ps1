# Test script for v4.8.1 services
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exePath = Join-Path $repoRoot "dist\SS-CAM.exe"
[Reflection.Assembly]::LoadFrom($exePath) | Out-Null

Write-Host "--- TEST: WorkSessionTrackerService ---"
$tracker = [SS_CAM.Services.WorkSessionTrackerService]::Instance
Write-Host "Tracker initial state: $($tracker.State)"
Write-Host "Elapsed formatted: $($tracker.FormattedTime)"

$tracker.StartOrResume("202609_0085D_SS_Rejal", "0085D_SS_Rejal", "SS", "E:\Test")
Write-Host "Started session for: $($tracker.ActiveProjectName) | State: $($tracker.State)"
if ($tracker.State -eq [SS_CAM.Services.WorkSessionState]::Running) {
    Write-Host "PASS: Session is Running"
} else {
    Write-Host "FAIL: Session not running"
}

$tracker.Pause()
if ($tracker.State -eq [SS_CAM.Services.WorkSessionState]::Paused) {
    Write-Host "PASS: Session Paused"
} else {
    Write-Host "FAIL: Session not paused"
}

$tracker.Resume()
if ($tracker.State -eq [SS_CAM.Services.WorkSessionState]::Running) {
    Write-Host "PASS: Session Resumed"
} else {
    Write-Host "FAIL: Session not resumed"
}

$tracker.StopAndReset()
if ($tracker.State -eq [SS_CAM.Services.WorkSessionState]::Idle) {
    Write-Host "PASS: Session Reset to Idle"
} else {
    Write-Host "FAIL: Session not idle"
}

Write-Host "`n--- TEST: CommandPaletteService ---"
# All category search
$resultsAll = [SS_CAM.Services.CommandPaletteService]::Search("", "All", "", 50)
Write-Host "Search All count: $($resultsAll.Count)"
if ($resultsAll.Count -gt 25) {
    Write-Host "PASS: All category returns full indexed palette items ($($resultsAll.Count))"
} else {
    Write-Host "FAIL: All category returned $($resultsAll.Count)"
}

$resultsModules = [SS_CAM.Services.CommandPaletteService]::Search("Dashboard", "Navigation", "", 20)
Write-Host "Search Navigation ('Dashboard') count: $($resultsModules.Count)"
if ($resultsModules.Count -gt 0) {
    Write-Host "PASS: Found module: $($resultsModules[0].Title) -> TargetPage: $($resultsModules[0].TargetPageType.Name)"
} else {
    Write-Host "FAIL: Dashboard module not found"
}

$resultsColors = [SS_CAM.Services.CommandPaletteService]::Search("Blue", "Brand Colors", "", 20)
Write-Host "Search Brand Colors ('Blue') count: $($resultsColors.Count)"
if ($resultsColors.Count -gt 0) {
    Write-Host "PASS: Found color: $($resultsColors[0].Title) ($($resultsColors[0].Subtitle)) - HasSwatch: $($resultsColors[0].HasSwatch)"
} else {
    Write-Host "FAIL: Color Blue not found"
}

$resultsActions = [SS_CAM.Services.CommandPaletteService]::Search("Theme", "Actions", "", 20)
Write-Host "Search Actions ('Theme') count: $($resultsActions.Count)"
if ($resultsActions.Count -gt 0) {
    Write-Host "PASS: Found action: $($resultsActions[0].Title)"
} else {
    Write-Host "FAIL: Theme action not found"
}

$resultsCopy = [SS_CAM.Services.CommandPaletteService]::Search("Hook", "Copywriting", "", 20)
Write-Host "Search Copywriting ('Hook') count: $($resultsCopy.Count)"
if ($resultsCopy.Count -gt 0) {
    Write-Host "PASS: Found copywriting item: $($resultsCopy[0].Title)"
} else {
    Write-Host "FAIL: Hook not found"
}

Write-Host "`n--- TEST: LiveTaskSyncService ---"
$testWs = Join-Path $repoRoot "QA\TestWorkspace\LiveSyncTest"
if (-not (Test-Path $testWs)) { New-Item -ItemType Directory -Path $testWs -Force | Out-Null }

$liveSync = [SS_CAM.Services.LiveTaskSyncService]::Instance
$liveSync.Initialize($testWs)

$entry = New-Object SS_CAM.Services.LiveTaskEntry
$entry.StaffId = "SS-007"
$entry.DesignerName = "Ahmad Designer"
$entry.ProjectId = "202609_0099W_SS_LiveTest"
$entry.ProjectName = "SS Live Stream Task"
$entry.Client = "SS"
$entry.State = "Running"
$entry.ElapsedSeconds = 125
$entry.SessionNotes = "Editing hero canvas"

$liveSync.BroadcastSession($entry)
Write-Host "Broadcasted live session for: $($entry.DesignerName)"

# Verify JSON persisted on disk in _Team folder
$teamJson = Join-Path $testWs "_Team\live_tasks.json"
if (Test-Path $teamJson) {
    $rawJson = Get-Content $teamJson -Raw
    Write-Host "PASS: live_tasks.json exists on disk ($( $rawJson.Length ) bytes)"
} else {
    Write-Host "FAIL: live_tasks.json was not created"
}

# Read back live tasks
$liveTasks = $liveSync.GetLiveTasks()
Write-Host "Live tasks count: $($liveTasks.Count)"
if ($liveTasks.Count -gt 0) {
    $found = $liveTasks | Where-Object { $_.StaffId -eq "SS-007" }
    if ($found -ne $null -and $found.State -eq "Running" -and $found.Initials -eq "AD") {
        Write-Host "PASS: Live task read correctly (Author=$($found.DisplayAuthor), Initials=$($found.Initials), FormattedTime=$($found.FormattedTime), StatusLabel=$($found.StatusLabel))"
    } else {
        Write-Host "FAIL: Live task fields mismatch"
    }
} else {
    Write-Host "FAIL: No live tasks returned"
}

