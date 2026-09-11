<script lang="ts">
  import { onMount } from 'svelte';
  import { appState } from '$lib/stores/appState.svelte';
  import { ApiClient } from '$lib/services/api';
  import FluentButton from '$lib/components/ui/FluentButton.svelte';

  // ─── Types ─────────────────────────────────────────────────────────────────
  interface OrderAttachment {
    filename: string;
    size: number;
    sizeFormatted: string;
    uploadedAt: string;
    url: string;
  }

  interface CreativeOrder {
    id: string;
    title: string;
    entity: string;
    priority: string;
    channel?: 'digital' | 'print';
    format: string;
    customSize?: string;
    material?: string;
    materialType?: string;
    copy: string;
    targetDate: string;
    attachmentNote?: string;
    requester: string;
    requesterRole?: string;
    status: 'pending' | 'in_progress' | 'for_approval' | 'done' | 'cancelled';
    submittedAt: string;
    updatedAt: string;
    assignedTo: string | null;
    projectId: string | null;
    attachments?: OrderAttachment[];
    attachmentCount?: number;
    nasPath?: string;
  }

  interface UploadFileItem {
    filename: string;
    sizeFormatted: string;
    fileData: string;
  }

  // ─── State ─────────────────────────────────────────────────────────────────
  let orders        = $state<CreativeOrder[]>([]);
  let isLoading     = $state(false);
  let isSubmitting  = $state(false);
  let showForm      = $state(false);
  let filterStatus  = $state('all');
  let activeOrderId = $state<string | null>(null);
  let editingOrderId = $state<string | null>(null);

  // Form fields
  let f_title          = $state('');
  let f_entity         = $state('');
  let f_priority       = $state('tier_1');
  let f_channel        = $state<'digital' | 'print'>('digital');
  let f_format         = $state('9_16_video');
  let f_customSize     = $state('');
  let f_material       = $state('');
  let f_copy           = $state('');
  let f_targetDate     = $state('');
  let f_attachmentNote = $state('');
  let f_files          = $state<UploadFileItem[]>([]);
  let formError        = $state('');
  let submitSuccess    = $state(false);

  // ─── Constants ─────────────────────────────────────────────────────────────
  const ENTITIES = [
    { id: 'SSH', label: 'Holding',             full: 'SuamiSihat Holding'            },
    { id: 'SSC', label: 'Healthcare & Clinic', full: 'SuamiSihat Healthcare & Clinic'},
    { id: 'SSW', label: 'Wellness',            full: 'SuamiSihat Wellness'           },
    { id: 'SSE', label: 'E-Commerce',          full: 'SuamiSihat E-Commerce'         },
    { id: 'SST', label: 'Technology',          full: 'SuamiSihat Technology'         },
  ];

  const PRIORITIES = [
    {
      id: 'tier_0',
      label: 'Low / Pipeline',
      window: '2 – 4+ weeks / Next month',
      note: 'Strategic backlog, future campaign, or brand prep. No immediate rush.',
    },
    {
      id: 'tier_1',
      label: 'Standard',
      window: '3 – 5 working days',
      note: 'Routine campaign or scheduled content.',
    },
    {
      id: 'tier_2',
      label: 'Fast-Track',
      window: 'Next business day',
      note: 'Time-sensitive promotion or launch support.',
    },
    {
      id: 'tier_3',
      label: 'Urgent',
      window: 'Same business day',
      note: 'Critical escalation. Use only when necessary.',
    },
  ];

  const DIGITAL_FORMATS = [
    { id: '9_16_video',     label: '9:16 Video',        sub: 'TikTok · Reels · Story (1080×1920)'     },
    { id: '1_1_feed',       label: '1:1 Social Feed',   sub: 'Instagram · Facebook · LinkedIn (1080×1080)' },
    { id: '4_5_portrait',   label: '4:5 Portrait Feed', sub: 'Meta / IG Recommended Feed (1080×1350)' },
    { id: '16_9_landscape', label: '16:9 Landscape',    sub: 'YouTube · Slides · LED / TV (1920×1080)' },
    { id: 'print_digital',  label: 'Digital Banner',    sub: 'Website Hero · Email · Google Display' },
    { id: 'custom_digital', label: 'Custom Screen Size', sub: 'Specify width × height in px' },
  ];

  const PRINT_FORMATS = [
    { id: 'print_packaging_box', label: 'Packaging Box & Sleeve', sub: 'Medicine box · Supplement carton · Sleeve' },
    { id: 'print_label',         label: 'Bottle / Jar / Vial Label', sub: 'Product sticker · Vial · Tamper seal' },
    { id: 'print_posm',          label: 'POSM Poster',            sub: 'A4 · A3 · A2 · A1 Posters' },
    { id: 'print_banner_rollup', label: 'Roll-Up / Bunting',      sub: '2.5×6 ft · 2×5 ft · Pull-up banner' },
    { id: 'print_flyer',         label: 'Flyer / Leaflet',        sub: 'A4 Tri-fold · A5 Handout' },
    { id: 'custom_print',        label: 'Custom Print Size',      sub: 'Specify dimensions (mm / cm / inch / ft)' },
  ];

  const LEGACY_FORMATS = [
    { id: 'other', label: 'Other', sub: 'Specify in the brief field' }
  ];

  const PRINT_MATERIALS = [
    { id: 'waterproof_vinyl',     label: 'Waterproof Vinyl / Synthetic Sticker', desc: 'Moisture-resistant (Compounding Pharmacy bottle labels)' },
    { id: 'mirrorkote_paper',     label: 'Mirrorkote Gloss Paper Sticker',       desc: 'Standard box seals & dry product labels' },
    { id: 'artcard_gloss',        label: 'Art Card 260/310gsm (Gloss Lam)',      desc: 'Standard packaging boxes & marketing cards' },
    { id: 'artcard_matte_spotuv', label: 'Art Card 260/310gsm (Matte Lam + Spot UV)', desc: 'Premium branded packaging boxes' },
    { id: 'simili_paper',         label: 'Simili Paper 80/100gsm',               desc: 'Prescription pads, clinic forms & letterheads' },
    { id: 'tarpaulin',            label: 'Tarpaulin 380/440gsm',                 desc: 'Outdoor clinic banners & signage' },
    { id: 'synthetic_paper',      label: 'Synthetic Paper',                      desc: 'Indoor roll-up banners & tear-resistant posters' },
    { id: 'rigid_box',            label: 'Rigid Box / Hard Board',               desc: 'Luxury gift sets & premium clinic kits' },
    { id: 'other_material',       label: 'Custom / Other Material',              desc: 'Specify in brief or size field' },
  ];

  const PRIORITY_COLOR: Record<string, { fg: string; bg: string; border: string }> = {
    tier_0: { fg: '#475569', bg: '#F8FAFC', border: '#CBD5E1' },
    tier_1: { fg: '#065F46', bg: '#ECFDF5', border: '#A7F3D0' },
    tier_2: { fg: '#92400E', bg: '#FFFBEB', border: '#FDE68A' },
    tier_3: { fg: '#991B1B', bg: '#FEF2F2', border: '#FECACA' },
  };

  const STATUS_META: Record<string, { label: string; fg: string; bg: string }> = {
    pending:      { label: 'Pending',           fg: '#475569', bg: '#F1F5F9' },
    in_progress:  { label: 'In Progress',       fg: '#1D4ED8', bg: '#EFF6FF' },
    for_approval: { label: 'For Approval',      fg: '#92400E', bg: '#FFFBEB' },
    done:         { label: 'Added to Backlog',  fg: '#065F46', bg: '#ECFDF5' },
    cancelled:    { label: 'Cancelled',         fg: '#991B1B', bg: '#FEF2F2' },
  };

  // ─── Derived ───────────────────────────────────────────────────────────────
  const filteredOrders = $derived(
    filterStatus === 'all' ? orders : orders.filter(o => o.status === filterStatus)
  );

  const counts = $derived({
    all:          orders.length,
    pending:      orders.filter(o => o.status === 'pending').length,
    in_progress:  orders.filter(o => o.status === 'in_progress').length,
    for_approval: orders.filter(o => o.status === 'for_approval').length,
    done:         orders.filter(o => o.status === 'done').length,
  });

  const formFilled = $derived([f_title, f_entity, f_priority, f_format, f_copy, f_targetDate]
    .filter(v => (v || '').trim().length > 0).length);

  const formValid = $derived(
    formFilled === 6 &&
    ((f_format !== 'custom_digital' && f_format !== 'custom_print') || f_customSize.trim().length > 0)
  );

  const isDesigner = $derived(
    (() => {
      const r = (appState.currentUser?.role || '').toLowerCase();
      const roles: string[] = ((appState.currentUser as any)?.roles || []).map((x: string) => x.toLowerCase());
      return r.includes('admin') ||
             r.includes('director') ||
             r.includes('designer') ||
             roles.some(x => x.includes('admin') || x.includes('designer') || x.includes('director'));
    })()
  );

  let roster = $state<{ name: string; staffId: string }[]>([]);

  // ─── Lifecycle ─────────────────────────────────────────────────────────────
  onMount(async () => {
    loadOrders();
    try {
      const res = await ApiClient.getStaffRoster();
      if (res && res.roster) {
        roster = res.roster.map((m: any) => ({ name: m.name, staffId: m.staffId }));
      }
    } catch {}
  });

  async function loadOrders() {
    isLoading = true;
    try {
      const res = await ApiClient.request<{ success: boolean; orders: CreativeOrder[] }>('/orders');
      orders = res.orders || [];
    } catch {
      appState.addToast('Unable to load order queue. Please refresh.', 'error', 'Connection Error');
    } finally {
      isLoading = false;
    }
  }

  function openForm() {
    editingOrderId   = null;
    formError        = '';
    submitSuccess    = false;
    f_title          = '';
    f_entity         = '';
    f_priority       = 'tier_1';
    f_channel        = 'digital';
    f_format         = '9_16_video';
    f_customSize     = '';
    f_material       = '';
    f_copy           = '';
    f_targetDate     = new Date(Date.now() + 3 * 86400000).toISOString().split('T')[0];
    f_attachmentNote = '';
    f_files          = [];
    showForm = true;
  }

  function openEditForm(order: CreativeOrder) {
    editingOrderId   = order.id;
    formError        = '';
    submitSuccess    = false;
    f_title          = order.title || '';
    f_entity         = order.entity || 'SSH';
    f_priority       = order.priority || 'tier_1';
    f_channel        = order.channel || ((order.format && (order.format.startsWith('print_') || order.format === 'custom_print')) ? 'print' : 'digital');
    f_format         = order.format || (f_channel === 'print' ? 'print_packaging_box' : '9_16_video');
    f_customSize     = order.customSize || '';
    f_material       = order.material || order.materialType || '';
    f_copy           = order.copy || '';
    f_targetDate     = order.targetDate || '';
    f_attachmentNote = order.attachmentNote || '';
    f_files          = [];
    showForm         = true;
  }

  const COMMERCIAL_PRESETS = [
    { id: 'tiktok_hook', label: '📱 TikTok/Reels Video', channel: 'digital' as const, format: '9_16_video', entity: 'SSE', priority: 'tier_1', prefix: 'TikTok Video Hook - ' },
    { id: 'meta_feed', label: '🖼️ Meta/IG Promo Post', channel: 'digital' as const, format: '1_1_feed', entity: 'SSE', priority: 'tier_1', prefix: 'Feed Promo Post - ' },
    { id: 'clinic_print', label: '🏥 Clinic Roll-Up Bunting', channel: 'print' as const, format: 'print_banner_rollup', entity: 'SSC', priority: 'tier_2', prefix: 'Clinic Event Bunting - ' },
    { id: 'urgent_tactical', label: '🔥 Urgent Tactical Ad', channel: 'digital' as const, format: '9_16_video', entity: 'SSE', priority: 'tier_3', prefix: 'URGENT: Flash Promo - ' }
  ];

  function applyPreset(presetId: string) {
    const p = COMMERCIAL_PRESETS.find(x => x.id === presetId);
    if (!p) return;
    f_channel = p.channel;
    f_format = p.format;
    f_entity = p.entity;
    f_priority = p.priority;
    if (!f_title || COMMERCIAL_PRESETS.some(x => f_title.startsWith(x.prefix))) {
      f_title = p.prefix;
    }
  }

  function canEdit(order: CreativeOrder): boolean {
    if (!order) return false;
    // Rule 2: Locked once added to backlog (done) or cancelled
    if (order.status === 'done' || order.status === 'cancelled') return false;
    // Rule 1: Privileged designers and admins can always edit active requests
    if (isDesigner) return true;
    // Rule 1: Original requester can edit
    const currentName = (appState.currentUser?.name || '').trim().toLowerCase();
    const orderReq = (order.requester || '').trim().toLowerCase();
    if (currentName && orderReq && (currentName === orderReq || orderReq.includes(currentName) || currentName.includes(orderReq))) {
      return true;
    }
    return false;
  }

  function selectPriority(id: string) {
    f_priority = id;
    if (id === 'tier_0') {
      const minDate = new Date(Date.now() + 21 * 86400000).toISOString().split('T')[0];
      if (!f_targetDate || f_targetDate < minDate) {
        f_targetDate = minDate;
      }
    }
  }

  function switchChannel(channel: 'digital' | 'print') {
    f_channel = channel;
    if (channel === 'digital') {
      if (!DIGITAL_FORMATS.some(f => f.id === f_format)) {
        f_format = '9_16_video';
      }
      f_material = '';
    } else {
      if (!PRINT_FORMATS.some(f => f.id === f_format)) {
        f_format = 'print_packaging_box';
      }
      if (!f_material) {
        f_material = 'waterproof_vinyl';
      }
    }
  }

  function formatBytes(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  function handleFileInput(e: Event) {
    const target = e.target as HTMLInputElement;
    if (!target.files || target.files.length === 0) return;
    Array.from(target.files).forEach(file => {
      if (file.size > 50 * 1024 * 1024) {
        appState.addToast(`${file.name} exceeds 50MB limit`, 'warning');
        return;
      }
      const reader = new FileReader();
      reader.onload = (ev) => {
        const fileData = ev.target?.result as string;
        f_files = [...f_files, {
          filename: file.name,
          sizeFormatted: formatBytes(file.size),
          fileData
        }];
      };
      reader.readAsDataURL(file);
    });
    target.value = '';
  }

  function removeSelectedFile(index: number) {
    f_files = f_files.filter((_, i) => i !== index);
  }

  async function handleSubmit(e: SubmitEvent) {
    e.preventDefault();
    formError    = '';
    isSubmitting = true;
    try {
      if (editingOrderId) {
        await ApiClient.request(`/orders/${encodeURIComponent(editingOrderId)}`, {
          method: 'PATCH',
          body: JSON.stringify({
            title:          f_title.trim(),
            entity:         f_entity,
            priority:       f_priority,
            channel:        f_channel,
            format:         f_format,
            customSize:     f_customSize.trim(),
            material:       f_material,
            copy:           f_copy.trim(),
            targetDate:     f_targetDate,
            attachmentNote: f_attachmentNote.trim(),
          }),
        });

        // Upload any new attachments if added during edit
        if (f_files.length > 0) {
          for (const file of f_files) {
            try {
              await ApiClient.request(`/orders/${encodeURIComponent(editingOrderId)}/attachments`, {
                method: 'POST',
                body: JSON.stringify({ filename: file.filename, fileData: file.fileData })
              });
            } catch (attErr) {
              console.error('[OrderFormView] Failed saving attachment on edit:', attErr);
            }
          }
        }

        submitSuccess = true;
        appState.addToast('Creative request has been updated.', 'success', 'Changes Saved');
        await loadOrders();
        setTimeout(() => { showForm = false; submitSuccess = false; editingOrderId = null; }, 1400);
      } else {
        await ApiClient.request('/orders', {
          method: 'POST',
          body: JSON.stringify({
            title:          f_title.trim(),
            entity:         f_entity,
            priority:       f_priority,
            channel:        f_channel,
            format:         f_format,
            customSize:     f_customSize.trim(),
            material:       f_material,
            copy:           f_copy.trim(),
            targetDate:     f_targetDate,
            attachmentNote: f_attachmentNote.trim(),
            attachments:    f_files.map(f => ({ filename: f.filename, fileData: f.fileData }))
          }),
        });
        submitSuccess = true;
        appState.addToast('Your creative request has been submitted and queued.', 'success', 'Request Received');
        await loadOrders();
        setTimeout(() => { showForm = false; submitSuccess = false; }, 1600);
      }
    } catch (err: any) {
      formError = err.message || 'Submission failed. Please review your inputs and try again.';
    } finally {
      isSubmitting = false;
    }
  }

  async function updateStatus(id: string, status: string) {
    try {
      await ApiClient.request(`/orders/${encodeURIComponent(id)}`, {
        method: 'PATCH',
        body: JSON.stringify({ status }),
      });
      appState.addToast('Order status updated.', 'success');
      await loadOrders();
    } catch (err: any) {
      appState.addToast(err.message || 'Update failed.', 'error');
    }
  }

  async function cancelOrder(id: string) {
    try {
      await ApiClient.request(`/orders/${encodeURIComponent(id)}`, { method: 'DELETE' });
      appState.addToast('Order has been cancelled.', 'warning', 'Cancelled');
      await loadOrders();
    } catch (err: any) {
      appState.addToast(err.message || 'Cancellation failed.', 'error');
    }
  }

  async function assignDesigner(id: string, designerName: string) {
    try {
      await ApiClient.request(`/orders/${encodeURIComponent(id)}`, {
        method: 'PATCH',
        body: JSON.stringify({ assignedTo: designerName || null }),
      });
      appState.addToast(designerName ? `Assigned order to ${designerName}.` : 'Cleared designer assignment.', 'success');
      await loadOrders();
    } catch (err: any) {
      appState.addToast(err.message || 'Assignment failed.', 'error');
    }
  }

  async function linkProject(id: string, projId: string) {
    if (!projId.trim()) return;
    try {
      await ApiClient.request(`/orders/${encodeURIComponent(id)}`, {
        method: 'PATCH',
        body: JSON.stringify({ projectId: projId.trim() }),
      });
      appState.addToast(`Linked order to project ${projId.trim()}.`, 'success');
      await loadOrders();
    } catch (err: any) {
      appState.addToast(err.message || 'Linking failed.', 'error');
    }
  }

  async function handleUploadToExistingOrder(orderId: string, e: Event) {
    const target = e.target as HTMLInputElement;
    if (!target.files || target.files.length === 0) return;
    const files = Array.from(target.files);
    target.value = '';

    for (const file of files) {
      if (file.size > 50 * 1024 * 1024) {
        appState.addToast(`${file.name} exceeds 50MB limit`, 'warning');
        continue;
      }
      const reader = new FileReader();
      reader.onload = async (ev) => {
        const fileData = ev.target?.result as string;
        try {
          await ApiClient.request(`/orders/${encodeURIComponent(orderId)}/attachments`, {
            method: 'POST',
            body: JSON.stringify({ filename: file.name, fileData })
          });
          appState.addToast(`Uploaded ${file.name} to order NAS folder`, 'success');
          await loadOrders();
        } catch (err: any) {
          appState.addToast(err.message || 'Upload failed', 'error');
        }
      };
      reader.readAsDataURL(file);
    }
  }

  async function deleteAttachment(orderId: string, filename: string) {
    if (!confirm(`Remove attachment "${filename}" from NAS _Orders folder?`)) return;
    try {
      await ApiClient.request(`/orders/${encodeURIComponent(orderId)}/attachments/${encodeURIComponent(filename)}`, {
        method: 'DELETE'
      });
      appState.addToast(`Removed ${filename}.`, 'info');
      await loadOrders();
    } catch (err: any) {
      appState.addToast(err.message || 'Failed to remove attachment', 'error');
    }
  }

  async function importToProject(orderId: string, projectId: string) {
    if (!projectId) {
      appState.addToast('Please link a project ID first before importing attachments.', 'warning');
      return;
    }
    try {
      const res = await ApiClient.request<{ success: boolean; count: number; destination: string }>(`/orders/${encodeURIComponent(orderId)}/import-to-project`, {
        method: 'POST',
        body: JSON.stringify({ projectId })
      });
      appState.addToast(`Successfully ingested ${res.count} attachments into project ${projectId} (${res.destination})!`, 'success', 'Files Ingested');
      await loadOrders();
    } catch (err: any) {
      appState.addToast(err.message || 'Ingestion failed.', 'error');
    }
  }

  async function copyNasPath(order: CreativeOrder) {
    const uncPath = order.nasPath || `\\\\SSNAS\\Creative-Team\\_Orders\\${order.id}`;
    try {
      await navigator.clipboard.writeText(uncPath);
      appState.addToast(`Copied NAS folder path: ${uncPath}`, 'success');
    } catch {
      appState.addToast(uncPath, 'info');
    }
  }

  async function copyBrief(order: CreativeOrder) {
    const text = `PROJECT: ${order.title}\n` +
      `ENTITY: ${order.entity}\n` +
      `PRIORITY: ${priorityLabel(order.priority)}\n` +
      `FORMAT: ${formatLabel(order.format, order.material || order.materialType, order.customSize)}\n` +
      (order.channel ? `CHANNEL: ${order.channel === 'print' ? 'Print, Packaging & POSM' : 'Digital & Social Screen'}\n` : '') +
      (order.material || order.materialType ? `MATERIAL: ${PRINT_MATERIALS.find(m => m.id === (order.material || order.materialType))?.label ?? (order.material || order.materialType)}\n` : '') +
      (order.customSize ? `CUSTOM DIMENSIONS: ${order.customSize}\n` : '') +
      `DEADLINE: ${fmtDate(order.targetDate)}\n` +
      `REQUESTER: ${order.requester}\n\n` +
      `--- BRIEF & COPY ---\n${order.copy}\n` +
      (order.attachmentNote ? `\n--- ASSET REFERENCES ---\n${order.attachmentNote}\n` : '');
    try {
      await navigator.clipboard.writeText(text);
      appState.addToast('Copied brief & script to clipboard!', 'success');
    } catch {
      appState.addToast('Unable to access clipboard.', 'warning');
    }
  }

  function fmtDate(iso: string) {
    if (!iso) return '—';
    try { return new Date(iso).toLocaleDateString('en-MY', { day: 'numeric', month: 'short', year: 'numeric' }); }
    catch { return iso; }
  }

  function priorityLabel(id: string) {
    return PRIORITIES.find(p => p.id === id)?.label ?? '—';
  }
  function formatLabel(id: string, material?: string, customSize?: string) {
    const all = [...DIGITAL_FORMATS, ...PRINT_FORMATS, ...LEGACY_FORMATS];
    const match = all.find(f => f.id === id);
    let str = match?.label ?? id?.replace(/_/g, ' ') ?? '—';
    if (customSize && (id === 'custom_digital' || id === 'custom_print')) {
      str += ` (${customSize})`;
    }
    if (material) {
      const mat = PRINT_MATERIALS.find(m => m.id === material);
      str += ` · ${mat ? mat.label.split('(')[0].trim() : material}`;
    }
    return str;
  }

  function getChannelInfo(order: CreativeOrder) {
    const isPrint = order.channel === 'print' || 
      (order.format && (order.format.startsWith('print_') || order.format === 'custom_print'));
    const allFormats = [...DIGITAL_FORMATS, ...PRINT_FORMATS, ...LEGACY_FORMATS];
    const fmt = allFormats.find(f => f.id === order.format);
    const mat = PRINT_MATERIALS.find(m => m.id === (order.material || order.materialType));
    
    let icon = '📱';
    if (isPrint) {
      if (order.format === 'print_label') icon = '🏷️';
      else if (order.format === 'print_banner_rollup') icon = '🚩';
      else if (order.format === 'print_posm') icon = '🖼️';
      else icon = '📦';
    } else {
      if (order.format === '16_9_landscape') icon = '🖥️';
      else if (order.format === '1_1_feed' || order.format === '4_5_portrait') icon = '📸';
      else if (order.format === 'print_digital') icon = '🌐';
      else icon = '📱';
    }

    return {
      isPrint,
      formatLabel: fmt?.label || order.format?.replace(/_/g, ' ') || 'Deliverable',
      icon,
      customSize: order.customSize,
      materialLabel: mat ? mat.label.split('(')[0].trim() : (order.material || order.materialType || null)
    };
  }
</script>

<!-- ══════════════════════════════════════════════════════════════════════════ -->
<!-- LAYOUT CONTAINER                                                          -->
<!-- ══════════════════════════════════════════════════════════════════════════ -->
<div class="view-wrap">

  <!-- ─── PAGE HEADER ──────────────────────────────────────────────────────── -->
  <div class="page-header">
    <div class="page-header-text">
      <div class="page-kicker">Creative Operations</div>
      <h1 class="page-title">Order Requests</h1>
      <p class="page-desc">
        Intake creative briefs, ad copy requirements, and campaign requests.
        Structured submission pipeline with SLA priority windows.
      </p>
    </div>
    <div class="page-actions">
      <FluentButton appearance="secondary" size="sm" onclick={loadOrders}>Refresh</FluentButton>
      <FluentButton appearance="primary" size="sm" onclick={openForm}>New Request</FluentButton>
    </div>
  </div>

  <!-- ─── STATUS FILTER TABS ────────────────────────────────────────────────── -->
  <div class="filter-row" role="tablist" aria-label="Filter by order status">
    {#each [
      { key: 'all',          label: 'All Requests'      },
      { key: 'pending',      label: 'Pending'           },
      { key: 'in_progress',  label: 'In Progress'       },
      { key: 'for_approval', label: 'For Approval'      },
      { key: 'done',         label: 'Added to Backlog'  },
    ] as tab}
      <button
        class="filter-tab {filterStatus === tab.key ? 'active' : ''}"
        role="tab"
        aria-selected={filterStatus === tab.key}
        onclick={() => filterStatus = tab.key}
      >
        {tab.label}
        <span class="tab-count">{counts[tab.key as keyof typeof counts]}</span>
      </button>
    {/each}
  </div>

  <!-- ─── FORM MODAL ──────────────────────────────────────────────────────── -->
  {#if showForm}
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div
      class="modal-backdrop"
      onclick={(e) => { if (e.target === e.currentTarget) showForm = false; }}
    >
      <div class="modal-panel" role="dialog" aria-modal="true" aria-label="New Creative Request">

        {#if submitSuccess}
          <!-- ── Success State ── -->
          <div class="success-state">
            <div class="success-mark" aria-hidden="true">
              <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="10" />
                <polyline points="9 12 11 14 15 10" />
              </svg>
            </div>
            <h2 class="success-heading">{editingOrderId ? 'Changes Saved' : 'Request Submitted'}</h2>
            <p class="success-body">
              {editingOrderId ? 'Your creative request updates have been saved to the queue.' : 'Your creative brief has been added to the design queue. The team will acknowledge within your selected priority window.'}
            </p>
          </div>

        {:else}
          <!-- ── Modal Header ── -->
          <div class="modal-header">
            <div>
              <div class="modal-kicker">{editingOrderId ? 'Edit Order Request' : 'Creative Operations · New Brief'}</div>
              <h2 class="modal-title">{editingOrderId ? `Edit Request · ${editingOrderId}` : 'Order Request Intake'}</h2>
            </div>
            <button
              class="close-btn"
              type="button"
              onclick={() => showForm = false}
              aria-label="Close"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
              </svg>
            </button>
          </div>

          <!-- ── Progress Indicator ── -->
          <div class="progress-bar" aria-label="Form completion: {formFilled} of 6 fields">
            {#each Array(6) as _, i}
              <div class="progress-seg {i < formFilled ? 'filled' : ''}"></div>
            {/each}
            <span class="progress-label">{formFilled} / 6</span>
          </div>

          <!-- ── Quick Commercial Presets ── -->
          {#if !editingOrderId}
            <div class="preset-strip">
              <div class="preset-label">QUICK COMMERCIAL PRESETS</div>
              <div class="preset-buttons">
                {#each COMMERCIAL_PRESETS as preset}
                  <button
                    type="button"
                    class="preset-btn"
                    onclick={() => applyPreset(preset.id)}
                    title="Autofill {preset.label}"
                  >
                    {preset.label}
                  </button>
                {/each}
              </div>
            </div>
          {/if}

          <!-- ── Form ── -->
          <form class="form-body" onsubmit={handleSubmit} novalidate>

            <!-- 1. Project Title -->
            <div class="field">
              <label class="field-label" for="f-title">
                Project Title
                <span class="req-mark" aria-hidden="true">*</span>
              </label>
              <input
                id="f-title"
                class="input"
                type="text"
                bind:value={f_title}
                placeholder="e.g. ANDROLAB Alpha — TikTok Hook Batch, September 2026"
                maxlength="120"
                autocomplete="off"
                required
              />
              <span class="char-hint" aria-live="polite">{f_title.length} / 120</span>
            </div>

            <!-- 2. Requesting Entity -->
            <div class="field">
              <div class="field-label" id="entity-label">
                Requesting Entity
                <span class="req-mark" aria-hidden="true">*</span>
              </div>
              <div class="seg-group" role="radiogroup" aria-labelledby="entity-label">
                {#each ENTITIES as ent}
                  <label
                    class="seg-btn {f_entity === ent.id ? 'selected' : ''}"
                    title={ent.full}
                  >
                    <input
                      type="radio"
                      name="f-entity"
                      value={ent.id}
                      bind:group={f_entity}
                      class="sr-only"
                    />
                    <span class="seg-code">{ent.id}</span>
                    <span class="seg-sub">{ent.label}</span>
                  </label>
                {/each}
              </div>
            </div>

            <!-- 3. Priority Tier -->
            <div class="field">
              <div class="field-label" id="priority-label">
                Priority Tier
                <span class="req-mark" aria-hidden="true">*</span>
              </div>
              <div class="card-group" role="radiogroup" aria-labelledby="priority-label">
                {#each PRIORITIES as p}
                  {@const sel = f_priority === p.id}
                  <!-- svelte-ignore a11y_click_events_have_key_events -->
                  <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
                  <label class="option-card {sel ? 'selected' : ''}" onclick={() => selectPriority(p.id)}>
                    <input
                      type="radio"
                      name="f-priority"
                      value={p.id}
                      bind:group={f_priority}
                      class="sr-only"
                    />
                    <div class="option-card-inner">
                      <div class="option-header">
                        <span class="option-dot {p.id}" aria-hidden="true"></span>
                        <span class="option-label">{p.label}</span>
                      </div>
                      <span class="option-window">{p.window}</span>
                      <span class="option-note">{p.note}</span>
                    </div>
                  </label>
                {/each}
              </div>
            </div>

            <!-- 4. Format, Size & Material -->
            <div class="field">
              <div class="field-label" id="format-label">
                Format &amp; Production Specs
                <span class="req-mark" aria-hidden="true">*</span>
              </div>

              <!-- Channel Switcher (Digital vs Print) -->
              <div class="channel-toggle-bar" role="tablist" aria-label="Select Asset Media Channel">
                <button
                  type="button"
                  class="channel-tab {f_channel === 'digital' ? 'active' : ''}"
                  role="tab"
                  aria-selected={f_channel === 'digital'}
                  onclick={() => switchChannel('digital')}
                >
                  <iconify-icon icon="fluent:phone-screen-24-regular" style="font-size:16px;"></iconify-icon>
                  <span>Digital &amp; Social Screen</span>
                </button>
                <button
                  type="button"
                  class="channel-tab {f_channel === 'print' ? 'active' : ''}"
                  role="tab"
                  aria-selected={f_channel === 'print'}
                  onclick={() => switchChannel('print')}
                >
                  <iconify-icon icon="fluent:print-24-regular" style="font-size:16px;"></iconify-icon>
                  <span>Print, Packaging &amp; POSM</span>
                </button>
              </div>

              <!-- Formats Grid -->
              <div class="format-grid" role="radiogroup" aria-labelledby="format-label">
                {#each (f_channel === 'digital' ? DIGITAL_FORMATS : PRINT_FORMATS) as fmt}
                  <label class="format-item {f_format === fmt.id ? 'selected' : ''}">
                    <input
                      type="radio"
                      name="f-format"
                      value={fmt.id}
                      bind:group={f_format}
                      class="sr-only"
                    />
                    <span class="fmt-label">{fmt.label}</span>
                    <span class="fmt-sub">{fmt.sub}</span>
                  </label>
                {/each}
              </div>

              <!-- Contextual Custom Size Input -->
              {#if f_format === 'custom_digital' || f_format === 'custom_print'}
                <div class="sub-field-box">
                  <label class="sub-field-label" for="f-custom-size">
                    Custom Dimensions
                    <span class="req-mark">*</span>
                  </label>
                  <input
                    id="f-custom-size"
                    class="input"
                    type="text"
                    bind:value={f_customSize}
                    placeholder={f_channel === 'digital' ? 'e.g. 1200 x 628 px (width x height)' : 'e.g. 150 x 85 x 45 mm (Packaging Box) or 3 x 7 ft'}
                    required
                  />
                  <span class="field-hint">Specify width, height, and unit (px, mm, cm, inch, ft).</span>
                </div>
              {/if}

              <!-- Contextual Material Type Picker (For Print & Packaging) -->
              {#if f_channel === 'print'}
                <div class="sub-field-box">
                  <label class="sub-field-label" for="f-material">
                    Material Type &amp; Lamination
                    <span class="optional-label">Recommended for Print</span>
                  </label>
                  <select id="f-material" class="select-input" bind:value={f_material}>
                    <option value="">— Select Material / Substrate —</option>
                    {#each PRINT_MATERIALS as mat}
                      <option value={mat.id}>{mat.label} — {mat.desc}</option>
                    {/each}
                  </select>
                  <span class="field-hint">Crucial for compounding bottles, medicine box cards, and waterproof clinic collateral.</span>
                </div>
              {/if}
            </div>

            <!-- 5. Brief / Copy -->
            <div class="field">
              <label class="field-label" for="f-copy">
                Brief &amp; Copy
                <span class="req-mark" aria-hidden="true">*</span>
              </label>
              <textarea
                id="f-copy"
                class="textarea"
                bind:value={f_copy}
                placeholder="Include: headline, promotion price, call-to-action, doctor name, and any specific messaging guidelines. You may also paste a SSNAS folder path or Google Drive link to your raw assets."
                rows="5"
                required
              ></textarea>
              <p class="field-hint">
                The more complete this field, the fewer clarification rounds required.
              </p>
            </div>

            <!-- 6. Target Date + Reference Link -->
            <div class="field-row">
              <div class="field" style="flex: 1; min-width: 0;">
                <label class="field-label" for="f-date">
                  Target Delivery Date
                  <span class="req-mark" aria-hidden="true">*</span>
                </label>
                <input
                  id="f-date"
                  class="input"
                  type="date"
                  bind:value={f_targetDate}
                  min={new Date().toISOString().split('T')[0]}
                  required
                />
              </div>
              <div class="field" style="flex: 1; min-width: 0;">
                <label class="field-label" for="f-ref">
                  Asset Reference
                  <span class="optional-label">Optional</span>
                </label>
                <input
                  id="f-ref"
                  class="input"
                  type="text"
                  bind:value={f_attachmentNote}
                  placeholder="\\SSNAS\Creative-Team\... or drive.google.com/..."
                />
              </div>
            </div>

            <!-- 7. File Attachments (NAS _Orders Vault) -->
            <div class="field">
              <label class="field-label" for="f-file-input">
                Upload Reference Files / Assets (NAS Temporary Vault)
                <span class="optional-label">Optional • Stored in \\SSNAS\_Orders</span>
              </label>
              <div class="file-dropzone">
                <input
                  id="f-file-input"
                  type="file"
                  multiple
                  class="sr-only"
                  onchange={handleFileInput}
                />
                <label for="f-file-input" class="dropzone-label">
                  <iconify-icon icon="fluent:folder-arrow-up-24-regular" style="font-size:24px; color:var(--brand-accent);"></iconify-icon>
                  <span class="dropzone-text">Click to choose files or drop images, logos, briefs, PDFs here</span>
                  <span class="dropzone-hint">PNG, JPG, WEBP, PDF, DOCX, ZIP • Up to 50 MB per file</span>
                </label>
              </div>

              {#if f_files.length > 0}
                <div class="selected-files-list">
                  {#each f_files as file, idx}
                    <div class="file-chip">
                      <span class="file-name" title={file.filename}>{file.filename}</span>
                      <span class="file-size">({file.sizeFormatted})</span>
                      <button
                        type="button"
                        class="file-remove-btn"
                        onclick={() => removeSelectedFile(idx)}
                        title="Remove file"
                      >✕</button>
                    </div>
                  {/each}
                </div>
              {/if}
            </div>

            <!-- Error -->
            {#if formError}
              <div class="error-msg" role="alert">
                <svg width="13" height="13" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                  <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/>
                </svg>
                {formError}
              </div>
            {/if}

            <!-- Footer Actions -->
            <div class="modal-footer">
              <button type="button" class="btn-ghost" onclick={() => showForm = false}>
                Cancel
              </button>
              <button
                type="submit"
                class="btn-primary {formValid && !isSubmitting ? '' : 'disabled'}"
                disabled={!formValid || isSubmitting}
              >
                {#if isSubmitting}
                  <span class="spinner" aria-hidden="true"></span>
                  {editingOrderId ? 'Saving…' : 'Submitting…'}
                {:else}
                  {editingOrderId ? 'Save Changes' : 'Submit Request'}
                {/if}
              </button>
            </div>
          </form>
        {/if}
      </div>
    </div>
  {/if}

  <!-- ─── ORDER TABLE ──────────────────────────────────────────────────────── -->
  {#if isLoading}
    <div class="state-shell">
      <div class="loading-spinner" aria-label="Loading"></div>
      <p class="state-label">Loading order queue…</p>
    </div>

  {:else if filteredOrders.length === 0}
    <div class="state-shell empty">
      <div class="empty-icon" aria-hidden="true">
        <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.25" stroke-linecap="round" stroke-linejoin="round">
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
          <polyline points="14 2 14 8 20 8"/>
          <line x1="16" y1="13" x2="8" y2="13"/>
          <line x1="16" y1="17" x2="8" y2="17"/>
        </svg>
      </div>
      <p class="state-label">
        {filterStatus === 'all' ? 'No creative requests on record.' : `No ${STATUS_META[filterStatus]?.label ?? filterStatus} orders.`}
      </p>
      <p class="state-sub">Submit the first request or adjust the status filter.</p>
      {#if filterStatus === 'all'}
        <FluentButton appearance="primary" size="sm" onclick={openForm}>New Request</FluentButton>
      {/if}
    </div>

  {:else}
    <div class="table-shell">
      <table class="order-table" aria-label="Creative order queue">
        <thead>
          <tr>
            <th scope="col">Order ID</th>
            <th scope="col">Project Title</th>
            <th scope="col">Entity</th>
            <th scope="col">Priority</th>
            <th scope="col">Format</th>
            <th scope="col">Target Date</th>
            <th scope="col">Submitted by</th>
            <th scope="col">Status</th>
            <th scope="col" class="col-actions">Actions</th>
          </tr>
        </thead>
        <tbody>
          {#each filteredOrders as order (order.id)}
            {@const sm  = STATUS_META[order.status] ?? STATUS_META.pending}
            {@const pr  = PRIORITY_COLOR[order.priority]}
            {@const expanded = activeOrderId === order.id}
            {@const ch  = getChannelInfo(order)}
            <tr
              class="order-row {expanded ? 'row-open' : ''}"
              aria-expanded={expanded}
              tabindex="0"
              role="button"
              onclick={() => activeOrderId = expanded ? null : order.id}
              onkeydown={(e) => e.key === 'Enter' && (activeOrderId = expanded ? null : order.id)}
            >
              <td>
                <span class="id-tag">{order.id}</span>
              </td>
              <td>
                <span class="title-cell">{order.title}</span>
              </td>
              <td>
                <span class="entity-tag">{order.entity}</span>
              </td>
              <td>
                {#if pr}
                  <span
                    class="priority-tag {order.priority === 'tier_0' ? 'priority-tier0' : ''}"
                    style="color: {pr.fg}; background: {pr.bg}; border-color: {pr.border};"
                  >
                    {#if order.priority === 'tier_0'}🗓️ {/if}
                    {#if order.priority === 'tier_3'}⚡ {/if}
                    {priorityLabel(order.priority)}
                  </span>
                {:else}
                  <span class="priority-tag">—</span>
                {/if}
              </td>
              <td>
                <div class="format-cell-cluster">
                  <div class="format-badge-row">
                    <span class="channel-chip {ch.isPrint ? 'chip-print' : 'chip-digital'}">
                      <span class="channel-icon">{ch.icon}</span>
                      <span>{ch.formatLabel}</span>
                    </span>
                  </div>
                  {#if ch.customSize || ch.materialLabel}
                    <div class="format-sub-row">
                      {#if ch.customSize}
                        <span class="size-chip" title="Custom Dimensions">📐 {ch.customSize}</span>
                      {/if}
                      {#if ch.materialLabel}
                        <span class="mat-chip" title="Print Substrate">🏷️ {ch.materialLabel}</span>
                      {/if}
                    </div>
                  {/if}
                </div>
              </td>
              <td>
                <span class="meta-cell">{fmtDate(order.targetDate)}</span>
              </td>
              <td>
                <span class="meta-cell">{order.requester}</span>
              </td>
              <td>
                <span
                  class="status-tag {order.status === 'done' ? 'status-backlog' : ''}"
                  style="color: {sm.fg}; background: {sm.bg};"
                >
                  {#if order.status === 'done'}<span class="lock-icon" title="Brief locked in backlog">🔒 </span>{/if}
                  {sm.label}
                </span>
              </td>
              <td class="col-actions" onclick={(e) => e.stopPropagation()}>
                <div class="action-cluster">
                  {#if canEdit(order)}
                    <button class="act-btn act-blue" onclick={() => openEditForm(order)} title="Edit Request Details">Edit</button>
                  {/if}
                  {#if isDesigner}
                    {#if order.status === 'pending'}
                      <button class="act-btn act-blue" onclick={() => updateStatus(order.id, 'in_progress')} title="Start">Start</button>
                    {:else if order.status === 'in_progress'}
                      <button class="act-btn act-amber" onclick={() => updateStatus(order.id, 'for_approval')} title="Send for Approval">Review</button>
                    {:else if order.status === 'for_approval'}
                      <button class="act-btn act-green" onclick={() => updateStatus(order.id, 'done')} title="Accept into design backlog">Add to Backlog</button>
                    {/if}
                  {/if}
                  {#if (isDesigner || canEdit(order)) && order.status !== 'done' && order.status !== 'cancelled'}
                    <button class="act-btn act-red" onclick={() => cancelOrder(order.id)} title="Cancel Order">Cancel</button>
                  {/if}
                </div>
              </td>
            </tr>

            {#if expanded}
              <tr class="detail-row">
                <td colspan={isDesigner ? 9 : 8}>
                  <div class="detail-panel">
                    <div class="detail-grid">
                      <div class="detail-col wide">
                        <span class="detail-label">Brief &amp; Copy</span>
                        <pre class="detail-copy">{order.copy}</pre>
                      </div>
                      <div class="detail-col">
                        <span class="detail-label">Format &amp; Specifications</span>
                        <div class="spec-pills-wrap">
                          <span class="channel-chip {ch.isPrint ? 'chip-print' : 'chip-digital'}">
                            <span class="channel-icon">{ch.icon}</span>
                            <span>{ch.isPrint ? 'Print, Packaging & POSM' : 'Digital & Social Screen'} · {ch.formatLabel}</span>
                          </span>
                          {#if ch.customSize}
                            <span class="size-chip">📐 {ch.customSize}</span>
                          {/if}
                          {#if ch.materialLabel}
                            <span class="mat-chip">🏷️ {ch.materialLabel}</span>
                          {/if}
                        </div>
                      </div>
                      {#if order.material || order.materialType}
                        <div class="detail-col">
                          <span class="detail-label">Print Material &amp; Finishing</span>
                          <span class="detail-val">🏷️ {PRINT_MATERIALS.find(m => m.id === (order.material || order.materialType))?.label ?? (order.material || order.materialType)}</span>
                        </div>
                      {/if}
                      {#if order.customSize}
                        <div class="detail-col">
                          <span class="detail-label">Custom Dimensions</span>
                          <span class="detail-val">📐 {order.customSize}</span>
                        </div>
                      {/if}
                      {#if order.attachmentNote}
                        <div class="detail-col">
                          <span class="detail-label">Asset Reference</span>
                          <span class="detail-mono">{order.attachmentNote}</span>
                        </div>
                      {/if}
                      <div class="detail-col">
                        <span class="detail-label">Date Submitted</span>
                        <span class="detail-val">{fmtDate(order.submittedAt)}</span>
                      </div>
                      {#if order.assignedTo}
                        <div class="detail-col">
                          <span class="detail-label">Assigned Designer</span>
                          <span class="detail-val">{order.assignedTo}</span>
                        </div>
                      {/if}
                      {#if order.projectId}
                        <div class="detail-col">
                          <span class="detail-label">Project Folder</span>
                          <button
                            class="proj-link"
                            onclick={() => appState.navigate('project-detail', { id: order.projectId! })}
                          >Open Workspace ↗</button>
                        </div>
                      {/if}
                    </div>

                    <!-- ── Attached Reference Files Section ── -->
                    <div class="detail-attachments-section">
                      <div class="att-header">
                        <span class="detail-label">
                          Attached Reference Files ({order.attachments?.length || 0})
                        </span>
                        <div class="att-header-actions">
                          {#if canEdit(order)}
                            <button
                              type="button"
                              class="text-link-btn"
                              onclick={(e) => { e.stopPropagation(); openEditForm(order); }}
                              title="Edit project brief and parameters"
                            >
                              ✏️ Edit Request
                            </button>
                          {/if}
                          <button
                            type="button"
                            class="text-link-btn"
                            onclick={(e) => { e.stopPropagation(); copyNasPath(order); }}
                            title="Copy physical UNC path for Windows Explorer"
                          >
                            📁 Copy NAS Folder Path
                          </button>
                          <label class="upload-more-btn" onclick={(e) => e.stopPropagation()}>
                            <span>+ Upload File</span>
                            <input
                              type="file"
                              multiple
                              class="sr-only"
                              onchange={(e) => handleUploadToExistingOrder(order.id, e)}
                            />
                          </label>
                        </div>
                      </div>

                      {#if order.attachments && order.attachments.length > 0}
                        <div class="attachments-grid">
                          {#each order.attachments as att}
                            <div class="att-card" onclick={(e) => e.stopPropagation()}>
                              <div class="att-icon-box">
                                {#if att.filename.match(/\.(png|jpg|jpeg|webp|gif|svg)$/i)}
                                  <iconify-icon icon="fluent:image-24-regular" style="color:#0078D4; font-size:20px;"></iconify-icon>
                                {:else if att.filename.match(/\.pdf$/i)}
                                  <iconify-icon icon="fluent:document-pdf-24-regular" style="color:#E11D48; font-size:20px;"></iconify-icon>
                                {:else if att.filename.match(/\.(mp4|mov|avi|mkv)$/i)}
                                  <iconify-icon icon="fluent:video-24-regular" style="color:#7C3AED; font-size:20px;"></iconify-icon>
                                {:else if att.filename.match(/\.(zip|rar|7z|tar|gz)$/i)}
                                  <iconify-icon icon="fluent:folder-zip-24-regular" style="color:#D97706; font-size:20px;"></iconify-icon>
                                {:else}
                                  <iconify-icon icon="fluent:document-24-regular" style="color:#64748B; font-size:20px;"></iconify-icon>
                                {/if}
                              </div>
                              <div class="att-info">
                                <a
                                  href={att.url}
                                  target="_blank"
                                  rel="noreferrer"
                                  class="att-filename"
                                  title={`Open ${att.filename}`}
                                >{att.filename}</a>
                                <span class="att-meta">{att.sizeFormatted || '—'}</span>
                              </div>
                              <div class="att-actions">
                                <a
                                  href={att.url}
                                  target="_blank"
                                  rel="noreferrer"
                                  class="att-act-btn"
                                  title="View in new tab"
                                >↗</a>
                                {#if isDesigner}
                                  <button
                                    type="button"
                                    class="att-act-btn att-del"
                                    onclick={() => deleteAttachment(order.id, att.filename)}
                                    title="Delete attachment"
                                  >✕</button>
                                {/if}
                              </div>
                            </div>
                          {/each}
                        </div>

                        {#if isDesigner && order.projectId}
                          <div class="ingest-row">
                            <button
                              type="button"
                              class="act-btn act-blue mgt-btn"
                              onclick={(e) => { e.stopPropagation(); importToProject(order.id, order.projectId!); }}
                              title="Copy all attachments directly to project 01_BRIEF_ASSETS folder"
                            >
                              📥 Copy All Attachments to Project {order.projectId} (01_BRIEF_ASSETS)
                            </button>
                          </div>
                        {/if}
                      {:else}
                        <div class="att-empty">
                          <span class="att-empty-text">No files attached yet. Requesters and designers can attach references here.</span>
                        </div>
                      {/if}
                    </div>

                    <!-- ── Processing & Management Controls ── -->
                    {#if isDesigner}
                      <div class="detail-management-bar">
                        <div class="mgt-col">
                          <span class="detail-label">Designer Handover</span>
                          <select
                            class="mgt-select"
                            value={order.assignedTo || ''}
                            onclick={(e) => e.stopPropagation()}
                            onchange={(e) => {
                              e.stopPropagation();
                              assignDesigner(order.id, (e.target as HTMLSelectElement).value);
                            }}
                          >
                            <option value="">-- Assign Designer --</option>
                            {#each (roster.length > 0 ? roster : [{name:'Harussani'},{name:'Haikal'},{name:'Aliff'},{name:'Raihan'},{name:'Hasan'}]) as staff}
                              <option value={staff.name} selected={order.assignedTo === staff.name}>{staff.name}</option>
                            {/each}
                          </select>
                        </div>

                        <div class="mgt-col">
                          <span class="detail-label">Project Workspace Linking</span>
                          <div class="mgt-link-row" onclick={(e) => e.stopPropagation()}>
                            <input
                              type="text"
                              class="mgt-input"
                              placeholder="Link Project ID (e.g. 0007V)"
                              value={order.projectId || ''}
                              id={`proj-input-${order.id}`}
                              onkeydown={(e) => {
                                if (e.key === 'Enter') {
                                  linkProject(order.id, (e.target as HTMLInputElement).value);
                                }
                              }}
                            />
                            <button
                              class="act-btn act-blue mgt-btn"
                              onclick={() => {
                                const input = document.getElementById(`proj-input-${order.id}`) as HTMLInputElement;
                                if (input) linkProject(order.id, input.value);
                              }}
                            >Link</button>
                          </div>
                        </div>

                        <div class="mgt-col actions-right">
                          <span class="detail-label">Studio Handover</span>
                          <button
                            class="act-btn act-blue mgt-btn"
                            onclick={(e) => { e.stopPropagation(); copyBrief(order); }}
                            title="Copy title, entity, format, and full script to clipboard"
                          >
                            📋 Copy Brief &amp; Script
                          </button>
                        </div>
                      </div>
                    {/if}
                  </div>
                </td>
              </tr>
            {/if}
          {/each}
        </tbody>
      </table>
    </div>
  {/if}

  <!-- ─── INFO STRIP ───────────────────────────────────────────────────────── -->
  <div class="info-strip">
    <div class="info-block">
      <h3 class="info-heading">For Requesters</h3>
      <ul class="info-list">
        <li>Complete all six fields accurately to avoid revision cycles.</li>
        <li>Select a priority tier that reflects your actual deadline, not urgency preference.</li>
        <li>Include raw asset links (SSNAS path or Google Drive) where applicable.</li>
        <li>Track your request status in real-time through this queue.</li>
      </ul>
    </div>
    <div class="info-divider" aria-hidden="true"></div>
    <div class="info-block">
      <h3 class="info-heading">For Design Team</h3>
      <ul class="info-list">
        <li>All briefs arrive structured — title, format, copy, and deadline are pre-defined.</li>
        <li>Progress orders through <strong>Start → For Review → Complete</strong> to reflect live status.</li>
        <li>Link a project folder once the workspace has been created on SSNAS.</li>
        <li>Urgent Tier 3 requests require documented justification in the audit log.</li>
      </ul>
    </div>
  </div>

</div>

<style>
  /* ── Layout ── */
  .view-wrap {
    display: flex;
    flex-direction: column;
    gap: 20px;
    padding-bottom: 48px;
    font-family: var(--font-family);
  }

  /* ── Page Header ── */
  .page-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    flex-wrap: wrap;
    gap: 16px;
  }
  .page-kicker {
    font-size: 11px;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.7px;
    color: var(--brand-accent);
    margin-bottom: 6px;
  }
  .page-title {
    font-size: 24px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0 0 5px;
    letter-spacing: -0.3px;
  }
  .page-desc {
    font-size: 13px;
    color: var(--text-secondary);
    margin: 0;
    line-height: 1.55;
    max-width: 540px;
  }
  .page-actions {
    display: flex;
    gap: 8px;
    flex-shrink: 0;
    align-items: center;
  }

  /* ── Filter Tabs ── */
  .filter-row {
    display: flex;
    gap: 2px;
    border-bottom: 1px solid var(--surface-card-border);
    padding-bottom: 1px;
    flex-wrap: wrap;
  }
  .filter-tab {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 7px 14px;
    font-size: 12.5px;
    font-weight: 600;
    color: var(--text-tertiary);
    background: transparent;
    border: none;
    border-bottom: 2px solid transparent;
    cursor: pointer;
    transition: color var(--transition-fast), border-color var(--transition-fast);
    margin-bottom: -1px;
    white-space: nowrap;
  }
  .filter-tab:hover  { color: var(--text-primary); }
  .filter-tab.active {
    color: var(--brand-accent);
    border-bottom-color: var(--brand-accent);
    font-weight: 700;
  }
  .tab-count {
    font-size: 11px;
    font-weight: 700;
    color: inherit;
    background: var(--surface-card-border);
    padding: 1px 6px;
    border-radius: var(--radius-pill);
    opacity: 0.8;
  }
  .filter-tab.active .tab-count {
    background: rgba(33, 161, 247, 0.15);
  }

  /* ── Modal Backdrop ── */
  .modal-backdrop {
    position: fixed;
    inset: 0;
    background: rgba(2, 32, 87, 0.55);
    backdrop-filter: blur(5px);
    -webkit-backdrop-filter: blur(5px);
    z-index: 700;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 20px;
  }
  .modal-panel {
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-xl);
    width: 100%;
    max-width: 660px;
    max-height: 90vh;
    overflow-y: auto;
    box-shadow: var(--shadow-xl);
    animation: panel-enter 0.22s cubic-bezier(0.34, 1.4, 0.64, 1);
  }
  @keyframes panel-enter {
    from { transform: translateY(12px) scale(0.97); opacity: 0; }
    to   { transform: translateY(0)    scale(1);    opacity: 1; }
  }

  /* ── Success State ── */
  .success-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    text-align: center;
    padding: 56px 40px;
    gap: 14px;
    animation: fade-up 0.3s ease;
  }
  @keyframes fade-up {
    from { opacity: 0; transform: translateY(8px); }
    to   { opacity: 1; transform: translateY(0); }
  }
  .success-mark {
    width: 64px;
    height: 64px;
    border-radius: 50%;
    background: var(--color-success-bg);
    color: var(--color-success);
    display: flex;
    align-items: center;
    justify-content: center;
    margin-bottom: 4px;
  }
  .success-heading {
    font-size: 20px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0;
  }
  .success-body {
    font-size: 13.5px;
    color: var(--text-secondary);
    margin: 0;
    max-width: 380px;
    line-height: 1.55;
  }

  /* ── Modal Header ── */
  .modal-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    padding: 24px 24px 0;
    gap: 12px;
  }
  .modal-kicker {
    font-size: 10.5px;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.6px;
    color: var(--brand-accent);
    margin-bottom: 5px;
  }
  .modal-title {
    font-size: 18px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0;
  }
  .close-btn {
    width: 30px;
    height: 30px;
    border-radius: var(--radius-md);
    border: 1px solid var(--surface-card-border);
    background: var(--surface-card);
    color: var(--text-tertiary);
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    transition: all var(--transition-fast);
  }
  .close-btn:hover {
    background: var(--color-danger-bg);
    color: var(--color-danger);
    border-color: var(--color-danger-border);
  }

  /* ── Progress Bar ── */
  .progress-bar {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 14px 24px 0;
  }
  .progress-seg {
    flex: 1;
    height: 2px;
    background: var(--surface-card-border);
    border-radius: 2px;
    transition: background var(--transition-fast);
  }
  .progress-seg.filled {
    background: var(--brand-accent);
  }
  .progress-label {
    font-size: 10.5px;
    font-weight: 700;
    color: var(--text-tertiary);
    margin-left: 8px;
    white-space: nowrap;
    min-width: 28px;
    text-align: right;
  }

  /* ── Commercial Presets Strip ── */
  .preset-strip {
    margin: 14px 24px 0;
    padding: 10px 14px;
    background: var(--surface-card-subtle, #F8FAFC);
    border: 1px solid var(--surface-card-border, #E2E8F0);
    border-radius: 8px;
  }
  .preset-label {
    font-size: 10px;
    font-weight: 800;
    color: var(--text-tertiary, #94A3B8);
    letter-spacing: 0.5px;
    margin-bottom: 8px;
  }
  .preset-buttons {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
  }
  .preset-btn {
    font-size: 11.5px;
    font-weight: 600;
    padding: 5px 12px;
    border-radius: 6px;
    background: var(--surface-card, #FFFFFF);
    border: 1px solid var(--surface-card-border, #CBD5E1);
    color: var(--text-primary, #0F172A);
    cursor: pointer;
    transition: all 0.15s ease;
  }
  .preset-btn:hover {
    border-color: var(--brand-accent, #21A1F7);
    color: var(--brand-primary, #043388);
    background: #EBF4FE;
  }

  /* ── Form Body ── */
  .form-body {
    padding: 20px 24px 24px;
    display: flex;
    flex-direction: column;
    gap: 22px;
  }
  .field {
    display: flex;
    flex-direction: column;
    gap: 7px;
    position: relative;
  }
  .field-row {
    display: flex;
    gap: 16px;
  }
  @media (max-width: 580px) {
    .field-row { flex-direction: column; }
  }
  .field-label {
    font-size: 12.5px;
    font-weight: 700;
    color: var(--text-primary);
    display: flex;
    align-items: center;
    gap: 5px;
    user-select: none;
  }
  .req-mark {
    color: var(--color-danger);
    font-weight: 700;
    line-height: 1;
  }
  .optional-label {
    font-size: 10.5px;
    font-weight: 600;
    color: var(--text-tertiary);
    background: var(--surface-card-subtle);
    border: 1px solid var(--surface-card-border);
    padding: 1px 5px;
    border-radius: 4px;
  }
  .char-hint {
    position: absolute;
    right: 0;
    bottom: -18px;
    font-size: 10.5px;
    color: var(--text-tertiary);
    pointer-events: none;
  }

  /* Text Inputs */
  .input, .textarea {
    width: 100%;
    box-sizing: border-box;
    padding: 8px 12px;
    border-radius: var(--radius-md);
    border: 1px solid var(--surface-card-border);
    background: var(--surface-card-subtle);
    color: var(--text-primary);
    font-size: 13px;
    font-family: var(--font-family);
    transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
  }
  .input::placeholder, .textarea::placeholder { color: var(--text-tertiary); }
  .input:focus, .textarea:focus {
    outline: none;
    border-color: var(--brand-accent);
    box-shadow: 0 0 0 3px rgba(33, 161, 247, 0.14);
    background: var(--surface-card);
  }
  .textarea {
    resize: vertical;
    min-height: 100px;
    line-height: 1.55;
  }
  .field-hint {
    font-size: 11.5px;
    color: var(--text-tertiary);
    line-height: 1.45;
    margin-top: 1px;
  }

  /* Segmented Entity Selector */
  .seg-group {
    display: flex;
    gap: 6px;
    flex-wrap: wrap;
  }
  .seg-btn {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 8px 14px;
    min-width: 70px;
    border-radius: var(--radius-md);
    border: 1.5px solid var(--surface-card-border);
    background: var(--surface-card);
    cursor: pointer;
    text-align: center;
    transition: border-color var(--transition-fast), background var(--transition-fast), box-shadow var(--transition-fast);
  }
  .seg-btn:hover {
    border-color: var(--brand-accent);
  }
  .seg-btn.selected {
    border-color: var(--brand-accent);
    background: rgba(33, 161, 247, 0.07);
    box-shadow: 0 0 0 2px rgba(33, 161, 247, 0.18);
  }
  .seg-code {
    font-size: 13px;
    font-weight: 800;
    color: var(--text-primary);
    letter-spacing: 0.3px;
  }
  .seg-sub {
    font-size: 10px;
    font-weight: 500;
    color: var(--text-tertiary);
    margin-top: 1px;
  }

  /* Priority Cards */
  .card-group {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
  }
  .option-card {
    flex: 1;
    min-width: 140px;
    border-radius: var(--radius-md);
    border: 1.5px solid var(--surface-card-border);
    background: var(--surface-card);
    cursor: pointer;
    transition: border-color var(--transition-fast), background var(--transition-fast), box-shadow var(--transition-fast);
    overflow: hidden;
  }
  .option-card:hover { border-color: var(--brand-secondary); }
  .option-card.selected {
    border-color: var(--brand-accent);
    background: rgba(33, 161, 247, 0.05);
    box-shadow: 0 0 0 2px rgba(33, 161, 247, 0.18);
  }
  .option-card-inner {
    display: flex;
    flex-direction: column;
    gap: 4px;
    padding: 12px 14px;
  }
  .option-header {
    display: flex;
    align-items: center;
    gap: 7px;
    margin-bottom: 2px;
  }
  .option-dot {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    flex-shrink: 0;
  }
  .option-dot.tier_0 { background: #64748B; }
  .option-dot.tier_1 { background: var(--color-success); }
  .option-dot.tier_2 { background: var(--color-warning); }
  .option-dot.tier_3 { background: var(--color-danger); }
  .option-label {
    font-size: 13px;
    font-weight: 700;
    color: var(--text-primary);
  }
  .option-window {
    font-size: 11.5px;
    font-weight: 700;
    color: var(--text-secondary);
  }
  .option-note {
    font-size: 11px;
    color: var(--text-tertiary);
    line-height: 1.4;
  }

  /* Format Grid */
  .format-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(135px, 1fr));
    gap: 8px;
  }
  .format-item {
    display: flex;
    flex-direction: column;
    gap: 3px;
    padding: 10px 12px;
    border-radius: var(--radius-md);
    border: 1.5px solid var(--surface-card-border);
    background: var(--surface-card);
    cursor: pointer;
    transition: border-color var(--transition-fast), background var(--transition-fast), box-shadow var(--transition-fast);
  }
  .format-item:hover { border-color: var(--brand-accent); }
  .format-item.selected {
    border-color: var(--brand-accent);
    background: rgba(33, 161, 247, 0.06);
    box-shadow: 0 0 0 2px rgba(33, 161, 247, 0.16);
  }
  .fmt-label {
    font-size: 12.5px;
    font-weight: 700;
    color: var(--text-primary);
  }
  .fmt-sub {
    font-size: 10.5px;
    color: var(--text-tertiary);
    line-height: 1.4;
  }

  /* Channel & Custom Field Controls */
  .channel-toggle-bar {
    display: flex;
    gap: 6px;
    background: var(--surface-card-subtle);
    padding: 4px;
    border-radius: var(--radius-md);
    border: 1px solid var(--surface-card-border);
    margin-bottom: 8px;
  }
  .channel-tab {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    padding: 8px 14px;
    border-radius: var(--radius-sm);
    border: 1px solid transparent;
    background: transparent;
    color: var(--text-secondary);
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    transition: all var(--transition-fast);
  }
  .channel-tab:hover {
    color: var(--text-primary);
    background: rgba(255, 255, 255, 0.05);
  }
  .channel-tab.active {
    background: var(--surface-card);
    color: var(--brand-accent);
    border-color: var(--surface-card-border);
    font-weight: 700;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
  }
  .sub-field-box {
    display: flex;
    flex-direction: column;
    gap: 6px;
    background: var(--surface-card-subtle);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-md);
    padding: 12px 14px;
    margin-top: 8px;
  }
  .sub-field-label {
    font-size: 12px;
    font-weight: 700;
    color: var(--text-primary);
    display: flex;
    align-items: center;
    gap: 6px;
  }
  .select-input {
    width: 100%;
    box-sizing: border-box;
    padding: 8px 12px;
    border-radius: var(--radius-md);
    border: 1px solid var(--surface-card-border);
    background: var(--surface-card);
    color: var(--text-primary);
    font-size: 13px;
    font-family: var(--font-family);
    transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
    cursor: pointer;
  }
  .select-input:focus {
    outline: none;
    border-color: var(--brand-accent);
    box-shadow: 0 0 0 3px rgba(33, 161, 247, 0.14);
  }

  /* Error */
  .error-msg {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 10px 14px;
    border-radius: var(--radius-md);
    background: var(--color-danger-bg);
    border: 1px solid var(--color-danger-border);
    color: var(--color-danger);
    font-size: 13px;
    font-weight: 600;
  }

  /* Modal Footer */
  .modal-footer {
    display: flex;
    justify-content: flex-end;
    align-items: center;
    gap: 10px;
    padding-top: 16px;
    border-top: 1px solid var(--surface-card-border);
    margin-top: 2px;
  }
  .btn-ghost {
    padding: 8px 16px;
    border-radius: var(--radius-md);
    border: 1px solid var(--surface-card-border);
    background: var(--surface-card);
    color: var(--text-secondary);
    font-size: 13px;
    font-weight: 600;
    font-family: var(--font-family);
    cursor: pointer;
    transition: all var(--transition-fast);
  }
  .btn-ghost:hover {
    color: var(--text-primary);
    border-color: var(--text-tertiary);
  }
  .btn-primary {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    padding: 9px 20px;
    border-radius: var(--radius-md);
    border: none;
    background: var(--brand-primary);
    color: #FFFFFF;
    font-size: 13.5px;
    font-weight: 700;
    font-family: var(--font-family);
    cursor: pointer;
    transition: background var(--transition-fast), box-shadow var(--transition-fast);
    box-shadow: var(--shadow-md);
  }
  .btn-primary:hover:not(.disabled) { background: var(--brand-secondary); box-shadow: var(--shadow-lg); }
  .btn-primary.disabled { opacity: 0.42; cursor: not-allowed; }
  .spinner {
    width: 13px;
    height: 13px;
    border: 2px solid rgba(255, 255, 255, 0.35);
    border-top-color: #FFFFFF;
    border-radius: 50%;
    animation: spin 0.65s linear infinite;
    flex-shrink: 0;
  }
  @keyframes spin { to { transform: rotate(360deg); } }

  /* ── Table ── */
  .table-shell {
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-lg);
    overflow: hidden;
    box-shadow: var(--shadow-sm);
  }
  .order-table {
    width: 100%;
    border-collapse: collapse;
  }
  .order-table thead tr {
    border-bottom: 1px solid var(--surface-card-border);
    background: var(--surface-card-subtle);
  }
  .order-table th {
    padding: 10px 14px;
    font-size: 11px;
    font-weight: 700;
    color: var(--text-tertiary);
    text-transform: uppercase;
    letter-spacing: 0.5px;
    text-align: left;
    white-space: nowrap;
  }
  .order-row {
    border-bottom: 1px solid var(--surface-card-border);
    cursor: pointer;
    transition: background var(--transition-fast);
    outline: none;
  }
  .order-row:last-of-type { border-bottom: none; }
  .order-row:hover   { background: var(--surface-card-hover); }
  .order-row:focus-visible { box-shadow: inset 0 0 0 2px var(--brand-accent); }
  .order-row.row-open { background: rgba(33, 161, 247, 0.04); }
  .order-table td {
    padding: 11px 14px;
    font-size: 12.5px;
    color: var(--text-primary);
    vertical-align: middle;
  }
  .col-actions { text-align: right; }

  /* Cell Types */
  .id-tag {
    font-family: var(--font-mono);
    font-size: 11px;
    font-weight: 700;
    color: var(--brand-accent);
    background: rgba(33, 161, 247, 0.09);
    padding: 2px 7px;
    border-radius: var(--radius-sm);
    white-space: nowrap;
  }
  .title-cell {
    font-weight: 600;
    color: var(--text-primary);
    font-size: 13px;
    max-width: 220px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    display: block;
  }
  .entity-tag {
    font-size: 11px;
    font-weight: 700;
    color: var(--text-brand);
    background: rgba(4, 51, 136, 0.08);
    padding: 2px 7px;
    border-radius: var(--radius-sm);
    letter-spacing: 0.3px;
  }
  .priority-tag {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    font-size: 11.5px;
    font-weight: 700;
    padding: 2px 8px;
    border-radius: var(--radius-sm);
    border: 1px solid transparent;
    white-space: nowrap;
  }
  .priority-tier0 {
    letter-spacing: 0.3px;
    border: 1px dashed #94A3B8 !important;
  }
  .meta-cell {
    color: var(--text-secondary);
    font-size: 12.5px;
  }
  .status-tag {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    font-size: 11.5px;
    font-weight: 700;
    padding: 3px 9px;
    border-radius: var(--radius-pill);
    white-space: nowrap;
  }
  .status-backlog {
    border: 1px solid #A7F3D0;
    font-weight: 800;
  }
  .lock-icon {
    font-size: 10px;
    line-height: 1;
  }

  /* Contextual Format & Packaging Chips */
  .format-cell-cluster {
    display: flex;
    flex-direction: column;
    gap: 3px;
    max-width: 250px;
  }
  .format-badge-row {
    display: flex;
    align-items: center;
    gap: 5px;
  }
  .format-sub-row {
    display: flex;
    align-items: center;
    gap: 4px;
    flex-wrap: wrap;
    margin-top: 1px;
  }
  .channel-chip {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    font-size: 11px;
    font-weight: 700;
    padding: 2px 7px;
    border-radius: var(--radius-sm);
    white-space: nowrap;
  }
  .channel-icon {
    font-size: 11px;
    line-height: 1;
  }
  .chip-digital {
    background: rgba(33, 161, 247, 0.12);
    color: #0284C7;
    border: 1px solid rgba(33, 161, 247, 0.25);
  }
  .chip-print {
    background: rgba(189, 154, 115, 0.14);
    color: #9A7B4F;
    border: 1px solid rgba(189, 154, 115, 0.3);
  }
  .size-chip {
    font-size: 10px;
    font-weight: 700;
    font-family: var(--font-mono);
    color: var(--text-secondary);
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    padding: 1px 5px;
    border-radius: 4px;
  }
  .mat-chip {
    font-size: 10px;
    font-weight: 600;
    color: var(--text-tertiary);
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    padding: 1px 5px;
    border-radius: 4px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 170px;
  }
  .spec-pills-wrap {
    display: flex;
    align-items: center;
    gap: 6px;
    flex-wrap: wrap;
    margin-top: 3px;
  }

  /* Row Actions */
  .action-cluster {
    display: flex;
    justify-content: flex-end;
    gap: 5px;
    flex-wrap: nowrap;
  }
  .act-btn {
    padding: 4px 10px;
    border-radius: var(--radius-sm);
    font-size: 11.5px;
    font-weight: 700;
    font-family: var(--font-family);
    border: 1px solid var(--surface-card-border);
    background: var(--surface-card);
    cursor: pointer;
    transition: all var(--transition-fast);
    white-space: nowrap;
  }
  .act-blue  { color: #1D4ED8; }
  .act-blue:hover  { background: #EFF6FF; border-color: #BFDBFE; }
  .act-amber { color: #92400E; }
  .act-amber:hover { background: #FFFBEB; border-color: #FDE68A; }
  .act-green { color: #065F46; }
  .act-green:hover { background: #ECFDF5; border-color: #A7F3D0; }
  .act-red   { color: var(--color-danger); }
  .act-red:hover   { background: var(--color-danger-bg); border-color: var(--color-danger-border); }

  /* Expanded Detail Panel */
  .detail-row td { padding: 0; border-bottom: 1px solid var(--surface-card-border); }
  .detail-panel {
    padding: 16px 20px;
    background: var(--surface-card-subtle);
    border-top: 1px solid var(--surface-card-border);
  }
  .detail-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 18px;
  }
  .detail-col { display: flex; flex-direction: column; gap: 5px; }
  .detail-col.wide { grid-column: 1 / -1; }
  .detail-label {
    font-size: 10.5px;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    color: var(--text-tertiary);
  }
  .detail-copy {
    font-size: 13px;
    color: var(--text-primary);
    white-space: pre-wrap;
    word-break: break-word;
    font-family: var(--font-family);
    margin: 0;
    line-height: 1.55;
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-md);
    padding: 10px 12px;
  }
  .detail-val  { font-size: 13px; color: var(--text-primary); font-weight: 500; }
  .detail-mono {
    font-family: var(--font-mono);
    font-size: 11.5px;
    color: var(--text-secondary);
    word-break: break-all;
  }
  .proj-link {
    font-size: 12.5px;
    font-weight: 700;
    color: var(--brand-accent);
    background: none;
    border: none;
    cursor: pointer;
    padding: 0;
    text-align: left;
    text-decoration: underline;
    transition: opacity var(--transition-fast);
    font-family: var(--font-family);
  }
  .proj-link:hover { opacity: 0.7; }

  /* ── Management Bar ── */
  .detail-management-bar {
    display: flex;
    gap: 16px;
    flex-wrap: wrap;
    margin-top: 16px;
    padding-top: 14px;
    border-top: 1px dashed var(--surface-card-border);
    align-items: flex-end;
  }
  .mgt-col {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }
  .mgt-col.actions-right {
    margin-left: auto;
  }
  .mgt-btn-group {
    display: flex;
    gap: 6px;
    align-items: center;
    flex-wrap: wrap;
  }
  .mgt-btn {
    padding: 5px 12px;
    font-size: 12px;
    font-weight: 600;
  }
  .mgt-select {
    padding: 5px 10px;
    font-size: 12px;
    border-radius: var(--radius-md);
    border: 1px solid var(--surface-card-border);
    background: var(--surface-card);
    color: var(--text-primary);
    font-family: var(--font-family);
  }
  .mgt-link-row {
    display: flex;
    gap: 6px;
    align-items: center;
  }
  .mgt-input {
    width: 170px;
    padding: 5px 8px;
    font-size: 12px;
    border-radius: var(--radius-md);
    border: 1px solid var(--surface-card-border);
    background: var(--surface-card);
    color: var(--text-primary);
    font-family: var(--font-family);
  }
  .status-done-badge {
    font-size: 12px;
    font-weight: 700;
    color: #065F46;
    background: #ECFDF5;
    padding: 4px 8px;
    border-radius: var(--radius-md);
    border: 1px solid #A7F3D0;
  }
  .text-muted {
    color: var(--text-secondary);
  }

  /* ── File Attachments & Dropzone ── */
  .file-dropzone {
    border: 1.5px dashed var(--surface-card-border);
    border-radius: var(--radius-md);
    background: var(--surface-card-subtle);
    padding: 18px 14px;
    text-align: center;
    cursor: pointer;
    transition: border-color var(--transition-fast), background var(--transition-fast);
  }
  .file-dropzone:hover {
    border-color: var(--brand-accent);
    background: rgba(33, 161, 247, 0.04);
  }
  .dropzone-label {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 5px;
    cursor: pointer;
  }
  .dropzone-text {
    font-size: 12.5px;
    font-weight: 600;
    color: var(--text-primary);
  }
  .dropzone-hint {
    font-size: 11px;
    color: var(--text-tertiary);
  }
  .selected-files-list {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-top: 10px;
  }
  .file-chip {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 10px;
    border-radius: var(--radius-sm);
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    font-size: 12px;
  }
  .file-name {
    max-width: 180px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: var(--text-primary);
    font-weight: 500;
  }
  .file-size {
    color: var(--text-tertiary);
    font-size: 11px;
  }
  .file-remove-btn {
    background: none;
    border: none;
    cursor: pointer;
    color: var(--color-danger);
    padding: 0 2px;
    font-size: 11px;
    font-weight: 700;
    line-height: 1;
  }

  /* ── Detail Panel Attachments ── */
  .detail-attachments-section {
    margin-top: 16px;
    padding-top: 14px;
    border-top: 1px dashed var(--surface-card-border);
  }
  .att-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 10px;
    flex-wrap: wrap;
    gap: 8px;
  }
  .att-header-actions {
    display: flex;
    align-items: center;
    gap: 10px;
  }
  .text-link-btn {
    background: none;
    border: none;
    color: var(--brand-accent);
    font-size: 11.5px;
    font-weight: 600;
    cursor: pointer;
    text-decoration: underline;
    padding: 0;
  }
  .upload-more-btn {
    display: inline-flex;
    align-items: center;
    padding: 3px 10px;
    font-size: 11.5px;
    font-weight: 600;
    background: rgba(33, 161, 247, 0.08);
    color: var(--brand-accent);
    border: 1px solid rgba(33, 161, 247, 0.2);
    border-radius: var(--radius-sm);
    cursor: pointer;
    transition: background var(--transition-fast);
  }
  .upload-more-btn:hover {
    background: rgba(33, 161, 247, 0.15);
  }
  .attachments-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(230px, 1fr));
    gap: 10px;
    margin-top: 8px;
  }
  .att-card {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 12px;
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-md);
    transition: border-color var(--transition-fast);
  }
  .att-card:hover {
    border-color: var(--brand-accent);
  }
  .att-icon-box {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 32px;
    height: 32px;
    border-radius: var(--radius-sm);
    background: var(--surface-card-subtle);
    flex-shrink: 0;
  }
  .att-info {
    display: flex;
    flex-direction: column;
    min-width: 0;
    flex: 1;
  }
  .att-filename {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-primary);
    text-decoration: none;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .att-filename:hover {
    color: var(--brand-accent);
    text-decoration: underline;
  }
  .att-meta {
    font-size: 10.5px;
    color: var(--text-tertiary);
  }
  .att-actions {
    display: flex;
    gap: 4px;
    align-items: center;
  }
  .att-act-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 22px;
    height: 22px;
    border-radius: var(--radius-sm);
    background: var(--surface-card-subtle);
    border: 1px solid var(--surface-card-border);
    color: var(--text-secondary);
    font-size: 11px;
    text-decoration: none;
    cursor: pointer;
  }
  .att-act-btn:hover {
    background: var(--surface-card-hover);
    color: var(--text-primary);
  }
  .att-act-btn.att-del {
    color: var(--color-danger);
  }
  .att-act-btn.att-del:hover {
    background: var(--color-danger-bg);
    border-color: var(--color-danger-border);
  }
  .ingest-row {
    margin-top: 10px;
  }
  .att-empty {
    padding: 14px;
    text-align: center;
    border: 1px dashed var(--surface-card-border);
    border-radius: var(--radius-md);
    background: var(--surface-card);
  }
  .att-empty-text {
    font-size: 11.5px;
    color: var(--text-tertiary);
  }

  /* ── State Shells ── */
  .state-shell {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 10px;
    padding: 60px 24px;
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-lg);
    text-align: center;
  }
  .loading-spinner {
    width: 32px;
    height: 32px;
    border: 2.5px solid var(--surface-card-border);
    border-top-color: var(--brand-accent);
    border-radius: 50%;
    animation: spin 0.7s linear infinite;
  }
  .empty-icon { color: var(--text-tertiary); opacity: 0.4; }
  .state-label {
    font-size: 15px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0;
  }
  .state-sub {
    font-size: 13px;
    color: var(--text-secondary);
    margin: 0;
    max-width: 360px;
  }

  /* ── Info Strip ── */
  .info-strip {
    display: flex;
    gap: 0;
    background: var(--surface-card);
    border: 1px solid var(--surface-card-border);
    border-radius: var(--radius-lg);
    padding: 24px;
    flex-wrap: wrap;
  }
  .info-block {
    flex: 1;
    min-width: 240px;
    padding-right: 24px;
  }
  .info-block:last-child { padding-right: 0; padding-left: 24px; }
  .info-divider {
    width: 1px;
    background: var(--surface-card-border);
    align-self: stretch;
    flex-shrink: 0;
  }
  .info-heading {
    font-size: 13px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0 0 12px;
    padding-bottom: 10px;
    border-bottom: 1px solid var(--surface-card-border);
  }
  .info-list {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: 8px;
  }
  .info-list li {
    font-size: 12.5px;
    color: var(--text-secondary);
    line-height: 1.5;
    padding-left: 14px;
    position: relative;
  }
  .info-list li::before {
    content: '';
    position: absolute;
    left: 0;
    top: 7px;
    width: 4px;
    height: 4px;
    border-radius: 50%;
    background: var(--brand-accent);
    flex-shrink: 0;
  }
  .info-list li strong { color: var(--text-primary); font-weight: 700; }
  @media (max-width: 640px) {
    .info-divider { display: none; }
    .info-block, .info-block:last-child { padding: 0; }
    .info-block:last-child { padding-top: 20px; border-top: 1px solid var(--surface-card-border); }
    .info-strip { flex-direction: column; gap: 20px; }
  }

  /* ── Utility ── */
  .sr-only {
    position: absolute;
    width: 1px; height: 1px;
    padding: 0; margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border-width: 0;
  }
</style>
