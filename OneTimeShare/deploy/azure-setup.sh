#!/bin/bash
# deploy/azure-setup.sh
# Run this ONCE to provision all Azure resources.
# Azure Container Registry is NOT used. The Docker image is built locally
# and pushed to Docker Hub (or another OCI-compatible registry).
# Prerequisites: Azure CLI logged in, correct subscription selected.
# Usage: bash azure-setup.sh

set -euo pipefail

###############################################
# CONFIGURATION – edit these before running  #
###############################################
RESOURCE_GROUP="rg-onetimeshare"
LOCATION="westeurope"
STORAGE_ACCOUNT="stonetimeshare$RANDOM"   # must be globally unique
FILE_SHARE_NAME="otsdata"
BLOB_CONTAINER="onetimeshare-assets"
ACA_ENV="aca-env-onetimeshare"

echo "==> Creating resource group..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

echo "==> Creating storage account..."
az storage account create \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku Standard_LRS \
  --kind StorageV2

STORAGE_KEY=$(az storage account keys list \
  --account-name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[0].value" -o tsv)

BLOB_CONNECTION_STRING=$(az storage account show-connection-string \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query connectionString -o tsv)

echo "==> Creating Azure Files share for persistent data..."
az storage share create \
  --name "$FILE_SHARE_NAME" \
  --account-name "$STORAGE_ACCOUNT" \
  --account-key "$STORAGE_KEY"

echo "==> Creating Blob container for encrypted assets..."
az storage container create \
  --name "$BLOB_CONTAINER" \
  --account-name "$STORAGE_ACCOUNT" \
  --account-key "$STORAGE_KEY"

echo "==> Creating Container Apps environment..."
az containerapp env create \
  --name "$ACA_ENV" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION"

echo "==> Mounting Azure Files in Container Apps environment..."
az containerapp env storage set \
  --name "$ACA_ENV" \
  --resource-group "$RESOURCE_GROUP" \
  --storage-name "otsdata" \
  --account-name "$STORAGE_ACCOUNT" \
  --azure-file-account-key "$STORAGE_KEY" \
  --azure-file-share-name "$FILE_SHARE_NAME" \
  --access-mode ReadWrite

echo ""
echo "=== SETUP COMPLETE ==="
echo "Now build and push the Docker image:"
echo "  bash deploy/build-push.sh"
echo ""
echo "Then fill in deploy/deploy-app.sh with these values:"
echo "  STORAGE_ACCOUNT=$STORAGE_ACCOUNT"
echo "  BLOB_CONNECTION_STRING=$BLOB_CONNECTION_STRING"
echo "  ACA_ENV=$ACA_ENV"
echo "  RESOURCE_GROUP=$RESOURCE_GROUP"
