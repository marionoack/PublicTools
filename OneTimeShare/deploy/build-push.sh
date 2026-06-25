#!/bin/bash
# deploy/build-push.sh
# Builds the Docker image locally and pushes it to a container registry.
#
# Supported registries:
#   Docker Hub  : docker.io/<REGISTRY_USERNAME>/onetimeshare
#   GitHub GHCR : ghcr.io/<GITHUB_USER>/onetimeshare
#   Any other OCI-compatible registry
#
# Prerequisites:
#   - Docker Desktop with a Professional (or higher) license
#   - `docker login` completed for the target registry
#
# Usage:
#   bash deploy/build-push.sh

set -euo pipefail

###############################################
# CONFIGURATION – edit these before running  #
###############################################

# Registry host:
#   Docker Hub : docker.io
#   GitHub GHCR: ghcr.io
#   Other      : registry.example.com
REGISTRY_HOST="docker.io"

# Your registry username (Docker Hub username, GitHub username, etc.)
REGISTRY_USERNAME=""

IMAGE_NAME="onetimeshare"
IMAGE_TAG="latest"
FULL_IMAGE="${REGISTRY_HOST}/${REGISTRY_USERNAME}/${IMAGE_NAME}:${IMAGE_TAG}"

# Root of the repository (one level up from this script)
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "==> Building Docker image: ${FULL_IMAGE}"
docker build \
  --platform linux/amd64 \
  -t "${FULL_IMAGE}" \
  "${REPO_ROOT}"

echo "==> Pushing image to registry..."
docker push "${FULL_IMAGE}"

echo ""
echo "=== IMAGE PUSHED ==="
echo "Use this value for IMAGE in deploy/deploy-app.sh:"
echo "  IMAGE=${FULL_IMAGE}"
