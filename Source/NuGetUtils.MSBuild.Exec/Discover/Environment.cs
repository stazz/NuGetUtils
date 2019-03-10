/*
 * Copyright 2019 Stanislav Muhametsin. All rights Reserved.
 *
 * Licensed  under the  Apache License,  Version 2.0  (the "License");
 * you may not use  this file  except in  compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed  under the  License is distributed on an "AS IS" BASIS,
 * WITHOUT  WARRANTIES OR CONDITIONS  OF ANY KIND, either  express  or
 * implied.
 *
 * See the License for the specific language governing permissions and
 * limitations under the License. 
 */
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Repositories;
using NuGet.Versioning;
using NuGetUtils.Lib.Restore;
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec.Discover
{
   internal sealed class EnvironmentInspector
   {
      private readonly String _packageID;
      private readonly String _packageVersion;
      private readonly Boolean _packageIDIsSelf;
      private readonly String _projectFileLocation;

      public EnvironmentInspector(
         String packageID,
         String packageVersion,
         Boolean packageIDIsSelf,
         String projectFileLocation
         )
      {
         this._packageID = packageID;
         this._packageVersion = packageVersion;
         this._packageIDIsSelf = packageIDIsSelf;
         this._projectFileLocation = projectFileLocation;
      }

      public EnvironmentInspectionResult InspectEnvironment(
         BoundRestoreCommandUser restorer,
         String sdkPackageID,
         String sdkPackageVErsion
         )
      {
         const String SELF = "self";

         var packageID = this._packageID;
         global::NuGet.Packaging.Core.PackageIdentity selfPackageIdentity = null;
         var projectFile = this._projectFileLocation;
         var packageIDIsSelf = this._packageIDIsSelf;
         var localRepos = restorer.LocalRepositories;
         var errors = new List<String>();
         if ( String.IsNullOrEmpty( packageID ) )
         {
            if ( packageIDIsSelf )
            {
               // Package ID was specified as self
               if ( String.IsNullOrEmpty( projectFile ) )
               {
                  errors.Add( "NMSBT003" );
               }
               else
               {
                  projectFile = Path.GetFullPath( projectFile );
                  // The usage of this task factory comes from the package itself, deduce the package ID
                  selfPackageIdentity = SearchWithinNuGetRepository(
                     projectFile,
                     localRepos
                        .FirstOrDefault( kvp => projectFile.StartsWith( Path.GetFullPath( kvp.Key ) ) )
                        .Value
                        ?.RepositoryRoot
                     );

                  if ( selfPackageIdentity == null )
                  {
                     // Failed to deduce this package ID
                     // No PackageID element and no PackageIDIsSelf element either
                     errors.Add( "NMSBT004" );
                  }
                  else
                  {
                     packageID = selfPackageIdentity.Id;
                  }


               }
            }
            else
            {
               // No PackageID element and no PackageIDIsSelf element either
               errors.Add( "NMSBT002" );
            }
         }
         else if ( packageIDIsSelf )
         {
            packageID = null;
            errors.Add( "NMSBT005" );
         }

         String packageVersion = null;
         if ( !String.IsNullOrEmpty( packageID ) )
         {

            packageVersion = this._packageVersion;
            if (
               ( String.IsNullOrEmpty( packageVersion ) && packageIDIsSelf )
               || String.Equals( packageVersion, SELF, StringComparison.OrdinalIgnoreCase )
               )
            {
               // Instead of floating version, we need to deduce our version
               NuGetVersion deducedVersion = null;
               if ( selfPackageIdentity == null )
               {
                  // <PackageID> was specified normally, and <PackageVersion> was self
                  var localPackage = localRepos.Values
                     .SelectMany( lr => lr.FindPackagesById( packageID ) )
                     .Where( lp => projectFile.StartsWith( lp.ExpandedPath ) )
                     .FirstOrDefault();
                  if ( localPackage == null )
                  {
                     errors.Add( "NMSBT006" );
                  }
                  else
                  {
                     deducedVersion = localPackage.Version;
                  }
               }
               else
               {
                  // <PackageIDIsSelf> was specified, and no version was specified
                  deducedVersion = selfPackageIdentity.Version;
               }

               packageVersion = deducedVersion?.ToNormalizedString();
            }
         }

         return new EnvironmentInspectionResult()
         {
            ThisFramework = restorer.ThisFramework.ToString(),
            ThisRuntimeID = restorer.RuntimeIdentifier,
            SDKPackageID = sdkPackageID,
            SDKPackageVersion = sdkPackageVErsion,
            PackageID = packageID,
            PackageVersion = packageVersion,
            Errors = errors.ToArray()
         };
      }

      private static PackageIdentity SearchWithinNuGetRepository(
         String projectFile,
         String localRepoRoot
         )
      {
         // Both V2 and V3 repositories have .nupkg file in its package root directory, so we search for that.
         // But if localRepoRoot is specified, then we assume it is for V3 repository, so we will also require .nuspec file to be present.

         var root = Path.GetPathRoot( projectFile );
         var nupkgFileFilter = "*" + PackagingCoreConstants.NupkgExtension;
         var nuspecFileFilter = "*" + PackagingConstants.ManifestExtension;

         var splitDirs = Path
            .GetDirectoryName( projectFile ).Substring( root.Length ) // Remove root
            .Split( new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries ) // Get all directory components
            .AggregateIntermediate_AfterAggregation( root, ( accumulated, item ) => Path.Combine( accumulated, item ) ); // Transform ["dir1", "dir2", "dir3"] into ["root:\dir1", "root:\dir1\dir2", "root:\dir1\dir2\dir3" ]
         if ( !String.IsNullOrEmpty( localRepoRoot ) )
         {
            // Only include subfolders of localRepoRoot
            localRepoRoot = Path.GetFullPath( localRepoRoot );
            var repoRootDirLength = localRepoRoot.Substring( root.Length ).Length;
            splitDirs = splitDirs.Where( dir => dir.Length > repoRootDirLength );
         }

         var nupkgFileInfo = splitDirs
            .Reverse() // Enumerate from innermost directory towards outermost directory
            .Select( curDir =>
            {
               var curDirInfo = new DirectoryInfo( curDir );
               var thisNupkgFileInfo = curDirInfo
                  .EnumerateFiles( nupkgFileFilter, SearchOption.TopDirectoryOnly )
                  .FirstOrDefault();

               FileInfo thisNuspecFileInfo = null;
               if (
                  thisNupkgFileInfo != null
                  && !String.IsNullOrEmpty( localRepoRoot )
                  && ( thisNuspecFileInfo = curDirInfo.EnumerateFiles( nuspecFileFilter, SearchOption.TopDirectoryOnly ).FirstOrDefault() ) == null
                  )
               {
                  // .nuspec file is not present, and we are within v3 repo root, so maybe this nupkg file is content... ?
                  thisNupkgFileInfo = null;
               }

               return (thisNupkgFileInfo, thisNuspecFileInfo);
            } )
            .FirstOrDefault( tuple =>
            {
               return tuple.thisNupkgFileInfo != null;
            } );

         PackageIdentity retVal;
         if ( nupkgFileInfo.thisNupkgFileInfo != null )
         {
            // See if we have nuspec information
            if ( nupkgFileInfo.thisNuspecFileInfo != null )
            {
               // We can read package identity from nuspec file
               retVal = new NuspecReader( nupkgFileInfo.thisNuspecFileInfo.FullName ).GetIdentity();
            }
            else
            {
               // Have to read package identity from nupkg file
               using ( var archiveReader = new PackageArchiveReader( nupkgFileInfo.thisNupkgFileInfo.FullName ) )
               {
                  retVal = archiveReader.GetIdentity();
               }
            }
         }
         else
         {
            retVal = null;
         }

         return retVal;
      }
   }
}
