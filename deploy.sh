#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/var/www/ecommerce-backend"
IMAGE_NAME="ecommerce_backend_image"
CONTAINER_NAME="ecommerce_backend_container"
HOST_PORT="5489"
CONTAINER_PORT="8080"

cd "$APP_DIR"

echo "==> Pulling latest code..."
git pull origin main

echo "==> Stopping old container (if any)..."
docker stop "$CONTAINER_NAME" 2>/dev/null || true
docker rm "$CONTAINER_NAME" 2>/dev/null || true

echo "==> Building Docker image..."
docker build -t "$IMAGE_NAME" .

echo "==> Starting container..."
# Optional: create .env.deploy on server for DB/JWT overrides (not in git)
ENV_FILE_ARGS=()
if [ -f .env.deploy ]; then
  ENV_FILE_ARGS=(--env-file .env.deploy)
fi

docker run -d \
  --name "$CONTAINER_NAME" \
  --restart unless-stopped \
  --add-host=host.docker.internal:host-gateway \
  -p "${HOST_PORT}:${CONTAINER_PORT}" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  "${ENV_FILE_ARGS[@]}" \
  "$IMAGE_NAME"

echo "==> Deploy done."
docker ps --filter "name=$CONTAINER_NAME"
echo "API: http://$(hostname -I | awk '{print $1}'):${HOST_PORT}"
