# SS-CAM Project Roadmap

> **Living document.** Updated with every release. Last updated: 2026-09-09.

---

## ✅ Released Milestones

| Version | Date | Highlights |
|---------|------|-----------|
| **v1.9.0** | 2024 | Initial internal release — Project Creator, basic dashboard |
| **v2.0.0** | 2025-01 | WPF-UI Fluent 2 redesign, Wellbeing module, Mind Drop notes |
| **v2.1.0** | 2025-03 | Radio player, Brand Assets vault |
| **v2.2.0** | 2025-06 | Workstation Health scanner, dark mode tokens |
| **v2.3.0** | 2026-01 | Search & Copy v1, Markdown README preview |
| **v2.3.6** | 2026-08-06 | Version badge fix, AV metadata patch, stability improvements |
| **v2.4.0** | 2026-08-10 | Dashboard Inspiration Widget (40 tips + RSS), Project Brief Markdown Editor, Search & Copy Catalog layout |
| **v2.5.0** | 2026-08-10 | Quick Notes module, Task Manager Kanban, Team Board, Frontmatter injection in Project Creator |
| **v2.6.0** | 2026-08-11 | Smoke-test bug-fix release: FrontmatterService P0 fix, theme toggle wired, dead code removed, dynamic rescan count, static HttpClient |
| **v2.6.2** | 2026-08-11 | Creative Workflow Modernization: App bridge launcher, 1-Click ZIP Finalizer, Brand Kit Quick-Tray, Visual Asset Lightbox |
| **v2.6.3** | 2026-08-11 | Audit Remediation & Diagnostic Logging: Dynamic token standardization across 4 modules, Segoe Fluent vector icon standardization |
| **v3.0.0** | 2026-08-12 | Major Release: Full Fluent 2 overhaul across all 12 modules, 5 theme profiles (Falconia, Metamorphosis, Catppuccin, Rosé Pine, Nord) |
| **v3.0.1** | 2026-08-12 | Categorized Fluent 2 sidebar navigation (5 visual categories + headers/separators), adaptive bottom live bar collapse state |
| **v3.1.0** | 2026-08-12 | QR Code Studio & Generator module, Sound Engineer visualizer with floating Mars symbols, Radio studio polish |
| **v3.1.2** | 2026-08-12 | Multi-user isolation on shared NAS drives (`_{username}` scoping), team-wide shared presets |
| **v3.2.0** | 2026-08-12 | Big Calendar module (`CalendarPage`), Task Manager Calendar Date & FIFO Queue Order sorting, SSNAS Synology Drive Setup guide |
| **v3.3.0** | 2026-08-13 | Fluent 2 Startup Splash Window, Centralized Notification & Clipboard Services, Task Manager queue & parser upgrades |
| **v3.4.0** | 2026-08-13 | Starter Canvas Engine (.af/.psd/.ai), Web Design presets, Search Category Filter, Calendar Quick Actions |
| **v3.5.0** | 2026-08-17 | In-App Project Brief Markdown Editor in Search & Copy, Workspace Designer Folder Scoping, Repository Hygiene & Architecture Cleanup |
| **v3.5.0-linux** | 2026-08-14 | Linux Desktop Edition: Initial native Avalonia UI (.NET 8) port for Fedora Linux & Synology Drive Client (`~/SynologyDrive/`) |
| **v3.6.0** | 2026-08-17 | Microsoft Fluent UI Web (`sscam-fluentui-web`) design tokens, 3-Tier F-Pattern Dashboard analytics, Copywriting AI script presets |
| **v3.6.1** | 2026-08-17 | Metamorphosis theme solid surface opacity overhaul, legibility fixes for drawer panels & cards |
| **v4.0.0** | 2026-08-18 | **Centralized Vault Hierarchy & ClickUp 3.0 Task Workspace**: Year-first NAS hierarchy (`Creative-Team/[YYYY]/[YYYYMM_Month]/[Project]`), 5-folder structure, 68%/32% 2-column task workspace, in-app Copywriting Studio (`03_COPYWRITING/COPY.md`), JSONL contextual comments (`_comments.jsonl`), enterprise RBAC & immutable audit logs |
| **v4.0.1** | 2026-08-18 | **Patch Release**: Real async GitHub Releases API update checker with NAS `version.json` fallback, landing page synchronization |
| **v4.1.0** | 2026-08-19 | **Desktop Feature Parity & Studio Overhaul**: Desktop Copywriting Studio (`CopywritingPage`), contextual discussions data layer (`ProjectCommentService`), and ClickUp 3.0-style 2-column task workspace |
| **v4.2.0** | 2026-08-20 | **Desktop NAS Sync & Batch Operations + Web Real-Time SSE**: Background `WorkspaceWatcherService`, `ThumbnailCacheService`, UI virtualization, batch operations ribbon, and Web SSE event stream + HTTP 206 video range streaming |
| **v4.3.0** | 2026-08-20 | **Asset Export, Packaging & Naming Engine**: Desktop `ExportPackagingService` (1-click ZIP with `HANDOVER_SUMMARY.html`), `AssetNamingService` canonical sanitizer, and Web `ExportService` ZIP streaming |
| **v4.4.0** | 2026-08-20 | **Designer Workload Heatmaps & Creative SLA Analytics**: Live designer capacity radars (`WorkloadSlaService`), capacity progress meters, and operational SLA telemetry across Desktop and Web |
| **v4.4.1** | 2026-08-26 | **Radio Stream, Deep Scanner & Copywriting FlowDocument Engine**: Official SuamiSihat Radio Stream preset `#1`, deep month-container project discovery, dynamic project ID auto-calculation, FlowDocument markdown rendering by default, sanitized designer catalog filtering |
| **v4.4.2** | 2026-08-27 | **Art Director Polish & Live Ad/WhatsApp Preview Engine**: Live Split-View formatting preview (WhatsApp & Meta Ads), one-click Hook & CTA snippet drawer, dynamic status pill badges, polished vector empty states, and overline typographic rhythm |
| **v4.4.3** | 2026-08-27 | **Radio Visualizer Overhaul & Station Upgrades**: Dynamic real-time playback gating for 69 Mars symbols & 6 SuamiSihat logomarks, song wavelength sinusoidal vertical oscillation & beat kick pulsing across all visualizer modes, curated Nightwave Plaza & SomaFM Groove Salad stations, Malaysia holidays calendar integration |
| **v4.4.4** | 2026-08-27 | **Creative Wellbeing & Biometric Suite Overhaul**: Real-time 5-axis biometric spider radar, dynamic 30-day focus intensity heatmap, interactive vector water hydration tracker with sinusoidal waves, and burnout risk analytics |
| **v4.5.0** | 2026-08-28 | **Master Brand System v3.5.1 Integration & Brand Assets Vault Modernization**: Full alignment with official SuamiSihat Master Brand System Guide, Multi-Format Color Matrix (BAL/RAL standard, CIE-Lab, Pantone, CSS tokens), 5 Corporate Sub-Brands Hub with 1-click folder launchers, interactive Surface Contrast Previewer ($L \ge 50\%$ rule), 4-tier typography scale reference |
| **v4.5.1** | 2026-08-30 | **Cross-Platform Ecosystem Synchronization & Companion Harmonization**: Windows Desktop Client v4.5.1 (single-file executable, UTF-8 BOM, 100% theme-adaptive DynamicResource tokens), Web Management Portal Svelte 5 / Node.js 20 (zero-emoji Fluent 2 design, desktop mobile dock layout fix, 28/28 passed test suite), Android Companion App (Compose UI, Studio Lounge, and live NAS sync) |
| **v4.6.0** | 2026-09-01 | **Beta Release — Android Native Companion Modernization, Live ICY Stream Metadata Engine, Preflight Quality Auditor & Desk Companion Standby Mode**: Desktop Preflight Quality Validator & Auto-Fix Scaffolding, Android 2×2 Bento KPI Telemetry & Persistent Local Caching (`ProjectCacheManager`), real-time AzuraCast/Laut.fm/SomaFM live song broadcasting metadata on TopAppBar & bottom cassette deck, interactive `SsHero` animated wave mesh splash screen, interactive `FluentMarkdownViewer` with live task checkbox syncing, OLED Desk Standby Mode, and Material You monochromatic icons |
| **v4.6.1** | 2026-09-04 | **Multi-Platform Release — Cross-Platform Creative Orders Real-Time Sync, Order Requests Scaffolding Engine & Desktop Startup Resilience**: Direct live REST API integration between Desktop (Windows WPF & Linux Avalonia) and central Web Portal (`/api/orders`), automatic JWT authentication and live queue fetching with local Synology NAS ledger caching (`creative-orders.jsonl`), instant bidirectional status sync (`PATCH /api/orders/{id}`), 1-Click project vault scaffolding (`01_Brief_and_Copy/COPY.md` + frontmatter), startup splash hang resolution, task manager Kanban overdue suppression, frontmatter YAML quote sanitization, web creative direction matrix preview & auto-wrapping markdown editor, shared team board test isolation, Android Companion App release (v4.6.1, Code 462), and Linux Avalonia release package. |
| **v4.6.2** | 2026-09-08 | **NAS Temporary Attachment Vault, Designer Task Handover, Web Multi-File Upload & Desktop Project Creator Auto-Ingestion**: Desktop Task Manager Kanban card task ownership handover & reassignment menu (`README.md` + frontmatter sync), Synology NAS temporary intake directory (`\\SSNAS\Creative-Team\_Orders\<ORDER_ID>\`) with JSONL persistence, Web Portal drag-and-drop multi-file upload dropzone, role detection fix for composite roles (`"Admin, Designer"`), 1-click project ingestion into `01_BRIEF_ASSETS`, and Desktop Project Creator live order discovery, auto-population, and automatic attachment copy |
| **v4.7.0** | 2026-09-09 | **Velocity Navigation Engine, Canva Cloud Bridge, Per-User Team Storage & Visual Timeline Alignment**: 0 ms desktop navigation (`NavigationCacheMode="Required"`), Canva Creative Cloud Bridge with platform auto-size launcher and `.url` scaffolding, per-user team storage architecture (`_Team/Users/{staffId}/avatar.jpg` + `profile.json`), bi-directional avatar synchronization across Web/Desktop/Android, Big Calendar 3-letter day names (`Mon..Sun`), holiday-strict red highlighting, and off-day schedule conflict prevention |
| **v4.8.0** | 2026-09-09 | **Interactive Visual Asset Revision Diff Slider, Copywriting Studio Live Preview & Cross-Platform Avatar Sync**: 5-mode Fluent 2 Visual Diff Inspector (Vertical/Horizontal Split swipe, Side-by-Side dual view, Opacity Blend onion skin, 32bpp Euclidean Pixel Difference mapping), automated revision pair detection (`_v1` → `_v2`, `draft` → `final`), synchronized lockstep zoom & pan, Copywriting Studio split-view live preview (WhatsApp chat & Meta Ad feed simulation with OG previews and 1-click exporters), per-user team storage architecture, and zero-warning Source Guardian audit |
| **v4.8.1** | 2026-09-10 | **Global Studio Command Palette (`Ctrl + K`), Art Director 60-30-10 Polish, Live Work Session Stopwatch, Real-Time Team Task Stream & Creative Operations Upgrade**: Universal keyboard quick-launcher for 15 modules, brand colors, and copy hooks; 60-30-10 Fluent 2 visual hierarchy; live stopwatch, project status pill, and crash-resilient session tracker drawer; real-time studio live tasks feed on Main Dashboard (`LiveTaskSyncService`); team task start/resume desktop toast notifications; comprehensive resolution of dropdown text cropping, baseline clipping, and mnemonic underscore stripping across all views; Creative Request low-priority intake tier (`tier_0`), contextual Digital vs. Print format & material architecture, full request editing, "Added to Backlog" intake refinement, and pure production queue cleanup |

---

## 🎯 Active Milestone — v4.9.0: Smart Ingester & Copywriting Diff Engine (Target: Q4 2026)

### 1. Drag-and-Drop Folder Ingester
* Drag external assets directly onto project cards in Project Creator or Search Copy to automatically ingest and organize into `01_BRIEF_ASSETS`, `02_SOURCE`, etc.

### 2. Copywriting & Brief Revision Diff Engine
* Visual color-coded diff viewer (green additions / red deletions) for `COPY.md` scripts and `README.md` project briefs across revision cycles.

### 3. AI Brief Intelligence & Style Preflight Assistant
* Integrated LLM assistant to validate brief completeness, detect tone deviations, and auto-suggest missing brand tokens.

---

## 🔮 Planned — v4.9.0: Global Studio Command Palette & Ingester (Target: Q4 2026)

### 1. Global Command Palette (`Ctrl + K`)
* Universal keyboard launcher for jumping to projects, copying brand hex codes, searching snippets, or toggling radio stations without leaving the current view.

### 2. Drag-and-Drop Folder Ingester
* Drag external assets directly onto project cards in Project Creator to automatically organize into `01_BRIEF_ASSETS`, `02_SOURCE_FILES`, etc.

---

## 📱 & 🐧 Planned — v5.0.0: Multi-Platform Ecosystem Expansion (Target: Q1 2027)

| Feature / Component | Target Stack | Description |
|---|---|---|
| **Linux Fedora Native Client (`src/SS-CAM.Linux`)** | C# / Avalonia UI 12.1 (.NET 8/10 LTS) | Complete feature parity for Fedora/Ubuntu workstations with Fluent 2 styling, local `~/SynologyDrive/` workspace integration, and native Skia desktop rendering. |
| **Android Native Client (`src/SS-CAM.Android`)** | Kotlin + Jetpack Compose | Native mobile companion for creative leads & reviewers: instant deliverable review, 1-tap approvals/revisions, push notifications, task tracking, and brand color palette picker. |
| **Web Portal Admin & Control Console (`src/SS-CAM.Web`)** | Svelte 5 + Node.js Express (Docker) | Central administration hub: corporate holding switcher (SSH, SSC, SSW, SSE, SST), user provisioning, immutable audit log explorer, webhook dispatch, and remote API gateway. |
| **Multi-Workspace NAS Switching** | Cross-Platform | Fast switching between business unit shares (`Creative-Team`, `Video-Production`, `Marketing-Assets`). |

---

## 🏥 Planned — v5.1.0: Clinic Operations & PERNAS Franchise Standardization (Target: Q1–Q2 2027)

> **Strategic Alignment**: Directly aligns with the **PERNAS (Perbadanan Nasional Berhad)** Franchise Development Grant application for SuamiSihat Clinic (SSC).  
> **Official Proposal Reference**: [`docs/KERTAS_CADANGAN_GERAN_PERNAS_SSCAM.md`](./docs/KERTAS_CADANGAN_GERAN_PERNAS_SSCAM.md) · [PDF Version](./docs/KERTAS_CADANGAN_GERAN_PERNAS_SSCAM.pdf)

| Feature / Module | Target Platform | Description & Operational Impact |
|---|---|---|
| **In-Clinic Patient Consultation Suite** | Android Tablet (`SS-CAM.Android`) & Web (`SS-CAM.Web`) | Interactive 3D medical anatomy diagrams, treatment procedure simulations (ESWT Shockwave Therapy, TRT hormonal therapy, PE/ED treatment roadmaps), and recovery timelines used by doctors in consultation rooms. |
| **Waiting Lounge TV Signage Engine** | Android TV (`SS-CAM.Android`) / Web Kiosk | Automated on-device digital signage player: dynamic doctor-on-duty schedules, queue announcements, and KKM-approved health literacy video loops running without internet buffering. |
| **KKM Regulatory Compliance Gateway** | WPF Desktop (`SS-CAM`) & Web Admin | Single source of truth for Ministry of Health (KKM) / Medical Advertising Board (LIU/MAB) pre-approved claims, audit logs, and locked templates preventing unauthorized branch claims. |
| **Branch Marketing & Patient Intake Kit** | Web Portal & Desktop (`QrCodePage`) | Dynamic branch QR code generator (touchless patient check-in, WhatsApp consult, Google reviews) and automated branch address/contact injection into HQ campaign templates. |
| **Post-Treatment Digital Care Dispatcher** | Web Portal & Mobile Companion | 1-click WhatsApp/SMS aftercare guide generator delivering bilingual recovery leaflets, dosage precautions, and follow-up appointment reminders directly to patient smartphones. |
| **Franchise SOP Knowledge Base** | Synology NAS Vault (`_Clinic/SOP_Manuals`) | Version-controlled clinical and operational Standard Operating Procedure (SOP) manuals ensuring uniform service delivery across all franchise clinic branches. |

---

## 🔮 Future Exploration — v5.2.0: Enterprise Intelligence & Asset Versioning (Target: Q3 2027)

| Area | Idea |
|------|------|
| **Asset Revision Snapshots** | Visual diff timeline and rollback engine for `.afdesign` and `.psd` binaries |
| **AI Creative Assistant** | Local/Offline LLM integration for generating ad hooks, drafting campaign briefs, and translating copy |
| **Real-time Live Sync Hub** | High-throughput bi-directional synchronization bridge between native clients and NAS storage |

---

## Architecture Constraints

The following constraints apply to all versions and must be respected in planning:

| Constraint | Reason |
|-----------|--------|
| **C# 5 syntax only** | MSBuild `v4.0.30319` on the build machine caps at `/langversion:5` |
| **No new NuGet packages** | `Costura.Fody` single-file bundling makes adding packages complex and risky |
| **`System.Net.Http`** | Already a framework assembly on .NET 4.8; use for all HTTP instead of `WebClient` |
| **`System.Xml.Linq`** | Available; use for RSS/XML parsing |
| **No WPF-UI breaking changes** | Locked to `WPF-UI 3.0.4` |
| **JSON via Newtonsoft.Json** | Already bundled; use for all serialisation |
| **NAS path separator** | Always use `Path.Combine` — never hardcode `\` or `/` |

---

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md) for architecture, namespace conventions, and build instructions.
