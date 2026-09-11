<script lang="ts">
  import { onMount } from 'svelte';
  import { appState } from '$lib/stores/appState.svelte';
  import { projectStore } from '$lib/stores/projectStore.svelte';
  import { ApiClient } from '$lib/services/api';
  import FluentIcons, { type IconName } from '$lib/components/ui/FluentIcons.svelte';

  interface Props {
    open?: boolean;
    onClose?: () => void;
  }

  let {
    open = $bindable(false),
    onClose
  }: Props = $props();

  type PaletteTab = 'all' | 'projects' | 'colors' | 'hooks' | 'actions';

  let query = $state<string>('');
  let activeTab = $state<PaletteTab>('all');
  let selectedIndex = $state<number>(0);
  let inputRef: HTMLInputElement | null = $state(null);

  // ─── SUAMISIHAT MASTER BRAND SYSTEM v3.5.1 DESIGN TOKENS ──────────────────
  interface BrandToken {
    type: 'token';
    name: string;
    code: string;
    cssVar: string;
    brand: string;
    description: string;
  }

  const BRAND_PALETTE: BrandToken[] = [
    // Core Identity
    { type: 'token', name: 'SS Prussian Blue (Brand Primary)', code: '#022057', cssVar: '--ss-prussian-blue', brand: 'SS', description: 'Deep Clinical Navy · 60% Shell/Header' },
    { type: 'token', name: 'SS Blue (Primary Royal)', code: '#043388', cssVar: '--ss-blue', brand: 'SS', description: 'SuamiSihat Primary Blue · Identity Anchor' },
    { type: 'token', name: 'SS Azure Sky (10% Focus Accent)', code: '#21A1F7', cssVar: '--ss-azure', brand: 'SS', description: 'Conversion Accent · High-contrast UI highlight & link' },
    { type: 'token', name: 'SS Malibu Sky (Light Sky Accent)', code: '#6DC6EC', cssVar: '--ss-malibu', brand: 'SS', description: 'Subtle sky highlight & badge tint' },

    // Secondary Warm & Luxury
    { type: 'token', name: 'SS Lion Gold (Warm Secondary)', code: '#BD9A73', cssVar: '--ss-lion', brand: 'SS', description: 'Metallic Luxury & Packaging Secondary' },
    { type: 'token', name: 'SS Fawn Warm (Subtle Earth)', code: '#CCAC8D', cssVar: '--ss-fawn', brand: 'SS', description: 'Warm sand tone for secondary container surfaces' },
    { type: 'token', name: 'SS Arylide Yellow (Attention Focus)', code: '#E5D15C', cssVar: '--ss-arylide', brand: 'SS', description: 'Direct-Response Attention badge & urgent promo' },
    { type: 'token', name: 'SS Banana Yellow (Vibrant Promo)', code: '#FCE53D', cssVar: '--ss-banana', brand: 'SS', description: 'High-visibility retail callout & flash badge' },
    { type: 'token', name: 'SS Neutral Black (Strict Typography)', code: '#1C1C1C', cssVar: '--ss-neutral-black', brand: 'SS', description: 'Strict AAA Typography anchor (No #000000 body text)' },

    // Corporate Subsidiaries
    { type: 'token', name: 'SSH Royal Gold (Holding & Luxury)', code: '#D4AF37', cssVar: '--ssh-gold', brand: 'SSH', description: 'SuamiSihat Holding & Packaging Gold Foil' },
    { type: 'token', name: 'SSC Healthcare Emerald (Care & Clinic)', code: '#10B981', cssVar: '--ssc-emerald', brand: 'SSC', description: 'SuamiSihat Care, Clinic & Compounding Pharmacy' },
    { type: 'token', name: 'SSW Wellness Coral (Spa & Wellness)', code: '#F43F5E', cssVar: '--ssw-coral', brand: 'SSW', description: 'SuamiSihat Wellness, Spa & Aromatherapy' },
    { type: 'token', name: 'SSE E-Commerce Violet (Marketplace)', code: '#8B5CF6', cssVar: '--sse-violet', brand: 'SSE', description: 'SuamiSihat E-Commerce & Online Marketplace' },
    { type: 'token', name: 'SST Tech Cyan (Infrastructure)', code: '#06B6D4', cssVar: '--sst-cyan', brand: 'SST', description: 'SuamiSihat Technology, Dev & Creative Studio' },

    // Canvas Surfaces
    { type: 'token', name: 'Dark Slate Canvas (60% Base)', code: '#090D16', cssVar: '--bg-app', brand: 'DARK', description: 'OLED Master Slate Canvas base' },
    { type: 'token', name: 'Card Surface Glass (30% Structure)', code: '#0F172A', cssVar: '--surface-card', brand: 'CARD', description: 'Elevated Fluent 2 container surface' }
  ];

  // ─── DIRECT-RESPONSE MARKETING HOOKS & CTAs DIRECTORY ──────────────────────
  interface CopyHook {
    type: 'hook';
    id: string;
    category: 'Problem & Hook' | 'Clinical Proof' | 'Urgency & Offer' | 'High-Converting CTA';
    title: string;
    copyText: string;
    tags: string[];
  }

  const COPY_HOOKS: CopyHook[] = [
    {
      type: 'hook',
      id: 'hook-1',
      category: 'Problem & Hook',
      title: '3 Tanda Tenaga Lelaki Merosot & Rawatan Pantas',
      copyText: '3 Tanda Utama Tenaga Lelaki Merosot Selepas Umur 30 & Kaedah Rawatan Klinikal Pantas Tanpa Kesan Sampingan.',
      tags: ['tenaga', 'letih', 'umur 30', 'problem', 'hook', 'vitality', 'copy']
    },
    {
      type: 'hook',
      id: 'hook-2',
      category: 'Problem & Hook',
      title: 'Kenapa Cepat Letih Walaupun Cukup Tidur 8 Jam?',
      copyText: 'Pernah rasa badan lemau, lesu, dan hilang fokus waktu petang walaupun tidur 8 jam? Ini punca sebenar peredaran darah mikro tersumbat.',
      tags: ['letih', 'tidur', 'fokus', 'stamina', 'darah', 'hook', 'copy']
    },
    {
      type: 'hook',
      id: 'hook-3',
      category: 'Clinical Proof',
      title: 'Formula Tradisional Gred-A Diuji Makmal Bertauliah',
      copyText: 'Dirumus khas dengan ekstrak herba gred-A standard piawaian makmal bertauliah untuk memulihkan stamina optimum secara 100% semulajadi.',
      tags: ['formula', 'klinikal', 'makmal', 'tongkat ali', 'herba', 'proof', 'copy']
    },
    {
      type: 'hook',
      id: 'hook-4',
      category: 'Clinical Proof',
      title: '14,000+ Pengguna Berpuas Hati Seawal 7 Hari',
      copyText: 'Lebih 14,000+ lelaki di seluruh Malaysia telah merasai lonjakan tenaga dan keyakinan seawal 7 hari penggunaan konsisten.',
      tags: ['testimoni', 'pakar', '14000', 'malaysia', '7 hari', 'proof', 'copy']
    },
    {
      type: 'hook',
      id: 'hook-5',
      category: 'Urgency & Offer',
      title: 'Tawaran Promosi Gaji: Diskaun 35% + Free Postage',
      copyText: '🔥 PROMOSI GAJI: Dapatkan Diskaun 35% + Percuma 1 Kotak Travel Pack & Penghantaran Percuma Hari Ini Sahaja!',
      tags: ['promosi', 'diskaun', 'free postage', 'travel pack', 'offer', 'urgency', 'copy']
    },
    {
      type: 'hook',
      id: 'hook-6',
      category: 'High-Converting CTA',
      title: 'CTA WhatsApp Consultation (Direct Closing)',
      copyText: 'Tekan butang di bawah untuk sesi konsultasi peribadi bersama Penasihat Kesihatan SuamiSihat sekarang. Privasi 100% terjamin.',
      tags: ['cta', 'whatsapp', 'konsultasi', 'closing', 'privasi']
    },
    {
      type: 'hook',
      id: 'hook-7',
      category: 'High-Converting CTA',
      title: 'CTA Order Online (COD / Sampai Baru Bayar)',
      copyText: 'Klik link untuk tempah sekarang. Pilihan COD disediakan — Barang sampai di tangan baru bayar kepada posmen!',
      tags: ['cta', 'cod', 'order', 'tempah', 'bayar']
    }
  ];

  interface QuickAction {
    type: 'action';
    id: string;
    label: string;
    icon: IconName;
    category: string;
    execute: () => void;
  }

  // Quick Action Commands
  const QUICK_ACTIONS: QuickAction[] = [
    { type: 'action', id: 'nav-ai', label: 'Open Creative AI Studio (Gemini Assistant)', icon: 'sparkles', category: 'AI Tools', execute: () => appState.navigate('copy-studio') },
    { type: 'action', id: 'nav-dashboard', label: 'Go to Dashboard', icon: 'dashboard', category: 'Navigation', execute: () => appState.navigate('dashboard') },
    { type: 'action', id: 'nav-projects', label: 'Open Project Manager', icon: 'folder', category: 'Navigation', execute: () => appState.navigate('projects') },
    { type: 'action', id: 'nav-review', label: 'Go to Review Queue', icon: 'checkCircle', category: 'Navigation', execute: () => appState.navigate('deliverables') },
    { type: 'action', id: 'nav-orders', label: 'Open Creative Requests Queue', icon: 'edit', category: 'Navigation', execute: () => appState.navigate('order-form') },
    { type: 'action', id: 'nav-team', label: 'View Team & Workload', icon: 'users', category: 'Navigation', execute: () => appState.navigate('team') },
    { type: 'action', id: 'nav-copy', label: 'Open Copywriting Studio', icon: 'edit', category: 'Navigation', execute: () => appState.navigate('copy-studio') },
    { type: 'action', id: 'nav-admin', label: 'Open Studio Administration', icon: 'settings', category: 'Governance', execute: () => appState.navigate('admin') },
    { type: 'action', id: 'act-theme', label: 'Toggle Theme (Falconia / Metamorphosis)', icon: 'colorPalette', category: 'System', execute: () => toggleTheme() },
    { type: 'action', id: 'act-rescan', label: 'Rescan Synology NAS Vault', icon: 'history', category: 'System', execute: () => rescanVault() },
    { type: 'action', id: 'act-download', label: 'Download SS-CAM Desktop App (v4.9.0)', icon: 'desktop', category: 'Ecosystem', execute: () => window.open('https://suamisihat.github.io/ss_cam/', '_blank') },
  ];

  function toggleTheme() {
    const current = document.documentElement.getAttribute('data-theme') || 'falconia';
    const validThemes = ['falconia', 'metamorphosis', 'catppuccin'] as const;
    const idx = validThemes.indexOf(current as any);
    const next = validThemes[(idx + 1) % validThemes.length];
    document.documentElement.setAttribute('data-theme', next);
    appState.setTheme(next as any);
    appState.addToast(`Theme switched to ${next.charAt(0).toUpperCase() + next.slice(1)}`, 'info');
  }

  function rescanVault() {
    appState.addToast('Rescanning Synology NAS workspace...', 'info');
    projectStore.loadProjects();
    projectStore.loadDashboard();
  }

  // Filter Matchers
  const allFilteredProjects = $derived.by(() => {
    const q = query.trim().toLowerCase();
    if (!q) return projectStore.projects.slice(0, 10).map(p => ({
      type: 'project' as const,
      id: p.id,
      jobId: p.jobId || p.id,
      title: p.title || 'Untitled Project',
      brand: p.brand || 'SS',
      designer: p.designer || 'Unassigned',
      status: p.status || 'in-progress',
      execute: () => appState.navigate('project-detail', { id: p.id })
    }));

    return projectStore.projects.filter(p => {
      const text = `${p.jobId || ''} ${p.title || ''} ${p.designer || ''} ${p.brand || ''} ${(p.tags || []).join(' ')} ${p.status || ''}`.toLowerCase();
      return text.includes(q);
    }).map(p => ({
      type: 'project' as const,
      id: p.id,
      jobId: p.jobId || p.id,
      title: p.title || 'Untitled Project',
      brand: p.brand || 'SS',
      designer: p.designer || 'Unassigned',
      status: p.status || 'in-progress',
      execute: () => appState.navigate('project-detail', { id: p.id })
    }));
  });

  const allFilteredTokens = $derived.by(() => {
    const q = query.trim().toLowerCase();
    if (!q) return BRAND_PALETTE;
    return BRAND_PALETTE.filter(t => {
      return t.name.toLowerCase().includes(q) ||
             t.code.toLowerCase().includes(q) ||
             t.brand.toLowerCase().includes(q) ||
             t.cssVar.toLowerCase().includes(q) ||
             t.description.toLowerCase().includes(q);
    });
  });

  const allFilteredHooks = $derived.by(() => {
    const q = query.trim().toLowerCase();
    if (!q) return COPY_HOOKS;
    return COPY_HOOKS.filter(h => {
      return h.title.toLowerCase().includes(q) ||
             h.copyText.toLowerCase().includes(q) ||
             h.category.toLowerCase().includes(q) ||
             h.tags.some(tag => tag.toLowerCase().includes(q));
    });
  });

  const allFilteredActions = $derived.by(() => {
    const q = query.trim().toLowerCase();
    if (!q) return QUICK_ACTIONS;
    return QUICK_ACTIONS.filter(a => {
      return a.label.toLowerCase().includes(q) || a.category.toLowerCase().includes(q);
    });
  });

  // Combined Search Results based on Active Tab
  const searchResults = $derived.by(() => {
    const q = query.trim().toLowerCase();

    if (activeTab === 'projects') {
      return allFilteredProjects;
    }
    if (activeTab === 'colors') {
      return allFilteredTokens.map(t => ({
        ...t,
        execute: () => copyToken(t.code, t.name, t.cssVar, false)
      }));
    }
    if (activeTab === 'hooks') {
      return allFilteredHooks.map(h => ({
        ...h,
        execute: () => copyHook(h)
      }));
    }
    if (activeTab === 'actions') {
      return allFilteredActions;
    }

    // Tab === 'all'
    const maxProjects = q ? 5 : 3;
    const maxTokens = q ? 4 : 4;
    const maxHooks = q ? 4 : 3;
    const maxActions = q ? 4 : 3;

    const pList = allFilteredProjects.slice(0, maxProjects);
    const tList = allFilteredTokens.slice(0, maxTokens).map(t => ({
      ...t,
      execute: () => copyToken(t.code, t.name, t.cssVar, false)
    }));
    const hList = allFilteredHooks.slice(0, maxHooks).map(h => ({
      ...h,
      execute: () => copyHook(h)
    }));
    const aList = allFilteredActions.slice(0, maxActions);

    return [...pList, ...tList, ...hList, ...aList];
  });

  async function copyToken(code: string, name: string, cssVar: string, isShift: boolean) {
    const valueToCopy = isShift ? `var(${cssVar})` : code;
    try {
      await navigator.clipboard.writeText(valueToCopy);
      appState.addToast(`Copied ${valueToCopy} (${name}) to clipboard`, 'success');
    } catch {
      appState.addToast(`Value: ${valueToCopy}`, 'info');
    }
  }

  async function copyHook(hook: CopyHook) {
    try {
      await navigator.clipboard.writeText(hook.copyText);
      appState.addToast(`Copied Hook: "${hook.title}" to clipboard!`, 'success', 'Marketing Hook Copied');
    } catch {
      appState.addToast(hook.copyText, 'info');
    }
  }

  function handleSelect(item: any, e?: MouseEvent) {
    if (!item) return;
    if (item.type === 'token') {
      const isShift = e?.shiftKey || false;
      copyToken(item.code, item.name, item.cssVar, isShift);
    } else if (item.type === 'hook') {
      copyHook(item);
    } else if (item.execute) {
      item.execute();
    }
    closeModal();
  }

  function closeModal() {
    open = false;
    query = '';
    activeTab = 'all';
    selectedIndex = 0;
    if (onClose) onClose();
  }

  function handleKeydown(e: KeyboardEvent) {
    if (!open) return;

    if (e.key === 'Escape') {
      e.preventDefault();
      closeModal();
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (searchResults.length > 0) {
        selectedIndex = (selectedIndex + 1) % searchResults.length;
      }
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      if (searchResults.length > 0) {
        selectedIndex = (selectedIndex - 1 + searchResults.length) % searchResults.length;
      }
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (searchResults.length > 0 && searchResults[selectedIndex]) {
        handleSelect(searchResults[selectedIndex]);
      }
    }
  }

  $effect(() => {
    if (open) {
      setTimeout(() => {
        if (inputRef) inputRef.focus();
      }, 50);
    }
  });
</script>

<svelte:window onkeydown={handleKeydown} />

{#if open}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="palette-backdrop" onclick={(e) => { if (e.target === e.currentTarget) closeModal(); }}>
    <div class="palette-modal">
      <!-- Search Input Bar -->
      <div class="palette-search-header">
        <FluentIcons name="search" size={18} color="#21A1F7" />
        <input 
          bind:this={inputRef}
          type="text" 
          class="palette-input" 
          placeholder="Search projects (0085D), brand colors (#043388), hooks (tenaga), or studio actions..." 
          bind:value={query}
        />
        {#if query}
          <button class="clear-btn" onclick={() => query = ''} title="Clear query">
            <FluentIcons name="close" size={14} />
          </button>
        {/if}
        <span class="esc-badge" onclick={closeModal}>ESC</span>
      </div>

      <!-- Category Filter Tabs -->
      <div class="palette-tabs" role="tablist" aria-label="Filter command categories">
        <button 
          class="tab-btn" 
          class:active={activeTab === 'all'} 
          onclick={() => { activeTab = 'all'; selectedIndex = 0; }}
          role="tab"
          aria-selected={activeTab === 'all'}
        >
          All Results
        </button>
        <button 
          class="tab-btn" 
          class:active={activeTab === 'projects'} 
          onclick={() => { activeTab = 'projects'; selectedIndex = 0; }}
          role="tab"
          aria-selected={activeTab === 'projects'}
        >
          Projects ({allFilteredProjects.length})
        </button>
        <button 
          class="tab-btn" 
          class:active={activeTab === 'colors'} 
          onclick={() => { activeTab = 'colors'; selectedIndex = 0; }}
          role="tab"
          aria-selected={activeTab === 'colors'}
        >
          Brand Colors ({allFilteredTokens.length})
        </button>
        <button 
          class="tab-btn" 
          class:active={activeTab === 'hooks'} 
          onclick={() => { activeTab = 'hooks'; selectedIndex = 0; }}
          role="tab"
          aria-selected={activeTab === 'hooks'}
        >
          Copywriting Hooks ({allFilteredHooks.length})
        </button>
        <button 
          class="tab-btn" 
          class:active={activeTab === 'actions'} 
          onclick={() => { activeTab = 'actions'; selectedIndex = 0; }}
          role="tab"
          aria-selected={activeTab === 'actions'}
        >
          Studio Actions ({allFilteredActions.length})
        </button>
      </div>

      <!-- Results Container -->
      <div class="palette-body">
        {#if searchResults.length === 0}
          <div class="palette-empty">
            <FluentIcons name="search" size={36} color="rgba(255,255,255,0.2)" />
            <p style="margin-top: 10px;">No matching results found for "{query}"</p>
            <span class="empty-hint">Try searching by Job ID (e.g. <code>0085D</code>), brand (<code>SSH</code>), token (<code>Azure</code>), or hook (<code>letih</code>).</span>
          </div>
        {:else}
          <div class="results-list">
            {#each searchResults as item, index}
              <!-- svelte-ignore a11y_click_events_have_key_events -->
              <!-- svelte-ignore a11y_no_static_element_interactions -->
              <div 
                class="result-row {selectedIndex === index ? 'selected' : ''}"
                onclick={(e) => handleSelect(item, e)}
                onmouseenter={() => selectedIndex = index}
              >
                {#if item.type === 'project'}
                  <div class="result-icon icon-project">
                    <FluentIcons name="folder" size={16} />
                  </div>
                  <div class="result-info">
                    <div class="result-title-row">
                      <span class="badge-brand">{item.brand}</span>
                      <span class="job-id-tag">{item.jobId}</span>
                      <span class="item-title">{item.title}</span>
                    </div>
                    <div class="result-sub">
                      <span>Designer: <b>{item.designer}</b></span>
                      <span>·</span>
                      <span class="status-pill status-{item.status}">{item.status}</span>
                    </div>
                  </div>
                  <span class="action-shortcut">
                    <span>Open Project</span>
                    <FluentIcons name="arrowRight" size={11} />
                  </span>

                {:else if item.type === 'token'}
                  <div class="color-swatch-box" style="background: {item.code};"></div>
                  <div class="result-info">
                    <div class="result-title-row">
                      <span class="item-title">{item.name}</span>
                      <code class="hex-badge">{item.code}</code>
                      <code class="var-badge">{item.cssVar}</code>
                    </div>
                    <div class="result-sub">{item.description}</div>
                  </div>
                  <span class="action-shortcut token-shortcut">
                    <span>Click: Hex · Shift+Click: Var</span>
                    <FluentIcons name="copy" size={11} />
                  </span>

                {:else if item.type === 'hook'}
                  <div class="result-icon icon-hook">
                    <FluentIcons name="edit" size={16} />
                  </div>
                  <div class="result-info">
                    <div class="result-title-row">
                      <span class="badge-hook-category">{item.category}</span>
                      <span class="item-title">{item.title}</span>
                    </div>
                    <div class="result-sub hook-preview-text">"{item.copyText}"</div>
                  </div>
                  <span class="action-shortcut">
                    <span>Copy Hook</span>
                    <FluentIcons name="copy" size={11} />
                  </span>

                {:else if item.type === 'action'}
                  <div class="result-icon icon-action">
                    <FluentIcons name={item.icon} size={16} />
                  </div>
                  <div class="result-info">
                    <div class="result-title-row">
                      <span class="item-title">{item.label}</span>
                    </div>
                    <div class="result-sub">{item.category}</div>
                  </div>
                  <span class="action-shortcut">
                    <span>Execute</span>
                  </span>
                {/if}
              </div>
            {/each}
          </div>
        {/if}
      </div>

      <!-- Footer Quick Tips -->
      <div class="palette-footer">
        <div class="footer-tip">
          <kbd>↑</kbd><kbd>↓</kbd> <span>Navigate</span>
        </div>
        <div class="footer-tip">
          <kbd>↵</kbd> <span>Select / Execute</span>
        </div>
        <div class="footer-tip">
          <kbd>Shift</kbd>+<span>Click</span> <span>Copy CSS Var</span>
        </div>
        <div class="footer-tip">
          <kbd>ESC</kbd> <span>Dismiss</span>
        </div>
        <div class="footer-sync">
          <span class="sync-dot"></span>
          <span>Master Brand System v3.5.1</span>
        </div>
      </div>
    </div>
  </div>
{/if}

<style>
  .palette-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    width: 100vw;
    height: 100vh;
    background: rgba(2, 10, 28, 0.8);
    backdrop-filter: blur(14px);
    -webkit-backdrop-filter: blur(14px);
    display: flex;
    align-items: flex-start;
    justify-content: center;
    padding-top: 10vh;
    z-index: 2000;
    animation: fadeIn 0.15s cubic-bezier(0.4, 0, 0.2, 1);
  }

  @keyframes fadeIn {
    from { opacity: 0; transform: translateY(-8px); }
    to { opacity: 1; transform: translateY(0); }
  }

  .palette-modal {
    width: 92%;
    max-width: 720px;
    background: #090D16;
    border: 1px solid rgba(33, 161, 247, 0.25);
    border-radius: 14px;
    overflow: hidden;
    box-shadow: 0 25px 60px -12px rgba(0, 0, 0, 0.85), 0 0 0 1px rgba(33, 161, 247, 0.2);
    display: flex;
    flex-direction: column;
    max-height: 75vh;
  }

  /* Search Header */
  .palette-search-header {
    display: flex;
    align-items: center;
    padding: 14px 18px;
    background: rgba(15, 23, 42, 0.95);
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    gap: 12px;
  }

  .palette-input {
    flex: 1;
    background: transparent;
    border: none;
    outline: none;
    color: #FFFFFF;
    font-size: 15px;
    font-weight: 500;
  }

  .palette-input::placeholder {
    color: #64748B;
    font-size: 13px;
  }

  .clear-btn {
    background: transparent;
    border: none;
    color: #94A3B8;
    cursor: pointer;
    font-size: 14px;
    padding: 4px 6px;
    border-radius: 4px;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .clear-btn:hover {
    color: #FFFFFF;
    background: rgba(255, 255, 255, 0.1);
  }

  .esc-badge {
    font-size: 10px;
    font-weight: 800;
    padding: 2px 7px;
    border-radius: 4px;
    background: rgba(255, 255, 255, 0.08);
    border: 1px solid rgba(255, 255, 255, 0.15);
    color: #94A3B8;
    cursor: pointer;
    font-family: monospace;
  }

  /* Category Filter Tabs */
  .palette-tabs {
    display: flex;
    gap: 4px;
    padding: 8px 14px;
    background: rgba(15, 23, 42, 0.65);
    border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    overflow-x: auto;
  }

  .tab-btn {
    background: transparent;
    border: 1px solid transparent;
    color: #94A3B8;
    font-size: 11.5px;
    font-weight: 600;
    padding: 4px 10px;
    border-radius: 6px;
    cursor: pointer;
    white-space: nowrap;
    transition: all 0.15s ease;
  }

  .tab-btn:hover {
    color: #FFFFFF;
    background: rgba(255, 255, 255, 0.05);
  }

  .tab-btn.active {
    color: #21A1F7;
    background: rgba(33, 161, 247, 0.14);
    border-color: rgba(33, 161, 247, 0.3);
    font-weight: 700;
  }

  /* Body & Results */
  .palette-body {
    flex: 1;
    overflow-y: auto;
    padding: 8px;
    max-height: 440px;
  }

  .results-list {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .result-row {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 9px 12px;
    border-radius: 8px;
    background: transparent;
    cursor: pointer;
    transition: all 0.12s ease;
    border: 1px solid transparent;
  }

  .result-row:hover, .result-row.selected {
    background: rgba(33, 161, 247, 0.12);
    border-color: rgba(33, 161, 247, 0.3);
  }

  .result-icon {
    font-size: 15px;
    width: 32px;
    height: 32px;
    border-radius: 8px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(255, 255, 255, 0.05);
    flex-shrink: 0;
  }

  .icon-project { color: #21A1F7; }
  .icon-hook { color: #F59E0B; background: rgba(245, 158, 11, 0.1); }
  .icon-action { color: #10B981; }

  .color-swatch-box {
    width: 30px;
    height: 30px;
    border-radius: 7px;
    border: 2px solid rgba(255, 255, 255, 0.25);
    flex-shrink: 0;
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.35);
  }

  .result-info {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: 2px;
    overflow: hidden;
  }

  .result-title-row {
    display: flex;
    align-items: center;
    gap: 8px;
    overflow: hidden;
  }

  .badge-brand {
    font-size: 9.5px;
    font-weight: 800;
    padding: 2px 5px;
    border-radius: 4px;
    background: var(--brand-primary, #043388);
    color: #FFFFFF;
    letter-spacing: 0.3px;
  }

  .badge-hook-category {
    font-size: 9.5px;
    font-weight: 800;
    padding: 2px 6px;
    border-radius: 4px;
    background: rgba(245, 158, 11, 0.18);
    color: #FBBF24;
    border: 1px solid rgba(245, 158, 11, 0.3);
    white-space: nowrap;
  }

  .job-id-tag {
    font-size: 11px;
    font-weight: 700;
    color: #21A1F7;
    font-family: monospace;
  }

  .item-title {
    font-size: 13px;
    font-weight: 600;
    color: #F8FAFC;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .hex-badge {
    font-size: 11px;
    font-weight: 700;
    padding: 2px 6px;
    background: rgba(0, 0, 0, 0.35);
    border-radius: 4px;
    color: #38BDF8;
    font-family: monospace;
  }

  .var-badge {
    font-size: 10px;
    font-weight: 600;
    padding: 2px 5px;
    background: rgba(255, 255, 255, 0.05);
    border-radius: 4px;
    color: #94A3B8;
    font-family: monospace;
  }

  .result-sub {
    font-size: 11px;
    color: #94A3B8;
    display: flex;
    align-items: center;
    gap: 6px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .hook-preview-text {
    color: #CBD5E1;
    font-style: italic;
  }

  .status-pill {
    font-size: 9.5px;
    font-weight: 700;
    text-transform: uppercase;
    padding: 1px 5px;
    border-radius: 3px;
  }

  .status-in-progress { background: rgba(0, 120, 212, 0.2); color: #60A5FA; }
  .status-review { background: rgba(245, 158, 11, 0.2); color: #FBBF24; }
  .status-revision { background: rgba(217, 119, 6, 0.2); color: #FB923C; }
  .status-approved, .status-done { background: rgba(16, 185, 129, 0.2); color: #34D399; }

  .action-shortcut {
    font-size: 11px;
    font-weight: 600;
    color: #64748B;
    opacity: 0;
    transition: opacity 0.15s ease;
    display: flex;
    align-items: center;
    gap: 4px;
    flex-shrink: 0;
  }

  .token-shortcut {
    font-size: 10px;
  }

  .result-row.selected .action-shortcut {
    opacity: 1;
    color: #21A1F7;
  }

  /* Empty State */
  .palette-empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 40px 20px;
    text-align: center;
    color: #94A3B8;
  }

  .empty-hint {
    font-size: 11px;
    color: #64748B;
    margin-top: 6px;
  }

  .empty-hint code {
    background: rgba(255, 255, 255, 0.08);
    padding: 2px 5px;
    border-radius: 3px;
    color: #21A1F7;
  }

  /* Footer */
  .palette-footer {
    display: flex;
    align-items: center;
    padding: 9px 16px;
    background: rgba(9, 13, 22, 0.98);
    border-top: 1px solid rgba(255, 255, 255, 0.08);
    gap: 14px;
    font-size: 11px;
    color: #64748B;
    flex-wrap: wrap;
  }

  .footer-tip {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .footer-tip kbd {
    background: rgba(255, 255, 255, 0.1);
    border: 1px solid rgba(255, 255, 255, 0.15);
    border-radius: 3px;
    padding: 1px 5px;
    font-size: 10px;
    font-family: inherit;
    color: #94A3B8;
  }

  .footer-sync {
    margin-left: auto;
    display: flex;
    align-items: center;
    gap: 6px;
    color: #21A1F7;
    font-weight: 600;
  }

  .sync-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: #21A1F7;
    box-shadow: 0 0 6px #21A1F7;
  }
</style>
