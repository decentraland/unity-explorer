#!/bin/bash

# Post-build notarization script for Unity Build Automation
# Requires APP_SPECIFIC_PASSWORD environment variable to be set in the build target dashboard

APPLE_ID="developers@decentraland.org"
TEAM_ID="8T73XM973P"

echo "START"

if [[ -z "$APP_SPECIFIC_PASSWORD" ]]; then
    echo "APP_SPECIFIC_PASSWORD environment variable is not set"
    exit 1
fi

if [[ -z "$UNITY_PLAYER_PATH" ]]; then
    echo "UNITY_PLAYER_PATH environment variable is not set"
    exit 1
fi

APP_PATH="$UNITY_PLAYER_PATH"

echo "Submitting $APP_PATH for notarization..."

xcrun notarytool submit "$APP_PATH" \
    --apple-id "$APPLE_ID" \
    --password "$APP_SPECIFIC_PASSWORD" \
    --team-id "$TEAM_ID" \
    --wait

if [[ $? -eq 0 ]]; then
    echo "Notarization succeeded."
else
    echo "Notarization failed."
    exit 1
fi
