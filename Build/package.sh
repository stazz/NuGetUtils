#!/bin/sh

set -xe

# First, package all
"$@"

# Then, remove the NuGetUtils.MSBuild.Exec package
PACKAGE_PATH=$(find /repo-dir/BuildTarget/Release/bin/ -mindepth 1 -maxdepth 1 -type f -name 'NuGetUtils.MSBuild.Exec.*.nupkg')
if [[ ! -f "${PACKAGE_PATH}" ]]; then
  exit 1
fi

rm -f "${PACKAGE_PATH}"

# Build NuGet.MSBuild.Exec in special way - .NET Core will have facade library + dedicated DLL for various NuGet versions
dotnet build -nologo "/p:CIPackageVersionSuffix=${GIT_COMMIT_HASH}" "/repo-dir/contents/Source/Packaging/NuGetUtils.MSBuild.Exec.Packaging/NuGetUtils.MSBuild.Exec.Packaging.build"

# Now re-pack the NuGetUtils.MSBuild.Exec package
dotnet pack -nologo --no-build /p:Configuration=Release /p:IsCIBuild=true /p:TargetFramework=netcoreapp1.1 "/p:CIPackageVersionSuffix=${GIT_COMMIT_HASH}" "/repo-dir/contents/Source/Packaging/NuGetUtils.MSBuild.Exec.Packaging/NuGetUtils.MSBuild.Exec.Packaging.csproj"
