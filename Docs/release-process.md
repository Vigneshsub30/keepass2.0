# KeePass Release Process

This document describes the end-to-end release process for the KeePass 2.x
(.NET 10 port).  It covers beta publishing, security review, stable promotion,
and emergency rollback.

---

## Overview

```
main
 └── Build CI (build.yml)
       └── Smoke test passes on all 5 matrix entries
             └── publish-beta.yml          ← automated on push to main
                   └── Beta pre-release
                         └── Security Gate (security-gate.yml) ← automated
                               └── Human sign-off (production environment) ← manual
                                     └── promote-to-stable.yml
                                           └── Stable GA release
```

---

## 1. Beta Publishing

Beta releases are created automatically by `publish-beta.yml` on every push
to the `main` branch after all CI checks pass.

**What the workflow does:**
1. Downloads the signed artifacts produced by `build.yml`.
2. Creates a GitHub pre-release tagged `beta-<short-sha>`.
3. Uploads all platform artifacts (Windows MSI, macOS DMG, Linux .deb,
   AppImage) plus SBOM files.

**Triggering manually (re-publish):**
```bash
gh workflow run publish-beta.yml \
  --ref main \
  -f force=true
```

---

## 2. Security Review Gate

Before any beta build can be promoted to stable, it must pass through two
sequential gates defined in `promote-to-stable.yml`.

### Gate 1 — Automated Security Pre-Checks

Implemented in `.github/workflows/security-gate.yml` (called as a reusable
workflow).  This gate cannot be bypassed by a human approver; it must pass
before the promotion job is eligible to run.

| Pre-check | Failure action |
|-----------|---------------|
| All smoke test jobs from the candidate build passed | Blocks promotion |
| `grype` SBOM scan — zero critical/high CVEs | Blocks promotion |
| GPG artifact signatures verify | Blocks promotion |

### Gate 2 — Human Security Sign-Off

After Gate 1 passes, the `promote` job waits for manual approval from a
reviewer configured in the **`production`** GitHub Actions environment.

**Required reviewer actions before approving:**
1. Complete `docs/security-review-checklist.md` for the candidate build.
2. Attach the completed checklist as a comment on the workflow run or the
   associated pull request.
3. Confirm no open critical findings in the Accepted Risks section.
4. Click **Approve** in the GitHub Actions environment review UI.

**Configuring reviewers (repository admin):**
```
GitHub → Settings → Environments → production → Required reviewers
```

---

## 3. Stable Promotion

Once both gates pass, trigger the promotion workflow:

```bash
gh workflow run promote-to-stable.yml \
  --ref main \
  -f beta_tag=beta-abc1234 \
  -f stable_tag=v2.62.0 \
  -f smoke_test_run_id=123456789 \
  -f release_notes="Stable release of KeePass 2.62.0."
```

**What the workflow does:**
1. Runs the automated security gate (Gate 1).
2. Waits for human approval (Gate 2).
3. Downloads all artifacts from the beta release.
4. Verifies SHA-256 checksums (if `SHA256SUMS` file present).
5. Creates a new stable GitHub release tagged `<stable_tag>`.
6. Annotates the beta release as promoted.
7. Emits an immutable audit summary in the workflow run.

**Immutable audit trail:**
The workflow summary records the approver, candidate tag, smoke test run ID,
and approval timestamp.  This record is retained as long as the workflow run
is stored in GitHub (90 days by default; configure under Settings → Actions).

---

## 4. Version Info Manifests

After stable promotion, update the in-app update checker manifests:

```powershell
# Generate stub manifests (must be RSA-signed externally before publishing)
./Build/rollback/update-version-info.ps1 `
  -Version "2.62.0" `
  -DownloadUrlBase "https://keepass.info/download/" `
  -OutputDir "./Build/version-info"
```

The generated `version2x.txt` and `version2x-beta.txt` stubs must be
signed with the KeePass RSA-4096 private key and uploaded to the
`keepass.info` CDN.

---

## 5. Emergency Rollback

If a stable release must be retracted (critical regression, security
vulnerability), use the rollback workflow:

```bash
gh workflow run rollback.yml \
  --ref main \
  -f bad_release_tag=v2.62.0 \
  -f target_release_tag=v2.61.1 \
  -f rollback_reason="CVE-XXXX-YYYYY: critical vulnerability in vault parser"
```

See the platform-specific runbooks for user-facing rollback steps:
- [`docs/runbooks/rollback-windows.md`](runbooks/rollback-windows.md)
- [`docs/runbooks/rollback-macos.md`](runbooks/rollback-macos.md)
- [`docs/runbooks/rollback-linux.md`](runbooks/rollback-linux.md)

---

## 6. Release Checklist Summary

| Step | Owner | Automated |
|------|-------|-----------|
| Build CI passes on all 5 matrix entries | CI | ✅ |
| Smoke test exits 0 on all platforms | CI | ✅ |
| Beta published to GitHub pre-releases | CI | ✅ |
| SBOM generated and attached to beta release | CI | ✅ |
| `security-review-checklist.md` completed | Security reviewer | ❌ |
| Automated security gate passes (grype, signatures) | CI | ✅ |
| Human sign-off in `production` environment | Security reviewer | ❌ |
| Stable release created from verified beta artifacts | CI | ✅ |
| Version-info manifests signed and published to CDN | Release engineer | ❌ |
| Smoke test re-run against stable release artifact | QA | ❌ |

---

## 7. Responsible Parties

| Role | Responsibility |
|------|---------------|
| **Release engineer** | Triggers `promote-to-stable.yml`; publishes version-info manifests |
| **Security reviewer** | Completes `security-review-checklist.md`; approves `production` environment |
| **QA** | Manual smoke test checklist (`docs/smoke-test-checklist.md`) against RC |
| **Maintainer** | Final authority; can veto promotion even after Gate 2 |
