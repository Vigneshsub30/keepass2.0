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
