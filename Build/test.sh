#!/bin/sh

set -xe

SCRIPTPATH=$(readlink -f "$0")
SCRIPTDIR=$(dirname "$SCRIPTPATH")
GIT_ROOT=$(readlink -f "${SCRIPTDIR}/..")
BASE_ROOT=$(readlink -f "${GIT_ROOT}/..")

# Package the project used in tests via NuGet (no need to build it as it was built by custom build.sh script)
dotnet pack \
  -nologo \
  --no-build \
  -c Release \
  /p:IsCIBuild=true \
  "/p:CIPackageVersionSuffix=${GIT_COMMIT_HASH}" \
  "${GIT_ROOT}/Source/NuGetUtils.MSBuild.Exec/TestPackage"

# Now install the package
rm -rf /root/.nuget/packages/nugetutils.msbuild.exec.testpackage
LOCAL_TEMP_NUGET_SOURCE="${BASE_ROOT}/local_nuget_repo"
TEST_PACKAGE_PATH="${BASE_ROOT}/BuildTarget/Release/bin/NuGetUtils.MSBuild.Exec.TestPackage.1.0.0.nupkg"
mkdir "${LOCAL_TEMP_NUGET_SOURCE}"
dotnet nuget push \
  "${TEST_PACKAGE_PATH}" \
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

# Remember to remove the test package from output folder so it won't be included to artifacts
rm -rf "${TEST_PACKAGE_PATH}"
