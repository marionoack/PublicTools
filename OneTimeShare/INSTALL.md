# OneTimeShare — Installation Manual

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Local Development Setup](#local-development-setup)
4. [First-Admin Bootstrap](#first-admin-bootstrap)
5. [Azure Resource Creation](#azure-resource-creation)
6. [Building and Pushing the Docker Image](#building-and-pushing-the-docker-image)
7. [Deployment to Azure Container Apps](#deployment-to-azure-container-apps)
8. [Custom Domain Setup](#custom-domain-setup)
9. [Backup and Restore](#backup-and-restore)
10. [Operational Maintenance](#operational-maintenance)
11. [Security Notes](#security-notes)

---

## 1. Overview

OneTimeShare is an ASP.NET Core 9 Razor Pages application that lets authenticated users share encrypted secrets and files via cryptic one-time URLs.

| Component | Technology |
|-----------|-----------|
| Web app | ASP.NET Core 9 Razor Pages |
| Auth | ASP.NET Core Identity (username/password) |
| Database | SQLite via Entity Framework Core |
| File storage | Azure Blob Storage (local filesystem in dev) |
| Encryption | AES-256-GCM with per-asset keys |
| Hosting | Azure Container Apps (scale-to-zero) |
| Persistence mount | Azure Files (SQLite + Data Protection keys) |

---

## 2. Prerequisites

### Local development

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Any text editor or Visual Studio 2022+

### Azure deployment

- **Docker Desktop** with a **Professional (or higher) license** — required to build and push the production image locally.
- Azure CLI ≥ 2.60 (`az --version`)
- Active Azure subscription with permission to create resource groups, storage accounts, and Container Apps.  
  **Azure Container Registry is not required and not used.**
- A container registry account to store the image. Docker Hub is the recommended option:
  - [Docker Hub](https://hub.docker.com/) — free public repos; private repos require a paid plan. A Professional Docker license already includes private repos.
  - [GitHub Container Registry (ghcr.io)](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry) — alternative if you host the code on GitHub.
- Bash shell (Linux, macOS, or WSL on Windows) to run the deployment scripts.
- **No Entra ID / Azure AD access required.**

---

## 3. Local Development Setup

```bash
# 1. Clone the repository
git clone <repo-url>
cd OneTimeShare

# 2. Restore packages
dotnet restore

# 3. Run the application (auto-creates SQLite DB and seeds first admin)
dotnet run
```

The app starts on `https://localhost:5001` and `http://localhost:5000`.

### Development configuration

Development settings live in `appsettings.Development.json` (not committed to git). A template:

```json
{
  "DetailedErrors": true,
  "Storage": { "Provider": "Local" },
  "SeedAdmin": {
    "Username": "admin",
    "Password": "Admin1234!",
    "DisplayName": "Administrator"
  },
  "Encryption": {
    "MasterKey": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="
  }
}
```

> ⚠️ The development `MasterKey` above is all-zeros and is fine for local testing only. **Never use it in production.**

In development, uploaded files are stored in `App_Data/blobs/` relative to the project root. This directory is excluded from git.

---

## 4. First-Admin Bootstrap

On the **first startup**, if no admin account exists in the database, the application reads the `SeedAdmin` configuration block and creates the admin user automatically.

| Config key | Environment variable | Description |
|---|---|---|
| `SeedAdmin:Username` | `SeedAdmin__Username` | Username for the first admin |
| `SeedAdmin:Password` | `SeedAdmin__Password` | Password (min 8 chars, requires upper, digit, symbol) |
| `SeedAdmin:DisplayName` | `SeedAdmin__DisplayName` | Display name shown in the navbar |

**These values are only used once** — if an admin already exists, they are ignored. After the first login, you can delete or change these environment variables.

To add more admins later: log in as admin → **Admin → Users → Add User** → check *Administrator*.

---

## 5. Azure Resource Creation

Run the setup script once:

```bash
cd deploy
# Edit the CONFIGURATION section at the top of the file
nano azure-setup.sh

bash azure-setup.sh
```

This script creates:

| Resource | Purpose |
|---|---|
| Resource group | Container for all resources |
| Storage account | Hosts both Azure Files and Blob Storage |
| Azure Files share `otsdata` | Mounted at `/data` — holds SQLite DB and Data Protection keys |
| Blob container `onetimeshare-assets` | Encrypted file/secret blobs |
| Container Apps environment | Runtime environment |

> **No Azure Container Registry is created.** The Docker image is built locally and pushed to an external registry (see next section).

The script prints the values you will need in `deploy-app.sh`. **Copy them.**

### Generating the master encryption key

```bash
openssl rand -base64 32
```

Store this value safely (e.g., password manager). **If you lose it, all encrypted assets become unreadable.**

---

## 6. Building and Pushing the Docker Image

The Docker image is built on your local machine with Docker Desktop and pushed to a container registry. Azure Container Apps will pull from that registry when starting containers.

### Option A — Docker Hub (recommended)

1. Log in to Docker Hub:

   ```bash
   docker login docker.io
   ```

2. Edit `deploy/build-push.sh` — set `REGISTRY_HOST=docker.io` and your `REGISTRY_USERNAME`.

3. Build and push:

   ```bash
   bash deploy/build-push.sh
   ```

   This produces an image tagged `docker.io/yourusername/onetimeshare:latest`.

   > If you want the image to be **private**, make sure the repository on Docker Hub is set to *Private* before the first push (Hub → Repositories → Create repository → Visibility: Private). With a Docker Professional license, private repositories are included.

### Option B — GitHub Container Registry (ghcr.io)

1. Create a GitHub Personal Access Token with `write:packages` scope.

2. Log in:

   ```bash
   echo "<YOUR_PAT>" | docker login ghcr.io -u <GITHUB_USERNAME> --password-stdin
   ```

3. Edit `deploy/build-push.sh` — set `REGISTRY_HOST=ghcr.io` and `REGISTRY_USERNAME=<GITHUB_USERNAME>`.

4. Build and push:

   ```bash
   bash deploy/build-push.sh
   ```

### Subsequent deployments

After every code change, run:

```bash
bash deploy/update-image.sh
```

This rebuilds the image locally, pushes it, and triggers a new Container App revision.

---

## 7. Deployment to Azure Container Apps

Fill in `deploy/deploy-app.sh` with the values from steps 5 and 6, then:

```bash
bash deploy/deploy-app.sh
```

The script:
1. Creates the Container App with scale-to-zero (`--min-replicas 0`) and a single-replica maximum.
2. Configures the registry credentials so Container Apps can pull the image.
3. Mounts the Azure Files share at `/data`.
4. Sets all required environment variables.
5. Prints the public HTTPS URL.

### Updating the application after code changes

```bash
bash deploy/update-image.sh
```

Edit `IMAGE` at the top of `update-image.sh` to match the full image reference used in `build-push.sh`.

### Required environment variables

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | `DataSource=/data/app.db;Cache=Shared` |
| `Storage__Provider` | `Azure` |
| `Storage__AzureBlobConnectionString` | Connection string for the storage account |
| `Storage__ContainerName` | `onetimeshare-assets` |
| `Encryption__MasterKey` | Base64-encoded 32-byte key |
| `SeedAdmin__Username` | First admin username (one-time) |
| `SeedAdmin__Password` | First admin password (one-time) |
| `SeedAdmin__DisplayName` | First admin display name (one-time) |

---

## 8. Custom Domain Setup

1. In the Azure Portal, open the Container App → **Custom domains** → **Add custom domain**.
2. Copy the CNAME/TXT verification values shown.
3. In your DNS provider, add:
   - A `CNAME` record: `share.yourdomain.com` → `<app>.azurecontainerapps.io`
   - The `TXT` verification record if required.
4. Azure will automatically provision and renew a managed TLS certificate.
5. After the domain is validated, the app is accessible at `https://share.yourdomain.com`.

---

## 9. Backup and Restore

### Database backup

The SQLite database is stored on the Azure Files share at `/data/app.db`.

```bash
# Download current DB
az storage file download \
  --account-name <STORAGE_ACCOUNT> \
  --share-name otsdata \
  --path app.db \
  --dest ./backup-$(date +%Y%m%d).db
```

### Database restore

```bash
# Scale app to zero first to avoid concurrent writes
az containerapp update --name onetimeshare --resource-group rg-onetimeshare --min-replicas 0 --max-replicas 0

# Upload restored DB
az storage file upload \
  --account-name <STORAGE_ACCOUNT> \
  --share-name otsdata \
  --source ./backup-YYYYMMDD.db \
  --path app.db

# Scale back up
az containerapp update --name onetimeshare --resource-group rg-onetimeshare --min-replicas 0 --max-replicas 1
```

### Blob data backup

Encrypted blobs are stored in the `onetimeshare-assets` container. Use AzCopy or the Azure Portal to download or replicate them:

```bash
azcopy copy "https://<STORAGE_ACCOUNT>.blob.core.windows.net/onetimeshare-assets" \
  "./blob-backup/" --recursive
```

> Blobs are encrypted with per-asset AES-256-GCM keys. Without the master key from `Encryption__MasterKey`, blobs cannot be decrypted even if downloaded.

---

## 10. Operational Maintenance

### Viewing logs

```bash
az containerapp logs show \
  --name onetimeshare \
  --resource-group rg-onetimeshare \
  --follow
```

### Downloading audit logs

Log in to the app as an administrator → **Admin → Audit Logs** → click **Download JSON**.

The JSON file contains all login, upload, download, and access-failure events.

### Rotating the master encryption key

**This requires re-encrypting all assets and is a destructive operation.** Proceed carefully:

1. Download all blobs and the database.
2. Decrypt each blob with the old key.
3. Re-encrypt each blob with the new key and update `EncryptedKeyData` in the database.
4. Update the `Encryption__MasterKey` environment variable.
5. Redeploy.

A key-rotation utility is not included; implement one if regular rotation is required.

### Disabling a user

Log in as admin → **Admin → Users** → **Edit** → check *Disabled* → Save.  
The user's existing shares remain accessible to public recipients but the user cannot log in.

### Deleting the application

```bash
az group delete --name rg-onetimeshare --yes --no-wait
```

This deletes all resources including the database and all blobs. There is no recovery.

---

## 11. Security Notes

- **Master key**: Store the `Encryption__MasterKey` value in a separate secrets manager (e.g., a password vault). It is the only key protecting all stored content.
- **Seed admin password**: Remove or blank `SeedAdmin__Password` from environment variables after the first admin logs in, or after creating a replacement admin account.
- **Single replica**: The app is intentionally limited to one Container App replica because SQLite does not support concurrent multi-writer access. Do not raise `max-replicas` above 1.
- **Scale to zero**: The app scales to zero when idle. The first request after a cold start takes ~5–15 seconds. This is expected behavior for cost-optimized hosting.
- **File size limit**: Upload limit is 5 MB, enforced at both the browser form and Kestrel level.
- **Share tokens**: Tokens are 24-character URL-safe Base64 strings from 18 cryptographically secure random bytes (~144 bits of entropy). They are not predictable or enumerable.
- **HTTPS only**: The application is deployed with HTTPS enforced by the Azure Container Apps ingress. The `UseHttpsRedirection` middleware further ensures all HTTP requests are redirected.
