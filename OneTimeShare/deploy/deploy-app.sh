#!/bin/bash
# deploy/deploy-app.sh
# Creates or updates the Container App with all required environment variables.
# The Docker image is pulled from Docker Hub (or another registry) – no ACR needed.
# Run after azure-setup.sh and build-push.sh.
# Re-run to update environment variables or to redeploy after an image change.

set -euo pipefail

###############################################
# Fill in these values                        #
###############################################
RESOURCE_GROUP="rg-onetimeshare"
ACA_ENV="aca-env-onetimeshare"
ACA_APP="onetimeshare"

# Full image reference built by build-push.sh, e.g.:
#   docker.io/yourusername/onetimeshare:latest
#   ghcr.io/yourusername/onetimeshare:latest
IMAGE=""

# Registry credentials so Container Apps can pull the image.
# Docker Hub: your Docker Hub username and a Personal Access Token
#   (Hub → Account Settings → Personal access tokens → New token, scope: Read)
# GitHub GHCR: your GitHub username and a PAT with read:packages scope
REGISTRY_SERVER=""      # e.g. docker.io  or  ghcr.io
REGISTRY_USERNAME=""
REGISTRY_PASSWORD=""    # PAT / access token – never a plain password

BLOB_CONNECTION_STRING=""   # from azure-setup.sh

# Generate a 32-byte master key once and store it safely:
#   openssl rand -base64 32
MASTER_ENCRYPTION_KEY=""

# First admin – only used when no admin exists in the DB
SEED_ADMIN_USERNAME="admin"
SEED_ADMIN_PASSWORD=""      # Change this!
SEED_ADMIN_DISPLAYNAME="Administrator"

###############################################

echo "==> Deploying Container App: $ACA_APP"

az containerapp create \
  --name "$ACA_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --environment "$ACA_ENV" \
  --image "$IMAGE" \
  --registry-server "$REGISTRY_SERVER" \
  --registry-username "$REGISTRY_USERNAME" \
  --registry-password "$REGISTRY_PASSWORD" \
  --ingress external \
  --target-port 8080 \
  --min-replicas 0 \
  --max-replicas 1 \
  --scale-rule-name "http-rule" \
  --scale-rule-type http \
  --scale-rule-http-concurrency 10 \
  --volume-mount "volumeName=otsdata,mountPath=/data" \
  --volume "name=otsdata,storageType=AzureFile,storageName=otsdata" \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=DataSource=/data/app.db;Cache=Shared" \
    "Storage__Provider=Azure" \
    "Storage__AzureBlobConnectionString=${BLOB_CONNECTION_STRING}" \
    "Storage__ContainerName=onetimeshare-assets" \
    "Encryption__MasterKey=${MASTER_ENCRYPTION_KEY}" \
    "SeedAdmin__Username=${SEED_ADMIN_USERNAME}" \
    "SeedAdmin__Password=${SEED_ADMIN_PASSWORD}" \
    "SeedAdmin__DisplayName=${SEED_ADMIN_DISPLAYNAME}"

echo "==> Deployment done."
FQDN=$(az containerapp show \
  --name "$ACA_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.configuration.ingress.fqdn" -o tsv)
echo "App URL: https://$FQDN"
