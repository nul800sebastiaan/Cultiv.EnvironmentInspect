#!/bin/bash
set -e

# Merge Release Branch to Develop
# Merges the current release branch back to its corresponding develop branch
#
# Usage: ./merge-to-develop.sh <semver>
# Example: ./merge-to-develop.sh "1.0.0"

SEMVER="$1"

if [ -z "$SEMVER" ]; then
  echo "Error: Missing required argument"
  echo "Usage: $0 <semver>"
  exit 1
fi

# Extract version from release branch (e.g., release/v2 -> v2)
VERSION_SUFFIX=$(echo "${GITHUB_REF#refs/heads/release/}")
DEVELOP_BRANCH="develop/$VERSION_SUFFIX"

echo "Merging ${GITHUB_REF#refs/heads/} into $DEVELOP_BRANCH after release $SEMVER"

# Configure git
git config user.name "github-actions[bot]"
git config user.email "github-actions[bot]@users.noreply.github.com"

# Fetch the develop branch
git fetch origin "$DEVELOP_BRANCH"

# Checkout develop branch
git checkout "$DEVELOP_BRANCH"

# Merge release branch into develop
git merge --no-ff "origin/${GITHUB_REF#refs/heads/}" -m "chore: merge ${GITHUB_REF#refs/heads/} into $DEVELOP_BRANCH after release $SEMVER"

# Push changes
git push origin "$DEVELOP_BRANCH"

echo "Successfully merged release to develop branch"
