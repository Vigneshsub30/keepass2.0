# KeePass Security Review Checklist

**Template version:** 1.0
**Use for:** Every promotion from beta to stable (general availability).

A security reviewer must complete all sections below, check every item, and
document any accepted risks before approving the promotion workflow.
The completed checklist is attached as an immutable workflow artifact.

---

## Meta

| Field | Value |
|-------|-------|
| Reviewer | |
| Reviewer GitHub handle | |
| Candidate build SHA | |
| Candidate release tag | |
| Review date (UTC) | |
| Checklist version | 1.0 |

---

## 1. Cryptographic Validation

All cryptographic self-test vectors must pass in **Release** configuration on
every supported platform before this section can be checked.

| Check | Status |
|-------|--------|
| `SelfTest.Perform()` returns `SelfTest.ExpectedTestCount` (16) on Windows (x64) | ☐ PASS  ☐ FAIL  ☐ N/A |
| `SelfTest.Perform()` returns `SelfTest.ExpectedTestCount` (16) on Windows (arm64) | ☐ PASS  ☐ FAIL  ☐ N/A |
| `SelfTest.Perform()` returns `SelfTest.ExpectedTestCount` (16) on macOS (arm64) | ☐ PASS  ☐ FAIL  ☐ N/A |
| `SelfTest.Perform()` returns `SelfTest.ExpectedTestCount` (16) on macOS (x64) | ☐ PASS  ☐ FAIL  ☐ N/A |
| `SelfTest.Perform()` returns `SelfTest.ExpectedTestCount` (16) on Linux (x64) | ☐ PASS  ☐ FAIL  ☐ N/A |
| `--smoke-test` flag exits 0 on all five CI matrix entries | ☐ PASS  ☐ FAIL  ☐ N/A |
| KDBX round-trip verifier passes for KDBX 3.1, 4.0, and 4.1 fixtures | ☐ PASS  ☐ FAIL  ☐ N/A |
| AES, Salsa20, ChaCha20, SHA-256, BLAKE2b, Argon2d, Argon2id, HMAC-SHA-256, HOTP/TOTP vectors all pass in Release mode | ☐ PASS  ☐ FAIL  ☐ N/A |

**Notes:**

---

## 2. Dependency CVE Audit

The automated security-gate workflow runs `grype` against the CycloneDX SBOM
generated during the build.  Confirm the automated result, then review any
findings manually.

| Check | Status |
|-------|--------|
| SBOM artifact is present for this candidate build | ☐ PASS  ☐ FAIL |
| `grype` scan reports **zero** critical-severity CVEs | ☐ PASS  ☐ FAIL |
| `grype` scan reports **zero** high-severity CVEs, OR all high-severity findings are documented as accepted risks below | ☐ PASS  ☐ FAIL |
| All accepted-risk CVEs have a justification and a re-review date in Section 7 | ☐ PASS  ☐ N/A |

**Scan summary (paste `grype` output or link to artifact):**

```
(paste grype summary here)
```

**Notes:**

---

## 3. Plugin Trust Model Review

| Check | Status |
|-------|--------|
| `PluginMetadataInspector` uses `MetadataLoadContext` (no plugin code executes at inspection time) | ☐ PASS  ☐ FAIL |
| `PublisherKeyAllowList` enforces RSA-4096 signature verification when the allow-list is non-empty | ☐ PASS  ☐ FAIL |
| PLGX loading routes through the same `PluginManager.LoadPlugin` gate as DLL loading | ☐ PASS  ☐ FAIL |
| `AceSecurity.LockPluginPublisherAllowList` defaults to `false`; cannot be overridden by user config when set `true` in enforced config | ☐ PASS  ☐ FAIL |
| Plugin audit log (`PluginAuditLogger`) records load, reject, and unload events for this release | ☐ PASS  ☐ FAIL |

**Notes:**

---

## 4. Code Signing Verification

| Check | Status |
|-------|--------|
| Windows (x64) MSI and portable ZIP are Authenticode-signed with the production certificate | ☐ PASS  ☐ FAIL |
| Windows (arm64) MSI and portable ZIP are Authenticode-signed with the production certificate | ☐ PASS  ☐ FAIL |
| macOS (arm64) DMG is notarized and Developer ID Application–signed | ☐ PASS  ☐ FAIL |
| macOS (x64) DMG is notarized and Developer ID Application–signed | ☐ PASS  ☐ FAIL |
| Linux (x64) `.deb` and AppImage are GPG-signed with the release key | ☐ PASS  ☐ FAIL |
| The stable version-info file (`version2x.txt`) is signed with RSA-4096 private key | ☐ PASS  ☐ FAIL |
| Artifacts pass `sigcheck` (Windows) / `codesign --verify` (macOS) / `gpg --verify` (Linux) | ☐ PASS  ☐ FAIL |

**Notes:**

---

## 5. Platform Capability Gap Assessment

| Capability | Windows | macOS | Linux |
|------------|---------|-------|-------|
| Clipboard credential auto-clear | ☐ | ☐ | ☐ |
| Screen-capture protection (DRM/hidden window) | ☐ | ☐ | ☐ |
| Auto-type (global hot-key + window matching) | ☐ | ☐ | ☐ |
| Atomic vault save (TxF / rename fallback) | ☐ | ☐ | ☐ |
| Post-commit vault integrity check | ☐ | ☐ | ☐ |
| Session lock detection | ☐ | ☐ | ☐ |
| Single-instance enforcement | ☐ | ☐ | ☐ |

**Gap notes (document anything not checked above):**

---

## 6. Data Compatibility Verification

| Check | Status |
|-------|--------|
| KDBX 3.1 golden-file fixture opens without error on all platforms | ☐ PASS  ☐ FAIL |
| KDBX 4.0 golden-file fixture opens without error on all platforms | ☐ PASS  ☐ FAIL |
| KDBX 4.1 golden-file fixture opens without error on all platforms | ☐ PASS  ☐ FAIL |
| A vault created by this release can be opened by KeePass 2.61.1 (backward compatibility) | ☐ PASS  ☐ FAIL |
| KeePass 2.61.1 vault can be opened by this release without format upgrade prompt | ☐ PASS  ☐ FAIL |

**Notes:**

---

## 7. Accepted Risks

Document any findings or capability gaps that are accepted for this release.
Each entry must include:
- CVE/finding ID (or description if no CVE)
- Severity
- Justification for acceptance
- Mitigating controls in place
- Re-review date (must be before the next release)

| ID | Severity | Justification | Mitigating Controls | Re-Review Date |
|----|----------|---------------|---------------------|---------------|
| | | | | |

---

## 8. Sign-Off

I confirm that I have completed this checklist, reviewed all findings, and
documented all accepted risks.  I approve the promotion of the candidate build
to the stable (GA) release channel.

| Field | Value |
|-------|-------|
| Reviewer name | |
| Reviewer GitHub handle | |
| Approval timestamp (UTC) | |
| Checklist version | 1.0 |
| Override of automated check | ☐ None  ☐ Accepted Risk (see Section 7) |
