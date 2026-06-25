#!/bin/bash
# deploy/update-image.sh
# Rebuilds the Docker image locally, pushes it to the registry, and
# triggers a new Container App revision to pull the updated image.
# Run after every code change that should go to production.

set -euo pipefail

RESOURCE_GROUP="rg-onetimeshare"
ACA_APP="onetimeshare"

# Must match the IMAGE value used in deploy-app.sh
IMAGE=""   # e.g. docker.io/yourusername/onetimeshare:latest

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "==> Building Docker image: ${IMAGE}"
docker build \
  --platform linux/amd64 \
  -t "${IMAGE}" \
  "${REPO_ROOT}"

echo "==> Pushing image to registry..."
docker push "${IMAGE}"

echo "==> Updating Container App to new image revision..."
az containerapp update \
  --name "$ACA_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --image "${IMAGE}"

echo "==> Update complete."
