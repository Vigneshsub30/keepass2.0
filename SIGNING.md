# Windows Authenticode Signing

KeePass.exe, KeePassLib.dll, and TrlUtil.exe are Authenticode-signed on every push
to `main` (and on version tags) using **Azure Trusted Signing** via OIDC federation.
The private key never leaves the Azure HSM; no certificate file is stored in the
repository or in CI secrets.

## How It Works

1. GitHub Actions authenticates to Azure using OIDC federation (no client secret).
2. `azure/trusted-signing-action` calls the Azure Trusted Signing service to sign
   each binary in place using the certificate profile stored in the signing account.
3. An RFC 3161 timestamp is embedded using `http://timestamp.acs.microsoft.com`
   so signatures remain valid after the certificate expires.
4. `signtool verify /pa` is run against each signed file to confirm the signature
   chain is valid before the artifact is uploaded.

## Azure Setup (One-time)

### 1 — Create an Azure Trusted Signing account

```bash
az extension add --name trustedsigning
az group create --name rg-keepass-signing --location eastus
az trustedsigning create \
  --name keepass-signing \
  --resource-group rg-keepass-signing \
  --location eastus \
  --sku Basic
az trustedsigning certificate-profile create \
  --account-name keepass-signing \
  --resource-group rg-keepass-signing \
  --profile-name KeePassPublicTrust \
  --profile-type PublicTrust
```

### 2 — Register a Microsoft Entra app and configure federated identity

```bash
# Create the app registration
APP_ID=$(az ad app create --display-name "KeePass CI Signing" --query appId -o tsv)
SP_ID=$(az ad sp create --id $APP_ID --query id -o tsv)

# Federated credential: allow GitHub Actions on the main branch
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-keepass-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:Vigneshsub30/keepass2.0:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Also allow version tags:
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-keepass-tags",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:Vigneshsub30/keepass2.0:ref:refs/tags/*",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

### 3 — Assign the Trusted Signing Certificate Profile Signer role

```bash
SIGNING_RESOURCE_ID=$(az trustedsigning show \
  --name keepass-signing \
  --resource-group rg-keepass-signing \
  --query id -o tsv)
az role assignment create \
  --role "Trusted Signing Certificate Profile Signer" \
  --assignee $SP_ID \
  --scope $SIGNING_RESOURCE_ID
```

### 4 — Configure GitHub repository secrets and variables

Add the following **Repository Secrets** in GitHub → Settings → Secrets and variables → Actions:

| Secret name | Value |
|---|---|
| `AZURE_CLIENT_ID` | App registration Client ID (from step 2) |
| `AZURE_TENANT_ID` | Azure AD Tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure Subscription ID |

Add the following **Repository Variable** to enable/disable signing:

| Variable name | Value |
|---|---|
| `SIGNING_ENABLED` | `true` (set to `false` to disable signing without removing the steps) |

## Verifying a Signed Binary

**Using PowerShell:**
```powershell
$sig = Get-AuthenticodeSignature .\KeePass.exe
$sig.Status          # should be "Valid"
$sig.SignerCertificate.Subject
```

**Using signtool.exe:**
```cmd
signtool verify /pa /v KeePass.exe
```

**Using gh CLI attestation:**
```bash
gh attestation verify KeePass.dll -R Vigneshsub30/keepass2.0
```

## Signing Infrastructure Reference

| Item | Value |
|---|---|
| Signing endpoint | `https://eus.codesigning.azure.net/` |
| Timestamp server | `http://timestamp.acs.microsoft.com` |
| Certificate profile | `KeePassPublicTrust` (Public Trust profile) |
| Signing action | `azure/trusted-signing-action@v0.4` |

---

# macOS Developer ID Signing and Notarization

macOS Gatekeeper requires every distributed application to be signed with a
**Developer ID Application** certificate and notarized by Apple.  This pipeline
uses `codesign`, `hdiutil`, and `xcrun notarytool` on the `macos-14` runner.

> **Prerequisite**: The Avalonia macOS head (`build/KeePass.app`) must exist
> before these steps execute.  Until WO-052 lands the steps are no-ops
> (skipped via the `bundle-check` output).

## How It Works

1. The Developer ID certificate (base64 .p12) is imported into a temporary
   keychain created for the CI job.
2. `codesign --deep --hardened-runtime --entitlements macOS/entitlements.plist`
   signs every binary inside the `.app` bundle.
3. `hdiutil create` produces a `.dmg` and `codesign` signs the disk image.
4. `xcrun notarytool submit --wait` submits the `.dmg` to Apple and blocks
   until the submission is approved (timeout 30 minutes).
5. `xcrun stapler staple` attaches the notarization ticket to the `.dmg` so
   it can be verified offline.
6. `spctl --assess` verifies the full chain.
7. The temporary keychain is deleted in a final `always()` cleanup step.

## Apple Setup (One-time)

### 1 — Export the Developer ID certificate

In Xcode → Settings → Accounts → Manage Certificates, export the
**Developer ID Application** certificate as a `.p12` file.

```bash
# Convert .p12 to base64 for storage in GitHub secrets
base64 -i developer_id.p12 | pbcopy   # copies to clipboard on macOS
```

### 2 — Generate an App Store Connect API key

1. Sign in to [App Store Connect](https://appstoreconnect.apple.com).
2. Users and Access → Keys → Generate API Key (role: Developer).
3. Download the `.p8` file — it can only be downloaded once.
4. Note the **Key ID** and **Issuer ID** from the Keys page.

### 3 — Configure GitHub repository secrets

Add the following **Repository Secrets**:

| Secret name | Value |
|---|---|
| `MACOS_DEVELOPER_ID_CERTIFICATE` | Base64-encoded `.p12` file |
| `MACOS_CERTIFICATE_PASSWORD` | `.p12` export password |
| `MACOS_KEYCHAIN_PASSWORD` | Any strong password for the temporary CI keychain |
| `APPLE_API_KEY_ID` | App Store Connect API Key ID |
| `APPLE_API_ISSUER_ID` | App Store Connect Issuer ID |
| `APPLE_API_KEY_CONTENT` | Contents of the `.p8` API key file |

Add the following **Repository Variable** to enable/disable signing:

| Variable name | Value |
|---|---|
| `MACOS_SIGNING_ENABLED` | `true` (set to `false` to disable without removing steps) |

## Verifying a Signed .dmg

```bash
# Verify the app bundle inside the mounted DMG
spctl --assess --type open --context context:primary-signature -v /Volumes/KeePass/KeePass.app

# Verify codesign
codesign --verify --deep --strict --verbose=4 /Volumes/KeePass/KeePass.app

# Check notarization ticket is stapled
xcrun stapler validate KeePass.dmg
```

---

# Linux GPG Signing and Checksum Generation

Linux artifacts are accompanied by a `SHA256SUMS` file and a detached GPG
signature `SHA256SUMS.sig`, enabling users and package managers to verify
artifact integrity before installation.

## How It Works

1. `sha256sum` generates a `SHA256SUMS` file listing the hash and filename of
   every Linux build artifact.
2. The GPG private key (stored as a GitHub Actions secret) is imported into a
   temporary keyring.
3. `gpg --detach-sign --armor` creates `SHA256SUMS.sig` — a detached ASCII-armored
   signature.
4. Both files are uploaded as `KeePass-linux-checksums` artifacts.
5. A cleanup step removes the key from the keyring unconditionally.

## GPG Key Setup (One-time)

```bash
# Generate a dedicated code-signing key (no expiry for CI use)
gpg --batch --gen-key <<EOF_KEY
%no-protection
Key-Type: RSA
Key-Length: 4096
Subkey-Type: RSA
Subkey-Length: 4096
Name-Real: KeePass Release Signing
Name-Email: releases@keepass.example.com
Expire-Date: 0
EOF_KEY

# Get the key fingerprint
gpg --list-secret-keys --keyid-format long

# Export the private key (base64-encode for GitHub secret)
gpg --export-secret-key --armor <FINGERPRINT> | base64

# Publish the public key (GitHub, keyserver, or website)
gpg --export --armor <FINGERPRINT> > keepass-release-signing.asc
```

## Configure GitHub Repository Secrets

| Secret name | Value |
|---|---|
| `LINUX_GPG_SIGNING_KEY` | Base64-encoded ASCII-armored GPG private key |
| `LINUX_GPG_KEY_ID` | GPG key fingerprint or email |
| `LINUX_GPG_PASSPHRASE` | Key passphrase (leave empty for unprotected key) |

Add the following **Repository Variable**:

| Variable name | Value |
|---|---|
| `LINUX_SIGNING_ENABLED` | `true` |

## Verifying Checksums and Signature

```bash
# Import the public key (substitute the actual key URL/file)
curl -sSL https://keepass.example.com/keepass-release-signing.asc | gpg --import

# Verify the GPG signature
gpg --verify SHA256SUMS.sig SHA256SUMS

# Verify file checksums
sha256sum --check SHA256SUMS
```
