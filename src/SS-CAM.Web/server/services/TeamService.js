const fs = require('fs');
const path = require('path');
const config = require('../config');

class TeamService {
  static getUsersDir() {
    const usersDir = path.join(config.WORKSPACE_ROOT, '_Team', 'Users');
    if (!fs.existsSync(usersDir)) {
      try { fs.mkdirSync(usersDir, { recursive: true }); } catch (e) {}
    }
    return usersDir;
  }

  static getUserDir(staffId) {
    if (!staffId) return null;
    const cleanId = String(staffId).trim().toUpperCase();
    const userDir = path.join(this.getUsersDir(), cleanId);
    if (!fs.existsSync(userDir)) {
      try { fs.mkdirSync(userDir, { recursive: true }); } catch (e) {}
    }
    return userDir;
  }

  static getAvatarPath(staffId) {
    if (!staffId) return null;
    const cleanId = String(staffId).trim().toUpperCase();
    const userDir = path.join(this.getUsersDir(), cleanId);
    const candidates = ['avatar.jpg', 'avatar.jpeg', 'avatar.png', 'avatar.webp'];
    for (const file of candidates) {
      const full = path.join(userDir, file);
      if (fs.existsSync(full)) return full;
    }
    return null;
  }

  static saveAvatarFile(staffId, avatarData) {
    if (!staffId || !avatarData) return null;
    try {
      const userDir = this.getUserDir(staffId);
      if (!userDir) return null;

      if (avatarData.startsWith('data:image/')) {
        const commaIdx = avatarData.indexOf(',');
        if (commaIdx >= 0) {
          const base64Str = avatarData.substring(commaIdx + 1);
          const buffer = Buffer.from(base64Str, 'base64');
          const avatarFile = path.join(userDir, 'avatar.jpg');
          fs.writeFileSync(avatarFile, buffer);
          return `/api/users/${encodeURIComponent(staffId.trim().toUpperCase())}/avatar`;
        }
      } else if (avatarData.startsWith('/api/users/')) {
        return avatarData;
      }
    } catch (err) {
      console.error(`[TeamService] saveAvatarFile error for ${staffId}:`, err.message);
    }
    return null;
  }

  static saveUserProfile(staffId, profileData) {
    if (!staffId) return;
    try {
      const userDir = this.getUserDir(staffId);
      if (userDir) {
        const pPath = path.join(userDir, 'profile.json');
        fs.writeFileSync(pPath, JSON.stringify(profileData, null, 2), 'utf8');
      }
    } catch (e) {}
  }

  static getRosterPath() {
    const configDir = path.join(config.WORKSPACE_ROOT, '_Team', '_Config');
    if (!fs.existsSync(configDir)) {
      try { fs.mkdirSync(configDir, { recursive: true }); } catch (e) {}
    }
    return path.join(configDir, 'staff_directory.json');
  }

  static getLiveTasksPath() {
    const teamDir = path.join(config.WORKSPACE_ROOT, '_Team');
    if (!fs.existsSync(teamDir)) {
      try { fs.mkdirSync(teamDir, { recursive: true }); } catch (e) {}
    }
    return path.join(teamDir, 'live_tasks.json');
  }

  /**
   * Reads active live tasks telemetry ledger from Synology NAS (_Team/live_tasks.json).
   * Strips UTF-8 BOM, parses JSON array, and excludes stale sessions (>16 hours).
   */
  static getLiveTasks() {
    try {
      const p = this.getLiveTasksPath();
      if (!fs.existsSync(p)) return [];
      let raw = fs.readFileSync(p, 'utf8');
      if (raw.charCodeAt(0) === 0xFEFF) raw = raw.slice(1);
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];

      const cutoff = Date.now() - 16 * 60 * 60 * 1000;
      const roster = this.getStaffRoster();
      const rosterMap = new Map();
      roster.forEach(m => {
        if (m.staffId) rosterMap.set(m.staffId.toUpperCase(), m);
        if (m.name) rosterMap.set(m.name.toLowerCase(), m);
      });

      const COLOR_PALETTE = ['#0078D4', '#106EBE', '#7C3AED', '#D97706', '#21A1F7', '#059669', '#EF4444', '#8B5CF6'];

      return parsed
        .filter(t => {
          if (!t) return false;
          const hb = t.LastHeartbeat || t.StartedAt;
          if (!hb) return true;
          const dt = new Date(hb).getTime();
          return isNaN(dt) || dt >= cutoff;
        })
        .map(t => {
          const staffKey = (t.StaffId || '').toUpperCase();
          const nameKey = (t.DesignerName || '').toLowerCase();
          const member = rosterMap.get(staffKey) || rosterMap.get(nameKey);

          let avatarColor = member?.avatarColor;
          if (!avatarColor) {
            const seed = staffKey || nameKey || 'DESIGNER';
            let hash = 0;
            for (let i = 0; i < seed.length; i++) hash = (hash * 31 + seed.charCodeAt(i)) >>> 0;
            avatarColor = COLOR_PALETTE[hash % COLOR_PALETTE.length];
          }

          return {
            ...t,
            AvatarColor: avatarColor,
            DesignerName: t.DesignerName || member?.name || t.StaffId || 'Designer'
          };
        });
    } catch (err) {
      return [];
    }
  }

  /**
   * Loads canonical staff directory from NAS, seeding default if missing.
   */
  static getStaffRoster() {
    const rosterPath = this.getRosterPath();
    const defaultTeam = [
      { staffId: 'SS0004', username: 'harussani', name: 'Harussani', email: 'harussani.suamisihat@gmail.com', role: 'Head of Creative', department: 'Creative Production', defaultBrand: 'SS', avatarColor: '#0078D4', active: true },
      { staffId: 'SS0035', username: 'haikal', name: 'Haikal', email: 'haikal.suamisihat@gmail.com', role: 'Multimedia Designer', department: 'Multimedia & Motion', defaultBrand: 'SS', avatarColor: '#106EBE', active: true },
      { staffId: 'SS0037', username: 'aliff', name: 'Aliff', email: 'aliffnaz.suamisihat@gmail.com', role: 'Multimedia Designer', department: 'Multimedia & Motion', defaultBrand: 'SSE', avatarColor: '#7C3AED', active: true },
      { staffId: 'SS0073', username: 'raihan', name: 'Raihan', email: 'raihan.suamisihat@gmail.com', role: 'Head of Marketing & Sale', department: 'Marketing & Sales', defaultBrand: 'SS', avatarColor: '#D97706', active: true },
      { staffId: 'SS0001', username: 'hasan', name: 'Hasan', email: 'hasan@suamisihat.com', role: 'Chief Executive Officer', department: 'Executive Management', defaultBrand: 'SS', avatarColor: '#21A1F7', active: true },
      { staffId: 'SS0071', username: 'gaddafi', name: 'Gaddafi', email: 'gaddafi@suamisihat.com', role: 'Co-Chief Executive Officer', department: 'Executive Management', defaultBrand: 'SS', avatarColor: '#059669', active: true }
    ];

    if (!fs.existsSync(rosterPath)) {
      try {
        fs.writeFileSync(rosterPath, JSON.stringify(defaultTeam, null, 2), 'utf8');
        return defaultTeam;
      } catch (err) {
        return defaultTeam;
      }
    }

    try {
      const raw = fs.readFileSync(rosterPath, 'utf8');
      const json = raw.replace(/^\uFEFF/, '');
      const roster = JSON.parse(json);
      const list = Array.isArray(roster) && roster.length > 0 ? roster : defaultTeam;

      // Auto-enrich members with physical avatars from _Team/Users/{staffId}/avatar.*
      list.forEach(m => {
        if (!m.avatarUrl || !m.avatar) {
          const diskAvatar = this.getAvatarPath(m.staffId);
          if (diskAvatar) {
            const url = `/api/users/${encodeURIComponent(m.staffId)}/avatar`;
            if (!m.avatarUrl) m.avatarUrl = url;
            if (!m.avatar) m.avatar = url;
          }
        }
      });

      return list;
    } catch (err) {
      console.error('[TeamService] Failed to parse staff_directory.json:', err.message);
      return defaultTeam;
    }
  }

  /**
   * Saves staff directory to NAS atomically with SMB fallback.
   */
  static saveStaffRoster(roster) {
    const rosterPath = this.getRosterPath();
    const json = JSON.stringify(roster, null, 2);
    try {
      const tempFile = `${rosterPath}.tmp.${Date.now()}`;
      fs.writeFileSync(tempFile, json, 'utf8');
      try {
        fs.renameSync(tempFile, rosterPath);
      } catch (renameErr) {
        // SMB UNC share lock fallback
        fs.writeFileSync(rosterPath, json, 'utf8');
        try { fs.unlinkSync(tempFile); } catch (e) {}
      }
    } catch (err) {
      fs.writeFileSync(rosterPath, json, 'utf8');
    }
    return roster;
  }

  static addStaffMember(member) {
    const roster = this.getStaffRoster();
    const staffId = (member.staffId || '').trim().toUpperCase();
    if (!staffId) throw new Error('Staff ID is required (e.g. SS0080).');

    const existing = roster.find(m => m.staffId.toLowerCase() === staffId.toLowerCase());
    if (existing) {
      throw new Error(`Staff ID '${staffId}' already exists in the directory.`);
    }

    const username = (member.username || member.name.toLowerCase().replace(/\s+/g, '')).trim();
    
    // Normalize multi-role support (Array or comma-separated string)
    let roles = ['Designer'];
    if (Array.isArray(member.roles) && member.roles.length > 0) {
      roles = member.roles;
    } else if (typeof member.role === 'string' && member.role.trim()) {
      roles = member.role.split(',').map(r => r.trim()).filter(Boolean);
    }
    const roleString = roles.join(', ') || 'Designer';

    let avatarUrl = member.avatarUrl || '';
    let avatarVal = member.avatar || '';
    if (member.avatar && member.avatar.startsWith('data:image/')) {
      const savedUrl = this.saveAvatarFile(staffId, member.avatar);
      if (savedUrl) {
        avatarUrl = savedUrl;
        avatarVal = savedUrl;
      }
    }

    const newMember = {
      staffId,
      username,
      name: member.name.trim(),
      email: member.email ? member.email.trim() : `${username}@suamisihat.com`,
      role: roleString,
      roles: roles.length > 0 ? roles : ['Designer'],
      department: member.department ? member.department.trim() : 'Creative Production',
      defaultBrand: (member.defaultBrand || 'SS').trim().toUpperCase(),
      avatar: avatarVal,
      avatarUrl: avatarUrl,
      avatarColor: member.avatarColor || '#0078D4',
      active: member.active !== false
    };

    roster.push(newMember);
    this.saveStaffRoster(roster);
    this.saveUserProfile(staffId, newMember);
    return newMember;
  }

  static updateStaffMember(staffId, updates) {
    const roster = this.getStaffRoster();
    const idx = roster.findIndex(m => m.staffId.toLowerCase() === staffId.toLowerCase() || (m.username && m.username.toLowerCase() === staffId.toLowerCase()));
    if (idx === -1) {
      throw new Error(`Staff member '${staffId}' not found.`);
    }

    const targetStaffId = roster[idx].staffId;

    // If avatar contains Base64 image, save physically to _Team/Users/{staffId}/avatar.jpg
    if (updates.avatar && typeof updates.avatar === 'string' && updates.avatar.startsWith('data:image/')) {
      const savedUrl = this.saveAvatarFile(targetStaffId, updates.avatar);
      if (savedUrl) {
        updates.avatarUrl = `${savedUrl}?t=${Date.now()}`;
        updates.avatar = updates.avatarUrl;
      }
    }

    let updatedRoles = updates.roles;
    let updatedRole = updates.role;

    if (typeof updatedRole === 'string' && updatedRole.trim()) {
      updatedRole = updatedRole.trim();
      if (!Array.isArray(updatedRoles) || updatedRoles.length === 0) {
        updatedRoles = updatedRole.split(',').map(r => r.trim()).filter(Boolean);
      }
    } else if (Array.isArray(updatedRoles) && updatedRoles.length > 0) {
      updatedRole = updatedRoles.join(', ');
    } else if (!updatedRoles && !updatedRole) {
      updatedRoles = roster[idx].roles || (roster[idx].role ? roster[idx].role.split(',').map(r => r.trim()).filter(Boolean) : ['Designer']);
      updatedRole = roster[idx].role || 'Designer';
    }

    roster[idx] = {
      ...roster[idx],
      ...updates,
      role: updatedRole || 'Designer',
      roles: updatedRoles || ['Designer'],
      DisplayText: `${targetStaffId} - ${updates.name || roster[idx].name} (${updatedRole || 'Designer'})`,
      staffId: targetStaffId // Preserve immutable Staff ID
    };

    this.saveStaffRoster(roster);
    this.saveUserProfile(targetStaffId, roster[idx]);
    return roster[idx];
  }

  static deleteStaffMember(staffId) {
    const roster = this.getStaffRoster();
    const targetId = staffId.trim().toUpperCase();
    const filtered = roster.filter(m => m.staffId.toUpperCase() !== targetId && m.username.toLowerCase() !== staffId.toLowerCase());
    if (filtered.length === roster.length) {
      throw new Error(`Staff member '${staffId}' not found.`);
    }

    this.saveStaffRoster(filtered);
    return { success: true, deletedStaffId: targetId };
  }

  /**
   * Evaluates if a member or role string belongs to active creative or admin tiers.
   */
  static isCreativeOrAdminRole(memberOrRole) {
    if (!memberOrRole) return false;
    const member = typeof memberOrRole === 'string' ? { role: memberOrRole } : memberOrRole;
    const roleLower = (member.role || member.officialTitle || '').toLowerCase();
    const deptLower = (member.department || '').toLowerCase();

    // If user has designer, copywriter, creative, art director, or admin roles, include them
    const rolesArr = Array.isArray(member.roles) ? member.roles.map(r => String(r).toLowerCase()) : [];
    if (rolesArr.includes('designer') || rolesArr.includes('admin') || rolesArr.includes('copywriter')) {
      return true;
    }
    if (roleLower.includes('designer') || roleLower.includes('copy') || roleLower.includes('creative') || roleLower.includes('art director') || roleLower.includes('admin') || roleLower.includes('multimedia')) {
      return true;
    }

    // Exclude standalone Managers, CEOs, Executive Directors, and Sales/Marketing Heads
    if (roleLower.includes('manager') || roleLower.includes('ceo') || roleLower.includes('chief') ||
        roleLower.includes('head of') || roleLower.includes('executive') || roleLower.includes('director of') ||
        deptLower.includes('executive') || deptLower.includes('management') || deptLower.includes('marketing & sales') ||
        roleLower === 'manager' || roleLower === 'mgr') {
      return false;
    }
    return true;
  }

  /**
   * Returns list of team members with assigned active workloads and capacity indicators.
   * Filters strictly to Designer, Copywriter & Admin role staff (excluding standalone Managers & Executives).
   */
  static getTeamDirectory() {
    const roster = this.getStaffRoster()
      .filter(m => m.active !== false)
      .filter(m => TeamService.isCreativeOrAdminRole(m));

    const WorkspaceService = require('./WorkspaceService');
    const metrics = WorkspaceService.getDashboardMetrics();
    const workloadMap = {};
    metrics.designerWorkload.forEach(dw => {
      workloadMap[dw.designer] = dw;
      if (dw.staffId) {
        workloadMap[dw.staffId] = dw;
      }
    });

    const allProjects = WorkspaceService.getAllProjects();

    const CATEGORY_SLA_MAP = {
      'D': { name: 'Graphic & Print Design', slaDays: 3, weight: 1.0, shortLabel: 'Graphic' },
      'S': { name: 'Social Media Content', slaDays: 2, weight: 0.7, shortLabel: 'Social' },
      'E': { name: 'E-Commerce', slaDays: 3, weight: 1.0, shortLabel: 'E-Com' },
      'W': { name: 'Web Design', slaDays: 5, weight: 1.5, shortLabel: 'Web' },
      'V': { name: 'Video Production', slaDays: 7, weight: 2.0, shortLabel: 'Video' },
      'P': { name: 'Brand Identity', slaDays: 10, weight: 2.5, shortLabel: 'Branding' }
    };

    function resolveCategoryConfig(presetType, presetCode) {
      if (presetCode && CATEGORY_SLA_MAP[presetCode.toUpperCase()]) {
        return CATEGORY_SLA_MAP[presetCode.toUpperCase()];
      }
      const typeStr = (presetType || '').toLowerCase();
      if (typeStr.includes('video') || typeStr.includes('motion')) return CATEGORY_SLA_MAP['V'];
      if (typeStr.includes('brand') || typeStr.includes('identity')) return CATEGORY_SLA_MAP['P'];
      if (typeStr.includes('web')) return CATEGORY_SLA_MAP['W'];
      if (typeStr.includes('social') || typeStr.includes('media')) return CATEGORY_SLA_MAP['S'];
      if (typeStr.includes('commerce') || typeStr.includes('e-com')) return CATEGORY_SLA_MAP['E'];
      return CATEGORY_SLA_MAP['D']; // Default 3 days / 1.0 slot
    }

    return roster.map(member => {
      const w = workloadMap[member.name] || workloadMap[member.staffId] || workloadMap[member.username] || {
        total: 0,
        active: 0,
        inProgress: 0,
        inReview: 0,
        revision: 0,
        overdue: 0,
        completed: 0
      };

      // Filter assigned projects for this designer
      const mName = (member.name || '').toLowerCase();
      const mStaff = (member.staffId || '').toLowerCase();
      const mUser = (member.username || '').toLowerCase();

      const memberProjects = allProjects.filter(p => {
        const d = (p.designer || '').toLowerCase();
        return d === mName || d === mStaff || d === mUser || (mName && d.includes(mName));
      }).map(p => {
        const catCfg = resolveCategoryConfig(p.presetType, p.presetCode);
        const subtasks = Array.isArray(p.subtasks) ? p.subtasks : [];
        let totalPts = catCfg.weight;
        if (subtasks.length > 0) {
          totalPts = subtasks.reduce((sum, st) => sum + (typeof st.weight === 'number' ? st.weight : 1.0), 0);
        } else if (typeof p.categoryWeight === 'number' && p.categoryWeight > 0) {
          totalPts = p.categoryWeight;
        }
        totalPts = Math.round(totalPts * 10) / 10;

        const completedSubtasksCount = subtasks.filter(st => {
          const s = (st.status || '').toLowerCase();
          return s === 'approved' || s === 'done' || s === 'completed';
        }).length;

        // Calculate relative deadline display matching desktop format e.g. "Due in 5d" or "Overdue 2d"
        let deadlineDisplay = '';
        if (p.deadline) {
          const clean = String(p.deadline).trim();
          const dt = new Date(clean);
          if (!isNaN(dt.getTime())) {
            const today = new Date();
            today.setHours(0, 0, 0, 0);
            const targetDt = new Date(dt);
            targetDt.setHours(0, 0, 0, 0);
            const diffDays = Math.ceil((targetDt.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
            const isCompleted = ['done', 'approved', 'completed'].includes((p.status || '').toLowerCase());
            if (isCompleted) {
              deadlineDisplay = targetDt.toISOString().substring(0, 10);
            } else if (diffDays < 0) {
              deadlineDisplay = `Overdue ${Math.abs(diffDays)}d`;
            } else if (diffDays === 0) {
              deadlineDisplay = 'Due Today';
            } else {
              deadlineDisplay = `Due in ${diffDays}d`;
            }
          } else {
            deadlineDisplay = clean.split('T')[0];
          }
        }

        return {
          id: p.id || p.jobId,
          jobId: p.jobId || p.id,
          title: p.title || 'Untitled Project',
          status: p.status || 'in-progress',
          brand: p.brand || 'SS',
          priority: p.priority || 'medium',
          deadline: p.deadline || null,
          deadlineDisplay,
          presetType: catCfg.name,
          presetCode: p.presetCode || 'D',
          slaDays: catCfg.slaDays,
          slotWeight: totalPts,
          totalWeight: totalPts,
          shortLabel: catCfg.shortLabel,
          subtasks,
          completedSubtasksCount,
          totalSubtasksCount: subtasks.length,
          subtaskProgressDisplay: subtasks.length > 0 
            ? `${completedSubtasksCount}/${subtasks.length} Done • ${totalPts} pts`
            : `${totalPts} pts`,
          categoryWeight: p.categoryWeight || null
        };
      });

      // Helper to identify active in-flight projects
      const isActiveStatus = (status) => {
        const s = (status || '').toLowerCase();
        return s === 'in-progress' || s === 'review' || s === 'revision';
      };

      // Calculate Category & Subtask-Weighted Active In-Flight Load
      // ONLY projects actively in-flight consume designer capacity: in-progress, review, revision
      let weightedLoad = 0;
      memberProjects.filter(p => isActiveStatus(p.status)).forEach(p => {
        let pWeight = (p.slotWeight || 1.0);
        if (Array.isArray(p.subtasks) && p.subtasks.length > 0) {
          const activeSubtasks = p.subtasks.filter(st => {
            const s = (st.status || 'in-progress').toLowerCase();
            return s !== 'approved' && s !== 'done' && s !== 'completed';
          });
          pWeight = activeSubtasks.reduce((sum, st) => sum + (typeof st.weight === 'number' ? st.weight : 1.0), 0);
        } else if (typeof p.categoryWeight === 'number' && p.categoryWeight > 0) {
          pWeight = p.categoryWeight;
        }
        weightedLoad += pWeight;
      });
      weightedLoad = Math.round(weightedLoad * 10) / 10;

      const activeCount = memberProjects.filter(p => isActiveStatus(p.status)).length;
      const backlogCount = memberProjects.filter(p => (p.status || '').toLowerCase() === 'backlog').length;

      // Studio Capacity scale (Max recommended studio bandwidth: 5.0 slot points)
      // 0 pts = Available (ready for assignment)
      // 0.1 - 3.5 pts = Normal (healthy active load)
      // 3.6 - 4.4 pts = High Workload (heavy workload)
      // 4.5 - 5.0 pts = At Capacity (maximum utilization)
      // > 5.0 pts OR >= 5 active projects = Overloaded (exceeds capacity bottleneck)
      let capacityPercent = Math.min(100, Math.round((weightedLoad / 5.0) * 100));
      let capacityStatus = 'Normal';
      let capacityColor = '#10B981'; // Green

      if (weightedLoad > 5.0 || (w.active && w.active >= 5) || activeCount >= 5) {
        capacityStatus = 'Overloaded';
        capacityColor = '#EF4444'; // Red
      } else if (weightedLoad >= 4.5) {
        capacityStatus = 'At Capacity';
        capacityColor = '#F97316'; // Orange
      } else if (weightedLoad >= 2.5 || (w.active && w.active >= 3) || activeCount >= 3) {
        capacityStatus = 'High Workload';
        capacityColor = '#F59E0B'; // Amber
      } else if (weightedLoad === 0 && (!w.active || w.active === 0) && activeCount === 0) {
        capacityStatus = 'Available';
        capacityColor = '#21A1F7'; // Azure
      }

      // Sort member projects: Active first (revision > in-progress > review), then backlog, then completed
      const STATUS_SORT_WEIGHT = {
        'revision': 1,
        'in-progress': 2,
        'review': 3,
        'backlog': 4,
        'on-hold': 5,
        'done': 6,
        'approved': 7,
        'cancelled': 8
      };

      const sortedProjects = [...memberProjects].sort((a, b) => {
        const rankA = STATUS_SORT_WEIGHT[(a.status || '').toLowerCase()] || 99;
        const rankB = STATUS_SORT_WEIGHT[(b.status || '').toLowerCase()] || 99;
        return rankA - rankB;
      });

      return {
        ...member,
        workload: {
          ...w,
          weightedLoad,
          capacityPercent,
          backlogCount
        },
        capacityStatus,
        capacityColor,
        activeCount,
        backlogCount,
        assignedProjects: sortedProjects.slice(0, 6),
        totalAssignedCount: memberProjects.length
      };
    });
  }
}

module.exports = TeamService;
