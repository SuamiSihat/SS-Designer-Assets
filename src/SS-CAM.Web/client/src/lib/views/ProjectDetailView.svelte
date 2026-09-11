<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { projectStore } from '$lib/stores/projectStore.svelte';
  import { appState } from '$lib/stores/appState.svelte';
  import { ApiClient } from '$lib/services/api';
  import type { DeliverableItem, ProjectFrontmatter, ProjectComment } from '$lib/types';
  import FluentCard from '$lib/components/ui/FluentCard.svelte';
  import FluentButton from '$lib/components/ui/FluentButton.svelte';
  import FluentDialog from '$lib/components/ui/FluentDialog.svelte';
  import FluentIcons from '$lib/components/ui/FluentIcons.svelte';
  import DeliverableLightbox from '$lib/components/features/DeliverableLightbox.svelte';
  import ProjectComments from '$lib/components/features/ProjectComments.svelte';
  import VaultIngesterModal from '$lib/components/features/VaultIngesterModal.svelte';
  import ShareLinkModal from '$lib/components/features/ShareLinkModal.svelte';
  import ProjectVersionTimelineModal from '$lib/components/features/ProjectVersionTimelineModal.svelte';
  import MarkdownEditor from '$lib/components/markdown/MarkdownEditor.svelte';

  interface Props {
    projectId?: string;
  }

  let { projectId = '' }: Props = $props();

  // View state
  type MainCanvasView = 'brief' | 'copywriting' | 'deliverables' | 'direction';
  let activeCanvasView = $state<MainCanvasView>('brief');
  let inspectorTab = $state<'properties' | 'discussion'>('properties');
  let inspectorOpen = $state<boolean>(true);
  let subtasksOpen = $state<boolean>(true);
  let showIngesterModal = $state<boolean>(false);
  let showShareModal = $state<boolean>(false);
  let showTimelineModal = $state<boolean>(false);
  let showMoreMenu = $state<boolean>(false);

  // Deliverables & Lightbox
  let selectedDeliverable = $state<DeliverableItem | null>(null);
  let lightboxOpen = $state<boolean>(false);
  let isSubmittingDecision = $state<boolean>(false);

  // Deletion State
  let showDeleteModal = $state<boolean>(false);
  let isDeleting = $state<boolean>(false);
  let isSavingDirection = $state<boolean>(false);
  let isEditingDirection = $state<boolean>(false);

  const isAdminUser = $derived.by(() => {
    const role = (appState.currentUser?.role || '').toLowerCase();
    return role.includes('admin') || role.includes('director') || role.includes('lead') || role.includes('manager') || role.includes('executive');
  });

  async function handleDeleteProject() {
    if (!p) return;
    isDeleting = true;
    try {
      await ApiClient.deleteProject(p.id);
      appState.addToast(`Project ${p.jobId || p.title} and subfolders deleted successfully.`, 'success');
      showDeleteModal = false;
      await projectStore.loadProjects();
      await projectStore.loadDashboard();
      appState.navigate('projects');
    } catch (err: any) {
      appState.addToast(`Failed to delete project: ${err.message}`, 'error');
    } finally {
      isDeleting = false;
    }
  }

  // Markdown Bodies & Frontmatter
  let currentReadmeBody = $state<string>('');
  let currentCopyBody = $state<string>('');
  let copyFilePath = $state<string>('');
  let copyStats = $state<{ words: number; chars: number; readingTimeMin: number }>({ words: 0, chars: 0, readingTimeMin: 1 });
  let currentFrontmatter = $state<ProjectFrontmatter>({});
  let projectComments = $state<ProjectComment[]>([]);
  let isLoadingCopy = $state<boolean>(false);
  let lastLoadedHash = $state<string | null>(null);

  const p = $derived(projectStore.selectedProject);

  // Real-time synchronization when SSE or store updates selectedProject
  $effect(() => {
    const proj = projectStore.selectedProject;
    if (proj) {
      const vHash = `${proj.id || ''}_${proj.versionHash || ''}_${proj.status || ''}_${proj.priority || ''}_${(proj.subtasks || []).length}`;
      if (vHash !== lastLoadedHash) {
        lastLoadedHash = vHash;
        currentReadmeBody = proj.readmeBody || proj.briefMarkdown || '';
        currentFrontmatter = {
          status: proj.status,
          designer: proj.designer,
          brand: proj.brand,
          manager: proj.manager,
          department: proj.department,
          deadline: proj.deadline,
          priority: proj.priority,
          tags: proj.tags || [],
          creative_direction: proj.creativeDirection || {},
          subtasks: Array.isArray((proj as any).subtasks) ? [...(proj as any).subtasks] : []
        };
        projectComments = (proj as any).comments || [];
      }
    }
  });

  // Staff Roster & Manager Selection
  interface StaffMember {
    staffId: string;
    username: string;
    name: string;
    role: string;
    department?: string;
    avatar?: string;
    avatarColor?: string;
  }
  let staffList = $state<StaffMember[]>([]);
  let isUpdatingManager = $state<boolean>(false);
  let selectedManager = $state<string>('Unassigned');

  function getInitials(name?: string): string {
    if (!name) return 'DS';
    const trimmed = name.trim();
    const parts = trimmed.split(/\s+/);
    if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    return trimmed.substring(0, 2).toUpperCase();
  }

  // Filter to strictly managerial and leadership roles
  const managerList = $derived.by(() => {
    return staffList.filter(staff => {
      const roleStr = (staff.role || '').toLowerCase();
      return (
        roleStr.includes('manager') ||
        roleStr.includes('director') ||
        roleStr.includes('admin') ||
        roleStr.includes('head') ||
        roleStr.includes('lead') ||
        roleStr.includes('ceo') ||
        roleStr.includes('executive')
      );
    });
  });

  const designerInfo = $derived.by(() => {
    if (!p) return null;
    const key = (p.designer || p.designerName || '').toLowerCase();
    if (!key || key === 'unassigned') return null;
    return staffList.find(s => 
      (s.username && s.username.toLowerCase() === key) ||
      (s.name && s.name.toLowerCase() === key) ||
      (s.staffId && s.staffId.toLowerCase() === key)
    ) || null;
  });

  const designerAvatarSrc = $derived.by(() => {
    return designerInfo?.avatarUrl || designerInfo?.avatar || (typeof localStorage !== 'undefined' ? (localStorage.getItem(`ss_cam_avatar_${designerInfo?.staffId}`) || (appState.currentUser?.staffId === designerInfo?.staffId ? (appState.currentUser.avatarUrl || appState.currentUser.avatar || '') : '')) : '');
  });

  const managerInfo = $derived.by(() => {
    const key = (selectedManager || (p ? p.manager : '') || '').toLowerCase();
    if (!key || key === 'unassigned') return null;
    return staffList.find(s => 
      (s.name && s.name.toLowerCase() === key) ||
      (s.username && s.username.toLowerCase() === key) ||
      (s.staffId && s.staffId.toLowerCase() === key)
    ) || null;
  });

  const managerAvatarSrc = $derived.by(() => {
    return managerInfo?.avatarUrl || managerInfo?.avatar || (typeof localStorage !== 'undefined' ? (localStorage.getItem(`ss_cam_avatar_${managerInfo?.staffId}`) || (appState.currentUser?.staffId === managerInfo?.staffId ? (appState.currentUser.avatarUrl || appState.currentUser.avatar || '') : '')) : '');
  });

  $effect(() => {
    if (p && p.manager && p.manager !== 'Unassigned') {
      selectedManager = p.manager;
    } else {
      selectedManager = 'Unassigned';
    }
  });

  async function loadStaffList() {
    try {
      const res = await ApiClient.getStaffAccounts();
      if (res && res.users) {
        staffList = res.users;
      }
    } catch {
      try {
        const res2 = await ApiClient.getStaffRoster();
        if (res2 && res2.roster) {
          staffList = res2.roster;
        }
      } catch (e) {
        console.warn('[ProjectDetailView] loadStaffList error:', e);
      }
    }
  }

  async function handleManagerChange(newManager: string) {
    if (!p) return;
    isUpdatingManager = true;
    selectedManager = newManager;
    try {
      await ApiClient.updateProject(p.id, { manager: newManager });
      currentFrontmatter.manager = newManager;
      if (projectStore.selectedProject) {
        projectStore.selectedProject.manager = newManager;
      }
      appState.addToast(`Reviewer updated to ${newManager === 'Unassigned' ? 'Unassigned' : newManager}`, 'success');
      await projectStore.loadProjectDetail(p.id);
    } catch (err: any) {
      appState.addToast(`Failed to update reviewer: ${err.message}`, 'error');
      selectedManager = p.manager || 'Unassigned';
    } finally {
      isUpdatingManager = false;
    }
  }

  // ─── Subtask & Deliverables Management ──────────────────────────────
  let showSubtaskModal = $state<boolean>(false);
  let isSavingSubtask = $state<boolean>(false);
  let editingSubtaskId = $state<string | null>(null);
  let subtaskForm = $state<{
    id: string;
    name: string;
    type: string;
    specs: string;
    weight: number;
    status: string;
  }>({
    id: '',
    name: '',
    type: 'master_video',
    specs: '',
    weight: 1.0,
    status: 'draft'
  });

  const subtaskStats = $derived.by(() => {
    const list = Array.isArray(currentFrontmatter.subtasks) && currentFrontmatter.subtasks.length > 0
      ? currentFrontmatter.subtasks
      : (Array.isArray(p?.subtasks) ? p.subtasks : []);
    const total = list.length;
    const completed = list.filter((s: any) => ['approved', 'done', 'completed'].includes((s.status || '').toLowerCase())).length;
    const inProgress = list.filter((s: any) => ['in-progress', 'in_progress', 'progress'].includes((s.status || '').toLowerCase())).length;
    const totalWeight = list.reduce((sum: number, s: any) => sum + (typeof s.weight === 'number' ? s.weight : 1.0), 0);
    const percent = total > 0 ? Math.round((completed / total) * 100) : 0;
    return { list, total, completed, inProgress, totalWeight: Math.round(totalWeight * 10) / 10, percent };
  });

  function getNextSubtaskId(): string {
    const list = Array.isArray(currentFrontmatter.subtasks) ? currentFrontmatter.subtasks : [];
    const num = list.length + 1;
    const preset = String(p?.preset || '').toLowerCase();
    const isVideo = preset.includes('video') || (p?.id || '').includes('V');
    const prefix = isVideo ? 'V' : 'ST-';
    return `${prefix}${String(num).padStart(2, '0')}`;
  }

  function openAddSubtaskModal() {
    const preset = String(p?.preset || '').toLowerCase();
    const isVideo = preset.includes('video') || (p?.id || '').includes('V');
    const nextId = getNextSubtaskId();
    editingSubtaskId = null;
    subtaskForm = {
      id: nextId,
      name: isVideo ? (nextId === 'V01' ? 'Master 60s Cut' : 'Hook Variation') : (nextId === 'ST-01' ? 'Key Visual Master' : 'Resize Variation'),
      type: isVideo ? (nextId === 'V01' ? 'master_video' : 'hook_variation') : (nextId === 'ST-01' ? 'key_visual' : 'resize'),
      specs: isVideo ? '9:16, 1080x1920' : '1:1, 1080x1080',
      weight: isVideo && nextId === 'V01' ? 2.0 : 1.0,
      status: 'draft'
    };
    showSubtaskModal = true;
  }

  function openEditSubtaskModal(st: any) {
    editingSubtaskId = st.id;
    subtaskForm = {
      id: st.id || '',
      name: st.name || '',
      type: st.type || 'master_video',
      specs: st.specs || '',
      weight: typeof st.weight === 'number' ? st.weight : 1.0,
      status: st.status || 'draft'
    };
    showSubtaskModal = true;
  }

  async function handleSaveSubtask() {
    if (!p) return;
    const name = subtaskForm.name.trim();
    if (!name) {
      appState.addToast('Deliverable / subtask name is required', 'warning');
      return;
    }
    const id = (subtaskForm.id.trim() || getNextSubtaskId()).toUpperCase();

    const currentList = Array.isArray(currentFrontmatter.subtasks) ? [...currentFrontmatter.subtasks] : [];
    let updatedList: any[];

    if (editingSubtaskId) {
      updatedList = currentList.map(st => {
        if (st.id === editingSubtaskId) {
          return {
            ...st,
            id,
            name,
            type: subtaskForm.type,
            specs: subtaskForm.specs.trim(),
            weight: Number(subtaskForm.weight) || 1.0,
            status: subtaskForm.status
          };
        }
        return st;
      });
    } else {
      if (currentList.some(st => String(st.id).toUpperCase() === id)) {
        appState.addToast(`Subtask with ID "${id}" already exists. Please choose a unique ID.`, 'warning');
        return;
      }
      updatedList = [
        ...currentList,
        {
          id,
          name,
          type: subtaskForm.type,
          specs: subtaskForm.specs.trim(),
          weight: Number(subtaskForm.weight) || 1.0,
          status: subtaskForm.status
        }
      ];
    }

    isSavingSubtask = true;
    try {
      await ApiClient.updateProject(p.id, { subtasks: updatedList });
      currentFrontmatter.subtasks = updatedList;
      if (projectStore.selectedProject) {
        projectStore.selectedProject.subtasks = updatedList;
      }
      showSubtaskModal = false;
      appState.addToast(editingSubtaskId ? 'Subtask updated successfully' : 'Deliverable / subtask added', 'success');
    } catch (err: any) {
      appState.addToast(`Failed to save subtask: ${err.message}`, 'error');
    } finally {
      isSavingSubtask = false;
    }
  }

  async function deleteSubtask(subtaskId: string) {
    if (!p) return;
    const currentList = Array.isArray(currentFrontmatter.subtasks) ? currentFrontmatter.subtasks : [];
    const updatedList = currentList.filter(st => st.id !== subtaskId);

    try {
      await ApiClient.updateProject(p.id, { subtasks: updatedList });
      currentFrontmatter.subtasks = updatedList;
      if (projectStore.selectedProject) {
        projectStore.selectedProject.subtasks = updatedList;
      }
      appState.addToast('Deliverable / subtask removed', 'success');
    } catch (err: any) {
      appState.addToast(`Failed to remove subtask: ${err.message}`, 'error');
    }
  }

  async function generatePresetSubtasks() {
    if (!p) return;
    const preset = String(p.preset || '').toLowerCase();
    const isVideo = preset.includes('video') || (p.id || '').includes('V');
    let generated: any[] = [];

    if (isVideo) {
      generated = [
        { id: 'V01', name: 'Master 60s Cut', type: 'master_video', specs: '9:16, 1080x1920', weight: 2.0, status: 'draft' },
        { id: 'V02', name: 'Hook Variation A', type: 'hook_variation', specs: '9:16, 1080x1920', weight: 0.5, status: 'draft' },
        { id: 'V03', name: 'Hook Variation B', type: 'hook_variation', specs: '9:16, 1080x1920', weight: 0.5, status: 'draft' }
      ];
    } else {
      generated = [
        { id: 'KV01', name: 'Key Visual Master', type: 'key_visual', specs: '1:1, 1080x1080', weight: 1.5, status: 'draft' },
        { id: 'RZ01', name: 'Story / Reels Resize', type: 'resize', specs: '9:16, 1080x1920', weight: 0.5, status: 'draft' },
        { id: 'RZ02', name: 'Landscape Banner Resize', type: 'resize', specs: '16:9, 1920x1080', weight: 0.5, status: 'draft' }
      ];
    }

    const currentList = Array.isArray(currentFrontmatter.subtasks) ? currentFrontmatter.subtasks : [];
    const merged = [...currentList];
    for (const g of generated) {
      if (!merged.some(m => m.id === g.id)) {
        merged.push(g);
      }
    }

    try {
      await ApiClient.updateProject(p.id, { subtasks: merged });
      currentFrontmatter.subtasks = merged;
      if (projectStore.selectedProject) {
        projectStore.selectedProject.subtasks = merged;
      }
      appState.addToast(`Generated ${generated.length} standard deliverable subtasks!`, 'success');
    } catch (err: any) {
      appState.addToast(`Failed to generate subtasks: ${err.message}`, 'error');
    }
  }

  async function cycleSubtaskStatus(subtaskId: string) {
    const p = projectStore.selectedProject;
    if (!p || !currentFrontmatter.subtasks) return;

    const nextStatusMap: Record<string, string> = {
      'draft': 'in-progress',
      'in-progress': 'done',
      'progress': 'done',
      'in_progress': 'done',
      'done': 'draft',
      'approved': 'draft',
      'completed': 'draft'
    };

    const updatedSubtasks = currentFrontmatter.subtasks.map((st: any) => {
      if (st.id === subtaskId) {
        const cur = (st.status || 'draft').toLowerCase();
        const next = nextStatusMap[cur] || 'in-progress';
        return { ...st, status: next };
      }
      return st;
    });

    currentFrontmatter.subtasks = updatedSubtasks;
    try {
      await ApiClient.updateProject(p.id, { subtasks: updatedSubtasks });
      if (projectStore.selectedProject) {
        projectStore.selectedProject.subtasks = updatedSubtasks;
      }
      appState.addToast('Subtask status updated', 'success');
    } catch (err: any) {
      appState.addToast(`Failed to update subtask: ${err.message}`, 'error');
      await projectStore.loadProjectDetail(p.id);
    }
  }

  let targetId = $derived(projectId || appState.routeParams.id || '');
  let lastLoadedId = $state<string | null>(null);

  onMount(() => {
    loadStaffList();
  });

  onDestroy(() => {
    // Guard: if the component unmounts mid-load (e.g. user navigates away),
    // ensure loadingDetail is reset so the next visit starts clean.
    projectStore.loadingDetail = false;
  });

  $effect(() => {
    const id = targetId;
    if (id && id !== lastLoadedId) {
      lastLoadedId = id;
      loadProject(id);
    }
  });

  async function loadProject(id: string) {
    const TIMEOUT_MS = 15000;
    const timeoutPromise = new Promise<never>((_, reject) =>
      setTimeout(() => reject(new Error('Project load timed out after 15s. The NAS may be slow — please try again.')), TIMEOUT_MS)
    );
    try {
      await Promise.race([projectStore.loadProjectDetail(id), timeoutPromise]);
    } catch (err: any) {
      // loadProjectDetail already handles its own errors via toast.
      // Timeout errors need explicit cleanup.
      if (err.message?.includes('timed out')) {
        projectStore.loadingDetail = false;
        appState.addToast(err.message, 'error');
      }
    }
    if (projectStore.selectedProject) {
      currentReadmeBody = projectStore.selectedProject.readmeBody || projectStore.selectedProject.briefMarkdown || '';
      currentFrontmatter = {
        status: projectStore.selectedProject.status,
        designer: projectStore.selectedProject.designer,
        brand: projectStore.selectedProject.brand,
        manager: projectStore.selectedProject.manager,
        department: projectStore.selectedProject.department,
        deadline: projectStore.selectedProject.deadline,
        priority: projectStore.selectedProject.priority,
        tags: projectStore.selectedProject.tags || [],
        creative_direction: projectStore.selectedProject.creativeDirection || {},
        subtasks: Array.isArray((projectStore.selectedProject as any).subtasks) ? [...(projectStore.selectedProject as any).subtasks] : []
      };
      projectComments = (projectStore.selectedProject as any).comments || [];
    }

    // Preload Copywriting Studio file
    await loadCopywriting(id);
  }

  async function loadCopywriting(id: string) {
    isLoadingCopy = true;
    try {
      const res = await ApiClient.getCopywritingMarkdown(id);
      if (res && res.copywriting) {
        currentCopyBody = res.copywriting.body || '';
        copyFilePath = res.copywriting.filePath || '';
        copyStats = res.copywriting.stats || { words: 0, chars: 0, readingTimeMin: 1 };
      }
    } catch (e) {
      console.warn('[ProjectDetailView] loadCopywriting warning:', e);
    } finally {
      isLoadingCopy = false;
    }
  }

  async function saveMarkdownBrief(newBody: string) {
    if (!p) return;
    try {
      const hash = p.versionHash || null;
      const res = await ApiClient.updateBrief(p.id, newBody, hash);
      appState.addToast('Creative Brief saved to Synology NAS (README.md)', 'success');
      currentReadmeBody = newBody;
      if (projectStore.selectedProject) {
        projectStore.selectedProject.readmeBody = newBody;
        projectStore.selectedProject.briefMarkdown = newBody;
        if (res?.versionHash) {
          projectStore.selectedProject.versionHash = res.versionHash;
          lastLoadedHash = `${projectStore.selectedProject.id || ''}_${res.versionHash}_${projectStore.selectedProject.status || ''}_${projectStore.selectedProject.priority || ''}`;
        }
      }
    } catch (err: any) {
      appState.addToast(`Failed to save brief: ${err.message}`, 'error');
    }
  }

  async function saveCopywritingMarkdown(newBody: string) {
    if (!p) return;
    try {
      const res = await ApiClient.updateCopywritingMarkdown(p.id, newBody);
      appState.addToast('Copywriting saved to NAS (03_COPYWRITING/COPY.md)', 'success');
      currentCopyBody = newBody;
      if (res.copywriting?.stats) {
        copyStats = res.copywriting.stats;
      }
    } catch (err: any) {
      appState.addToast(`Failed to save copy: ${err.message}`, 'error');
    }
  }

  function extractColorChips(paletteText?: string): string[] {
    if (!paletteText) return [];
    const hexMatches = paletteText.match(/#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b/g);
    return hexMatches || [];
  }

  async function saveCreativeDirection() {
    if (!p) return;
    isSavingDirection = true;
    try {
      const res = await ApiClient.updateCreativeDirection(p.id, currentFrontmatter.creative_direction || {});
      if (res?.creativeDirection && p) {
        p.creativeDirection = res.creativeDirection;
      }
      appState.addToast('Creative direction saved to README.md', 'success');
      isEditingDirection = false;
    } catch (err: any) {
      appState.addToast(`Failed to save creative direction: ${err.message}`, 'error');
    } finally {
      isSavingDirection = false;
    }
  }

  async function updateStatus(newStatus: string) {
    if (!p) return;
    try {
      await ApiClient.updateProject(p.id, { ...currentFrontmatter, status: newStatus as any });
      currentFrontmatter.status = newStatus as any;
      if (projectStore.selectedProject) {
        projectStore.selectedProject.status = newStatus as any;
      }
      appState.addToast(`Project status updated to ${newStatus.toUpperCase()}`, 'info');
    } catch (err: any) {
      appState.addToast(`Failed to update status: ${err.message}`, 'error');
    }
  }

  async function handleQuickDecision(decision: 'approved' | 'revision_requested') {
    if (!p || isSubmittingDecision) return;
    isSubmittingDecision = true;
    try {
      await ApiClient.submitDecision(p.id, {
        decision,
        comment: decision === 'approved' ? 'Formal manager approval via portal.' : 'Revisions requested on creative deliverables.'
      });
      appState.addToast(
        decision === 'approved' ? 'Project Approved & Signed Off' : 'Revision Requested recorded in audit log',
        decision === 'approved' ? 'success' : 'warning'
      );
      await loadProject(p.id);
    } catch (err: any) {
      appState.addToast(`Decision failed: ${err.message}`, 'error');
    } finally {
      isSubmittingDecision = false;
    }
  }

  function openLightbox(d: DeliverableItem) {
    selectedDeliverable = {
      ...d,
      project: d.project || {
        jobId: p?.jobId || p?.id || '',
        title: p?.title || '',
        brand: p?.brand || '',
        designer: p?.designer || '',
        status: p?.status || '',
        priority: p?.priority || '',
        deadline: p?.deadline || ''
      }
    };
    lightboxOpen = true;
  }

  function getCompanyFullName(code?: string): string {
    if (!code) return 'SuamiSihat Holding Sdn Bhd';
    const c = code.toUpperCase().trim();
    if (c === 'SSH' || c === 'SS') return 'SuamiSihat Holding Sdn Bhd';
    if (c === 'SSC') return 'SuamiSihat Healthcare Sdn Bhd';
    if (c === 'SSW') return 'SuamiSihat Wellness Sdn Bhd';
    if (c === 'SSE' || c === 'SSL') return 'SuamiSihat Ecommerce Sdn Bhd';
    if (c === 'SST') return 'SuamiSihat Technology Sdn Bhd';
    return `${code} Operating Unit`;
  }
</script>

<svelte:window onclick={() => { showMoreMenu = false; }} />

<div class="clickup-task-container">
  {#if projectStore.loadingDetail}
    <div class="loading-state">
      <div class="loading-spinner"></div>
      <p>Loading project workspace from Synology NAS…</p>
    </div>
  {:else if !p}
    <div class="empty-state">
      <h3>Project not found</h3>
      <p>The requested creative directory does not exist or has been moved.</p>
      <FluentButton appearance="primary" onclick={() => appState.navigate('projects')}>
        Return to Catalog
      </FluentButton>
    </div>
  {:else}
    <!-- ═══════════ MINIMAL & MODERN COMMAND HEADER ═══════════ -->
    <header class="task-command-header">
      <div class="task-breadcrumbs">
        <span class="crumb-link" onclick={() => appState.navigate('projects')}>Projects</span>
        <span class="crumb-sep">/</span>
        <span class="crumb-tag">{p.brand || 'SS'}</span>
        <span class="crumb-sep">/</span>
        <span class="crumb-current">{p.jobId || p.id}</span>
      </div>

      <div class="task-headline-row">
        <div class="headline-left">
          <div class="job-badge">{p.jobId || p.id}</div>
          <h1 class="task-title">{p.title}</h1>
        </div>

        <div class="headline-actions">
          <!-- Status Dropdown Pill -->
          <div class="status-selector-wrap">
            <select
              class="status-select status-{currentFrontmatter.status || 'review'}"
              value={currentFrontmatter.status || 'review'}
              onchange={(e) => updateStatus((e.target as HTMLSelectElement).value)}
            >
              <option value="backlog">Backlog</option>
              <option value="in-progress">In Progress</option>
              <option value="review">In Review</option>
              <option value="revision">Revision Required</option>
              <option value="approved">Approved</option>
              <option value="done">Completed</option>
            </select>
          </div>

          <!-- Reviewer Decision Actions -->
          {#if (currentFrontmatter.status || '').toLowerCase() === 'approved'}
            <div class="approved-tag">
              <FluentIcons name="checkCircle" size={13} color="#10B981" />
              <span>Approved &amp; Ready</span>
            </div>
            <FluentButton
              appearance="secondary"
              size="sm"
              loading={isSubmittingDecision}
              onclick={() => handleQuickDecision('revision_requested')}
            >
              <FluentIcons name="warning" size={13} color="#F59E0B" />
              <span style="margin-left: 5px;">Request Revision</span>
            </FluentButton>
          {:else}
            <FluentButton
              appearance="primary"
              size="sm"
              loading={isSubmittingDecision}
              onclick={() => handleQuickDecision('approved')}
            >
              <FluentIcons name="checkCircle" size={13} />
              <span style="margin-left: 5px;">Sign-Off</span>
            </FluentButton>

            <FluentButton
              appearance="secondary"
              size="sm"
              loading={isSubmittingDecision}
              onclick={() => handleQuickDecision('revision_requested')}
            >
              <FluentIcons name="warning" size={13} color="#F59E0B" />
              <span style="margin-left: 5px;">Request Revision</span>
            </FluentButton>
          {/if}

          <!-- Clean Action Outlines Group -->
          <div class="action-btn-group">
            <button
              class="action-btn-clean"
              onclick={() => (showIngesterModal = true)}
              title="Ingest raw files or deliverables to NAS"
            >
              <FluentIcons name="upload" size={13} />
              <span>Ingest</span>
            </button>

            <button
              class="action-btn-clean"
              onclick={() => (showShareModal = true)}
              title="Generate Client Review Link"
            >
              <FluentIcons name="link" size={13} />
              <span>Share</span>
            </button>

            <a
              href={`/api/projects/${encodeURIComponent(p.id)}/export`}
              download
              class="action-btn-clean"
              title="Export Handover ZIP"
            >
              <FluentIcons name="download" size={13} />
              <span>Export ZIP</span>
            </a>

            <!-- More Actions Dropdown -->
            <div class="more-menu-wrapper" onclick={(e) => e.stopPropagation()}>
              <button
                class="action-btn-clean icon-only"
                class:active={showMoreMenu}
                onclick={() => (showMoreMenu = !showMoreMenu)}
                title="More Actions"
                aria-label="More Actions"
              >
                <span style="font-weight: 800; font-size: 14px; line-height: 1;">···</span>
              </button>

              {#if showMoreMenu}
                <div class="more-dropdown-menu">
                  <a
                    href={`sscam://open?id=${encodeURIComponent(p.jobId || p.id)}`}
                    class="more-dropdown-item"
                    onclick={() => (showMoreMenu = false)}
                  >
                    <FluentIcons name="desktop" size={13} />
                    <span>Open in Desktop</span>
                  </a>

                  <button
                    class="more-dropdown-item"
                    onclick={() => { showTimelineModal = true; showMoreMenu = false; }}
                  >
                    <FluentIcons name="timeline" size={13} />
                    <span>Timeline &amp; Rollback</span>
                  </button>

                  {#if isAdminUser}
                    <div class="more-divider"></div>
                    <button
                      class="more-dropdown-item item-danger"
                      onclick={() => { showDeleteModal = true; showMoreMenu = false; }}
                    >
                      <FluentIcons name="delete" size={13} color="#EF4444" />
                      <span>Delete Project</span>
                    </button>
                  {/if}
                </div>
              {/if}
            </div>

            <!-- Toggle Properties Panel Button -->
            <button
              class="action-btn-clean icon-only"
              class:active={inspectorOpen}
              onclick={() => (inspectorOpen = !inspectorOpen)}
              title={inspectorOpen ? "Hide Properties Panel" : "Show Properties Panel"}
              aria-label="Toggle Properties Panel"
            >
              <svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor">
                <path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14zM15 7h2v10h-2V7z"/>
              </svg>
            </button>

            <!-- Toggle Subtasks Column Button -->
            <button
              class="action-btn-clean icon-only"
              class:active={subtasksOpen}
              onclick={() => (subtasksOpen = !subtasksOpen)}
              title={subtasksOpen ? "Hide Subtasks Column" : "Show Subtasks Column"}
              aria-label="Toggle Subtasks Column"
            >
              <FluentIcons name="checkbox" size={14} />
            </button>
          </div>
        </div>
      </div>

      <!-- Segmented View Switcher -->
      <div class="canvas-segmented-nav">
        <button
          class="canvas-nav-item"
          class:active={activeCanvasView === 'brief'}
          onclick={() => (activeCanvasView = 'brief')}
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/></svg>
          <span>Creative Brief</span>
        </button>

        <button
          class="canvas-nav-item"
          class:active={activeCanvasView === 'copywriting'}
          onclick={() => (activeCanvasView = 'copywriting')}
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/></svg>
          <span>Copywriting Studio</span>
          <span class="view-chip">{copyStats.words}w</span>
        </button>

        <button
          class="canvas-nav-item"
          class:active={activeCanvasView === 'deliverables'}
          onclick={() => (activeCanvasView = 'deliverables')}
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-2 10h-4v4h-2v-4H7v-2h4V7h2v4h4v2z"/></svg>
          <span>Deliverables &amp; Subtasks</span>
          {#if subtaskStats.total > 0}
            <span class="view-chip">{subtaskStats.completed}/{subtaskStats.total} Done</span>
          {:else if projectStore.activeDeliverables.length > 0}
            <span class="view-chip">{projectStore.activeDeliverables.length} files</span>
          {/if}
        </button>

        <button
          class="canvas-nav-item"
          class:active={activeCanvasView === 'direction'}
          onclick={() => (activeCanvasView = 'direction')}
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 3c-4.97 0-9 4.03-9 9 0 2.12.74 4.07 1.97 5.61L4.35 19.4c-.39.39-.39 1.02 0 1.41.39.39 1.02.39 1.41 0l1.9-1.9C9.36 19.64 10.63 20 12 20c4.97 0 9-4.03 9-9s-4.03-9-9-9zm0 15c-3.31 0-6-2.69-6-6s2.69-6 6-6 6 2.69 6 6-2.69 6-6 6z"/></svg>
          <span>Creative Direction</span>
        </button>
      </div>
    </header>

    <!-- ═══════════ 3-COLUMN WORKSPACE BODY ═══════════ -->
    <div
      class="task-workspace-grid"
      class:inspector-closed={!inspectorOpen}
      class:subtasks-closed={!subtasksOpen}
    >
      <!-- ─── LEFT/MAIN CANVAS AREA (68%) ─── -->
      <main class="main-document-canvas">
        {#if activeCanvasView === 'brief'}
          <!-- Creative Brief Markdown Editor with full toolbar -->
          <MarkdownEditor
            title="README.md"
            saveLabel="Save Brief to NAS"
            bind:value={currentReadmeBody}
            onSave={saveMarkdownBrief}
          />
        {:else if activeCanvasView === 'copywriting'}
          <!-- Dedicated Copywriting Studio Markdown Editor -->
          {#if isLoadingCopy}
            <div class="loading-state">Loading 03_COPYWRITING/COPY.md from NAS…</div>
          {:else}
            <MarkdownEditor
              title="03_COPYWRITING / COPY.md"
              saveLabel="Save Copy to NAS"
              bind:value={currentCopyBody}
              onSave={saveCopywritingMarkdown}
            />
          {/if}
        {:else if activeCanvasView === 'deliverables'}
          <!-- Deliverables & Subtasks Section -->
          <div class="deliverables-gallery-container">
            <div class="subtasks-container-card">
              <div class="subtasks-card-header">
                <div class="subtasks-header-left">
                  <FluentIcons name="checkbox" size={20} color="var(--brand-primary, #0078D4)" />
                  <div>
                    <h3 class="subtasks-section-title">Deliverables &amp; Subtasks Breakdown</h3>
                    <span class="subtasks-section-subtitle">
                      {#if subtaskStats.total > 0}
                        {subtaskStats.completed}/{subtaskStats.total} Completed ({subtaskStats.percent}%) • Total Weight: {subtaskStats.totalWeight} pts
                      {:else}
                        Track milestone deliverables, format specs, and point weights for this job
                      {/if}
                    </span>
                  </div>
                </div>
                <div class="subtasks-header-actions">
                  {#if subtaskStats.total === 0}
                    <button type="button" class="btn-subtask-action secondary" onclick={generatePresetSubtasks}>
                      ⚡ Auto-generate Presets
                    </button>
                  {/if}
                  <button type="button" class="btn-subtask-action primary" onclick={openAddSubtaskModal}>
                    <FluentIcons name="add" size={13} />
                    <span>Add Deliverable</span>
                  </button>
                </div>
              </div>

              {#if subtaskStats.total > 0}
                <div class="subtasks-progress-track large">
                  <div class="subtasks-progress-fill" style="width: {subtaskStats.percent}%;"></div>
                </div>

                <div class="subtasks-cards-grid">
                  {#each subtaskStats.list as st}
                    {@const isDone = ['approved', 'done', 'completed'].includes((st.status || '').toLowerCase())}
                    {@const isProgress = ['in-progress', 'in_progress', 'progress'].includes((st.status || '').toLowerCase())}
                    <div class="subtask-card-item" class:is-done={isDone}>
                      <div class="subtask-card-main">
                        <div class="subtask-card-topline">
                          <span class="st-id-tag">{st.id || 'ST'}</span>
                          <span class="st-pts-badge">{st.weight || 1} pts</span>
                        </div>
                        <h4 class="st-card-name" title={st.name}>{st.name}</h4>
                        <div class="st-card-meta">
                          {#if st.type}
                            <span class="st-meta-pill type-pill">{String(st.type).replace('_', ' ')}</span>
                          {/if}
                          {#if st.specs}
                            <span class="st-meta-pill specs-pill">{st.specs}</span>
                          {/if}
                        </div>
                      </div>

                      <div class="subtask-card-footer">
                        <button
                          type="button"
                          class="st-status-btn-full"
                          class:status-done={isDone}
                          class:status-progress={isProgress}
                          class:status-draft={!isDone && !isProgress}
                          onclick={() => cycleSubtaskStatus(st.id)}
                          title="Click to cycle status: Draft ➔ In Progress ➔ Done"
                        >
                          {isDone ? '✓ Completed' : (isProgress ? '⏳ In Progress' : 'Draft')}
                        </button>
                        <div class="st-actions-row">
                          <button
                            type="button"
                            class="st-icon-action"
                            onclick={() => openEditSubtaskModal(st)}
                            title="Edit Deliverable"
                            aria-label="Edit Deliverable"
                          >
                            <FluentIcons name="edit" size={12} />
                          </button>
                          <button
                            type="button"
                            class="st-icon-action danger"
                            onclick={() => deleteSubtask(st.id)}
                            title="Remove Deliverable"
                            aria-label="Remove Deliverable"
                          >
                            <FluentIcons name="dismiss" size={12} />
                          </button>
                        </div>
                      </div>
                    </div>
                  {/each}
                </div>
              {:else}
                <div class="subtasks-empty-hero">
                  <div class="empty-icon-wrap">
                    <FluentIcons name="checkbox" size={32} color="var(--text-tertiary, #94A3B8)" />
                  </div>
                  <h4>No Deliverables or Subtasks Configured</h4>
                  <p>Break down this creative job into trackable outputs (e.g. Master Video 9:16, Story Resizes, Hook variations) with milestone statuses and capacity point weights.</p>
                  <div class="empty-hero-actions">
                    <button type="button" class="btn-subtask-action primary" onclick={openAddSubtaskModal}>
                      <FluentIcons name="add" size={13} />
                      <span>Add Deliverable</span>
                    </button>
                    <button type="button" class="btn-subtask-action secondary" onclick={generatePresetSubtasks}>
                      ⚡ Generate Standard Set from Preset
                    </button>
                  </div>
                </div>
              {/if}
            </div>

            <div class="gallery-header" style="margin-top: 28px;">
              <div class="gallery-title-group">
                <h3>Production Output Assets</h3>
                <span class="gallery-subtitle">Found in <code>05_DELIVERABLES/</code> or <code>04_Production/</code> on Synology NAS</span>
              </div>
            </div>

            {#if projectStore.activeDeliverables.length === 0}
              <div class="empty-gallery">
                <div class="empty-icon-box">
                  <FluentIcons name="folder" size={36} color="rgba(255,255,255,0.2)" />
                </div>
                <p>No output media found in <code>05_DELIVERABLES</code> or <code>04_Production</code>.</p>
                <p class="empty-sub">Export output media (PNG, JPG, MP4, PDF) from Photoshop, Illustrator, or Blender into the project folder.</p>
              </div>
            {:else}
              <div class="deliverables-grid">
                {#each projectStore.activeDeliverables as d}
                  <!-- svelte-ignore a11y_click_events_have_key_events -->
                  <!-- svelte-ignore a11y_no_static_element_interactions -->
                  <div class="deliverable-card" onclick={() => openLightbox(d)}>
                    <div class="del-preview-box">
                      {#if d.isImage || d.previewType === 'image'}
                        <img src={d.previewUrl} alt={d.filename} loading="lazy" />
                      {:else if d.isVideo || d.previewType === 'video'}
                        <div class="del-video-thumb">
                          <!-- svelte-ignore a11y_media_has_caption -->
                          <video src={d.streamUrl || d.previewUrl} preload="metadata" muted playsinline></video>
                          <span class="del-play-badge">
                            <FluentIcons name="video" size={12} />
                            <span style="margin-left: 4px;">VIDEO</span>
                          </span>
                        </div>
                      {:else if d.isPdf || d.previewType === 'pdf'}
                        <div class="del-pdf-thumb">
                          <FluentIcons name="file" size={24} color="#EF4444" />
                          <span class="del-thumb-text">PDF DOCUMENT</span>
                        </div>
                      {:else if d.isAudio || d.previewType === 'audio'}
                        <div class="del-audio-thumb">
                          <FluentIcons name="video" size={24} color="#8B5CF6" />
                          <span class="del-thumb-text">AUDIO TRACK</span>
                        </div>
                      {:else}
                        <div class="doc-badge">{d.ext ? d.ext.toUpperCase() : 'FILE'}</div>
                      {/if}
                      <span class="format-pill">{d.format || (d.ext ? d.ext.toUpperCase() : 'MEDIA')}</span>
                    </div>
                    <div class="del-details">
                      <div class="del-filename" title={d.filename}>{d.filename}</div>
                      <div class="del-meta-row">
                        <span>{d.sizeFormatted || (d.sizeBytes ? ((d.sizeBytes / (1024 * 1024)).toFixed(2) + ' MB') : '0.00 MB')}</span>
                        <span class="status-tag status-{d.status || 'review'}">{d.status || 'review'}</span>
                      </div>
                    </div>
                  </div>
                {/each}
              </div>
            {/if}
          </div>
        {:else if activeCanvasView === 'direction'}
          <!-- Creative Direction Panel -->
          <FluentCard elevated>
            <div class="form-section-header">
              <div class="header-titles">
                <h3>Creative &amp; Visual Direction Matrix</h3>
                <p>Core visual tone, typography mood, and brand guidelines for designers.</p>
              </div>

              <div class="header-actions">
                {#if !isEditingDirection}
                  <FluentButton appearance="secondary" size="sm" onclick={() => (isEditingDirection = true)}>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                      <path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/>
                    </svg>
                    <span style="margin-left: 5px;">Edit Direction</span>
                  </FluentButton>
                {:else}
                  <div class="direction-action-group">
                    <FluentButton appearance="secondary" size="sm" onclick={() => (isEditingDirection = false)}>
                      Cancel
                    </FluentButton>
                    <FluentButton appearance="primary" size="sm" loading={isSavingDirection} onclick={saveCreativeDirection}>
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M17 3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/>
                      </svg>
                      <span style="margin-left: 5px;">Save Direction</span>
                    </FluentButton>
                  </div>
                {/if}
              </div>
            </div>

            {#if !isEditingDirection}
              <!-- ─── PREVIEW MODE (DEFAULT) ─── -->
              <div class="direction-preview-grid">
                <div class="preview-card">
                  <div class="preview-card-header">
                    <span class="preview-icon">🎨</span>
                    <span class="preview-title">Visual Concept / Style Direction</span>
                  </div>
                  <div class="preview-card-body">
                    {#if currentFrontmatter.creative_direction?.visual_concept}
                      <p class="preview-text highlight-concept">{currentFrontmatter.creative_direction.visual_concept}</p>
                    {:else}
                      <p class="preview-empty">No visual concept specified. Click "Edit Direction" to add.</p>
                    {/if}
                  </div>
                </div>

                <div class="preview-card">
                  <div class="preview-card-header">
                    <span class="preview-icon">🎯</span>
                    <span class="preview-title">Primary Color Palette Tokens</span>
                  </div>
                  <div class="preview-card-body">
                    {#if currentFrontmatter.creative_direction?.color_palette}
                      {@const chips = extractColorChips(currentFrontmatter.creative_direction.color_palette)}
                      <div class="palette-preview-wrap">
                        {#if chips.length > 0}
                          <div class="color-chips-row">
                            {#each chips as color}
                              <span class="color-swatch-dot" style="background-color: {color};" title={color}></span>
                            {/each}
                          </div>
                        {/if}
                        <p class="preview-text font-mono">{currentFrontmatter.creative_direction.color_palette}</p>
                      </div>
                    {:else}
                      <p class="preview-empty">No color palette tokens defined yet.</p>
                    {/if}
                  </div>
                </div>

                <div class="preview-card full-width">
                  <div class="preview-card-header">
                    <span class="preview-icon">👥</span>
                    <span class="preview-title">Target Audience Demographics &amp; Psychology</span>
                  </div>
                  <div class="preview-card-body">
                    {#if currentFrontmatter.creative_direction?.target_audience}
                      <div class="audience-content">{currentFrontmatter.creative_direction.target_audience}</div>
                    {:else}
                      <p class="preview-empty">No target audience profile documented yet. Click "Edit Direction" to add.</p>
                    {/if}
                  </div>
                </div>
              </div>
            {:else}
              <!-- ─── EDIT FORM MODE ─── -->
              <div class="form-grid">
                <div class="form-field">
                  <label class="form-label" for="dir-visual-concept">Visual Concept / Style Direction</label>
                  <input
                    id="dir-visual-concept"
                    type="text"
                    class="form-input"
                    bind:value={currentFrontmatter.creative_direction!.visual_concept}
                    placeholder="e.g. Modern Bold Minimalist, Dark Neon Accent"
                  />
                </div>

                <div class="form-field">
                  <label class="form-label" for="dir-color-palette">Primary Color Palette Tokens</label>
                  <input
                    id="dir-color-palette"
                    type="text"
                    class="form-input"
                    bind:value={currentFrontmatter.creative_direction!.color_palette}
                    placeholder="e.g. Prussian Blue #022057, SS Blue #043388, Gold #D4AF37"
                  />
                </div>

                <div class="form-field full-width">
                  <label class="form-label" for="dir-target-audience">Target Audience Demographics &amp; Psychology</label>
                  <textarea
                    id="dir-target-audience"
                    class="form-textarea"
                    rows="4"
                    bind:value={currentFrontmatter.creative_direction!.target_audience}
                    placeholder="Demographics, pain points, desired emotional response..."
                  ></textarea>
                </div>
              </div>
            {/if}
          </FluentCard>
        {/if}
      </main>

      <!-- ─── RIGHT INSPECTOR PANEL (32%) ─── -->
      {#if inspectorOpen}
        <aside class="task-inspector-panel">
          <!-- Inspector Tabs -->
          <div class="inspector-tabs">
            <button
              class="inspector-tab"
              class:active={inspectorTab === 'properties'}
              onclick={() => (inspectorTab = 'properties')}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6-3.6z"/></svg>
              <span>Properties</span>
            </button>

            <button
              class="inspector-tab"
              class:active={inspectorTab === 'discussion'}
              onclick={() => (inspectorTab = 'discussion')}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zM6 9h12v2H6V9zm8 5H6v-2h8v2zm4-6H6V6h12v2z"/></svg>
              <span>Discussion ({projectComments.length})</span>
            </button>
          </div>

          <div class="inspector-content">
            {#if inspectorTab === 'properties'}
              <!-- Properties Form -->
              <div class="properties-sheet">
                <div class="prop-group">
                  <span class="prop-label">Assignee (Designer)</span>
                  <div class="prop-value user-val">
                    <div class="user-avatar" style="background: {designerInfo?.avatarColor || 'var(--brand-primary, #043388)'};">
                      {#if designerAvatarSrc}
                        <img
                          src={designerAvatarSrc}
                          alt={p.designerName || p.designer}
                          class="avatar-photo"
                          onerror={(e) => ((e.currentTarget as HTMLElement).style.display = 'none')}
                        />
                      {:else}
                        {getInitials(p.designerName || p.designer || 'DS')}
                      {/if}
                    </div>
                    <span class="user-name">{designerInfo?.name || p.designerName || p.designer || 'Unassigned'}</span>
                  </div>
                </div>

                <div class="prop-group">
                  <span class="prop-label">Reviewer</span>
                  <div class="prop-value user-val-selectable">
                    <div class="user-avatar mgr-avatar" style="background: {managerInfo?.avatarColor || '#0284C7'};">
                      {#if managerAvatarSrc}
                        <img
                          src={managerAvatarSrc}
                          alt={selectedManager}
                          class="avatar-photo"
                          onerror={(e) => ((e.currentTarget as HTMLElement).style.display = 'none')}
                        />
                      {:else}
                        {getInitials(selectedManager && selectedManager !== 'Unassigned' ? selectedManager : 'AD')}
                      {/if}
                    </div>
                    <select
                      class="prop-manager-select"
                      bind:value={selectedManager}
                      disabled={isUpdatingManager}
                      onchange={() => handleManagerChange(selectedManager)}
                      aria-label="Select Reviewer"
                    >
                      <option value="Unassigned">-- Unassigned --</option>
                      {#if managerList.length === 0}
                        {#if selectedManager && selectedManager !== 'Unassigned'}
                          <option value={selectedManager}>{selectedManager}</option>
                        {/if}
                      {:else}
                        {#each managerList as mgr}
                          <option value={mgr.name}>
                            {mgr.name} {mgr.role ? `· ${mgr.role}` : ''}
                          </option>
                        {/each}
                        {#if selectedManager && selectedManager !== 'Unassigned' && !managerList.some(m => m.name.toLowerCase() === selectedManager.toLowerCase())}
                          <option value={selectedManager}>{selectedManager} (Current)</option>
                        {/if}
                      {/if}
                    </select>
                  </div>
                </div>

                <div class="prop-group">
                  <span class="prop-label">Corporate Brand / Subsidiary</span>
                  <div class="prop-value">
                    <span class="brand-chip">{p.brand || 'SS'}</span>
                    <span class="brand-full">{getCompanyFullName(p.brand || p.client)}</span>
                  </div>
                </div>

                <div class="prop-group">
                  <span class="prop-label">Priority Level</span>
                  <div class="prop-value">
                    {#if (p.priority || '').toLowerCase() === 'urgent'}
                      <span class="priority-chip priority-urgent">P3 (Urgent)</span>
                    {:else if (p.priority || '').toLowerCase() === 'high'}
                      <span class="priority-chip priority-high">P2 (High)</span>
                    {:else if (p.priority || '').toLowerCase() === 'medium' || (p.priority || '').toLowerCase() === 'standard'}
                      <span class="priority-chip priority-medium">P1 (Medium)</span>
                    {:else}
                      <span class="prop-empty">— (Low / No Badge)</span>
                    {/if}
                  </div>
                </div>

                <div class="prop-group">
                  <span class="prop-label">Campaign Deadline</span>
                  <div class="prop-value">
                    <FluentIcons name="calendar" size={13} />
                    <span style="margin-left: 6px;">{p.deadline ? String(p.deadline).split('T')[0] : '2026-08-30'}</span>
                  </div>
                </div>

                <div class="prop-group">
                  <span class="prop-label">Deliverables Storage</span>
                  <div class="prop-value">
                    <FluentIcons name="folder" size={13} />
                    <code style="margin-left: 6px;">{projectStore.activeDeliverables.length} files</code>
                  </div>
                </div>

                <!-- Approval Trail Summary -->
                <div class="approvals-mini-section">
                  <span class="prop-label">Recent Approvals &amp; Sign-Offs</span>
                  {#if p.approvals && p.approvals.length > 0}
                    <div class="mini-app-list">
                      {#each p.approvals.slice(0, 3) as a}
                        <div class="mini-app-card decision-{a.decision}">
                          <div class="mini-app-header">
                            <span class="mini-app-decision">{a.decision.replace('_', ' ').toUpperCase()}</span>
                            <span class="mini-app-time">{new Date(a.timestamp).toLocaleDateString()}</span>
                          </div>
                          <div class="mini-app-actor">{a.reviewer} ({a.role})</div>
                        </div>
                      {/each}
                    </div>
                  {:else}
                    <p class="no-approvals-text">No approval records yet.</p>
                  {/if}
                </div>
              </div>
            {:else}
              <!-- Threaded In-Project Comments inside Inspector -->
              <ProjectComments
                projectId={p.id}
                deliverables={projectStore.activeDeliverables}
                bind:comments={projectComments}
              />
            {/if}
          </div>
        </aside>
      {/if}

      <!-- ─── COL 3: DEDICATED SUBTASKS & DELIVERABLES COLUMN ─── -->
      {#if subtasksOpen}
        <aside class="task-subtasks-panel">
          <!-- Column Header with Title, Count, Weight & Add Button -->
          <div class="subtasks-column-header">
            <div class="subtasks-header-title-row">
              <div class="subtasks-title-badge-group">
                <FluentIcons name="checkbox" size={16} color="var(--brand-primary, #0078D4)" />
                <h3 class="subtasks-column-title">Subtasks &amp; Deliverables</h3>
              </div>
              <button
                type="button"
                class="btn-subtask-add-pill"
                onclick={openAddSubtaskModal}
                title="Add Deliverable / Subtask"
              >
                <FluentIcons name="add" size={11} />
                <span>Add</span>
              </button>
            </div>

            <!-- Stats & Progress Indicator -->
            <div class="subtasks-stats-row">
              <span class="subtasks-stats-text">
                {#if subtaskStats.total > 0}
                  {subtaskStats.completed}/{subtaskStats.total} Completed ({subtaskStats.percent}%)
                {:else}
                  0 deliverables defined
                {/if}
              </span>
              {#if subtaskStats.total > 0}
                <span class="subtasks-weight-badge">{subtaskStats.totalWeight} pts</span>
              {/if}
            </div>

            {#if subtaskStats.total > 0}
              <div class="subtasks-progress-track">
                <div class="subtasks-progress-fill" style="width: {subtaskStats.percent}%;"></div>
              </div>
            {/if}
          </div>

          <!-- Column Scrollable Content -->
          <div class="subtasks-column-body">
            {#if subtaskStats.total > 0}
              <div class="subtasks-cards-column-list">
                {#each subtaskStats.list as st}
                  {@const isDone = ['approved', 'done', 'completed'].includes((st.status || '').toLowerCase())}
                  {@const isProgress = ['in-progress', 'in_progress', 'progress'].includes((st.status || '').toLowerCase())}
                  <div class="subtask-column-card" class:is-done={isDone}>
                    <div class="subtask-card-header-line">
                      <div class="subtask-card-identity">
                        <span class="st-id-tag">{st.id || 'ST'}</span>
                        <span class="st-weight-pill">{st.weight || 1} pts</span>
                      </div>

                      <div class="subtask-card-actions">
                        <button
                          type="button"
                          class="st-status-btn-pill"
                          class:status-done={isDone}
                          class:status-progress={isProgress}
                          class:status-draft={!isDone && !isProgress}
                          onclick={() => cycleSubtaskStatus(st.id)}
                          title="Click to cycle status: Draft ➔ In Progress ➔ Completed"
                        >
                          {isDone ? '✓ Done' : (isProgress ? '⏳ In Progress' : '○ Draft')}
                        </button>
                        <button
                          type="button"
                          class="st-action-icon-btn"
                          onclick={() => openEditSubtaskModal(st)}
                          title="Edit Deliverable"
                        >
                          <FluentIcons name="edit" size={12} />
                        </button>
                        <button
                          type="button"
                          class="st-action-icon-btn danger"
                          onclick={() => deleteSubtask(st.id)}
                          title="Delete Deliverable"
                        >
                          <FluentIcons name="dismiss" size={12} />
                        </button>
                      </div>
                    </div>

                    <div class="subtask-card-content">
                      <h4 class="st-card-title" title={st.name}>{st.name}</h4>
                      {#if st.type || st.specs}
                        <div class="st-card-badges">
                          {#if st.type}
                            <span class="st-badge-type">{String(st.type).replace('_', ' ')}</span>
                          {/if}
                          {#if st.specs}
                            <span class="st-badge-specs" title={st.specs}>{st.specs}</span>
                          {/if}
                        </div>
                      {/if}
                    </div>
                  </div>
                {/each}
              </div>
            {:else}
              <div class="subtasks-column-empty">
                <div class="empty-subtask-icon">
                  <FluentIcons name="checkbox" size={32} color="var(--text-tertiary, #94A3B8)" />
                </div>
                <h5>No Deliverables Defined</h5>
                <p>Track milestone cutdowns, format specs, and point weights for this job.</p>
                <div class="empty-column-actions">
                  <button type="button" class="btn-subtask-action primary full-width" onclick={openAddSubtaskModal}>
                    <FluentIcons name="add" size={13} />
                    <span>Add Deliverable</span>
                  </button>
                  <button type="button" class="btn-subtask-action secondary full-width" onclick={generatePresetSubtasks}>
                    ⚡ Auto-generate Presets
                  </button>
                </div>
              </div>
            {/if}
          </div>

          {#if subtaskStats.total > 0}
            <div class="subtasks-column-footer">
              <button type="button" class="btn-preset-mini" onclick={generatePresetSubtasks} title="Add preset deliverables">
                ⚡ Add Preset Cutdowns
              </button>
            </div>
          {/if}
        </aside>
      {/if}
    </div>

    <!-- Deliverable Lightbox Modal -->
    <DeliverableLightbox
      deliverable={selectedDeliverable}
      bind:open={lightboxOpen}
      onClose={() => (lightboxOpen = false)}
      onApprove={async (d) => {
        await ApiClient.submitDecision(p.id, { decision: 'approved', deliverableId: d.id });
        appState.addToast(`Deliverable ${d.filename} approved`, 'success');
        await loadProject(p.id);
      }}
      onRevision={async (d) => {
        await ApiClient.submitDecision(p.id, { decision: 'revision_requested', deliverableId: d.id });
        appState.addToast(`Revision requested for ${d.filename}`, 'warning');
        await loadProject(p.id);
      }}
    />

    <!-- Delete Confirmation Dialog -->
    <FluentDialog
      bind:open={showDeleteModal}
      title="Delete Project & Files"
      confirmText="Permanently Delete"
      confirmAppearance="danger"
      loading={isDeleting}
      onConfirm={handleDeleteProject}
      onClose={() => (showDeleteModal = false)}
    >
      <div class="delete-dialog-body">
        <div class="delete-warning-banner">
          <div class="warning-title">
            <FluentIcons name="warning" size={16} color="#EF4444" />
            <span style="margin-left: 6px;">Irreversible Filesystem Operation</span>
          </div>
          <p class="warning-text">
            This will permanently delete the project folder and <strong>all 5 subdirectories</strong> on Synology NAS storage:
          </p>
          <ul class="subfolder-list">
            <li><code>01_BRIEF_ASSETS/</code></li>
            <li><code>02_SOURCE_FILES/</code></li>
            <li><code>03_COPYWRITING/</code> (including COPY.md)</li>
            <li><code>04_WORK_IN_PROGRESS/</code></li>
            <li><code>05_DELIVERABLES/</code> (all exported mockups and files)</li>
          </ul>
        </div>
        <div class="delete-target-info">
          <span class="target-label">Target Project:</span>
          <span class="target-val"><strong>{p.jobId || p.id}</strong> — {p.title}</span>
        </div>
      </div>
    </FluentDialog>

    <!-- Deliverable / Subtask Editor Dialog -->
    <FluentDialog
      bind:open={showSubtaskModal}
      title={editingSubtaskId ? `Edit Deliverable (${editingSubtaskId})` : 'Add Deliverable / Subtask'}
      confirmText={editingSubtaskId ? 'Save Changes' : 'Add Deliverable'}
      confirmAppearance="primary"
      loading={isSavingSubtask}
      onConfirm={handleSaveSubtask}
      onClose={() => (showSubtaskModal = false)}
    >
      <div class="subtask-modal-form">
        <div class="subtask-form-row two-col">
          <div class="subtask-form-group">
            <label class="form-label" for="st-form-id">Deliverable ID</label>
            <input
              id="st-form-id"
              class="form-input"
              type="text"
              placeholder="e.g. V01, KV01, ST-01"
              bind:value={subtaskForm.id}
            />
          </div>
          <div class="subtask-form-group">
            <label class="form-label" for="st-form-weight">Capacity Weight (pts)</label>
            <input
              id="st-form-weight"
              class="form-input"
              type="number"
              step="0.1"
              min="0.1"
              max="10.0"
              bind:value={subtaskForm.weight}
            />
          </div>
        </div>

        <div class="subtask-form-group">
          <label class="form-label" for="st-form-name">Deliverable Title / Name *</label>
          <input
            id="st-form-name"
            class="form-input"
            type="text"
            placeholder="e.g. Master 60s Cut, Key Visual 1:1, Hook Variation"
            bind:value={subtaskForm.name}
          />
        </div>

        <div class="subtask-form-row two-col">
          <div class="subtask-form-group">
            <label class="form-label" for="st-form-type">Deliverable Type</label>
            <select id="st-form-type" class="form-select" bind:value={subtaskForm.type}>
              <option value="master_video">Master Video</option>
              <option value="hook_variation">Hook Variation</option>
              <option value="cutdown">Cutdown / Teaser</option>
              <option value="key_visual">Key Visual</option>
              <option value="resize">Resize / Adaptation</option>
              <option value="print_packaging">Print / Packaging</option>
              <option value="banner">Display / Banner</option>
              <option value="standard">Standard Task</option>
            </select>
          </div>
          <div class="subtask-form-group">
            <label class="form-label" for="st-form-specs">Format &amp; Dimensions</label>
            <input
              id="st-form-specs"
              class="form-input"
              type="text"
              placeholder="e.g. 9:16, 1080x1920"
              bind:value={subtaskForm.specs}
            />
          </div>
        </div>

        <div class="subtask-form-group">
          <label class="form-label" for="st-form-status">Initial Status</label>
          <select id="st-form-status" class="form-select" bind:value={subtaskForm.status}>
            <option value="draft">Draft (Backlog)</option>
            <option value="in-progress">In Progress (Active)</option>
            <option value="done">Done / Completed</option>
          </select>
        </div>
      </div>
    </FluentDialog>

    <!-- Vault Ingester Modal -->
    <VaultIngesterModal
      bind:open={showIngesterModal}
      projectId={p?.id}
      projectTitle={p?.title}
    />

    <!-- Share Link Modal -->
    <ShareLinkModal
      bind:open={showShareModal}
      projectId={p?.id}
      projectTitle={p?.title}
    />

    <!-- Version Timeline Modal -->
    <ProjectVersionTimelineModal
      bind:open={showTimelineModal}
      projectId={p?.id}
      projectTitle={p?.title}
      onRollbackSuccess={() => {
        if (projectId) projectStore.loadProjectById(projectId);
      }}
    />
  {/if}
</div>

<style>
  .clickup-task-container {
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  /* ═══ TASK COMMAND HEADER ══════════════════════════════════════ */
  .task-command-header {
    display: flex;
    flex-direction: column;
    gap: 12px;
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-lg, 12px);
    padding: 16px 20px 0 20px;
    box-shadow: var(--shadow-sm);
  }

  .task-breadcrumbs {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 11.5px;
    color: var(--text-tertiary);
  }

  .crumb-link {
    cursor: pointer;
    color: var(--text-secondary);
    font-weight: 600;
  }
  .crumb-link:hover { color: var(--text-brand, #043388); }
  .crumb-sep { opacity: 0.4; }
  .crumb-tag {
    font-weight: 700;
    color: var(--text-brand, #043388);
    background: var(--brand-tint, #EBF4FE);
    padding: 1px 6px;
    border-radius: 4px;
  }
  .crumb-current { font-weight: 600; color: var(--text-primary); }

  .task-headline-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 16px;
    flex-wrap: wrap;
  }

  .headline-left {
    display: flex;
    align-items: center;
    gap: 10px;
  }

  .job-badge {
    font-family: monospace;
    font-size: 12px;
    font-weight: 800;
    color: var(--text-brand, #043388);
    background: var(--brand-tint, #EBF4FE);
    border: 1px solid #BFDBFE;
    padding: 3px 8px;
    border-radius: 6px;
  }

  .task-title {
    font-size: 20px;
    font-weight: 800;
    color: var(--text-primary);
    margin: 0;
  }

  .headline-actions {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
  }

  .approved-tag {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    padding: 5px 10px;
    border-radius: 6px;
    background: rgba(16, 185, 129, 0.12);
    color: #10B981;
    border: 1px solid rgba(16, 185, 129, 0.25);
    font-size: 12px;
    font-weight: 700;
  }

  .action-btn-group {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .action-btn-clean {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    padding: 0 10px;
    height: 30px;
    border-radius: 6px;
    font-size: 12px;
    font-weight: 600;
    color: var(--text-secondary);
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    cursor: pointer;
    text-decoration: none;
    transition: all 0.12s ease;
    font-family: inherit;
  }

  .action-btn-clean:hover, .action-btn-clean.active {
    background: var(--surface-card-subtle, #F8FAFC);
    color: var(--brand-primary, #0078D4);
    border-color: var(--brand-accent, #0078D4);
  }

  .action-btn-clean.icon-only {
    width: 30px;
    padding: 0;
    justify-content: center;
  }

  .more-menu-wrapper {
    position: relative;
    display: inline-block;
  }

  .more-dropdown-menu {
    position: absolute;
    top: calc(100% + 4px);
    right: 0;
    background: var(--surface-card, #FFFFFF);
    border: 1px solid var(--surface-card-border, #E2E8F0);
    border-radius: 8px;
    box-shadow: var(--shadow-lg, 0 10px 25px -5px rgba(0, 0, 0, 0.15));
    padding: 4px;
    display: flex;
    flex-direction: column;
    gap: 2px;
    z-index: 100;
    min-width: 170px;
  }

  .more-dropdown-item {
    display: flex;
    align-items: center;
    gap: 8px;
    text-align: left;
    padding: 7px 10px;
    font-size: 12px;
    font-weight: 500;
    color: var(--text-primary);
    background: transparent;
    border: none;
    border-radius: 6px;
    cursor: pointer;
    text-decoration: none;
    transition: all 0.1s;
    font-family: inherit;
  }

  .more-dropdown-item:hover {
    background: var(--brand-tint, rgba(0, 120, 212, 0.08));
    color: var(--brand-primary, #0078D4);
  }

  .more-dropdown-item.item-danger {
    color: #EF4444;
  }

  .more-dropdown-item.item-danger:hover {
    background: rgba(239, 68, 68, 0.08);
    color: #DC2626;
  }

  .more-divider {
    height: 1px;
    background: var(--surface-card-border);
    margin: 2px 0;
  }

  .status-select {
    padding: 5px 10px;
    height: 30px;
    border-radius: 6px;
    font-size: 12px;
    font-weight: 700;
    cursor: pointer;
    font-family: inherit;
    outline: none;
    border: 1px solid var(--surface-card-border);
  }
  .status-backlog { background: #F1F5F9; color: #475569; }
  .status-in-progress { background: #EBF4FE; color: #043388; border-color: #BFDBFE; }
  .status-review { background: #FFFBEB; color: #B45309; border-color: #FDE68A; }
  .status-revision { background: #FEF2F2; color: #B91C1C; border-color: #FECACA; }
  .status-approved { background: #ECFDF5; color: #047857; border-color: #A7F3D0; }
  .status-done { background: #F3E8FF; color: #7E22CE; border-color: #E9D5FF; }

  /* Segmented Nav */
  .canvas-segmented-nav {
    display: flex;
    gap: 4px;
    border-top: 1px solid var(--surface-card-border);
    padding-top: 6px;
    overflow-x: auto;
  }

  .canvas-nav-item {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    padding: 8px 14px;
    border: none;
    background: transparent;
    border-bottom: 2px solid transparent;
    font-size: 13px;
    font-weight: 600;
    color: var(--text-secondary);
    cursor: pointer;
    transition: all 0.14s;
    font-family: inherit;
    white-space: nowrap;
  }
  .canvas-nav-item:hover { color: var(--text-primary); }
  .canvas-nav-item.active {
    color: var(--brand-primary, #043388);
    border-bottom-color: var(--brand-primary, #043388);
    font-weight: 700;
  }

  .view-chip {
    font-size: 10px;
    font-weight: 800;
    background: var(--bg-app);
    padding: 1px 5px;
    border-radius: 4px;
    color: var(--text-tertiary);
  }

  /* ═══ 3-COLUMN WORKSPACE GRID ═════════════════════════════════ */
  .task-workspace-grid {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 280px 340px;
    gap: 16px;
    align-items: start;
    transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  }
  .task-workspace-grid.inspector-closed {
    grid-template-columns: minmax(0, 1fr) 360px;
  }
  .task-workspace-grid.subtasks-closed {
    grid-template-columns: minmax(0, 1fr) 320px;
  }
  .task-workspace-grid.inspector-closed.subtasks-closed {
    grid-template-columns: 1fr;
  }

  .main-document-canvas {
    display: flex;
    flex-direction: column;
    gap: 16px;
    min-width: 0;
  }

  /* Right Inspector Panel */
  .task-inspector-panel {
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-lg, 12px);
    box-shadow: var(--shadow-sm);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    position: sticky;
    top: 72px;
    max-height: calc(100vh - 100px);
  }

  .inspector-tabs {
    display: flex;
    border-bottom: 1px solid var(--surface-card-border);
    background: var(--surface-card-subtle, #F8FAFC);
  }

  .inspector-tab {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    padding: 10px;
    border: none;
    background: transparent;
    border-bottom: 2px solid transparent;
    font-size: 12px;
    font-weight: 600;
    color: var(--text-secondary);
    cursor: pointer;
    transition: all 0.12s;
    font-family: inherit;
  }
  .inspector-tab:hover { color: var(--text-primary); }
  .inspector-tab.active {
    color: var(--brand-primary, #043388);
    border-bottom-color: var(--brand-primary, #043388);
    font-weight: 700;
    background: var(--surface-card);
  }

  .inspector-content {
    padding: 16px;
    overflow-y: auto;
  }

  /* Properties Sheet */
  .properties-sheet {
    display: flex;
    flex-direction: column;
    gap: 14px;
  }

  .prop-group {
    display: flex;
    flex-direction: column;
    gap: 4px;
    padding-bottom: 10px;
    border-bottom: 1px solid var(--surface-card-border);
  }

  .prop-label {
    font-size: 11px;
    font-weight: 700;
    color: var(--text-tertiary);
    text-transform: uppercase;
    letter-spacing: 0.4px;
  }

  .prop-value {
    font-size: 13px;
    font-weight: 600;
    color: var(--text-primary);
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .user-val {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .user-avatar {
    width: 24px;
    height: 24px;
    border-radius: 50%;
    background: var(--brand-primary, #043388);
    color: #FFFFFF;
    font-size: 10px;
    font-weight: 800;
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;
    flex-shrink: 0;
  }
  .user-avatar img.avatar-photo {
    width: 100%;
    height: 100%;
    object-fit: cover;
    border-radius: 50%;
    display: block;
  }
  .mgr-avatar { background: #0284C7; }

  .user-val-selectable {
    display: flex;
    align-items: center;
    gap: 8px;
    position: relative;
    width: 100%;
  }

  .prop-manager-select {
    flex: 1;
    font-size: 12.5px;
    font-weight: 600;
    font-family: inherit;
    color: var(--text-primary);
    background: var(--bg-app);
    border: 1px solid var(--surface-card-border);
    border-radius: 6px;
    padding: 5px 8px;
    cursor: pointer;
    outline: none;
    transition: all 0.14s ease;
  }
  .prop-manager-select:hover:not(:disabled) {
    border-color: var(--brand-accent, #0078D4);
    background: var(--surface-card);
  }
  .prop-manager-select:focus {
    border-color: var(--brand-primary, #043388);
    box-shadow: 0 0 0 2px rgba(4, 51, 136, 0.15);
  }
  .prop-manager-select:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .brand-chip {
    font-weight: 800;
    font-size: 11px;
    color: var(--text-brand, #043388);
    background: var(--brand-tint, #EBF4FE);
    padding: 1px 6px;
    border-radius: 4px;
  }
  .brand-full { font-size: 12px; color: var(--text-secondary); }

  .priority-chip {
    font-size: 11px;
    font-weight: 800;
    text-transform: uppercase;
    padding: 2px 7px;
    border-radius: 4px;
  }
  .priority-urgent { background: #FEF2F2; color: #DC2626; border: 1px solid #FCA5A5; font-weight: 800; }
  .priority-high { background: #FFFBEB; color: #D97706; border: 1px solid #FDE68A; }
  .priority-medium { background: #EFF6FF; color: #2563EB; border: 1px solid #BFDBFE; }
  .priority-low { background: #F8FAFC; color: #64748B; border: 1px solid #E2E8F0; }

  .approvals-mini-section {
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding-top: 4px;
  }

  .mini-app-list {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .mini-app-card {
    padding: 8px 10px;
    border-radius: 6px;
    background: var(--bg-app);
    border: 1px solid var(--surface-card-border);
  }
  .mini-app-header {
    display: flex;
    justify-content: space-between;
    font-size: 11px;
    font-weight: 700;
  }
  .decision-approved .mini-app-decision { color: #047857; }
  .decision-revision_requested .mini-app-decision { color: #B91C1C; }
  .mini-app-time { font-size: 10px; color: var(--text-tertiary); font-weight: normal; }
  .mini-app-actor { font-size: 11px; color: var(--text-secondary); margin-top: 2px; }
  .no-approvals-text { font-size: 12px; color: var(--text-tertiary); margin: 0; }

  /* Deliverables Gallery */
  .deliverables-gallery-container {
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-lg, 12px);
    padding: 20px;
    box-shadow: var(--shadow-sm);
  }

  .gallery-header {
    margin-bottom: 16px;
  }
  .gallery-title-group h3 {
    font-size: 16px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0 0 2px 0;
  }
  .gallery-subtitle {
    font-size: 12px;
    color: var(--text-secondary);
  }

  .deliverables-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
    gap: 16px;
  }

  .deliverable-card {
    background: var(--surface-card-subtle, #F8FAFC);
    border: 1px solid var(--surface-card-border);
    border-radius: 8px;
    overflow: hidden;
    cursor: pointer;
    transition: all 0.14s;
    display: flex;
    flex-direction: column;
  }
  .deliverable-card:hover {
    transform: translateY(-2px);
    border-color: var(--brand-accent);
    box-shadow: var(--shadow-md);
  }

  .del-preview-box {
    height: 140px;
    background: var(--bg-app);
    display: flex;
    align-items: center;
    justify-content: center;
    position: relative;
    overflow: hidden;
  }
  .del-preview-box img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    transition: transform 0.2s ease;
  }
  .deliverable-card:hover .del-preview-box img {
    transform: scale(1.04);
  }

  .del-video-thumb,
  .del-pdf-thumb,
  .del-audio-thumb {
    width: 100%;
    height: 100%;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    background: #0B1120;
    color: #FFFFFF;
    position: relative;
  }
  .del-video-thumb video {
    width: 100%;
    height: 100%;
    object-fit: cover;
    opacity: 0.75;
  }
  .del-play-badge {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    background: rgba(0, 0, 0, 0.8);
    color: #FFFFFF;
    font-size: 10px;
    font-weight: 800;
    padding: 4px 8px;
    border-radius: 4px;
    border: 1px solid rgba(255, 255, 255, 0.25);
    backdrop-filter: blur(4px);
  }
  .del-thumb-icon {
    font-size: 30px;
    margin-bottom: 2px;
  }
  .del-thumb-text {
    font-size: 9.5px;
    font-weight: 800;
    color: #94A3B8;
    letter-spacing: 0.5px;
  }

  .doc-badge {
    font-size: 16px;
    font-weight: 900;
    color: var(--text-tertiary);
    background: var(--surface-card);
    padding: 8px 14px;
    border-radius: 6px;
    border: 1px solid var(--surface-card-border);
  }

  .format-pill {
    position: absolute;
    bottom: 6px;
    right: 6px;
    font-size: 9.5px;
    font-weight: 800;
    background: rgba(0, 0, 0, 0.7);
    color: #FFFFFF;
    padding: 1px 5px;
    border-radius: 4px;
  }

  .del-details {
    padding: 10px 12px;
    display: flex;
    flex-direction: column;
    gap: 4px;
  }
  .del-filename {
    font-size: 12.5px;
    font-weight: 700;
    color: var(--text-primary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .del-meta-row {
    display: flex;
    justify-content: space-between;
    font-size: 11px;
    color: var(--text-tertiary);
  }

  .status-tag {
    font-size: 10px;
    font-weight: 700;
    text-transform: uppercase;
    padding: 1px 5px;
    border-radius: 3px;
  }

  .empty-gallery {
    text-align: center;
    padding: 48px 16px;
    background: var(--bg-app);
    border-radius: 8px;
    border: 1px dashed var(--surface-card-border);
  }
  .empty-gallery .empty-icon { font-size: 32px; margin-bottom: 8px; }
  .empty-gallery p { font-size: 13.5px; font-weight: 700; color: var(--text-primary); margin: 0 0 4px 0; }
  .empty-gallery .empty-sub { font-size: 12px; color: var(--text-secondary); margin: 0; }

  /* Creative Direction Section */
  .form-section-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 16px;
    margin-bottom: 20px;
    flex-wrap: wrap;
  }
  .header-titles h3 {
    font-size: 16px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0 0 2px 0;
  }
  .header-titles p {
    font-size: 12px;
    color: var(--text-secondary);
    margin: 0;
  }
  .header-actions {
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .direction-action-group {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  /* Direction Preview Grid (Default View) */
  .direction-preview-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 16px;
  }
  .preview-card {
    background: var(--bg-app);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-md, 8px);
    padding: 16px 18px;
    display: flex;
    flex-direction: column;
    gap: 10px;
    box-sizing: border-box;
  }
  .preview-card.full-width {
    grid-column: 1 / -1;
  }
  .preview-card-header {
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .preview-icon {
    font-size: 15px;
  }
  .preview-title {
    font-size: 11.5px;
    font-weight: 700;
    color: var(--text-tertiary);
    text-transform: uppercase;
    letter-spacing: 0.5px;
  }
  .preview-card-body {
    min-height: 24px;
  }
  .preview-text {
    margin: 0;
    font-size: 13.5px;
    font-weight: 600;
    color: var(--text-primary);
    line-height: 1.55;
  }
  .highlight-concept {
    color: var(--brand-primary, #043388);
    font-weight: 700;
    font-size: 14px;
  }
  .palette-preview-wrap {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }
  .color-chips-row {
    display: flex;
    gap: 8px;
    align-items: center;
    flex-wrap: wrap;
  }
  .color-swatch-dot {
    width: 22px;
    height: 22px;
    border-radius: 50%;
    border: 2px solid rgba(255, 255, 255, 0.85);
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.2);
    display: inline-block;
    flex-shrink: 0;
  }
  .audience-content {
    font-size: 13px;
    line-height: 1.65;
    color: var(--text-primary);
    white-space: pre-wrap;
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: 6px;
    padding: 14px 16px;
  }
  .preview-empty {
    margin: 0;
    font-size: 12.5px;
    color: var(--text-tertiary);
    font-style: italic;
  }

  /* Creative Direction Edit Form */
  .form-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 14px;
    margin-bottom: 16px;
  }
  .form-field.full-width { grid-column: 1 / -1; }

  .form-label {
    display: block;
    font-size: 12px;
    font-weight: 700;
    color: var(--text-primary);
    margin-bottom: 6px;
  }

  .form-input, .form-textarea {
    width: 100%;
    padding: 10px 12px;
    border-radius: 6px;
    border: 1px solid var(--surface-card-border);
    background: var(--bg-app);
    color: var(--text-primary);
    font-size: 13px;
    font-family: inherit;
    box-sizing: border-box;
    outline: none;
    line-height: 1.5;
  }
  .form-input:focus, .form-textarea:focus {
    border-color: var(--brand-accent);
  }

  .loading-state, .empty-state {
    text-align: center;
    padding: 64px 20px;
    color: var(--text-secondary);
  }

  @media (max-width: 1240px) {
    .task-workspace-grid {
      grid-template-columns: minmax(0, 1fr) 260px 300px;
      gap: 12px;
    }
  }

  @media (max-width: 1040px) {
    .task-workspace-grid {
      grid-template-columns: minmax(0, 1fr) 280px;
    }
    .task-subtasks-panel {
      grid-column: 1 / -1;
      position: static;
      max-height: none;
    }
  }

  @media (max-width: 860px) {
    .task-workspace-grid {
      grid-template-columns: 1fr;
    }
    .task-inspector-panel,
    .task-subtasks-panel {
      position: static;
      max-height: none;
    }
  }

  /* ═══ DELETE DIALOG ═════════════════════════════════════════════ */
  .delete-dialog-body {
    display: flex;
    flex-direction: column;
    gap: 14px;
    color: var(--text-primary);
  }
  .delete-warning-banner {
    background: rgba(196, 43, 28, 0.08);
    border: 1px solid var(--color-danger, #C42B1C);
    border-radius: var(--radius-md, 8px);
    padding: 14px;
  }
  .warning-title {
    font-weight: 700;
    font-size: 0.95rem;
    color: var(--color-danger, #C42B1C);
    margin-bottom: 6px;
  }
  .warning-text {
    font-size: 0.85rem;
    color: var(--text-primary);
    margin: 0 0 8px 0;
    line-height: 1.4;
  }
  .subfolder-list {
    margin: 0;
    padding-left: 18px;
    font-size: 0.8rem;
    color: var(--text-secondary);
    display: flex;
    flex-direction: column;
    gap: 3px;
  }
  .subfolder-list code {
    font-family: var(--font-mono);
    color: var(--color-danger, #C42B1C);
    background: rgba(196, 43, 28, 0.06);
    padding: 1px 4px;
    border-radius: 3px;
  }
  .delete-target-info {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 10px 12px;
    background: var(--surface-card-subtle);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-md, 8px);
    font-size: 0.88rem;
  }
  .target-label {
    font-weight: 600;
    color: var(--text-secondary);
  }
  .target-val {
    color: var(--text-primary);
  }

  /* ═══ SUBTASKS & DELIVERABLES UI ═══════════════════════════════ */
  .subtasks-container-card {
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-lg, 12px);
    padding: 20px;
    box-shadow: var(--shadow-sm);
  }

  .subtasks-card-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 16px;
    flex-wrap: wrap;
    margin-bottom: 14px;
  }

  .subtasks-header-left {
    display: flex;
    align-items: center;
    gap: 10px;
  }

  .subtasks-section-title {
    font-size: 15px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0;
  }

  .subtasks-section-subtitle {
    font-size: 11.5px;
    color: var(--text-secondary);
  }

  .subtasks-header-actions {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .btn-subtask-action {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 6px 14px;
    border-radius: var(--radius-md, 8px);
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.15s ease;
  }

  .btn-subtask-action.primary {
    background: var(--brand-primary, #0078D4);
    color: #FFFFFF;
    border: 1px solid rgba(255, 255, 255, 0.1);
  }
  .btn-subtask-action.primary:hover {
    filter: brightness(1.1);
    box-shadow: 0 2px 6px rgba(0, 120, 212, 0.3);
  }

  .btn-subtask-action.secondary {
    background: var(--surface-card-subtle);
    color: var(--text-secondary);
    border: 1px solid var(--surface-card-border);
  }
  .btn-subtask-action.secondary:hover {
    color: var(--text-primary);
    background: rgba(255, 255, 255, 0.08);
  }

  .subtasks-progress-track {
    width: 100%;
    height: 6px;
    background: rgba(255, 255, 255, 0.08);
    border-radius: 999px;
    overflow: hidden;
    margin-bottom: 16px;
  }
  .subtasks-progress-track.large {
    height: 8px;
  }
  .subtasks-progress-track.mini {
    height: 4px;
    margin: 8px 0 10px 0;
  }

  .subtasks-progress-fill {
    height: 100%;
    background: linear-gradient(90deg, var(--brand-primary, #0078D4) 0%, #10B981 100%);
    border-radius: 999px;
    transition: width 0.3s ease;
  }

  .subtasks-cards-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 12px;
  }

  .subtask-card-item {
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    background: var(--surface-card-subtle);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-md, 8px);
    padding: 14px;
    transition: all 0.15s ease;
  }
  .subtask-card-item:hover {
    border-color: rgba(0, 120, 212, 0.4);
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
  }
  .subtask-card-item.is-done {
    border-color: rgba(16, 185, 129, 0.3);
    background: rgba(16, 185, 129, 0.04);
  }

  .subtask-card-topline {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 8px;
  }

  .st-id-tag {
    font-family: var(--font-mono, monospace);
    font-size: 10.5px;
    font-weight: 700;
    padding: 2px 6px;
    border-radius: 4px;
    background: rgba(0, 120, 212, 0.15);
    color: #21A1F7;
  }
  .st-id-tag.mini {
    font-size: 9.5px;
    padding: 1px 4px;
  }

  .st-pts-badge {
    font-size: 10.5px;
    font-weight: 700;
    padding: 2px 6px;
    border-radius: 4px;
    background: rgba(255, 255, 255, 0.06);
    color: var(--text-secondary);
  }

  .st-card-name {
    font-size: 13px;
    font-weight: 600;
    color: var(--text-primary);
    margin: 0 0 8px 0;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .st-card-meta {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin-bottom: 12px;
  }

  .st-meta-pill {
    font-size: 10.5px;
    padding: 2px 6px;
    border-radius: 4px;
    background: rgba(255, 255, 255, 0.05);
    color: var(--text-secondary);
    text-transform: capitalize;
  }
  .st-meta-pill.type-pill {
    color: #38BDF8;
    background: rgba(56, 189, 248, 0.1);
  }

  .subtask-card-footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    border-top: 1px solid rgba(255, 255, 255, 0.06);
    padding-top: 10px;
  }

  .st-status-btn-full {
    flex: 1;
    font-size: 11px;
    font-weight: 600;
    padding: 5px 10px;
    border-radius: 6px;
    cursor: pointer;
    text-align: center;
    transition: all 0.15s ease;
  }

  .st-actions-row {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .st-icon-action {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 26px;
    height: 26px;
    border-radius: 4px;
    background: transparent;
    border: 1px solid transparent;
    color: var(--text-secondary);
    cursor: pointer;
    transition: all 0.15s ease;
  }
  .st-icon-action:hover {
    background: rgba(255, 255, 255, 0.08);
    color: var(--text-primary);
  }
  .st-icon-action.danger:hover {
    background: rgba(239, 68, 68, 0.15);
    color: #EF4444;
  }

  /* Status Badge Colors */
  .status-done {
    background: rgba(16, 185, 129, 0.16) !important;
    color: #10B981 !important;
    border: 1px solid rgba(16, 185, 129, 0.3) !important;
  }
  .status-progress {
    background: rgba(0, 120, 212, 0.16) !important;
    color: #21A1F7 !important;
    border: 1px solid rgba(0, 120, 212, 0.3) !important;
  }
  .status-draft {
    background: rgba(148, 163, 184, 0.12) !important;
    color: #94A3B8 !important;
    border: 1px solid rgba(148, 163, 184, 0.25) !important;
  }

  /* Empty State Hero */
  .subtasks-empty-hero {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    text-align: center;
    padding: 36px 20px;
    background: rgba(255, 255, 255, 0.02);
    border: 1px dashed var(--surface-card-border);
    border-radius: var(--radius-md, 8px);
  }

  .empty-icon-wrap {
    width: 56px;
    height: 56px;
    border-radius: 50%;
    background: rgba(255, 255, 255, 0.04);
    display: flex;
    align-items: center;
    justify-content: center;
    margin-bottom: 12px;
  }

  .subtasks-empty-hero h4 {
    font-size: 15px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0 0 6px 0;
  }

  .subtasks-empty-hero p {
    font-size: 12.5px;
    color: var(--text-secondary);
    max-width: 480px;
    margin: 0 0 18px 0;
    line-height: 1.4;
  }

  .empty-hero-actions {
    display: flex;
    align-items: center;
    gap: 10px;
    flex-wrap: wrap;
    justify-content: center;
  }

  /* ═══ RIGHT SUBTASKS COLUMN PANEL ═════════════════════════════ */
  .task-subtasks-panel {
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-lg, 12px);
    box-shadow: var(--shadow-sm);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    position: sticky;
    top: 72px;
    max-height: calc(100vh - 100px);
  }

  .subtasks-column-header {
    padding: 14px 16px 12px;
    border-bottom: 1px solid var(--surface-card-border);
    background: var(--surface-card-subtle, #F8FAFC);
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .subtasks-header-title-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
  }

  .subtasks-title-badge-group {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .subtasks-column-title {
    margin: 0;
    font-size: 13px;
    font-weight: 700;
    color: var(--text-primary);
  }

  .btn-subtask-add-pill {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 3px 10px;
    border-radius: 12px;
    font-size: 11px;
    font-weight: 600;
    background: var(--brand-primary, #0078D4);
    color: #FFFFFF;
    border: none;
    cursor: pointer;
    transition: all 0.15s ease;
  }
  .btn-subtask-add-pill:hover {
    filter: brightness(1.1);
    transform: scale(1.02);
  }

  .subtasks-stats-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    font-size: 11px;
  }

  .subtasks-stats-text {
    color: var(--text-secondary);
    font-weight: 600;
  }

  .subtasks-weight-badge {
    font-size: 10.5px;
    font-weight: 700;
    color: #21A1F7;
    background: rgba(0, 120, 212, 0.12);
    padding: 2px 7px;
    border-radius: 4px;
  }

  .subtasks-progress-track {
    height: 4px;
    background: var(--surface-card-border, rgba(255, 255, 255, 0.1));
    border-radius: 2px;
    overflow: hidden;
  }

  .subtasks-progress-fill {
    height: 100%;
    background: linear-gradient(90deg, var(--brand-primary, #0078D4), #10B981);
    border-radius: 2px;
    transition: width 0.3s ease;
  }

  .subtasks-column-body {
    padding: 12px;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 8px;
    flex: 1;
  }

  .subtasks-cards-column-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .subtask-column-card {
    background: var(--surface-card-subtle, rgba(255, 255, 255, 0.02));
    border: 1px solid var(--surface-card-border);
    border-radius: 8px;
    padding: 10px 12px;
    display: flex;
    flex-direction: column;
    gap: 8px;
    transition: all 0.15s ease;
  }
  .subtask-column-card:hover {
    border-color: rgba(0, 120, 212, 0.4);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  }
  .subtask-column-card.is-done {
    border-color: rgba(16, 185, 129, 0.3);
    background: rgba(16, 185, 129, 0.04);
  }

  .subtask-card-header-line {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
  }

  .subtask-card-identity {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .st-id-tag {
    font-size: 10px;
    font-weight: 800;
    color: var(--brand-primary, #0078D4);
    background: rgba(0, 120, 212, 0.12);
    padding: 2px 6px;
    border-radius: 4px;
    letter-spacing: 0.5px;
  }

  .st-weight-pill {
    font-size: 10px;
    font-weight: 700;
    color: var(--text-tertiary, #94A3B8);
  }

  .subtask-card-actions {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .st-status-btn-pill {
    font-size: 10.5px;
    font-weight: 700;
    padding: 3px 8px;
    border-radius: 4px;
    border: none;
    cursor: pointer;
    transition: all 0.12s ease;
    font-family: inherit;
  }
  .st-status-btn-pill.status-done {
    background: rgba(16, 185, 129, 0.18);
    color: #10B981;
    border: 1px solid rgba(16, 185, 129, 0.3);
  }
  .st-status-btn-pill.status-progress {
    background: rgba(245, 158, 11, 0.18);
    color: #F59E0B;
    border: 1px solid rgba(245, 158, 11, 0.3);
  }
  .st-status-btn-pill.status-draft {
    background: rgba(148, 163, 184, 0.12);
    color: var(--text-secondary);
    border: 1px solid rgba(148, 163, 184, 0.2);
  }
  .st-status-btn-pill:hover {
    filter: brightness(1.15);
  }

  .st-action-icon-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 22px;
    height: 22px;
    border-radius: 4px;
    background: transparent;
    border: none;
    color: var(--text-tertiary);
    cursor: pointer;
    transition: all 0.12s ease;
  }
  .st-action-icon-btn:hover {
    color: var(--text-primary);
    background: rgba(255, 255, 255, 0.08);
  }
  .st-action-icon-btn.danger:hover {
    color: #EF4444;
    background: rgba(239, 68, 68, 0.12);
  }

  .subtask-card-content {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .st-card-title {
    margin: 0;
    font-size: 12.5px;
    font-weight: 600;
    color: var(--text-primary);
    line-height: 1.35;
  }
  .subtask-column-card.is-done .st-card-title {
    text-decoration: line-through;
    opacity: 0.75;
  }

  .st-card-badges {
    display: flex;
    align-items: center;
    gap: 6px;
    flex-wrap: wrap;
  }

  .st-badge-type {
    font-size: 9.5px;
    font-weight: 700;
    text-transform: uppercase;
    color: var(--brand-accent, #3B82F6);
    background: rgba(59, 130, 246, 0.1);
    padding: 1px 5px;
    border-radius: 3px;
  }

  .st-badge-specs {
    font-size: 10px;
    color: var(--text-secondary);
    background: rgba(255, 255, 255, 0.04);
    padding: 1px 6px;
    border-radius: 3px;
    border: 1px solid rgba(255, 255, 255, 0.06);
    max-width: 190px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .subtasks-column-empty {
    text-align: center;
    padding: 32px 16px;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 8px;
  }
  .empty-subtask-icon {
    margin-bottom: 4px;
  }
  .subtasks-column-empty h5 {
    margin: 0;
    font-size: 13px;
    font-weight: 700;
    color: var(--text-primary);
  }
  .subtasks-column-empty p {
    margin: 0;
    font-size: 11.5px;
    color: var(--text-tertiary);
    line-height: 1.45;
  }
  .empty-column-actions {
    margin-top: 12px;
    display: flex;
    flex-direction: column;
    gap: 8px;
    width: 100%;
  }

  .subtasks-column-footer {
    padding: 8px 12px;
    border-top: 1px solid var(--surface-card-border);
    background: var(--surface-card-subtle, #F8FAFC);
    display: flex;
    justify-content: center;
  }

  .btn-preset-mini {
    background: transparent;
    border: none;
    color: var(--brand-primary, #0078D4);
    font-size: 11px;
    font-weight: 600;
    cursor: pointer;
    padding: 4px 10px;
    border-radius: 4px;
    transition: all 0.12s ease;
  }
  .btn-preset-mini:hover {
    background: rgba(0, 120, 212, 0.08);
  }

  /* ═══ SUBTASK MODAL FORM ═══════════════════════════════════════ */
  .subtask-modal-form {
    display: flex;
    flex-direction: column;
    gap: 14px;
  }

  .subtask-form-row.two-col {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 12px;
  }

  .subtask-form-group {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .form-label {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-secondary);
  }

  .form-input, .form-select {
    width: 100%;
    padding: 8px 12px;
    font-size: 12.5px;
    color: var(--text-primary);
    background: var(--surface-card-subtle);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-md, 8px);
    outline: none;
    transition: all 0.15s ease;
    box-sizing: border-box;
  }
  .form-input:focus, .form-select:focus {
    border-color: var(--brand-primary, #0078D4);
    box-shadow: 0 0 0 2px rgba(0, 120, 212, 0.2);
  }
</style>
