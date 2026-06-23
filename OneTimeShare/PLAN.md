# OneTimeShare implementation plan

## Goal

Build a small web application for rarely used one-time sharing of secrets and files. The application uses username/password authentication, no Entra ID dependency, cryptic share links, optional share passwords, optional expiry limits, and per-owner management of shared assets.

## Confirmed decisions

- Stack: ASP.NET Core Razor Pages.
- Authentication: local username/password accounts.
- Hosting preference: lowest idle cost is preferred, cold starts are acceptable.
- No Entra access should be required for application authentication or administration.
- Maximum file upload size: 5 MB.
- Deleted shares are hard-deleted, but audit log entries remain.
- Password-protected public shares reveal no asset metadata before successful password verification.
- Users can update only their display name and password.
- The first administrator is created from one-time deployment/startup variables.
- Audit logging must include login, upload, and download events.
- Administrators can download audit logs as JSON.

## Recommended Azure architecture

- Azure Container Apps Consumption for the ASP.NET Core app, configured with scale-to-zero and a single replica.
- Azure Storage Account:
  - Blob Storage for encrypted uploaded files and secret payloads.
  - Azure Files mounted into the container for the SQLite database and Data Protection keys.
- SQLite with Entity Framework Core for users, metadata, counters, and audit records.
- App configuration/secrets via Container Apps environment variables/secrets.
- Custom subdomain bound to the Container App with managed TLS.

This keeps idle cost low while avoiding Entra-based login. The single-replica constraint is intentional because SQLite is not a multi-writer database.

## Security model

- ASP.NET Core Identity with cookie authentication.
- Passwords hashed by ASP.NET Core Identity password hasher.
- First administrator bootstrapped from deployment configuration on first startup.
- All share IDs generated with cryptographically secure random bytes and encoded URL-safely.
- Uploaded files and secret payloads encrypted by the application before storage in Blob Storage.
- A master encryption key is supplied via application configuration, not committed to source.
- Optional share password is hashed and verified server-side; the clear text password is never stored.
- Password-protected public share pages do not show filename, secret title, expiry, or other metadata before password verification.
- Download/view limits and expiry dates are enforced before returning asset content.
- Asset access increments counters atomically before or during successful retrieval.
- Admin functions require an administrator role; normal users can only manage their own profile and assets.

## Core user flows

1. Unauthenticated users see the login page as the start screen.
2. Authenticated users see their own shared assets, including type, creation date, expiry, remaining uses, and download/view counter.
3. Users can create a new secret or file share with:
   - Maximum file upload size of 5 MB.
   - Optional share password.
   - Optional maximum download/view count.
   - Optional expiry date/time.
4. Users can delete their own shares.
5. Users can update their own display name and password.
6. Administrators can create, update, remove users, assign admin status, and reset user passwords.
7. Administrators can download audit logs as a JSON file.
8. Public recipients can open a cryptic URL, optionally enter the share password, and fetch the secret/file if limits allow.

## Data model draft

- `ApplicationUser`
  - ASP.NET Core Identity user fields.
  - Display name.
  - Created/updated timestamps.
  - Disabled flag.
- `SharedAsset`
  - Owner user ID.
  - Public token.
  - Asset type: secret or file.
  - Display name.
  - Blob name.
  - Content type.
  - Encrypted content key metadata.
  - Optional share password hash.
  - Current access count.
  - Optional max access count.
  - Optional expiry timestamp.
  - Created/updated timestamps.
  - Deleted timestamp or hard-delete behavior.
- `AssetAccessLog`
  - Asset ID.
  - User ID where applicable.
  - Timestamp.
  - Event type: login, upload, download, share deleted, expired, limit reached, invalid password, not found.
  - Result: success or failure.
  - Remote IP hash or minimal diagnostic metadata, if needed.

## Implementation phases

1. Create the ASP.NET Core Razor Pages project with Identity, EF Core SQLite, validation, and base styling.
2. Add database schema, migrations, first-admin bootstrap, and local configuration templates.
3. Implement authenticated dashboard, profile update, password change, and admin user management.
4. Implement asset creation for secrets and file uploads.
5. Implement encryption, Blob Storage persistence, and asset deletion cleanup.
6. Implement public share URLs, password prompt, expiry/max-access enforcement, and counter updates.
7. Implement audit logging for login, upload, download, delete, failed share access, and admin JSON export.
8. Add deployment artifacts for Azure Container Apps, Storage Account, Azure Files mount, Blob container, secrets, and custom domain notes.
9. Create an installation manual covering local setup, Azure resource creation, deployment, one-time admin bootstrap variables, custom domain setup, backup/restore, and operational maintenance.
10. Add tests for authentication authorization rules, share limit enforcement, token generation, password-protected shares, expiry handling, upload size limits, hard-delete behavior, and audit log export.

## Resolved questions

- Maximum file upload size is 5 MB.
- Deleted shares are hard-deleted immediately, while logs remain.
- Password-protected shares show no metadata before password verification.
- Users can only change display name and password.
- First administrator is created from one-time variables.
- Logging covers login, upload, and download; administrators can export logs as JSON.
