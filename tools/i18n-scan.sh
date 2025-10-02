#!/usr/bin/env bash
set -euo pipefail

# Scan only Markdown (exclude *.sv.md). Purpose: enforce English-only docs by default.
scan_staged=false
if [[ "${1-}" == "--staged" ]]; then
  scan_staged=true
fi

files=()
if $scan_staged; then
  while IFS= read -r -d '' f; do
    files+=("$f")
  done < <(git diff --name-only --cached -z --diff-filter=ACM -- '*.md')
else
  while IFS= read -r -d '' f; do
    files+=("$f")
  done < <(git ls-files -z '*.md')
fi

bad=0
for f in "${files[@]}"; do
  [[ "$f" =~ \.sv\.md$ ]] && continue
  # Skip binary-marked files just in case
  if git check-attr --stdin --all < <(printf "%s\0" "$f") | grep -q 'binary: set'; then
    continue
  fi
  if grep -qP "[åäöÅÄÖ]" "$f"; then
    echo "❌ Found Swedish characters in $f"
    bad=1
  fi
done

if [[ $bad -ne 0 ]]; then
  echo "Commit blocked: English-only for Markdown; localized docs must end with .sv.md"
  exit 1
fi

echo "I18N check OK."