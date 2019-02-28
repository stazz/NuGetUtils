#!/bin/sh

set -xe

SCRIPTPATH=$(readlink -f "$0")
SCRIPTDIR=$(dirname "$SCRIPTPATH")
GIT_ROOT=$(readlink -f "${SCRIPTDIR}/..")
BASE_ROOT=$(readlink -f "${GIT_ROOT}/..")

if [[ "${RELATIVE_CS_OUTPUT}" ]]; then
  CS_OUTPUT=$(readlink -f "${BASE_ROOT}/${RELATIVE_CS_OUTPUT}")
fi

# Package the project used in tests via NuGet 
dotnet pack \
  -nologo \
  -c Release \
  /p:IsCIBuild=true \
  "/p:CIPackageVersionSuffix=${GIT_COMMIT_HASH}" \
  "${GIT_ROOT}/Source/NuGetUtils.MSBuild.Exec/TestPackage"

# Now install the package
LOCAL_TEMP_NUGET_SOURCE="${BASE_ROOT}/local_nuget_repo"
mkdir "${LOCAL_TEMP_NUGET_SOURCE}"
dotnet nuget push \
  "${BASE_ROOT}/BuildTarget/Release/bin/NuGetUtils.MSBuild.Exec.TestPackage.1.0.0.nupkg" \
  --source "${LOCAL_TEMP_NUGET_SOURCE}"

# Create required NuGet.config file
cat > "${BASE_ROOT}/NuGet.config" << EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <add key="Temp Local Source" value="${LOCAL_TEMP_NUGET_SOURCE}" />
    </packageSources>
</configuration>
EOF

# Now, run the tests.
"$@"