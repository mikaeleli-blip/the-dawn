#!/usr/bin/env bash
# Headless playtest: runs the real game scripts outside Unity (needs Mono: apt install mono-mcs mono-devel).
# Usage: ./run.sh <seed> <days> [player|idle] [speed] [outdir]
set -e
cd "$(dirname "$0")"
mcs -langversion:7.2 -target:exe -warn:0 -out:playtest.exe -r:System.Core.dll UnityShim.cs Harness.cs $(find ../../Assets -name '*.cs')
mono playtest.exe "${1:-101}" "${2:-48}" "${3:-player}" "${4:-4}" "${5:-out/seed${1:-101}_${3:-player}}"
echo "Summary: ${5:-out/seed${1:-101}_${3:-player}}/summary.txt"
