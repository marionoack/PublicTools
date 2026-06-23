#!/bin/bash
# deploy/deploy-app.sh
# Creates or updates the Container App with all required environment variables.
# Run after azure-setup.sh.  Re-run on every deployment to update the image.

set -euo pipefail

###############################################
# Fill in these values from azure-setup.sh   #
###############################################
RESOURCE_GROUP="rg-onetimeshare"
ACA_ENV="aca-env-onetimeshare"
ACA_APP="onetimeshare"
ACR_SERVER=""                     # e.g. acronetimeshare12345.azurecr.io
ACR_NAME=""                       # e.g. acronetimeshare12345
ACR_PASSWORD=""

BLOB_CONNECTION_STRING=""         # Azure Storage connection string

# Generate a 32-byte master key once and store it safely:
#   openssl rand -base64 32
MASTER_ENCRYPTION_KEY=""

# First admin (only used when no admin exists in DB)
SEED_ADMIN_USERNAME="admin"
SEED_ADMIN_PASSWORD=""            # Change this!
SEED_ADMIN_DISPLAYNAME="Administrator"

IMAGE="${ACR_SERVER}/onetimeshare:latest"

echo "==> Deploying Container App: $ACA_APP"

az containerapp create \
  --name "$ACA_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --environment "$ACA_ENV" \
  --image "$IMAGE" \
  --registry-server "$ACR_SERVER" \
  --registry-username "$ACR_NAME" \
  --registry-password "$ACR_PASSWORD" \
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
