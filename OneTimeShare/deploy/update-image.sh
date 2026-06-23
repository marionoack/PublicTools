#!/bin/bash
# deploy/update-image.sh
# Rebuilds the Docker image and updates the Container App to the new image.
# Run after every code change that should go to production.

set -euo pipefail

RESOURCE_GROUP="rg-onetimeshare"
ACA_APP="onetimeshare"
ACR_NAME=""          # e.g. acronetimeshare12345

echo "==> Building and pushing new image via ACR..."
az acr build \
  --registry "$ACR_NAME" \
  --image "onetimeshare:latest" \
  ..

echo "==> Restarting Container App to pick up new image..."
az containerapp update \
  --name "$ACA_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --image "$(az acr show --name $ACR_NAME --query loginServer -o tsv)/onetimeshare:latest"

echo "==> Update complete."
