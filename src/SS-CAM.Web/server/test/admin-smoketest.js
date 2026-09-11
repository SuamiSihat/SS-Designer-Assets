/**
 * SS-CAM Web Portal - Administration & Governance Automated Smoketest Suite
 * Art Director / Head of Creative Diagnostic & RBAC Integrity Verification
 */

const assert = require('assert');
const http = require('http');
const express = require('express');
const cors = require('cors');
const path = require('path');
const fs = require('fs');

const apiRoutes = require('../routes/api');
const { generateToken } = require('../middleware/auth');
const TeamService = require('../services/TeamService');
const CompanyService = require('../services/CompanyService');
const AuditService = require('../services/AuditService');
const WorkspaceService = require('../services/WorkspaceService');

console.log('🏛️  Starting SS-CAM Administration & Governance Smoketest...\n');

async function runAdminSmoketest() {
  let passed = 0;
  let failed = 0;

  // Setup ephemeral Express instance for end-to-end HTTP validation
  const app = express();
  app.use(cors());
  app.use(express.json());
  app.use('/api', apiRoutes);

  const server = await new Promise((resolve) => {
    const s = app.listen(0, '127.0.0.1', () => resolve(s));
  });

  const port = server.address().port;
  const baseUrl = `http://127.0.0.1:${port}/api`;

  // Generate valid Head of Creative Admin Bearer Token
  const headOfCreative = {
    staffId: 'SS0004',
    username: 'harussani',
    name: 'Harussani',
    role: 'Head of Creative, Admin',
    roles: ['Designer', 'Admin'],
    department: 'Creative Production'
  };
  const adminToken = generateToken(headOfCreative);
  const authHeaders = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${adminToken}`
  };

  async function test(name, fn) {
    try {
      await fn();
      console.log(`  ✅ PASS: ${name}`);
      passed++;
    } catch (err) {
      console.error(`  ❌ FAIL: ${name}`);
      console.error(`     Error: ${err.message}`);
      failed++;
    }
  }

  try {
    // ─── 1. Corporate Directory & Subsidiary Endpoints ─────────────────
    await test('GET /api/companies returns active subsidiary holding structure', async () => {
      const res = await fetch(`${baseUrl}/companies`);
      assert.strictEqual(res.status, 200, `Expected 200, got ${res.status}`);
      const data = await res.json();
      assert.strictEqual(data.success, true);
      assert.ok(Array.isArray(data.companies), 'data.companies should be an array');
      assert.ok(data.companies.length >= 5, `Expected >= 5 subsidiaries, got ${data.companies.length}`);

      const holding = data.companies.find(c => c.code === 'SSH');
      assert.ok(holding, 'Holding company SSH must exist');
      assert.strictEqual(holding.isParent, true, 'SSH must be marked as parent holding');

      const coreCodes = ['SSH', 'SSC', 'SSW', 'SSE', 'SST'];
      for (const code of coreCodes) {
        assert.ok(data.companies.some(c => c.code === code), `Subsidiary ${code} should be present in directory`);
      }
    });

    // ─── 2. Creative Team Roster & Harussani Role Verification ─────────
    await test('GET /api/team/roster recognizes Harussani as Head of Creative with Dual Tiers', async () => {
      const res = await fetch(`${baseUrl}/team/roster`);
      assert.strictEqual(res.status, 200, `Expected 200, got ${res.status}`);
      const data = await res.json();
      assert.ok(Array.isArray(data.roster), 'data.roster should be an array');

      const harussani = data.roster.find(u =>
        (u.staffId === 'SS0004') ||
        (u.username && u.username.toLowerCase() === 'harussani') ||
        (u.name && u.name.toLowerCase().includes('harussani'))
      );

      assert.ok(harussani, 'Harussani staff record (SS0004) must exist in directory');
      const roleStr = String(harussani.role || harussani.officialTitle || '');
      assert.ok(
        roleStr.toLowerCase().includes('head of creative') ||
        (Array.isArray(harussani.roles) && harussani.roles.some(r => r.toLowerCase().includes('head of creative'))),
        `Harussani role should reflect "Head of Creative", actual: "${roleStr}"`
      );

      // Verify creative recognition in TeamService
      assert.ok(
        TeamService.isCreativeOrAdminRole('Head of Creative'),
        'TeamService.isCreativeOrAdminRole must identify "Head of Creative" as an active creative role'
      );
    });

    // ─── 3. Staff Designation Update & Persistence ─────────────────────
    await test('PUT /api/users/SS0004 safely persists Official Job Title', async () => {
      const updatePayload = {
        name: 'Harussani',
        role: 'Head of Creative',
        officialTitle: 'Head of Creative',
        roles: ['Designer', 'Admin'],
        department: 'Creative Production',
        active: true
      };

      const res = await fetch(`${baseUrl}/users/SS0004`, {
        method: 'PUT',
        headers: authHeaders,
        body: JSON.stringify(updatePayload)
      });

      assert.strictEqual(res.status, 200, `Expected 200, got ${res.status}`);
      const data = await res.json();
      assert.strictEqual(data.success, true);
      assert.ok(data.user, 'Returned payload should contain updated user');
      assert.strictEqual(data.user.role, 'Head of Creative');
      assert.ok(Array.isArray(data.user.roles) && data.user.roles.includes('Admin'));
    });

    // ─── 4. Security Audit Trail Verification ──────────────────────────
    await test('GET /api/audit returns structured JSONL audit stream with authentication', async () => {
      // First, log a test event
      AuditService.logEvent({
        actor: 'Harussani (Head of Creative)',
        role: 'Admin',
        action: 'SMOKETEST_ADMIN_INSPECTION',
        entityType: 'System',
        entityId: 'ADMIN_PORTAL',
        details: { mode: 'minimalist-smoketest', status: 'verified' }
      });

      const res = await fetch(`${baseUrl}/audit?limit=20`, {
        headers: authHeaders
      });

      assert.strictEqual(res.status, 200, `Expected 200, got ${res.status}`);
      const data = await res.json();
      assert.ok(Array.isArray(data.logs), 'data.logs should be an array');
      assert.ok(data.logs.length > 0, 'Should return at least one audit log');

      const smoketestLog = data.logs.find(l => l.action === 'SMOKETEST_ADMIN_INSPECTION');
      assert.ok(smoketestLog, 'Newly created smoketest audit entry must be retrievable');
      assert.strictEqual(smoketestLog.role, 'Admin');
    });

    // ─── 5. System Health & Synology NAS Telemetry ─────────────────────
    await test('GET /api/system/status provides complete Synology NAS diagnostic metrics', async () => {
      const res = await fetch(`${baseUrl}/system/status`, {
        headers: authHeaders
      });

      assert.strictEqual(res.status, 200, `Expected 200, got ${res.status}`);
      const data = await res.json();
      assert.ok(data.workspaceRoot, 'workspaceRoot path should be present');
      assert.ok(typeof data.cachedProjects === 'number', 'cachedProjects count should be numeric');
      assert.ok(typeof data.synologyEngine === 'string', 'synologyEngine status indicator must be present');
      assert.ok(data.memory && data.memory.rss, 'Node.js memory RSS metrics should be reported');
    });

    // ─── 6. Workspace Candidate Discovery ──────────────────────────────
    await test('GET /api/system/workspace-candidates discovers accessible NAS mount paths', async () => {
      const res = await fetch(`${baseUrl}/system/workspace-candidates`, {
        headers: authHeaders
      });

      assert.strictEqual(res.status, 200, `Expected 200, got ${res.status}`);
      const data = await res.json();
      assert.ok(Array.isArray(data.candidates), 'candidates must be an array');
      assert.ok(data.candidates.length > 0, 'At least one workspace candidate must be discovered');
    });

    // ─── 7. Webhook Configuration Hub ──────────────────────────────────
    await test('GET /api/webhooks returns registered communication alert channels', async () => {
      const res = await fetch(`${baseUrl}/webhooks`, {
        headers: authHeaders
      });

      assert.strictEqual(res.status, 200, `Expected 200, got ${res.status}`);
      const data = await res.json();
      assert.strictEqual(data.success, true);
      assert.ok(Array.isArray(data.webhooks), 'webhooks must be an array');
    });

  } finally {
    server.close();
  }

  console.log('\n================================================================');
  console.log(`📊 Administration Smoketest Summary: ${passed} Passed, ${failed} Failed`);
  console.log('================================================================\n');

  if (failed > 0) {
    process.exit(1);
  }
  process.exit(0);
}

runAdminSmoketest().catch(err => {
  console.error('Fatal Smoketest Error:', err);
  process.exit(1);
});
