#!/bin/bash

# Config
TARGET_USER="mrdna"
TARGET_IP="192.168.100.76"
IMAGE_NAME="ghcr.io/nano-dna-studios/automatic-bluray-ripping"
TAG="dev-test"
TARGET_COMPOSE_DIR="~/Projects/ABR-Config"

echo "--- STEP 1: Deep Cleaning Project ---"
rm -rf Automatic-Bluray-Ripping/bin
rm -rf Automatic-Bluray-Ripping/obj

echo "--- STEP 2: Publishing for Linux-x64 ---"
dotnet publish "$PROJECT_FILE" -c Debug -r linux-x64 --self-contained true

echo "--- STEP 3: Building Local Docker Image ---"
docker build --build-arg BUILD_CONFIG="Debug" -t "${IMAGE_NAME}:${TAG}" .

echo "--- STEP 4: Transferring and Loading Image Directly ---"
docker save "${IMAGE_NAME}:${TAG}" | ssh "${TARGET_USER}@${TARGET_IP}" "docker load"

echo "--- STEP 5: Restarting Docker Compose on Target ---"
ssh "${TARGET_USER}@${TARGET_IP}" << EOF
  echo "--> Navigating to compose directory..."
  cd ${TARGET_COMPOSE_DIR}
  
  echo "--> Stopping old containers..."
  docker compose down
  
  echo "--> Starting new containers..."
  docker compose up -d --force-recreate
  
  echo "--> Done!"
EOF

echo "--- LOOP COMPLETE ---"
