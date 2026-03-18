#!/usr/bin/env bash
# Run once after cloning to activate the project git hooks.
# Usage: bash .githooks/setup.sh

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
HOOKS_DIR="$REPO_ROOT/.githooks"

git config core.hooksPath "$HOOKS_DIR"

chmod +x "$HOOKS_DIR/commit-msg"
chmod +x "$HOOKS_DIR/pre-commit"

echo "Git hooks activated from $HOOKS_DIR"
