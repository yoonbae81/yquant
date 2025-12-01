#!/usr/bin/env sh

# Load environment variables from .env file into current shell
# Usage: source ./load-env.sh [.env.file]

env_file="${1:-.env.local}"

if [ -f "$env_file" ]; then
  printf "Loading environment variables from %s...\n" "$env_file"

  set -a
  . "$env_file"
  set +a

  while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in
      ''|*\#*) continue ;;
      *=*) key="${line%%=*}"
           key=$(printf '%s\n' "$key" | sed 's/[[:space:]]*$//;s/^[[:space:]]*//')
           printf "  ✓ %s\n" "$key" ;;
    esac
  done < "$env_file"

  printf "\nEnvironment variables loaded successfully.\n"
  printf "You can now run: dotnet run --project src/03.Applications/[YourApp]\n"
else
  printf "Error: %s not found!\n" "$env_file" >&2
  printf "Please create %s from .env.example template\n" "$env_file" >&2
  return 1 2>/dev/null || exit 1
fi
