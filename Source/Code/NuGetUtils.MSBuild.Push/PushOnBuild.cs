/*
 * Copyright 2017 Stanislav Muhametsin. All rights Reserved.
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
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol.Core.Types;
using NuGet.Repositories;
using NuGetUtils.Lib.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Push
{
   public static class PushTask
   {
      public static async Task Execute(
         Input input,
         Func<String> projectFileGetter,
         CancellationToken token
         )
      {
         var packageFilePath = input.PackageFilePath;
         if ( !packageFilePath.IsNullOrEmpty() )
         {
            var sourceNames = input.SourceNames;
            if ( !sourceNames.IsNullOrEmpty() )
            {
               var settings = NuGetUtility.GetNuGetSettingsWithDefaultRootDirectory(
                  Path.GetDirectoryName( projectFileGetter() ),
                  input.NuGetConfigurationFilePath );
               var psp = new PackageSourceProvider( settings );
               var packagePath = Path.GetFullPath( packageFilePath );

               var identity = new AsyncLazy<PackageIdentity>( async () =>
               {
                  using ( var reader = new PackageArchiveReader( packagePath ) )
                  {
                     return await reader.GetIdentityAsync( token );
                  }
               } );
               var allRepositories = new Lazy<NuGetv3LocalRepository[]>( () =>
                  SettingsUtility.GetGlobalPackagesFolder( settings )
                     .Singleton()
                     .Concat( SettingsUtility.GetFallbackPackageFolders( settings ) )
                     .Select( repoPath => new NuGetv3LocalRepository( repoPath ) )
                     .ToArray()
                  );

               await Task.WhenAll( sourceNames.Select( sourceItem => PerformPushToSingleSourceAsync(
                    settings,
                    packagePath,
                    psp,
                    identity,
                    allRepositories,
                    new TextWriterLogger()
                    {
                       VerbosityLevel = input.LogLevel
                    },
                    sourceItem,
                    input.RetryTimeoutForDirectoryDeletionFail,
                    token
                    ) )
                    .ToArray()
                  );
            }
            else
            {
               await Console.Error.WriteLineAsync( $"No sources specified for push command, please specify at least one source via \"{nameof( input.SourceNames )}\" property." );
            }
         }
         else
         {
            await Console.Error.WriteLineAsync( $"No package file path specified for push command, please specify it via \"{nameof( input.PackageFilePath )}\" property." );
         }
      }

      private static async Task PerformPushToSingleSourceAsync(
         ISettings settings,
         String packagePath,
         PackageSourceProvider psp,
         AsyncLazy<PackageIdentity> identity,
         Lazy<NuGetv3LocalRepository[]> allRepositories,
         NuGet.Common.ILogger logger,
         PushSourceInfo sourceItem,
         Int32 retryTimeoutForDirectoryDeletionFail,
         CancellationToken token
         )
      {
         var skipOverwrite = sourceItem.SkipOverwriteLocalFeed.ParseAsBooleanSafe();
         var skipClearRepositories = sourceItem.SkipClearingLocalRepositories.ParseAsBooleanSafe();
         var skipOfflineFeedOptimization = sourceItem.SkipOfflineFeedOptimization.ParseAsBooleanSafe();
         var apiKey = sourceItem.ApiKey;
         var symbolSource = sourceItem.SymbolSource;
         var symbolApiKey = sourceItem.SymbolApiKey;
         var noServiceEndPoint = sourceItem.NoServiceEndPoint.ParseAsBooleanSafe();

         var source = sourceItem.ItemSpec;
         var isLocal = IsLocalFeed( psp, source, out var localPath );
         if ( isLocal && !skipOverwrite )
         {
            await DeleteDirAsync( retryTimeoutForDirectoryDeletionFail, OfflineFeedUtility.GetPackageDirectory( await identity, localPath ), token );
         }

         if ( isLocal && !skipOfflineFeedOptimization )
         {
            // The default v2 repo detection algorithm for PushRunner (PackageUpdateResource.IsV2LocalRepository) always returns true for empty folders, so let's use the OfflineFeedUtility here right away (which will assume v3 repository)
            await OfflineFeedUtility.AddPackageToSource(
               new OfflineFeedAddContext(
                  packagePath,
                  localPath,
                  logger,
                  true,
                  false,
                  false,
                  new PackageExtractionContext(
                     PackageSaveMode.Defaultv3,
                     XmlDocFileSaveMode.None,
                     ClientPolicyContext.GetClientPolicy( settings, logger ),
                     logger
                     )
                  ),
               token
               );
         }
         else
         {
            var timeoutString = sourceItem.PushTimeout;
            if ( String.IsNullOrEmpty( timeoutString ) || !Int32.TryParse( timeoutString, out var timeout ) )
            {
               timeout = 1000;
            }

            try
            {
               await PushRunner.Run(
                  settings,
                  psp,
                  packagePath,
                  source,
                  apiKey,
                  symbolSource,
                  symbolApiKey,
                  timeout,
                  false,
                  String.IsNullOrEmpty( symbolSource ),
                  noServiceEndPoint,
                  logger
                  );
            }
            catch ( HttpRequestException e ) when
               ( e.Message.Contains( "already exists. The server is configured to not allow overwriting packages that already exist." ) )
            {
               // Nuget.Server returns this message when attempting to overwrite a package.
               await Console.Out.WriteLineAsync( $"Package already exists on source {source}, not updated." );
            }
         }

         if ( !skipClearRepositories )
         {
            var id = await identity;
            foreach ( var repo in allRepositories.Value )
            {
               await DeleteDirAsync( retryTimeoutForDirectoryDeletionFail, repo.PathResolver.GetInstallPath( id.Id, id.Version ), token );
            }
         }
      }

      private static async Task DeleteDirAsync(
         Int32 retryTimeout,
         String dir,
         CancellationToken token
         )
      {
         if ( Directory.Exists( dir ) )
         {
            // There are problems with using Directory.Delete( dir, true ); while other process has file watchers on it
            Exception error = null;
            try
            {
               Directory.Delete( dir, true );
            }
            catch ( Exception exc )
            {
               error = exc;

            }

            if ( error != null )
            {
               if ( retryTimeout > 0 )
               {
                  await Task.Delay( retryTimeout, token );

                  try
                  {
                     Directory.Delete( dir, true );
                     error = null;
                  }
                  catch
                  {
                     // Do not retry more times to avoid endless/slow loop

                  }
               }

               if ( error != null )
               {
                  await Console.Out.WriteLineAsync( $"Failed to delete directory {dir}: {error.Message}." );
               }
            }
         }
      }

      private static Boolean IsLocalFeed(
         PackageSourceProvider psp,
         String source,
         out String path
         )
      {
         PackageSource sourceSpec;
         if ( Path.IsPathRooted( source ) )
         {
            path = source;
         }
         else if ( ( sourceSpec = psp.LoadPackageSources().FirstOrDefault( s => String.Equals( s.Name, source ) ) )?.IsLocal ?? false )
         {
            path = sourceSpec.Source;
         }
         else
         {
            path = null;
         }

         return path != null;
      }

      public sealed class Input
      {
         public String PackageFilePath { get; set; }

         public PushSourceInfo[] SourceNames { get; set; }

         public NuGet.Common.LogLevel LogLevel { get; set; }

         public String NuGetConfigurationFilePath { get; set; }

         public Int32 RetryTimeoutForDirectoryDeletionFail { get; set; } = 500;
      }

      public sealed class PushSourceInfo
      {
         public String ItemSpec { get; set; }

         public String SkipOverwriteLocalFeed { get; set; } // Is really boolean
         public String SkipClearingLocalRepositories { get; set; } // Is really boolean
         public String SkipOfflineFeedOptimization { get; set; } // Is really boolean
         public String ApiKey { get; set; }
         public String SymbolSource { get; set; }
         public String SymbolApiKey { get; set; }
         public String NoServiceEndPoint { get; set; } // Is really boolean
         public String PushTimeout { get; set; } // Is really Int32
      }
   }
}
