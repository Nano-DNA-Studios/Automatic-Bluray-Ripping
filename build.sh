#!/bin/bash

# 1. Configuration
PROJECT_FILE="Automatic-Bluray-Ripping/Automatic-Bluray-Ripping.csproj"

echo "--- STEP 1: Deep Cleaning Project ---"
rm -rf Automatic-Bluray-Ripping/bin
rm -rf Automatic-Bluray-Ripping/obj

echo "--- STEP 2: Publishing for Linux-x64 ---"
dotnet publish "$PROJECT_FILE" -c Release -r linux-x64 --self-contained true

echo "--- STEP 3: Rebuilding Docker Containers ---"
docker compose build