# SS-CAM — SuamiSihat™ Creative Assets Management

## Enterprise Creative Operations & Assets Management Platform

Standardized Project Vaults · ClickUp 3.0 Workspace · Copywriting Studio · Brand Asset Inspector · Synology NAS Native · Multi-Platform

[![Release](https://img.shields.io/badge/release-v4.9.0-blue?style=flat-square)](https://github.com/SuamiSihat/ss_cam/releases/tag/v4.9.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20%7C%20Linux%20%7C%20Android%20%7C%20Docker-blue?style=flat-square)](https://github.com/SuamiSihat/ss_cam)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8%20%7C%20.NET%208.0%20%7C%20Compose-purple?style=flat-square)](https://dotnet.microsoft.com)
[![Web Stack](https://img.shields.io/badge/web-Svelte%205%20%2B%20Node.js%2020-ff3e00?style=flat-square)](https://svelte.dev)
[![Design System](https://img.shields.io/badge/design-Fluent%202%20%2F%2060%3A30%3A10-0078D4?style=flat-square)](https://fluent2.microsoft.design)
[![License](https://img.shields.io/badge/licence-Internal%20Use-orange?style=flat-square)](./installer/EULA.txt)

---

## 🚀 What's New in v4.9.0 ("Visual Project Timeline & Gantt Inspector Drawer, Live Studio Workstream Telemetry, Command Palette v3.5.1 & Tri-Platform Parity")

* **📅 Visual Project Timeline & Interactive Gantt Inspector Drawer (`CalendarPage.xaml`)**:
  * **Docked Right Inspector Drawer (`ProjectDetailDrawer`)**: Slide-in 440px inspector drawer docked to the right edge of the visual calendar view with Fluent 2 glassmorphic styling. Click any project row or timeline bar in the Gantt chart (or "Inspect" in day cards) to view metadata and controls.
  * **Interactive Scheduling & Dynamic Date Calculations**: Dual date pickers for Start Date and Deadline with automatic duration calculation (`{N}d`), plus 1-click extension buttons (`+1d`, `+3d`, `+1w`).
  * **Malaysian Off-Day Conflict Alert & 1-Click Auto-Reschedule**: Real-time evaluation against weekends and national public holidays with a 1-click **"Fix Off-Day Conflict"** button that shifts deadlines to the next working day.
  * **Deliverables & Subtask Checklist Management**: Track deliverables completion (`{Done}/{Total} Deliverables Done (X%)`), 1-click status cycling (`Draft` ➔ `In Progress` ➔ `Done`), and inline editing synced directly with `README.md` frontmatter.
  * **Gantt Chart Subtask Status Indicators**: Left column displays `[✓ X/Y • Z pts]` badge (turns green on full completion); timeline bars feature a `✓ X/Y` pill badge; hover tooltips display complete deliverable breakdown.
* **📡 Live Studio Telemetry & Workstream Pulse (Web, Desktop, Android)**:
  * **Unified Telemetry Feed**: Real-time synchronization of active designer tasks via `<WorkspaceRoot>/_Team/live_tasks.json` with 16-hour session freshness filter and UTF-8 BOM safety.
  * **Web Management Portal**: Added elevated Live Studio Radar card on the main dashboard with digital stopwatches for each active designer workstation, paired with the top header pulse indicator (`● {N} in Studio` / `● Studio Idle`) and interactive task flyout.
  * **Android Native Companion**: Connected `DeskCompanionMode` with an infinite pulsing Emerald ticker (`● N IN STUDIO • DESIGNER: TASK`), live workstation sprint card, and elevated Team Hub live workstream section with designer initials avatars and session notes.
* **⚡ Master Brand System v3.5.1 & Command Palette Alignment**:
  * **Web Command Palette (`Ctrl + K`)**: Added 5 reactive category filter tabs (`All`, `Projects`, `Brand Colors`, `Copywriting Hooks`, `Studio Actions`), the 16 official Single-Source-of-Truth tokens with regular (HEX) and <kbd>Shift</kbd> (CSS variable) copy actions, and pre-scaffolded direct-response marketing hooks directory.
  * **Android Brand Hub**: Integrated the 16 official v3.5.1 color tokens with 1-tap clipboard copying, packaging dieline specs, and 7 high-converting Malay marketing hooks.
* **📦 Packaging Deliverables & Creative Orders Intake**:
  * Integrated dedicated packaging dieline formats (`pkg_box_sleeve` — Box & Sleeve, `pkg_label` — Bottle/Jar/Vial Label) with 300 DPI CMYK and die-cut bleed specs.
  * Added strategic `tier_0` (`🗓️ Low / Pipeline`) priority tier with automated +21 day delivery threshold guidance across Web and Mobile order forms.
* **🛡️ Production Release Packaging**:
  * Windows Desktop portable binary `dist/SS-CAM-v4.9.0.exe` (5.99 MB).
  * Android production app bundle `app-release.aab` (6.03 MB, RSA 2048 cryptographically verified) and standalone release `app-release.apk` (3.39 MB).


* **📋 Deliverables & Subtask Management Engine (`README.md` Frontmatter)**:
  * Unified deliverables schema structuring actual artwork outputs directly in `subtasks:` YAML frontmatter (ID, Name, Type, Weight, Status, Specs, Designer).
  * Subtask weight points dynamically aggregate to compute total project complexity points and calculate progress completion ratios across the studio.
  * 1-click status cycling (`Draft` ➔ `In Progress` ➔ `Done`) seamlessly available across Desktop Task Manager, Web Deliverables Gallery, and Android Companion.
* **🔄 Tri-Platform Real-Time Synchronization Architecture**:
  * **Windows Desktop (WPF 4.8)**: `WorkspaceWatcherService` watches Synology NAS directories (`D:\SynologyDrive\Creative-Team`) with 600ms debouncing. Both `DashboardPage` (KPI telemetry, recent projects, overdue metrics) and `TaskManagerPage` (Kanban boards, queue counts) subscribe and automatically re-render without manual refreshes.
  * **Web Management Portal (Svelte 5 / Node.js 20)**: `WorkspaceService` watches filesystem via Chokidar, parses YAML frontmatter with `js-yaml`, and broadcasts Server-Sent Events (SSE `project:updated` / `workspace:updated`) to all connected browser clients. `PUT /api/projects/:id` accepts and merges subtask arrays with SHA-256 version hash optimistic locking.
  * **Android Native Companion (Jetpack Compose / Retrofit)**: Integrated `updateProject` API in `SscamApiService`, powering instant optimistic local UI updates and real-time synchronization of project statuses, subtask deliverables, and `README.md` body edits from mobile devices.
* **🛡️ Frontmatter Parser Hierarchy Guard ([FrontmatterService.cs](file:///d:/HaNa_Innovation/ss_cam/src/SS-CAM/Services/FrontmatterService.cs))**:
  * Fixed parser indentation handling so child subtask statuses (e.g. `status: draft` under `subtasks:`) do not unintentionally overwrite top-level project status.

## 🚀 What's New in v4.8.1 ("Command Palette, 60-30-10 Polish, Live Work Stopwatch & Creative Operations")

* **⚡ Global Studio Command Palette (`Ctrl + K`) ([CommandPaletteService.cs](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Services/CommandPaletteService.cs))**:
  * Universal keyboard-first launcher accessible anywhere via <kbd>Ctrl</kbd> + <kbd>K</kbd>, global header spotlight button, or sidebar search trigger.
  * Instant access to 15 studio modules, official Master Brand System v3.5.1 color tokens with real-time swatches, pre-scaffolded marketing hooks & CTAs, live NAS project folders, and immediate execution of studio actions.
* **🎨 Art Director 60-30-10 Visual Hierarchy Polish**:
  * Strict adherence to 60% calm neutral canvas, 30% structural hierarchy surfaces, and 10% intentional brand/status accents (`#21A1F7` Azure & `#10B981` Emerald).
  * Redesigned `TitleBarStrip` with spotlight search box and live work status pill.
* **⏱️ Live Designer Work Session Stopwatch & Status Indicator ([WorkSessionTrackerService.cs](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Services/WorkSessionTrackerService.cs))**:
  * Pulsing status dot, active project code, live digital clock (`01:42:15`), and interactive timer popover drawer with designer filter and crash-resilient auto-recovery.
* **📡 Real-Time Live Studio Tasks & Work Stream ([LiveTaskSyncService.cs](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Services/LiveTaskSyncService.cs))**:
  * Multi-workstation synchronization via shared ledger at `<WorkspaceRoot>\_Team\live_tasks.json`, embedded dashboard live task stream with 1-second live digital tickers, and instant desktop toast alerts when team members begin or resume project tasks.
* **📋 Creative Request Operations Architecture & Intake Backlog Upgrade**:
  * Added strategic **Low / Pipeline (`tier_0`)** intake tier with automated 21-day minimum delivery threshold.
  * Segmented 2-step **Digital Screen vs. Print, Packaging & POSM** channel selectors with interactive custom dimensions and physical print substrate specifications.
  * Full request editing with Option A permission governance and automatic locking once accepted or cancelled.
  * Renamed intake status from "Completed" to **"Added to Backlog"** across Web, Windows, and Linux to ensure clear requester communication.
  * Removed duplicate "Lifecycle Actions" from expanded detail views, cleanly unifying all actions into the canonical table row.
  * Stripped UTF-8 BOM (`\uFEFF`) in Node.js backend parsers for seamless cross-platform .NET desktop interoperability, and purged all legacy demo orders from NAS storage.
* **🔧 Universal Dropdown Polish**:
  * Comprehensive resolution of dropdown text cropping, baseline clipping, and mnemonic underscore stripping across all 15 views in the desktop application.

## 🚀 What's New in v4.8.0 ("Visual Revision Diff & Copywriting Studio Live Preview")

* **🔍 Interactive Visual Asset Revision Diff Inspector ([VisualDiffDialog.xaml](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Dialogs/VisualDiffDialog.xaml))**:
  * **5 Interactive Inspection Modes**: Vertical Split swipe with draggable Fluent 2 grip badge, Horizontal Split swipe for 9:16 vertical video creatives, Side-by-Side dual view, Opacity Blend onion-skin (0%–100%) for alignment verification, and 32bpp Euclidean RGB Pixel Difference mapping highlighting altered regions in high-visibility magenta (`#FF007F`).
  * **Automated Revision Pair Detection**: Intelligently groups deliverables and project folders (`_v1` → `_v2`, `_rev1` → `_rev2`, `draft` → `final`) and auto-selects the active file pair.
  * **Synchronized Navigation**: Smooth mouse wheel zoom (0.1x to 10.0x) centered on cursor and middle/right-click drag-to-pan affecting both comparison layers in lockstep.
  * **Technical Asset Inspection Strip**: Displays resolution, file size with delta (`2.1 MB (-12.5%)`), green `1:1 MATCH` badge or amber `SCALED` indicator, and a 1-click `⇄ Swap` button.
  * **Catalog & Task Inspector Quick Launchers**: Added `Compare Revisions (Diff)` button in Assets Gallery, `Visual Revision Diff` in Task Inspector, and gallery double-click auto-seeding.
* **✍️ Copywriting Studio Split-View Live Preview & Formatting Engine ([CopywritingPage.xaml](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Views/CopywritingPage.xaml))**:
  * **Side-by-Side Split View**: Real-time markdown editor with live rendered preview synced at 150ms debounced interval.
  * **Segmented Sub-Preview Modes**: Toggle seamlessly between `[ Doc ]` (FlowDocument), `[ WhatsApp ]` (Chat Simulation), `[ Meta Ad ]` (Feed Sponsored Post), and `[ Both ]` (Dual side-by-side).
  * **WhatsApp Rich Inlines & OG Preview**: Converts `*bold*`, `_italic_`, `~strike~`, and code into RichText inlines; automatically extracts destination URLs to build dynamic OG preview cards with domain, title, and thumbnail.
  * **Meta Ad Feed Simulation**: Automatic headline extraction from `#` headers or YAML tags, dynamic CTA inference (`Send Message`, `Order Now`, `Shop Now`), and interactive `... See more` / `See less` text truncation.
  * **1-Click Platform Exporters**: Dedicated buttons to copy sanitized WhatsApp broadcast copy or structured Meta Ads Manager payload (`=== PRIMARY TEXT ===`, `=== HEADLINE ===`, `=== CALL TO ACTION ===`).
* **🖼️ Cross-Platform Avatar & User Profile Synchronization**:
  * Dedicated physical binary file storage inside `_Team/Users/{staffId}/avatar.jpg` and `profile.json`.
  * Sanitized `staff_directory.json` into a lightweight reference index (`avatarUrl: "/api/users/{staffId}/avatar"`), eliminating JSON bloating.
  * High-performance binary streaming route `GET /api/users/:id/avatar` with MIME type detection and HTTP cache headers.
  * Desktop `UserProfileService.cs` bi-directional auto-sync (mirrors local avatars to NAS `_Team/Users/{staffId}/avatar.jpg`) and complete camelCase `[JsonProperty]` mappings preventing credential stripping.
* **🛡️ Source Guardian 100% PASS**:
  * Replaced all raw high-byte Unicode characters in XAML attributes with XML entities (`&#x2122;`, `&#x26A0;`, `&#x1F1F2;&#x1F1FE;`), achieving zero warnings on XAML attributes.

## 🚀 What's New in v4.7.0 ("Velocity Navigation & Canva Cloud Bridge")

* **⚡ Instantaneous Tab Navigation (0 ms)**:
  * Enabled `NavigationCacheMode="Required"` across all 15 navigation views in the desktop application ([MainWindow.xaml](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/MainWindow.xaml)). Tab switching is now instantaneous with zero UI thread stutter, retaining search filters, active scroll positions, and loaded data.
  * Completely eliminated blocking synchronous recursive filesystem crawls on the UI thread in Dashboard, Calendar, and Task Manager views.
  * Resolved the `System.InvalidCastException` on the Order Requests page by statically isolating the card context menu.
* **🎨 Canva Creative Cloud Bridge in Project Creator ([ProjectCreatorPage.xaml](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Views/ProjectCreatorPage.xaml))**:
  * **Platform Auto-Size Deep Launcher**: 1-click **"Create on Canva (Auto-size)"** button dynamically opens Canva preconfigured with the exact dimensions of the selected platform preset (1:1 Feed 1080x1080, 9:16 Story 1080x1920, 16:9 Banner 1920x1080, A4/A3/A5 print dimensions).
  * **Windows Shortcut Auto-Scaffolding**: Automatically generates `02_SOURCE/Open_In_Canva.url` inside newly scaffolded projects for instant 1-click browser launching.
  * **Frontmatter Persistence**: Stores `canva_url: https://...` in `README.md` YAML frontmatter with automatic sync across Desktop and Web.
* **📋 Task Manager & Kanban Canva Badges ([TaskManagerPage.xaml](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Views/TaskManagerPage.xaml))**:
  * Prominent teal `[CANVA]` pill badge rendered on cards containing Canva design links.
  * **"Open in Canva"** quick action integrated into Kanban card context menus and the project detail drawer.
* **🌐 Web Management Portal Synchronization (`SS-CAM.Web`)**:
  * Added Canva Creative Cloud link input and direct launch button to `FrontmatterPanel.svelte`.
* **🖼️ Per-User Team Storage & Binary Avatar Streaming**:
  * Transitioned from monolithic Base64 string embedding in `staff_directory.json` to dedicated physical binary file storage inside `_Team/Users/{staffId}/avatar.jpg` and `profile.json`.
  * High-performance binary streaming route `GET /api/users/:id/avatar` with MIME type detection and HTTP cache headers.
  * Desktop `UserProfileService.cs` bi-directional auto-sync (mirrors local avatars to NAS `_Team/Users/{staffId}/avatar.jpg`) and complete camelCase `[JsonProperty]` mappings preventing credential stripping.
* **📅 Visual Gantt Timeline Off-Day Shading, Day Labels & Conflict Prevention**:
  * Two-tier stacked day headers with uniform 3-letter day names (`Mon, Tue, Wed, Thu, Fri, Sat, Sun`) via `MalaysiaHolidayService.GetDayLetter`.
  * Red color highlight (`#DC2626`) restricted strictly to official Malaysia Public Holidays, rendering weekends in clean neutral slate (`#64748B`).
  * Full-height column background fills and boundary lines across all project rows for Saturdays & Sundays (slate wash) and official Malaysia Public Holidays (soft red wash).
  * Business working day SLA engine skipping non-working days, plus active Gantt deadline conflict detection with `⚠️ Off-Day` badges and rescheduling alerts.
* **📱 Android Companion App Alignment (`SS-CAM.Android`)**:
  * Version bumped to `v4.7.0` (build 471), automatically streaming `/api/users/{staffId}/avatar`.

## 🚀 What's New in v4.6.2

* **🤝 Designer Task Handover ([TaskManagerPage.xaml](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Views/TaskManagerPage.xaml))**:
  * **Right-Click Quick Handover**: Instant re-assignment of any project card to any team designer via the card context menu with instant YAML frontmatter update and success toast.
  * **Detail Pane Task Owner Selection**: Dedicated editable designer combo box and "Hand Over Task" button in the project detail drawer for seamless workflow transfers.
* **📂 Synology NAS `_Orders` Temporary Attachment Vault**:
  * Dedicated temporary intake vault at `\\SSNAS\Creative-Team\_Orders\<ORDER_ID>\` for reference assets, briefs, logos, PDFs, and sketches.
  * Underscore prefix (`_Orders`) ensures project directory scanners safely ignore temporary order folders, preventing collisions with official year project vaults (`2026/`).
  * Realtime JSONL sync to `_Orders/creative-orders.jsonl` on the NAS.
* **🌐 Web Management Portal (`SS-CAM.Web`)**:
  * **Multi-File Upload Dropzone**: Modern Fluent 2 drag-and-drop file uploader in the "New Request" modal supporting multiple attachments up to 50MB per file.
  * **Role Detection Fix**: Resolved role checking bug where composite roles like `"Admin, Designer"` were denied action buttons and status dropdowns.
  * **Attachment Actions & Preview**: Direct preview, download, delete, and 1-click **"Copy NAS Folder Path"** to clipboard for opening in Windows Explorer.
  * **1-Click Project Ingestion**: Ingests all order attachments directly into the linked project's `01_BRIEF_ASSETS` directory on the NAS.
* **🪟 Desktop Standardized Project Creator ([ProjectCreatorPage.xaml](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Views/ProjectCreatorPage.xaml))**:
  * **Order Discovery Dropdown**: Discovers active orders from `_Orders/creative-orders.jsonl` with live attachment counts (`[📎 N files]`) and instant Sync button.
  * **Auto-Population**: Selecting an order auto-populates project title, sub-brand, and structured brief remarks.
  * **Automatic Attachment Ingestion**: Copies all attachments into `01_BRIEF_ASSETS/`, sets `order_id` in `README.md` frontmatter, and updates order status to `in_progress`.
* **🔄 Cross-Platform Creative Orders Real-Time Sync**:
  * Direct live REST API integration between Desktop (Windows WPF & Linux Avalonia) and the Web Management Portal (`/api/orders`).
  * Dedicated Desktop Order Requests management page ([OrderRequestsPage.xaml](file:///e:/Dev/Projects/SS-Brand-Assets/src/SS-CAM/Views/OrderRequestsPage.xaml)).
* **📱 Android Native Studio Companion (v4.6.2, Build 466)**:
  * Synchronized settings, metrics, and Creative Orders pipeline with signed Google Play AAB bundle & standalone APK.

---

## 🚀 Overview

**SS-CAM** (SuamiSihat Creative Assets Management) is an enterprise creative operations and digital asset platform developed for **SuamiSihat™ Holding Sdn. Bhd.** It unifies creative workflows across native Windows/Linux workstations, Android mobile devices, and centralized Synology NAS network storage.

SS-CAM eliminates project disorganization, scattered copywriting drafts, inconsistent brand palettes, and untracked deliverable approvals by providing a standardized filesystem vault hierarchy, a Markdown-as-database architecture, an in-app Copywriting Studio, multi-platform collaboration tools, automated 1-click handover ZIP packaging, and live designer capacity analytics.

---

## 📥 Multi-Platform Deployment Options

SS-CAM provides a comprehensive multi-client ecosystem to support diverse creative studio environments:

| Target Platform | Package / Variant | Deployment / Execution | Role in Ecosystem |
|---|---|---|---|
| 🪟 **Windows 10 / 11** | **Native WPF Single-File (`src/SS-CAM`)** | Portable executable: `.\dist\SS-CAM-v4.8.1.exe` | **Flagship Designer Client**: Offline-first, full Post Haste template generator, Preflight Quality Auditor, Direct Synology Drive I/O. |
| 🐧 **Linux Desktop (Fedora/Ubuntu)** | **Native Avalonia UI (`src/SS-CAM.Linux`)** | Standalone Tarball: `.\dist\SS-CAM-v4.6.2-linux-x64.tar.gz`<br>1-Command: `curl -fsSL https://raw.githubusercontent.com/SuamiSihat/ss_cam/SS-Master/installer/install-linux.sh \| sudo bash` | **Native Linux Desktop Client**: Skia graphics engine, GNOME/KDE `.desktop` integration, direct `~/SynologyDrive/` I/O. |
| 📱 **Android Native** | **Native Android App (`src/SS-CAM.Android`)** | [Google Play Store](https://play.google.com/store/apps/details?id=com.suamisihat.creative&hl=en-US&ah=4fxu9FCVL39aFVxQdNL2fGvtHd4&pli=1)<br>Direct APK: `.\dist\SS-CAM-v4.6.2-android-release.apk` | **Mobile Studio Companion**: 2×2 Bento KPI telemetry, 1-tap deliverable approvals, live ICY radio streaming, desk standby clock, push alerts. |
| 🌐 **Admin Web Portal** | **Docker Web Container (`src/SS-CAM.Web`)** | Deploy on Synology NAS / Linux Server: `cd src/SS-CAM.Web && docker compose up -d` | **Admin & Central Control Plane**: User provisioning, holding switcher (SSH/SSC/SSW/SSE/SST), audit logs, API hub. |

---

## 🌟 Core Features & Capabilities

### 1. Standardized 5-Folder Vault Hierarchy

All creative projects follow a canonical directory structure on Synology NAS (`Creative-Team/[YYYY]/[YYYYMM_Month]/[ProjectFolder]`), preventing file clutter and missing assets:

```text
📁 YYYYMM_NNNNX_BRAND_ProjectTitle/
├── 📁 01_BRIEF_ASSETS/        # Raw creative briefs, moodboards, reference imagery
├── 📁 02_SOURCE_FILES/        # Working Affinity Designer (.afdesign), Photoshop (.psd), Illustrator (.ai)
├── 📁 03_COPYWRITING/         # Dedicated COPY.md scripts, viral hook angles, and copy specs
├── 📁 04_WORK_IN_PROGRESS/    # Intermediate drafts, work-in-progress exports, and test renders
├── 📁 05_DELIVERABLES/        # Final approved mockups, high-res deliverables, and client exports
└── 📄 README.md               # YAML frontmatter metadata (status, priority, designer, revision)
```

### 2. ClickUp 3.0-Style 2-Column Task Workspace

* **Markdown Brief Canvas (68%)**: Full-featured GFM brief editor with live syntax highlighting, table rendering, callout alert blocks, and Mermaid diagrams.
* **Right Inspector Panel (32%)**: Collapsible inspector panel displaying job ID, designer routing, priority, campaign deadlines, holding subsidiary metadata, and deliverable review actions.
* **Deliverable Inspection & Review**: Lightbox modal with one-click `✓ Sign-Off` or `⚠️ Request Revision` actions that automatically increment revision rounds.

### 3. Dedicated Copywriting Studio & Live Telemetry

* **Direct NAS Persistence**: Automatically reads and writes to `03_COPYWRITING/COPY.md`.
* **Live Copy Analytics**: Computes real-time word count, character count, and estimated reading time.
* **Structured Hook Frameworks**: Pre-scaffolded templates for viral video hooks, product benefit scripts, and social ad copy.

### 4. Contextual Discussions & Notification Feed

* **NAS JSONL Discussion Engine**: Project-level comments stored in `_comments.jsonl` with support for `@mention` tags (e.g. `@hasan`, `@haikal`, `@harussani`).
* **Notification Drawer**: Global activity feed tracking mentions, approvals, revision requests, and project assignments.

### 5. Enterprise RBAC & Security Audit Logs

* **Role-Based Permissions**: Granular roles for `Admin`, `Director`, `Lead`, `Manager`, `Designer`, and `Copywriter`.
* **Permanent Audit Trail**: All critical operations (creations, deletions, sign-offs, role updates) are recorded to an immutable JSONL audit log (`_Team/_Audit/audit_log.jsonl`).
* **Safe Administrative Project Deletion**: Authorized administrative deletion with boundary checks, system folder protections (`_Team`, `#recycle`), and recursive NAS subfolder removal.

### 6. Minimal Brand Assets & Swatch Inspector

* **Live Swatch Telemetry**: Live interactive explorer for SuamiSihat holding palettes (`SSH`, `SSC`, `SSW`, `SSE`, `SST`) displaying **HEX**, **RGB**, **CMYK**, and **Pantone** breakdowns with 1-click clipboard copying.
* **Vector QR Code Studio**: Generate branded QR codes for URLs, Wi-Fi credentials, and vCards with high-resolution PNG export.

### 7. Creative Wellbeing & Biometric Rhythm

* **Real-Time 5-Axis Biometric Radar**: Live spider chart calculating creative flow, vitality, rest, focus, and pressure.
* **Biometric Flow Calibrator & 1-Click Rebalancers**: Tactile 1–5 baseline rating matrix and instant cognitive reset shortcuts.
* **30-Day Creative Focus Heatmap**: GitHub-style activity grid mapping daily deep work intensity and streaks.
* **Interactive Vector Hydration Tracker**: Real-time water intake tracking with animated wave physics and 8 glass cup tiles.
* **16-Second Box Breathing Coach**: Visual stress reset coach for high-intensity design sprints.
* **JAKIM Waktu Solat**: Real-time prayer timetable for 41 Malaysian zones with live countdowns and adhan notifications.
* **Focus Radio Player**: Low-latency stream player for Malaysian stations (BFM 89.9, Hitz, Era, Hot FM, Suria, THR Raaga) and lo-fi focus beats.

---

## 🎨 Design System & Visual Hierarchy

SS-CAM adheres to the **Microsoft Fluent 2** design language and the **SuamiSihat 60:30:10** color rule:

| Visual Ratio | Scope | Palette Tokens | Purpose |
|---|---|---|---|
| **60% Dominant** | Application Surfaces | Deep Prussian Blue (`#022057`) / Clean Slate (`#F8FAFC`) | Clean background canvas and visual balance |
| **30% Structure** | Structural Controls | SuamiSihat Azure (`#21A1F7`) & Royal Blue (`#043388`) | Navigation bars, cards, borders, text hierarchy |
| **10% Accent** | Action Energy | Warm Gold (`#BD9A73`) & Success Green (`#107C10`) | Primary CTAs, status badges, alert highlights |

### Available Desktop Themes

1. **SS Default**: Deep navy sidebar with clean white content canvas.
2. **Falconia**: Pure Fluent 2 Light mode with crisp typography and subtle card borders.
3. **Metamorphosis**: Dark glassmorphic theme with cyan glowing accents and frosted surfaces.

---

## 🏗️ Technical Architecture

```text
┌───────────────────────────────────────────────────────────────────────────────────┐
│                                 SS-CAM ECOSYSTEM                                  │
├─────────────────────────┬─────────────────────────┬───────────────────────────────┤
│ 🖥️ WINDOWS WORKSTATION   │ 🐧 FEDORA WORKSTATION   │ 📱 ANDROID MOBILE COMPANION   │
│ • C# WPF (.NET 4.8)     │ • Avalonia UI (.NET 8)  │ • Kotlin + Jetpack Compose    │
│ • WPF-UI (Fluent 2)     │ • Native Skia Engine    │ • Coil / Hardware Bitmaps     │
│ • Direct Local SSD I/O  │ • Local ~/SynologyDrive │ • Instant Push Alerts & Diff  │
├─────────────────────────┴─────────────────────────┴───────────────────────────────┤
│                          🌐 SYNOLOGY NAS ADMIN WEB PORTAL                         │
│                          • Svelte 5 (Runes) + TypeScript                          │
│                          • Node.js 20 Express + WebSocket + REST/SSE API          │
│                          • Central Administration, Holdings Switcher & Audit Logs │
│                          • Live Review Lightbox & Split Visual Comparison         │
└─────────────────────────────────────────┬─────────────────────────────────────────┘
                                          │
                                          ▼
┌───────────────────────────────────────────────────────────────────────────────────┐
│ 📂 SYNOLOGY NAS FILE SYSTEM (Markdown-as-Database Storage)                        │
│ • Canonical 5-Folder Hierarchy: \\SSNAS\Creative-Team\[YYYY]\[Month]\[Project]    │
│ • YAML Frontmatter Project Metadata (README.md)                                   │
│ • Markdown Copywriting Hooks (03_COPYWRITING/COPY.md)                             │
│ • Immutable Audit Logs & Team Profiles (_Team/_Audit/audit_log.jsonl)             │
│ • Cross-Platform Realtime Sync via Synology Drive & Chokidar File Watchers        │
└───────────────────────────────────────────────────────────────────────────────────┘
```

---

## 💻 System Requirements

| Specification | Desktop Client Requirement | Web Portal Requirement |
|---|---|---|
| **Operating System** | Windows 10 (1903+) / Windows 11 / Linux (x64) | Synology DSM 7.x / Ubuntu 22.04+ / Docker |
| **Runtime** | .NET Framework 4.8 (Windows) / .NET 8.0 (Linux) | Node.js 20 LTS or Docker Engine |
| **Memory (RAM)** | 4 GB Minimum (8 GB+ Recommended) | 512 MB Container RAM |
| **Storage Footprint**| ~5.7 MB (Single-File Portable Exe) | ~120 MB Docker Image |
| **Network** | Synology Drive Client or SMB `\\SSNAS\Creative-Team` | Port 4000 (HTTPS via Reverse Proxy) |

---

## 📄 License & Governance

SS-CAM is an internal digital assets & clinic operations management platform created for **SuamiSihat™ Holding Sdn. Bhd.** and **SuamiSihat Clinic (SSC)** franchise network.

* **Organization**: SuamiSihat Digital & Creative Production Division
* **Documentation**: [Master Ecosystem Specification](./docs/SS_CAM_ECOSYSTEM_MASTER_SPECIFICATION.md) · [GitHub Pages Landing Page](https://suamisihat.github.io/ss_cam/)
* **Franchise & Clinic Operations**: [PERNAS Grant Proposal](./docs/KERTAS_CADANGAN_GERAN_PERNAS_SSCAM.md) · [Official PDF Proposal](./docs/KERTAS_CADANGAN_GERAN_PERNAS_SSCAM.pdf)
* **Roadmap**: [Project Development Roadmap](./ROADMAP.md)
* **Repository**: [SuamiSihat/ss_cam](https://github.com/SuamiSihat/ss_cam)
* **License**: Internal Commercial Use Only — see [EULA](./installer/EULA.txt)
