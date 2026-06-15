#!/bin/bash

echo "--- STEP 1: Restarting Docker (Full Rebuild) ---"
docker compose down

echo "--- STEP 2: Relaunch Docker Containers ---"
docker compose up -d

echo "--- STEP 3: Monitoring Logs ---"
docker compose logs -f app