#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/var/www/ecommerce-backend"
IMAGE_NAME="ecommerce_backend_image"
CONTAINER_NAME="ecommerce_backend_container"
HOST_PORT="5489"
CONTAINER_PORT="8080"
ENV_FILE=".env.deploy"

cd "$APP_DIR"

echo "==> Pulling latest code..."
git pull origin main

if [ ! -f "$ENV_FILE" ]; then
  echo ""
  echo "ERROR: $ENV_FILE not found!"
  echo "Create it on the server:"
  echo "  cp .env.deploy.example $ENV_FILE"
  echo "  nano $ENV_FILE"
  echo ""
  exit 1
fi

if ! grep -qE '^ConnectionStrings__AppDbContextConnection=.+[^[:space:]]' "$ENV_FILE"; then
  echo ""
  echo "ERROR: ConnectionStrings__AppDbContextConnection is missing or empty in $ENV_FILE"
  echo ""
  exit 1
fi

if ! grep -qE '^Jwt__Key=.{32,}' "$ENV_FILE"; then
  echo ""
  echo "WARNING: Jwt__Key missing or shorter than 32 chars in $ENV_FILE"
  echo "         (appsettings.json Jwt may be used if set in the image)"
  echo ""
fi

echo "==> Stopping old container (if any)..."
docker stop "$CONTAINER_NAME" 2>/dev/null || true
docker rm "$CONTAINER_NAME" 2>/dev/null || true

echo "==> Building Docker image..."
docker build -t "$IMAGE_NAME" .

echo "==> Starting container..."
docker run -d \
  --name "$CONTAINER_NAME" \
  --restart unless-stopped \
  --add-host=host.docker.internal:host-gateway \
  -p "${HOST_PORT}:${CONTAINER_PORT}" \
  --env-file "$ENV_FILE" \
  "$IMAGE_NAME"

echo "==> Waiting for app to start..."
sleep 4

if ! docker ps --filter "name=^/${CONTAINER_NAME}$" --filter "status=running" --format '{{.Names}}' | grep -q "$CONTAINER_NAME"; then
  echo ""
  echo "ERROR: Container exited immediately. Last logs:"
  echo "----------------------------------------"
  docker logs "$CONTAINER_NAME" 2>&1 || true
  echo "----------------------------------------"
  echo ""
  echo "Fix $ENV_FILE (DB connection, JWT, etc.) then run ./deploy.sh again."
  exit 1
fi

if curl -sf "http://127.0.0.1:${HOST_PORT}/api/health" >/dev/null; then
  echo "==> Health check OK (http://127.0.0.1:${HOST_PORT}/api/health)"
else
  echo ""
  echo "WARNING: Container is running but /api/health did not respond."
  echo "Last logs:"
  docker logs "$CONTAINER_NAME" --tail 40 2>&1 || true
fi

echo "==> Deploy done."
docker ps --filter "name=$CONTAINER_NAME"
echo "API: http://$(hostname -I | awk '{print $1}'):${HOST_PORT}"
