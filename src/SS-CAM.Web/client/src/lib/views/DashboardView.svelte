<script lang="ts">
  import { onMount } from 'svelte';
  import { projectStore } from '$lib/stores/projectStore.svelte';
  import { appState } from '$lib/stores/appState.svelte';
  import { ApiClient } from '$lib/services/api';
  import FluentCard from '$lib/components/ui/FluentCard.svelte';
  import FluentButton from '$lib/components/ui/FluentButton.svelte';
  import FluentIcons from '$lib/components/ui/FluentIcons.svelte';
  import DashboardRadar from '$lib/components/features/DashboardRadar.svelte';
  import type { Project, ActivityNotification } from '$lib/types';

  type DashboardLens = 'studio' | 'my-workspace';
  let activeLens = $state<DashboardLens>('studio');
  let showDeepAnalytics = $state<boolean>(false);
  let myNotifications = $state<ActivityNotification[]>([]);
  let isLoadingPersonal = $state<boolean>(false);
  let currentSecondTicker = $state<number>(0);
  let tickerInterval: any = null;

  onMount(() => {
    projectStore.loadDashboard();
    projectStore.loadProjects();
    loadPersonalActivity();
    appState.loadLiveTasks();

    tickerInterval = setInterval(() => {
      currentSecondTicker += 1;
    }, 5000);

    return () => {
      if (tickerInterval) clearInterval(tickerInterval);
    };
  });

  function getLiveElapsedDisplay(task: any, _tick: number): string {
    let totalSec = 0;
    if (task.StartedAt) {
      const start = new Date(task.StartedAt).getTime();
      if (!isNaN(start)) {
        totalSec = Math.max(0, Math.floor((Date.now() - start) / 1000));
      }
    }
    if (totalSec <= 0 && task.ElapsedSeconds) {
      totalSec = Number(task.ElapsedSeconds) || 0;
    }
    const h = Math.floor(totalSec / 3600);
    const m = Math.floor((totalSec % 3600) / 60);
    if (h > 0) return `${h}h ${m}m`;
    if (m > 0) return `${m}m`;
    return `${totalSec}s`;
  }

  async function handleTimeRangeChange(range: string) {
    await projectStore.loadDashboard({ timeRange: range });
    const label = range === '30d' ? 'Last 30 Days' : range === '90d' ? 'Last 90 Days' : 'All-Time';
    appState.addToast(`Analytics window updated: ${label}`, 'info');
  }

  async function loadPersonalActivity() {
    isLoadingPersonal = true;
    try {
      const res = await ApiClient.getNotifications(10);
      myNotifications = res.notifications || [];
    } catch (e) {
      // Fallback
    } finally {
      isLoadingPersonal = false;
    }
  }

  function formatDeadlineDisplay(deadline?: string | null, status?: string): string {
    if (!deadline) return '';
    const clean = String(deadline).trim();
    const dt = new Date(clean);
    if (isNaN(dt.getTime())) return clean.split('T')[0];

    const isCompleted = ['done', 'approved', 'completed'].includes((status || '').toLowerCase());
    if (isCompleted) {
      return dt.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
    }

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const target = new Date(dt);
    target.setHours(0, 0, 0, 0);
    const diffDays = Math.ceil((target.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));

    if (diffDays < 0) return `Overdue ${Math.abs(diffDays)}d`;
    if (diffDays === 0) return 'Due Today';
    return `Due in ${diffDays}d`;
  }

  const kpis = $derived(projectStore.dashboardData?.kpis || {
    total: 0,
    active: 0,
    pendingReview: 0,
    revisionRequired: 0,
    completed: 0,
    overdue: 0,
    dueSoon: 0,
    highRevisionCount: 0
  });

  const workloads = $derived(projectStore.dashboardData?.designerWorkload || []);
  const slaData = $derived(projectStore.dashboardData?.slaMetrics || {
    avgTurnaroundDays: null,
    medianTurnaroundDays: null,
    p90TurnaroundDays: null,
    firstTimeRightPercent: null,
    avgRevisionCount: null,
    avgReviewAgeDays: 0,
    brandVelocity: [],
    competencySkills: []
  });

  const pipeline = $derived(projectStore.dashboardData?.pipeline || {
    backlog: 0,
    inProgress: 0,
    review: 0,
    revision: 0,
    approved: 0,
    done: 0
  });

  const brandDistribution = $derived(projectStore.dashboardData?.brandDistribution || {});
  const highRevisionProjects = $derived(
    (projectStore.dashboardData?.highRevisionProjects || []).filter(
      p => p && !['done', 'approved', 'completed', 'on-hold'].includes(p.status)
    )
  );

  // "My Workspace" Strictly Filtered to Current User
  const currentUserName = $derived(appState.currentUser?.name || '');
  const currentUserStaffId = $derived(appState.currentUser?.staffId || '');

  const myProjects = $derived.by(() => {
    if (!projectStore.projects || !Array.isArray(projectStore.projects)) return [];
    return projectStore.projects.filter(p => {
      if (!p) return false;
      const d = (p.designer || '').toLowerCase();
      const uName = (currentUserName || '').toLowerCase();
      const sId = (currentUserStaffId || '').toLowerCase();
      return (uName && d.includes(uName)) || (sId && d.includes(sId));
    });
  });

  const myRevisionItems = $derived.by(() => {
    return myProjects.filter(p => p && p.status === 'revision');
  });

  const myActiveItems = $derived.by(() => {
    return myProjects.filter(p => p && p.status === 'in-progress');
  });

  const myReviewItems = $derived.by(() => {
    return myProjects.filter(p => p && p.status === 'review');
  });

  const totalBrandAssets = $derived.by(() => {
    return Object.values(brandDistribution || {}).reduce((sum, count) => sum + (count || 0), 0) || 1;
  });

  const holdingBrands = ['all', 'SSH', 'SSC', 'SSW', 'SSE', 'SST', 'SS'];
</script>

<div class="dashboard-container">
  <!-- Board Header with Lens Switcher -->
  <div class="board-header">
    <div class="header-left-col">
      <div class="header-tag">
        <span class="badge badge-brand">
          {activeLens === 'studio' ? 'EXECUTIVE BOARD DECK' : 'PERSONAL CREATIVE DESK'}
        </span>
        <span class="header-meta">Synology Vault Live • {currentUserName} ({appState.currentUser?.role})</span>
      </div>
      <h1 class="header-title">
        {activeLens === 'studio' ? 'Creative Operations & Studio Performance' : 'My Production Workspace'}
      </h1>
      <p class="header-desc">
        {activeLens === 'studio' 
          ? 'Real-time studio health, production velocity, bottleneck aging, and brand portfolio distribution' 
          : 'Focused view of your active tasks, urgent revision requests, and direct team feedback'}
      </p>
    </div>

    <!-- Lens Switcher & Actions -->
    <div class="header-actions">
      <!-- Time Horizon Slicing for Studio Lens -->
      {#if activeLens === 'studio'}
        <div class="time-horizon-switcher" title="Analytics Time Window">
          <button
            type="button"
            class="horizon-btn"
            class:active={projectStore.dashboardTimeRange === '30d'}
            onclick={() => handleTimeRangeChange('30d')}
          >
            30D
          </button>
          <button
            type="button"
            class="horizon-btn"
            class:active={projectStore.dashboardTimeRange === '90d'}
            onclick={() => handleTimeRangeChange('90d')}
          >
            90D
          </button>
          <button
            type="button"
            class="horizon-btn"
            class:active={projectStore.dashboardTimeRange === 'all'}
            onclick={() => handleTimeRangeChange('all')}
          >
            All
          </button>
        </div>

        <!-- Brand Filter Dropdown -->
        <select
          class="brand-scope-select"
          value={projectStore.dashboardBrand}
          onchange={(e) => projectStore.loadDashboard({ brand: (e.target as HTMLSelectElement).value })}
          title="Filter by Sub-Brand Holding"
        >
          <option value="all">All Brands</option>
          {#each holdingBrands.slice(1) as b}
            <option value={b}>{b} Holding</option>
          {/each}
        </select>
      {/if}

      <div class="lens-switcher">
        <button
          class="lens-btn"
          class:active={activeLens === 'studio'}
          onclick={() => (activeLens = 'studio')}
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z"/></svg>
          Studio Overview
        </button>
        <button
          class="lens-btn"
          class:active={activeLens === 'my-workspace'}
          onclick={() => (activeLens = 'my-workspace')}
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>
          My Workspace
          {#if myRevisionItems.length > 0}
            <span class="lens-alert-pill">{myRevisionItems.length}</span>
          {/if}
        </button>
      </div>

      <FluentButton appearance="secondary" size="sm" onclick={() => { projectStore.loadDashboard(); loadPersonalActivity(); }}>
        <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z"/></svg>
        <span>Refresh</span>
      </FluentButton>
    </div>
  </div>

  <!-- ═══════════ STUDIO EXECUTIVE DECK LENS ═══════════ -->
  {#if activeLens === 'studio'}
    <!-- Top 4-KPI Commercial Summary Strip -->
    <div class="kpi-grid commercial-pillars">
      <FluentCard hoverLift borderAccent="#0284C7" onclick={() => appState.navigate('projects', { status: 'in-progress' })}>
        <div class="kpi-label">Active in Production</div>
        <div class="kpi-value">{kpis.active}</div>
        <div class="kpi-trend text-primary">In Creative Pipeline</div>
      </FluentCard>

      <FluentCard hoverLift borderAccent="#D97706" onclick={() => appState.navigate('deliverables')}>
        <div class="kpi-label">Pending Sign-Off</div>
        <div class="kpi-value" style="color: #D97706;">{kpis.pendingReview}</div>
        <div class="kpi-trend" style="color: #D97706;">Awaiting Approval</div>
      </FluentCard>

      <FluentCard hoverLift borderAccent="#10B981" onclick={() => appState.navigate('projects', { status: 'approved' })}>
        <div class="kpi-label">Approved &amp; Launch Ready</div>
        <div class="kpi-value" style="color: #10B981;">{kpis.completed}</div>
        <div class="kpi-trend" style="color: #10B981;">Ready for Release</div>
      </FluentCard>

      <FluentCard hoverLift borderAccent={kpis.overdue > 0 ? '#DC2626' : (kpis.dueSoon || 0) > 0 ? '#D97706' : '#64748B'} onclick={() => appState.navigate('projects', { isOverdue: true })}>
        <div class="kpi-label">Overdue &amp; At-Risk</div>
        <div class="kpi-value" style="color: {kpis.overdue > 0 ? '#DC2626' : (kpis.dueSoon || 0) > 0 ? '#D97706' : 'var(--text-primary)'}">
          {kpis.overdue}
        </div>
        <div class="kpi-trend" style="color: {kpis.overdue > 0 ? '#DC2626' : '#D97706'};">
          {#if kpis.overdue > 0}
            Immediate Action Required
          {:else if (kpis.dueSoon || 0) > 0}
            {kpis.dueSoon} Due in 48h
          {:else}
            On Track
          {/if}
        </div>
      </FluentCard>
    </div>

    <!-- High-Revision Friction Loop Alert (Only shown if friction exists) -->
    {#if highRevisionProjects.length > 0}
      <div class="friction-alert-card">
        <div class="friction-alert-header">
          <div class="friction-tag">
            <FluentIcons name="warning" size={16} color="#F59E0B" />
            <strong style="margin-left: 6px;">CREATIVE FRICTION ALERT ({highRevisionProjects.length})</strong>
          </div>
          <span class="friction-desc">Projects with &ge; 2 revision rounds require Art Director brief alignment &amp; feedback intervention</span>
        </div>
        <div class="friction-items-grid">
          {#each highRevisionProjects as hp}
            <!-- svelte-ignore a11y_click_events_have_key_events -->
            <!-- svelte-ignore a11y_no_static_element_interactions -->
            <div class="friction-item" onclick={() => appState.navigate('project-detail', { id: hp.id })}>
              <div class="fitem-left">
                <span class="fitem-id">{hp.jobId}</span>
                <span class="fitem-title">{hp.title}</span>
              </div>
              <div class="fitem-right">
                <span class="fitem-designer">
                  <FluentIcons name="user" size={11} />
                  <span style="margin-left: 4px;">{hp.designer}</span>
                </span>
                <span class="badge-rev-alert">Round {hp.revision}</span>
              </div>
            </div>
          {/each}
        </div>
      </div>
    {/if}

    <!-- Live Studio Activity Stream Card -->
    <div class="studio-livestream-card">
      <div class="livestream-header">
        <div class="livestream-header-left">
          <span class="livestream-pulse-ring" class:pulsing={appState.activeLiveTasks.length > 0}></span>
          <div>
            <div class="livestream-title-row">
              <h2 class="livestream-title">Active Creative Sessions</h2>
              <span class="livestream-status-badge" class:active={appState.activeLiveTasks.length > 0}>
                {appState.activeLiveTasks.length > 0 ? `${appState.activeLiveTasks.length} Active` : 'All Stations Idle'}
              </span>
            </div>
            <p class="livestream-subtitle">Real-time designer focus and campaign production synced from desktop SS-CAM</p>
          </div>
        </div>

        <div class="livestream-header-actions">
          <button
            class="refresh-stream-btn"
            onclick={() => appState.loadLiveTasks()}
            title="Refresh studio status"
            aria-label="Refresh live tasks"
          >
            <svg width="13" height="13" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z"/>
            </svg>
            <span>Sync</span>
          </button>
        </div>
      </div>

      {#if appState.activeLiveTasks.length === 0}
        <div class="livestream-empty">
          <div class="livestream-empty-icon">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
              <circle cx="12" cy="12" r="10"/>
              <polyline points="12 6 12 12 16 14"/>
            </svg>
          </div>
          <div class="livestream-empty-text">
            <strong>All Studio Workstations Currently Idle</strong>
            <span>When designers launch projects in desktop SS-CAM, active work sessions stream live here in real-time.</span>
          </div>
        </div>
      {:else}
        <div class="livestream-grid">
          {#each appState.activeLiveTasks as task}
            {@const elapsed = getLiveElapsedDisplay(task, currentSecondTicker)}
            {@const designerInitials = (task.DesignerName || task.StaffId || 'D').slice(0, 2).toUpperCase()}
            <div class="livestream-task-chip">
              <div class="livestream-chip-top">
                <div class="livestream-designer">
                  <div class="designer-avatar" style="background: {task.AvatarColor || 'var(--brand-accent)'};">
                    {designerInitials}
                  </div>
                  <div class="designer-meta">
                    <span class="designer-name">{task.DesignerName || task.StaffId}</span>
                    <span class="designer-workstation">{task.Role || 'Creative Staff'}</span>
                  </div>
                </div>

                <div class="livestream-clock" title="Session duration">
                  <span class="clock-icon">⏱</span>
                  <span class="clock-digits">{elapsed}</span>
                </div>
              </div>

              <div class="livestream-chip-body">
                <div class="task-client-badge">{task.Client || 'INTERNAL'}</div>
                <div class="task-project-name" title={task.ProjectName || task.ProjectId}>
                  {task.ProjectName || task.ProjectId}
                </div>
                {#if task.SessionNotes}
                  <div class="task-notes" title={task.SessionNotes}>
                    "{task.SessionNotes}"
                  </div>
                {/if}
              </div>

              <div class="livestream-chip-footer">
                <span class="pulse-status-dot"></span>
                <span class="task-state-label">{task.State || 'Designing'}</span>
                {#if task.ProjectId}
                  <button
                    class="task-jump-btn"
                    onclick={() => appState.navigate('project-detail', { id: task.ProjectId })}
                  >
                    Open Workspace ↗
                  </button>
                {/if}
              </div>
            </div>
          {/each}
        </div>
      {/if}
    </div>

    <!-- Creative Pipeline Funnel & Sub-Brand Balance -->
    <div class="studio-distribution-grid">
      <!-- Pipeline Progression Funnel -->
      <FluentCard elevated>
        <div class="card-section-header">
          <div>
            <h2>Creative Pipeline Stage Flow</h2>
            <p>Real-time lifecycle volume from intake to final archive</p>
          </div>
        </div>

        <div class="pipeline-funnel-container">
          <div class="funnel-step" onclick={() => appState.navigate('projects', { status: 'backlog' })}>
            <div class="funnel-count">{pipeline.backlog || 0}</div>
            <div class="funnel-bar bg-backlog"></div>
            <div class="funnel-label">Backlog</div>
          </div>
          <div class="funnel-connector">→</div>
          <div class="funnel-step" onclick={() => appState.navigate('projects', { status: 'in-progress' })}>
            <div class="funnel-count text-primary">{pipeline.inProgress || 0}</div>
            <div class="funnel-bar bg-inprogress"></div>
            <div class="funnel-label">In Progress</div>
          </div>
          <div class="funnel-connector">→</div>
          <div class="funnel-step" onclick={() => appState.navigate('deliverables')}>
            <div class="funnel-count text-review">{pipeline.review || 0}</div>
            <div class="funnel-bar bg-review"></div>
            <div class="funnel-label">Review</div>
          </div>
          <div class="funnel-connector">→</div>
          <div class="funnel-step" onclick={() => appState.navigate('projects', { status: 'revision' })}>
            <div class="funnel-count text-revision">{pipeline.revision || 0}</div>
            <div class="funnel-bar bg-revision"></div>
            <div class="funnel-label">Revision</div>
          </div>
          <div class="funnel-connector">→</div>
          <div class="funnel-step" onclick={() => appState.navigate('projects', { status: 'approved' })}>
            <div class="funnel-count text-success">{pipeline.approved || 0}</div>
            <div class="funnel-bar bg-approved"></div>
            <div class="funnel-label">Approved</div>
          </div>
          <div class="funnel-connector">→</div>
          <div class="funnel-step" onclick={() => appState.navigate('projects', { status: 'done' })}>
            <div class="funnel-count text-done">{pipeline.done || 0}</div>
            <div class="funnel-bar bg-done"></div>
            <div class="funnel-label">Archived</div>
          </div>
        </div>
      </FluentCard>

      <!-- Sub-Brand Asset Allocation -->
      <FluentCard elevated>
        <div class="card-section-header">
          <div>
            <h2>Brand Portfolio Allocation</h2>
            <p>Asset distribution across SuamiSihat holding subsidiaries</p>
          </div>
        </div>

        <div class="brand-bar-stack">
          {#each Object.entries(brandDistribution) as [brand, count]}
            {@const pct = Math.round((count / totalBrandAssets) * 100)}
            <div class="brand-row" onclick={() => appState.navigate('projects', { brand })}>
              <div class="brand-row-header">
                <span class="brand-badge-pill">{brand}</span>
                <span class="brand-stats-label">{count} assets ({pct}%)</span>
              </div>
              <div class="brand-bar-track">
                <div class="brand-bar-fill" style="width: {pct}%;"></div>
              </div>
            </div>
          {:else}
            <div class="empty-state">No brand assets recorded yet.</div>
          {/each}
        </div>
      </FluentCard>
    </div>

    <!-- Designer Production Load -->
    <div class="workload-section" style="margin-bottom: 24px;">
      <FluentCard elevated>
        <div class="card-section-header">
          <div>
            <h2>Designer Production Load</h2>
            <p>Real-time asset distribution and capacity across creative staff</p>
          </div>
        </div>

        <div class="workload-list">
          {#each workloads as w}
            {@const isCurrentDesigner = Boolean(appState.currentUser && (
              (w.staffId && appState.currentUser.staffId && w.staffId.toLowerCase() === appState.currentUser.staffId.toLowerCase()) ||
              (w.designer && appState.currentUser.name && w.designer.toLowerCase() === appState.currentUser.name.toLowerCase()) ||
              (w.name && appState.currentUser.name && w.name.toLowerCase() === appState.currentUser.name.toLowerCase()) ||
              (w.designer && appState.currentUser.username && w.designer.toLowerCase() === appState.currentUser.username.toLowerCase())
            ))}
            {@const wAvatar = w.avatarUrl || w.avatar || (typeof localStorage !== 'undefined' ? (
              (w.staffId ? localStorage.getItem(`ss_cam_avatar_${w.staffId}`) : null) ||
              (w.designer ? localStorage.getItem(`ss_cam_avatar_${w.designer}`) : null) ||
              (isCurrentDesigner ? (appState.currentUser?.avatarUrl || appState.currentUser?.avatar || localStorage.getItem('ss_cam_user_avatar') || '') : '')
            ) : '')}
            <div class="workload-row">
              <div class="workload-user">
                <div class="avatar-chip" style="background: {w.avatarColor || 'var(--brand-primary, #043388)'};">
                  {#if wAvatar}
                    <img
                      src={wAvatar}
                      alt={w.designer}
                      class="avatar-photo"
                      onerror={(e) => ((e.currentTarget as HTMLElement).style.display = 'none')}
                    />
                  {:else}
                    {(w.name || w.designer || 'D').charAt(0).toUpperCase()}
                  {/if}
                </div>
                <div class="user-info-col">
                  <div class="user-name">{w.name || w.designer || 'Unassigned'}</div>
                  <div class="user-role">{w.staffId ? `${w.staffId} · ` : ''}{w.total || 0} Total Projects</div>
                </div>
              </div>

              <div class="workload-mid-col">
                <div class="capacity-bar-container">
                  <div class="capacity-bar-fill" style="width: {w.capacityPercent || 0}%; background: {w.capacityColor || '#10B981'};"></div>
                </div>
                <span class="badge-capacity" style="color: {w.capacityColor || '#10B981'}; border-color: {w.capacityColor || '#10B981'};">
                  {w.capacityStatus || 'Optimal Bandwidth'}
                </span>
              </div>

              <div class="workload-stats">
                <span class="stat-pill stat-active">{w.active || 0} Active</span>
                <span class="stat-pill stat-review">{w.inReview || 0} Review</span>
                <span class="stat-pill stat-done">{w.completed || 0} Done</span>
              </div>
            </div>
          {:else}
            <div class="empty-state">No workload data recorded yet.</div>
          {/each}
        </div>
      </FluentCard>
    </div>

    <!-- Collapsible Advanced Studio Telemetry & SLA Accordion -->
    <div class="deep-analytics-accordion">
      <button
        type="button"
        class="deep-analytics-toggle"
        onclick={() => (showDeepAnalytics = !showDeepAnalytics)}
        aria-expanded={showDeepAnalytics}
      >
        <div class="deep-toggle-left">
          <FluentIcons name="chart" size={16} />
          <span>Advanced Studio Telemetry &amp; SLA Metrics</span>
        </div>
        <div class="deep-toggle-right">
          <span class="deep-toggle-hint">{showDeepAnalytics ? 'Hide Advanced Metrics' : 'Show Competency Radar & 4 SLA Metrics'}</span>
          <svg class="deep-chevron" class:open={showDeepAnalytics} width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
            <path d="M7 10l5 5 5-5z"/>
          </svg>
        </div>
      </button>

      {#if showDeepAnalytics}
        <div class="deep-analytics-content">
          <div class="radar-card-wrap" style="margin-bottom: 18px;">
            <FluentCard elevated>
              <div class="card-section-header">
                <div>
                  <h2>Art Director Skill Competency Matrix</h2>
                  <p>Multi-dimensional studio balance and design output readiness</p>
                </div>
              </div>
              <DashboardRadar skills={slaData.competencySkills} />
            </FluentCard>
          </div>

          <!-- Operational SLA & Creative Velocity 4-Card Grid -->
          <div class="sla-analytics-grid">
            <FluentCard elevated>
              <div class="sla-card-inner">
                <div class="sla-icon-box" style="background: rgba(16, 185, 129, 0.12); color: #10B981;">
                  <FluentIcons name="checkCircle" size={20} color="#10B981" />
                </div>
                <div>
                  <div class="sla-meta-label">FIRST-TIME RIGHT RATE</div>
                  <div class="sla-value" style="color: #10B981;">
                    {slaData.firstTimeRightPercent !== null && slaData.firstTimeRightPercent !== undefined ? `${slaData.firstTimeRightPercent}%` : '—'}
                  </div>
                  <div class="sla-desc">Projects signed off with 0 revisions</div>
                </div>
              </div>
            </FluentCard>

            <FluentCard elevated>
              <div class="sla-card-inner">
                <div class="sla-icon-box" style="background: rgba(4, 51, 136, 0.12); color: var(--brand-primary, #043388);">
                  <FluentIcons name="bolt" size={20} color="#00CFFF" />
                </div>
                <div>
                  <div class="sla-meta-label">AVG TURNAROUND VELOCITY</div>
                  <div class="sla-value">
                    {slaData.avgTurnaroundDays !== null && slaData.avgTurnaroundDays !== undefined ? `${slaData.avgTurnaroundDays} Days` : '—'}
                  </div>
                  <div class="sla-desc">
                    {#if slaData.medianTurnaroundDays !== null && slaData.medianTurnaroundDays !== undefined}
                      <span class="sla-stat-pill">p50: {slaData.medianTurnaroundDays}d</span>
                      <span class="sla-stat-pill">p90: {slaData.p90TurnaroundDays || slaData.avgTurnaroundDays}d</span>
                    {:else}
                      Brief kickoff to final approval
                    {/if}
                  </div>
                </div>
              </div>
            </FluentCard>

            <FluentCard elevated>
              <div class="sla-card-inner">
                <div class="sla-icon-box" style="background: rgba(217, 119, 6, 0.12); color: #D97706;">
                  <FluentIcons name="history" size={20} color="#D97706" />
                </div>
                <div>
                  <div class="sla-meta-label">AVG REVISION ROUNDS</div>
                  <div class="sla-value">
                    {slaData.avgRevisionCount !== null && slaData.avgRevisionCount !== undefined ? `${slaData.avgRevisionCount} Revs` : '—'}
                  </div>
                  <div class="sla-desc">Average iterations per completed project</div>
                </div>
              </div>
            </FluentCard>

            <FluentCard elevated>
              <div class="sla-card-inner">
                <div class="sla-icon-box" style="background: rgba(147, 51, 234, 0.12); color: #9333EA;">
                  <FluentIcons name="calendar" size={20} color="#9333EA" />
                </div>
                <div>
                  <div class="sla-meta-label">REVIEW QUEUE AGING</div>
                  <div class="sla-value" style="color: #9333EA;">
                    {slaData.avgReviewAgeDays || 0} Days
                  </div>
                  <div class="sla-desc">Average latency in review before sign-off</div>
                </div>
              </div>
            </FluentCard>
          </div>
        </div>
      {/if}
    </div>

  <!-- ═══════════ MY WORKSPACE LENS ═══════════ -->
  {:else}
    <!-- Personal KPI Strips -->
    <div class="personal-kpi-grid">
      <div class="personal-kpi-card highlight-revision">
        <div class="pkpi-meta">
          <span class="pkpi-icon">
            <FluentIcons name="warning" size={16} color="#EF4444" />
          </span>
          <span class="pkpi-title">Revisions Requiring Action</span>
        </div>
        <div class="pkpi-count">{myRevisionItems.length}</div>
        <span class="pkpi-desc">Deliverables awaiting updates</span>
      </div>

      <div class="personal-kpi-card highlight-active">
        <div class="pkpi-meta">
          <span class="pkpi-icon">
            <FluentIcons name="bolt" size={16} color="#00CFFF" />
          </span>
          <span class="pkpi-title">In Production</span>
        </div>
        <div class="pkpi-count">{myActiveItems.length}</div>
        <span class="pkpi-desc">Current active deliverables</span>
      </div>

      <div class="personal-kpi-card highlight-review">
        <div class="pkpi-meta">
          <span class="pkpi-icon">
            <FluentIcons name="calendar" size={16} color="#F59E0B" />
          </span>
          <span class="pkpi-title">Under Review</span>
        </div>
        <div class="pkpi-count">{myReviewItems.length}</div>
        <span class="pkpi-desc">Awaiting manager sign-off</span>
      </div>
    </div>

    <!-- Personal Two-Column Layout -->
    <div class="my-workspace-grid">
      <!-- Left: Assigned Projects & Action Queue -->
      <div class="my-queue-column">
        <FluentCard elevated>
          <div class="card-section-header">
            <div>
              <h2>My Priority Production Queue</h2>
              <p>Direct assignments and deliverables under your custody</p>
            </div>
          </div>

          {#if myProjects.length === 0}
            <div class="empty-workspace-state">
              <div class="empty-icon-box">
                <FluentIcons name="checkCircle" size={36} color="#10B981" />
              </div>
              <p class="empty-title">All tasks cleared</p>
              <p class="empty-desc">You have no active projects or pending revisions assigned right now.</p>
            </div>
          {:else}
            <div class="project-queue-list">
              {#each myProjects as proj (proj.id || proj.jobId)}
                <!-- svelte-ignore a11y_click_events_have_key_events -->
                <!-- svelte-ignore a11y_no_static_element_interactions -->
                <div class="queue-card" onclick={() => appState.navigate('project-detail', { id: proj.id || proj.jobId })}>
                  <div class="queue-card-header">
                    <div class="queue-id-tag">{proj.jobId || proj.id}</div>
                    <span class="status-pill status-{proj.status}">{proj.status}</span>
                  </div>
                  <h4 class="queue-title">{proj.title}</h4>
                  <div class="queue-footer">
                    <span class="queue-brand">Brand: {proj.brand || 'SS'}</span>
                    {#if proj.subtasks && proj.subtasks.length > 0}
                      {@const done = proj.completedSubtasksCount !== undefined ? proj.completedSubtasksCount : proj.subtasks.filter((s: any) => ['approved', 'done', 'completed'].includes((s.status || '').toLowerCase())).length}
                      {@const pts = proj.totalWeight || proj.subtasks.reduce((sum: number, s: any) => sum + (typeof s.weight === 'number' ? s.weight : 1), 0)}
                      <span class="queue-pts-pill" title="{done}/{proj.subtasks.length} Subtasks Done">
                        <svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" style="margin-right: 2px; vertical-align: -1px; display: inline-block;">
                          <circle cx="12" cy="12" r="9"/>
                          <circle cx="12" cy="12" r="4"/>
                        </svg>
                        {done}/{proj.subtasks.length} Done • {pts} pts
                      </span>
                    {/if}
                    <span class="queue-deadline">
                      <FluentIcons name="calendar" size={11} />
                      <span style="margin-left: 3px;">{proj.deadlineDisplay || formatDeadlineDisplay(proj.deadline, proj.status) || 'No deadline'}</span>
                    </span>
                    <span class="queue-action-link">Open Workspace →</span>
                  </div>
                </div>
              {/each}
            </div>
          {/if}
        </FluentCard>
      </div>

      <!-- Right: Direct Mentions & Discussion Highlights -->
      <div class="my-activity-column">
        <FluentCard elevated>
          <div class="card-section-header">
            <div>
              <h2>Mentions &amp; Discussion Feed</h2>
              <p>Recent collaboration notes directed to you</p>
            </div>
          </div>

          {#if myNotifications.length === 0}
            <div class="empty-workspace-state">
              <div class="empty-icon-box">
                <FluentIcons name="chat" size={36} color="rgba(255,255,255,0.2)" />
              </div>
              <p class="empty-title">No recent mentions</p>
              <p class="empty-desc">Team comments mentioning you will appear here.</p>
            </div>
          {:else}
            <div class="personal-activity-list">
              {#each myNotifications.slice(0, 6) as notif (notif.id)}
                <!-- svelte-ignore a11y_click_events_have_key_events -->
                <!-- svelte-ignore a11y_no_static_element_interactions -->
                <div
                  class="personal-activity-card"
                  onclick={() => {
                    if (notif.route === 'project-detail' && notif.routeId) {
                      appState.navigate('project-detail', { id: notif.routeId });
                    }
                  }}
                >
                  <div class="pact-header">
                    <span class="pact-title">{notif.title}</span>
                    <span class="pact-actor">{notif.actor}</span>
                  </div>
                  <p class="pact-message">{notif.message}</p>
                </div>
              {/each}
            </div>
          {/if}
        </FluentCard>
      </div>
    </div>
  {/if}
</div>

<style>
  .dashboard-container {
    display: flex;
    flex-direction: column;
    gap: 24px;
  }

  .board-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    flex-wrap: wrap;
    gap: 16px;
    padding-bottom: 16px;
    border-bottom: 1px solid var(--surface-card-border);
  }

  .header-left-col {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .header-tag {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 4px;
  }

  .badge-brand {
    font-size: 10px;
    font-weight: 800;
    letter-spacing: 0.6px;
    text-transform: uppercase;
    background: rgba(33, 161, 247, 0.15);
    color: #21A1F7;
    padding: 2px 8px;
    border-radius: var(--radius-sm, 4px);
    border: 1px solid rgba(33, 161, 247, 0.3);
  }

  .header-meta {
    font-size: 12px;
    color: var(--text-secondary);
    font-weight: 600;
  }

  .header-title {
    font-size: 24px;
    font-weight: 800;
    color: var(--text-primary);
    margin: 0;
  }

  .header-desc {
    font-size: 13px;
    color: var(--text-secondary);
    margin: 0;
  }

  .header-actions {
    display: flex;
    align-items: center;
    gap: 12px;
    flex-wrap: wrap;
  }

  /* Time Horizon Switcher */
  .time-horizon-switcher {
    display: flex;
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: 8px;
    padding: 3px;
    box-shadow: var(--shadow-sm);
    gap: 3px;
  }

  .horizon-btn {
    padding: 5px 12px;
    border: none;
    background: transparent;
    border-radius: 6px;
    font-size: 12px;
    font-weight: 700;
    color: var(--text-secondary);
    cursor: pointer;
    transition: all 0.14s ease;
    font-family: inherit;
  }
  .horizon-btn:hover {
    color: var(--text-primary);
    background: var(--surface-card-subtle);
  }
  .horizon-btn.active {
    background: var(--brand-primary, #043388);
    color: #FFFFFF;
    font-weight: 800;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.2);
  }

  /* Brand Scope Selector */
  .brand-scope-select {
    padding: 6px 10px;
    border-radius: 8px;
    border: 1px solid var(--surface-card-border);
    background: var(--surface-card);
    color: var(--text-primary);
    font-size: 12px;
    font-weight: 700;
    cursor: pointer;
    font-family: inherit;
    box-shadow: var(--shadow-sm);
    outline: none;
  }
  .brand-scope-select:focus {
    border-color: var(--brand-accent);
  }

  /* Lens Switcher */
  .lens-switcher {
    display: flex;
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: 8px;
    padding: 2px;
    box-shadow: var(--shadow-sm);
  }

  .lens-btn {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 6px 14px;
    border: none;
    background: transparent;
    border-radius: 6px;
    font-size: 12.5px;
    font-weight: 700;
    color: var(--text-secondary);
    cursor: pointer;
    transition: all 0.14s;
    font-family: inherit;
  }
  .lens-btn:hover {
    color: var(--text-primary);
  }
  .lens-btn.active {
    background: var(--brand-primary, #043388);
    color: #FFFFFF;
    box-shadow: var(--shadow-sm);
  }

  .lens-alert-pill {
    font-size: 10px;
    font-weight: 800;
    background: #EF4444;
    color: #FFFFFF;
    padding: 1px 5px;
    border-radius: 9999px;
  }

  /* Studio Lens KPIs */
  .kpi-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
    gap: 14px;
  }

  .commercial-pillars {
    grid-template-columns: repeat(4, 1fr);
  }

  .kpi-label {
    font-size: 11px;
    font-weight: 700;
    color: var(--text-secondary);
    text-transform: uppercase;
    letter-spacing: 0.5px;
  }

  .kpi-value {
    font-size: 28px;
    font-weight: 900;
    color: var(--text-primary);
    margin: 6px 0;
  }

  .kpi-trend {
    font-size: 11.5px;
    font-weight: 600;
  }
  .text-accent { color: var(--brand-accent, #21A1F7); }
  .text-primary { color: var(--brand-primary, #043388); }
  .text-review { color: #D97706; }
  .text-revision { color: #DC2626; }
  .text-success { color: #10B981; }
  .text-done { color: #047857; }

  /* Friction Alert Card */
  .friction-alert-card {
    background: #FEF2F2;
    border: 1px solid #F87171;
    border-radius: 10px;
    padding: 14px 18px;
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  .friction-alert-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
  }

  .friction-tag {
    display: flex;
    align-items: center;
    gap: 6px;
    color: #991B1B;
    font-size: 12px;
    letter-spacing: 0.4px;
  }

  .friction-desc {
    font-size: 12px;
    color: #7F1D1D;
  }

  .friction-items-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: 10px;
  }

  .friction-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: #FFFFFF;
    border: 1px solid #FECACA;
    border-radius: 6px;
    padding: 8px 12px;
    cursor: pointer;
    transition: all 0.12s ease;
  }
  .friction-item:hover {
    border-color: #DC2626;
    transform: translateY(-1px);
    box-shadow: var(--shadow-sm);
  }

  .fitem-left {
    display: flex;
    align-items: center;
    gap: 8px;
    overflow: hidden;
  }

  .fitem-id {
    font-family: monospace;
    font-size: 11px;
    font-weight: 800;
    color: #991B1B;
    background: #FEE2E2;
    padding: 2px 5px;
    border-radius: 4px;
  }

  .fitem-title {
    font-size: 12.5px;
    font-weight: 700;
    color: var(--text-primary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 170px;
  }

  .fitem-right {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-shrink: 0;
  }

  .fitem-designer {
    font-size: 11px;
    color: var(--text-secondary);
  }

  .badge-rev-alert {
    font-size: 10px;
    font-weight: 800;
    background: #DC2626;
    color: #FFFFFF;
    padding: 2px 6px;
    border-radius: 9999px;
  }

  /* Live Studio Workstream Card */
  .studio-livestream-card {
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-md, 12px);
    padding: 16px 20px;
    box-shadow: var(--shadow-sm);
    display: flex;
    flex-direction: column;
    gap: 14px;
    transition: border-color 0.2s;
  }
  .livestream-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    flex-wrap: wrap;
    gap: 12px;
  }
  .livestream-header-left {
    display: flex;
    align-items: center;
    gap: 12px;
  }
  .livestream-pulse-ring {
    width: 10px;
    height: 10px;
    border-radius: 50%;
    background: var(--text-tertiary);
    flex-shrink: 0;
  }
  .livestream-pulse-ring.pulsing {
    background: #10B981;
    box-shadow: 0 0 10px rgba(16, 185, 129, 0.8);
    animation: livePulse 1.8s infinite;
  }
  @keyframes livePulse {
    0% { transform: scale(0.95); opacity: 0.8; }
    50% { transform: scale(1.25); opacity: 1; }
    100% { transform: scale(0.95); opacity: 0.8; }
  }
  .livestream-title-row {
    display: flex;
    align-items: center;
    gap: 10px;
  }
  .livestream-title {
    font-size: 15px;
    font-weight: 800;
    color: var(--text-primary);
    margin: 0;
    letter-spacing: -0.2px;
  }
  .livestream-status-badge {
    font-size: 11px;
    font-weight: 700;
    padding: 2px 8px;
    border-radius: 9999px;
    background: var(--surface-card-subtle);
    border: 1px solid var(--surface-card-border);
    color: var(--text-secondary);
  }
  .livestream-status-badge.active {
    background: rgba(16, 185, 129, 0.1);
    border-color: rgba(16, 185, 129, 0.25);
    color: #059669;
  }
  .livestream-subtitle {
    font-size: 12px;
    color: var(--text-secondary);
    margin: 2px 0 0 0;
  }
  .livestream-header-actions {
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .refresh-stream-btn {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    padding: 4px 10px;
    border-radius: 6px;
    border: 1px solid var(--surface-card-border);
    background: var(--surface-card-subtle);
    color: var(--text-secondary);
    font-size: 11.5px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.14s ease;
  }
  .refresh-stream-btn:hover {
    background: var(--surface-card);
    border-color: var(--brand-accent);
    color: var(--brand-accent);
  }
  .livestream-empty {
    display: flex;
    align-items: center;
    gap: 14px;
    padding: 14px 16px;
    border-radius: 8px;
    background: var(--surface-card-subtle);
    border: 1px dashed var(--surface-card-border);
  }
  .livestream-empty-icon {
    color: var(--text-tertiary);
    display: flex;
    align-items: center;
  }
  .livestream-empty-text {
    display: flex;
    flex-direction: column;
    gap: 2px;
  }
  .livestream-empty-text strong {
    font-size: 12.5px;
    color: var(--text-primary);
  }
  .livestream-empty-text span {
    font-size: 11.5px;
    color: var(--text-secondary);
  }
  .livestream-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 12px;
  }
  .livestream-task-chip {
    background: var(--surface-card-subtle);
    border: 1px solid var(--surface-card-border);
    border-radius: 10px;
    padding: 12px 14px;
    display: flex;
    flex-direction: column;
    gap: 10px;
    position: relative;
    overflow: hidden;
    transition: transform 0.16s ease, border-color 0.16s ease, box-shadow 0.16s ease;
  }
  .livestream-task-chip:hover {
    transform: translateY(-2px);
    border-color: var(--brand-accent);
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.06);
    background: var(--surface-card);
  }
  .livestream-chip-top {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }
  .livestream-designer {
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .designer-avatar {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    color: #fff;
    font-size: 11.5px;
    font-weight: 800;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    box-shadow: 0 2px 5px rgba(0, 0, 0, 0.15);
  }
  .designer-meta {
    display: flex;
    flex-direction: column;
  }
  .designer-name {
    font-size: 12.5px;
    font-weight: 700;
    color: var(--text-primary);
  }
  .designer-workstation {
    font-size: 10.5px;
    color: var(--text-tertiary);
    font-family: var(--font-mono, monospace);
  }
  .livestream-clock {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    background: rgba(33, 161, 247, 0.08);
    border: 1px solid rgba(33, 161, 247, 0.22);
    padding: 3px 8px;
    border-radius: 6px;
  }
  .clock-icon {
    font-size: 11px;
  }
  .clock-digits {
    font-family: var(--font-mono, monospace);
    font-size: 12px;
    font-weight: 800;
    color: var(--brand-accent);
    letter-spacing: 0.5px;
  }
  .livestream-chip-body {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }
  .task-client-badge {
    align-self: flex-start;
    font-size: 9.5px;
    font-weight: 800;
    letter-spacing: 0.5px;
    text-transform: uppercase;
    color: var(--brand-accent);
    background: rgba(33, 161, 247, 0.1);
    padding: 1px 6px;
    border-radius: 4px;
  }
  .task-project-name {
    font-size: 13px;
    font-weight: 700;
    color: var(--text-primary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .task-notes {
    font-size: 11.5px;
    font-style: italic;
    color: var(--text-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .livestream-chip-footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding-top: 8px;
    border-top: 1px solid var(--surface-card-border);
    margin-top: 2px;
  }
  .pulse-status-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: #10B981;
    display: inline-block;
    box-shadow: 0 0 6px #10B981;
    margin-right: 4px;
  }
  .task-state-label {
    font-size: 11px;
    font-weight: 600;
    color: var(--text-secondary);
    flex: 1;
  }
  .task-jump-btn {
    border: none;
    background: transparent;
    color: var(--brand-accent);
    font-size: 11.5px;
    font-weight: 700;
    cursor: pointer;
    padding: 2px 6px;
    border-radius: 4px;
    transition: background 0.12s;
  }
  .task-jump-btn:hover {
    background: rgba(33, 161, 247, 0.1);
  }

  /* Studio Pipeline & Distribution */
  .studio-distribution-grid {
    display: grid;
    grid-template-columns: 3fr 2fr;
    gap: 20px;
  }

  .pipeline-funnel-container {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 6px;
    padding: 10px 0;
  }

  .funnel-step {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 6px;
    cursor: pointer;
    padding: 6px 4px;
    border-radius: 6px;
    transition: background 0.12s;
  }
  .funnel-step:hover {
    background: var(--surface-card-subtle, #F8FAFC);
  }

  .funnel-count {
    font-size: 18px;
    font-weight: 900;
    color: var(--text-primary);
  }

  .funnel-bar {
    width: 100%;
    height: 6px;
    border-radius: 3px;
  }
  .bg-backlog { background: #94A3B8; }
  .bg-inprogress { background: #0284C7; }
  .bg-review { background: #D97706; }
  .bg-revision { background: #DC2626; }
  .bg-approved { background: #10B981; }
  .bg-done { background: #047857; }

  .funnel-label {
    font-size: 11px;
    font-weight: 700;
    color: var(--text-secondary);
  }

  .funnel-connector {
    color: var(--text-tertiary, #CBD5E1);
    font-size: 14px;
    font-weight: 800;
  }

  .brand-bar-stack {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .brand-row {
    display: flex;
    flex-direction: column;
    gap: 3px;
    cursor: pointer;
    padding: 4px 6px;
    border-radius: 6px;
    transition: background 0.12s;
  }
  .brand-row:hover {
    background: var(--surface-card-subtle, #F8FAFC);
  }

  .brand-row-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .brand-badge-pill {
    font-size: 11px;
    font-weight: 800;
    color: var(--brand-primary, #043388);
  }

  .brand-stats-label {
    font-size: 11px;
    color: var(--text-secondary);
  }

  .brand-bar-track {
    width: 100%;
    height: 5px;
    border-radius: 3px;
    background: var(--surface-card-border, #E2E8F0);
    overflow: hidden;
  }

  .brand-bar-fill {
    height: 100%;
    background: var(--brand-primary, #043388);
    border-radius: 3px;
    transition: width 0.3s ease;
  }

  .analytics-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 20px;
  }

  .card-section-header h2 {
    font-size: 16px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0 0 2px 0;
  }
  .card-section-header p {
    font-size: 12px;
    color: var(--text-secondary);
    margin: 0 0 16px 0;
  }

  .workload-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  .workload-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 10px 14px;
    background: var(--bg-app);
    border-radius: 8px;
    border: 1px solid var(--surface-card-border);
  }

  .workload-user {
    display: flex;
    align-items: center;
    gap: 10px;
  }

  .avatar-chip {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    background: var(--brand-primary, #043388);
    color: #FFFFFF;
    font-weight: 800;
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;
    flex-shrink: 0;
  }
  .avatar-chip img.avatar-photo {
    width: 100%;
    height: 100%;
    object-fit: cover;
    border-radius: 50%;
    display: block;
  }

  .user-info-col {
    display: flex;
    flex-direction: column;
  }
  .user-name { font-size: 13px; font-weight: 700; color: var(--text-primary); }
  .user-role { font-size: 11px; color: var(--text-secondary); }

  .workload-mid-col {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;
    min-width: 110px;
  }

  .capacity-bar-container {
    width: 100%;
    height: 5px;
    border-radius: 3px;
    background: var(--surface-card-border, #E2E8F0);
    overflow: hidden;
  }
  .capacity-bar-fill {
    height: 100%;
    border-radius: 3px;
    transition: width 0.3s ease;
  }

  .badge-capacity {
    font-size: 9.5px;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.3px;
    padding: 1px 6px;
    border-radius: 3px;
    border: 1px solid currentColor;
    background: rgba(255, 255, 255, 0.05);
  }

  .workload-stats {
    display: flex;
    gap: 6px;
  }

  .stat-pill {
    font-size: 11px;
    font-weight: 700;
    padding: 3px 8px;
    border-radius: 4px;
  }
  .stat-active { background: #EBF4FE; color: #043388; border: 1px solid #BFDBFE; }
  .stat-review { background: #FFFBEB; color: #B45309; border: 1px solid #FDE68A; }
  .stat-done { background: #ECFDF5; color: #047857; border: 1px solid #A7F3D0; }

  /* ═══════════ SLA ANALYTICS GRID STYLES ═══════════ */
  .sla-analytics-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 16px;
  }

  .sla-card-inner {
    display: flex;
    align-items: center;
    gap: 14px;
    padding: 16px 14px;
  }

  .sla-icon-box {
    width: 42px;
    height: 42px;
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 18px;
    flex-shrink: 0;
  }

  .sla-meta-label {
    font-size: 9.5px;
    font-weight: 800;
    color: var(--text-tertiary, #94A3B8);
    letter-spacing: 0.5px;
    text-transform: uppercase;
  }

  .sla-value {
    font-size: 20px;
    font-weight: 900;
    color: var(--text-primary);
    margin: 2px 0;
  }

  .sla-desc {
    font-size: 11px;
    color: var(--text-secondary);
    display: flex;
    align-items: center;
    gap: 6px;
    flex-wrap: wrap;
    margin-top: 2px;
  }

  .sla-stat-pill {
    font-size: 10px;
    font-weight: 700;
    padding: 1px 5px;
    border-radius: 4px;
    background: var(--surface-card-subtle, #F1F5F9);
    border: 1px solid var(--surface-card-border, #E2E8F0);
    color: var(--text-secondary);
  }

  /* Deep Analytics Accordion */
  .deep-analytics-accordion {
    margin-top: 18px;
    border: 1px solid var(--surface-card-border, #E2E8F0);
    border-radius: 8px;
    background: var(--surface-card, #FFFFFF);
    overflow: hidden;
  }

  .deep-analytics-toggle {
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 12px 18px;
    background: transparent;
    border: none;
    cursor: pointer;
    font-size: 12.5px;
    font-weight: 600;
    color: var(--text-secondary);
    transition: background 0.15s ease;
  }

  .deep-analytics-toggle:hover {
    background: var(--surface-card-subtle, rgba(0,0,0,0.02));
    color: var(--text-primary);
  }

  .deep-toggle-left {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .deep-toggle-right {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .deep-toggle-hint {
    font-size: 11px;
    color: var(--text-tertiary, #94A3B8);
  }

  .deep-chevron {
    transition: transform 0.2s ease;
  }

  .deep-chevron.open {
    transform: rotate(180deg);
  }

  .deep-analytics-content {
    padding: 18px;
    border-top: 1px solid var(--surface-card-border, #E2E8F0);
    background: var(--surface-card-subtle, #FAFAFA);
  }

  /* ═══════════ MY WORKSPACE LENS STYLES ═══════════ */
  .personal-kpi-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
    gap: 16px;
  }

  .personal-kpi-card {
    padding: 16px 20px;
    border-radius: 12px;
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    box-shadow: var(--shadow-sm);
    display: flex;
    flex-direction: column;
    gap: 4px;
  }
  .personal-kpi-card.highlight-revision {
    border-left: 4px solid #EF4444;
  }
  .personal-kpi-card.highlight-active {
    border-left: 4px solid #0284C7;
  }
  .personal-kpi-card.highlight-review {
    border-left: 4px solid #F59E0B;
  }

  .pkpi-meta {
    display: flex;
    align-items: center;
    gap: 6px;
  }
  .pkpi-icon { font-size: 14px; }
  .pkpi-title { font-size: 12px; font-weight: 700; color: var(--text-secondary); text-transform: uppercase; }
  .pkpi-count { font-size: 32px; font-weight: 900; color: var(--text-primary); margin: 4px 0; }
  .pkpi-desc { font-size: 11.5px; color: var(--text-tertiary); }

  .my-workspace-grid {
    display: grid;
    grid-template-columns: 3fr 2fr;
    gap: 20px;
  }

  .project-queue-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  .queue-card {
    padding: 14px 16px;
    background: var(--surface-card-subtle, #F8FAFC);
    border: 1px solid var(--surface-card-border);
    border-radius: 8px;
    cursor: pointer;
    transition: all 0.14s;
    display: flex;
    flex-direction: column;
    gap: 6px;
  }
  .queue-card:hover {
    background: var(--surface-card);
    border-color: var(--brand-accent);
    transform: translateY(-1px);
    box-shadow: var(--shadow-sm);
  }

  .queue-card-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .queue-id-tag {
    font-family: monospace;
    font-size: 11.5px;
    font-weight: 800;
    color: var(--text-brand, #043388);
    background: var(--brand-tint, #EBF4FE);
    padding: 2px 6px;
    border-radius: 4px;
  }

  .status-pill {
    font-size: 10.5px;
    font-weight: 800;
    text-transform: uppercase;
    padding: 2px 7px;
    border-radius: 4px;
  }
  .status-revision { background: #FEF2F2; color: #B91C1C; border: 1px solid #FECACA; }
  .status-in-progress { background: #EBF4FE; color: #043388; border: 1px solid #BFDBFE; }
  .status-review { background: #FFFBEB; color: #B45309; border: 1px solid #FDE68A; }
  .status-approved { background: #ECFDF5; color: #047857; border: 1px solid #A7F3D0; }
  .status-done { background: #F1F5F9; color: #475569; border: 1px solid #CBD5E1; }

  .queue-title {
    font-size: 14px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0;
  }

  .queue-footer {
    display: flex;
    justify-content: space-between;
    align-items: center;
    flex-wrap: wrap;
    gap: 6px;
    font-size: 11.5px;
    color: var(--text-secondary);
    margin-top: 4px;
  }

  .queue-pts-pill {
    font-size: 10px;
    font-weight: 700;
    padding: 2px 7px;
    border-radius: 4px;
    background: rgba(168, 85, 247, 0.1);
    color: #C084FC;
    border: 1px solid rgba(168, 85, 247, 0.2);
    display: inline-flex;
    align-items: center;
  }

  .queue-action-link {
    font-weight: 700;
    color: var(--text-brand, #043388);
  }

  .personal-activity-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .personal-activity-card {
    padding: 10px 12px;
    background: var(--surface-card-subtle);
    border: 1px solid var(--surface-card-border);
    border-radius: 6px;
    cursor: pointer;
    transition: background 0.12s;
  }
  .personal-activity-card:hover {
    background: var(--surface-card);
    border-color: var(--brand-accent);
  }

  .pact-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 2px;
  }
  .pact-title { font-size: 12px; font-weight: 700; color: var(--text-primary); }
  .pact-actor { font-size: 10.5px; color: var(--text-tertiary); }
  .pact-message { font-size: 11.5px; color: var(--text-secondary); margin: 0; line-height: 1.35; }

  .empty-workspace-state {
    text-align: center;
    padding: 36px 16px;
    background: var(--bg-app);
    border-radius: 8px;
    border: 1px dashed var(--surface-card-border);
  }
  .empty-workspace-state .empty-icon { font-size: 28px; margin-bottom: 4px; }
  .empty-workspace-state .empty-title { font-size: 13.5px; font-weight: 700; color: var(--text-primary); margin: 0 0 2px 0; }
  .empty-workspace-state .empty-desc { font-size: 12px; color: var(--text-secondary); margin: 0; }

  @media (max-width: 1024px) {
    .commercial-pillars {
      grid-template-columns: repeat(2, 1fr);
    }
    .studio-distribution-grid,
    .sla-analytics-grid {
      grid-template-columns: repeat(2, 1fr);
    }
  }

  @media (max-width: 900px) {
    .analytics-grid, .my-workspace-grid, .studio-distribution-grid, .sla-analytics-grid {
      grid-template-columns: 1fr;
    }
  }

  @media (max-width: 600px) {
    .commercial-pillars {
      grid-template-columns: 1fr;
    }
  }
</style>
