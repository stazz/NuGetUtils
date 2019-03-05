#!/bin/sh

set -xe

SCRIPTPATH=$(readlink -f "$0")
SCRIPTDIR=$(dirname "$SCRIPTPATH")
GIT_ROOT=$(readlink -f "${SCRIPTDIR}/..")
BASE_ROOT=$(readlink -f "${GIT_ROOT}/..")

# Build everything in Source/Code directory first
"$@"

# Now build stuff in Source/NuGetUtils.MSBuild.Exec, so the results will be available in bin folder
find "${GIT_ROOT}/Source/NuGetUtils.MSBuild.Exec" \
  -mindepth 2 \
  -maxdepth 2 \
  -type f \
  -name '*.csproj' \
  -exec dotnet build -nologo /p:Configuration=Release /p:IsCIBuild=true "/p:CIPackageVersionSuffix=${GIT_COMMIT_HASH}" {} \;
