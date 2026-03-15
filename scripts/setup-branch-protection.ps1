#!/usr/bin/env pwsh
# ============================================================
# ZoneGuide - Setup Branch Protection Rules
# ============================================================
# Prerequisites: gh auth login (GitHub CLI authenticated)
# Usage: .\scripts\setup-branch-protection.ps1
# ============================================================

$ErrorActionPreference = "Stop"
$repo = "huobin1202/ZoneGuide"

Write-Host "=== Setting up branch protection for $repo ===" -ForegroundColor Cyan

# Check if gh is authenticated
try {
    gh auth status 2>&1 | Out-Null
} catch {
    Write-Host "ERROR: Please login first: gh auth login" -ForegroundColor Red
    exit 1
}

# 1. Protect 'main' branch - require PR with at least 1 approval
Write-Host "`n[1/2] Protecting 'main' branch..." -ForegroundColor Yellow
gh api repos/$repo/branches/main/protection `
    --method PUT `
    --input - << 'EOF'
{
  "required_status_checks": null,
  "enforce_admins": false,
  "required_pull_request_reviews": {
    "required_approving_review_count": 1,
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": false
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
EOF

if ($LASTEXITCODE -eq 0) {
    Write-Host "  main branch protected (requires 1 PR approval)" -ForegroundColor Green
} else {
    Write-Host "  Failed to protect main. You may need admin access or a paid plan." -ForegroundColor Red
}

# 2. Protect 'develop' branch - allow direct push but no force push/delete
Write-Host "`n[2/2] Protecting 'develop' branch..." -ForegroundColor Yellow
gh api repos/$repo/branches/develop/protection `
    --method PUT `
    --input - << 'EOF'
{
  "required_status_checks": null,
  "enforce_admins": false,
  "required_pull_request_reviews": null,
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
EOF

if ($LASTEXITCODE -eq 0) {
    Write-Host "  develop branch protected (no force push/delete)" -ForegroundColor Green
} else {
    Write-Host "  Failed to protect develop. You may need admin access or a paid plan." -ForegroundColor Red
}

Write-Host "`n=== Branch protection setup complete ===" -ForegroundColor Cyan
Write-Host @"

Git Flow Workflow:
  1. Create feature branch:  git checkout -b feature/my-feature develop
  2. Work on feature, commit changes
  3. Push feature branch:    git push -u origin feature/my-feature
  4. Create PR:              gh pr create --base develop --title "Feature: ..."
  5. After review & merge to develop, create PR to main for release
  6. Delete feature branch:  git branch -d feature/my-feature

"@ -ForegroundColor Gray
