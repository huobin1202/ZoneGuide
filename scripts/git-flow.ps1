#!/usr/bin/env pwsh
# ============================================================
# HeriStepAI - Git Flow Helper Script
# ============================================================
# Usage:
#   .\scripts\git-flow.ps1 feature start <name>    - Create feature branch
#   .\scripts\git-flow.ps1 feature finish <name>   - Create PR to develop
#   .\scripts\git-flow.ps1 release start <version>  - Create release branch
#   .\scripts\git-flow.ps1 release finish <version>  - Create PR to main
#   .\scripts\git-flow.ps1 hotfix start <name>      - Create hotfix from main
#   .\scripts\git-flow.ps1 hotfix finish <name>     - Create PR to main & develop
# ============================================================

param(
    [Parameter(Position=0)][string]$Type,       # feature, release, hotfix
    [Parameter(Position=1)][string]$Action,     # start, finish
    [Parameter(Position=2)][string]$Name        # branch name / version
)

$ErrorActionPreference = "Stop"

function Show-Help {
    Write-Host @"

HeriStepAI Git Flow Helper
==========================
Usage:
  .\scripts\git-flow.ps1 feature start <name>     Create feature/name from develop
  .\scripts\git-flow.ps1 feature finish <name>     Create PR: feature/name -> develop
  .\scripts\git-flow.ps1 release start <version>   Create release/version from develop
  .\scripts\git-flow.ps1 release finish <version>   Create PR: release/version -> main
  .\scripts\git-flow.ps1 hotfix start <name>       Create hotfix/name from main
  .\scripts\git-flow.ps1 hotfix finish <name>       Create PRs: hotfix/name -> main & develop

"@ -ForegroundColor Cyan
}

if (-not $Type -or -not $Action -or -not $Name) {
    Show-Help
    exit 0
}

switch ("$Type/$Action") {
    "feature/start" {
        Write-Host "Creating feature/$Name from develop..." -ForegroundColor Yellow
        git checkout develop
        git pull origin develop
        git checkout -b "feature/$Name"
        Write-Host "Branch feature/$Name created. Start coding!" -ForegroundColor Green
    }
    "feature/finish" {
        Write-Host "Finishing feature/$Name -> develop..." -ForegroundColor Yellow
        git push -u origin "feature/$Name"
        gh pr create --base develop --head "feature/$Name" --title "Feature: $Name" --body "Merge feature/$Name into develop"
        Write-Host "PR created! Review and merge on GitHub." -ForegroundColor Green
    }
    "release/start" {
        Write-Host "Creating release/$Name from develop..." -ForegroundColor Yellow
        git checkout develop
        git pull origin develop
        git checkout -b "release/$Name"
        Write-Host "Branch release/$Name created. Prepare for release!" -ForegroundColor Green
    }
    "release/finish" {
        Write-Host "Finishing release/$Name -> main..." -ForegroundColor Yellow
        git push -u origin "release/$Name"
        gh pr create --base main --head "release/$Name" --title "Release: $Name" --body "Release $Name - merge into main"
        Write-Host "PR created! Review and merge on GitHub." -ForegroundColor Green
    }
    "hotfix/start" {
        Write-Host "Creating hotfix/$Name from main..." -ForegroundColor Yellow
        git checkout main
        git pull origin main
        git checkout -b "hotfix/$Name"
        Write-Host "Branch hotfix/$Name created. Fix and commit!" -ForegroundColor Green
    }
    "hotfix/finish" {
        Write-Host "Finishing hotfix/$Name -> main & develop..." -ForegroundColor Yellow
        git push -u origin "hotfix/$Name"
        gh pr create --base main --head "hotfix/$Name" --title "Hotfix: $Name" --body "Hotfix $Name - merge into main"
        gh pr create --base develop --head "hotfix/$Name" --title "Hotfix: $Name (backport)" --body "Backport hotfix $Name into develop"
        Write-Host "PRs created for main and develop! Review and merge." -ForegroundColor Green
    }
    default {
        Write-Host "Unknown command: $Type $Action" -ForegroundColor Red
        Show-Help
    }
}
