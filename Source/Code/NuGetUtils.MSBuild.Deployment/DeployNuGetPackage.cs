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
using NuGet.Common;
using NuGetUtils.Lib.Common;
using NuGetUtils.Lib.Deployment;
using NuGetUtils.Lib.Restore;
using NuGetUtils.Lib.Restore.Agnostic;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Deployment
{
   public static class DeployNuGetPackageTask
   {
      public static async Task<Output> Execute(
         Input input,
         CancellationToken token
         )
      {
         Output output = null;
         if ( !String.IsNullOrEmpty( input.PackageID ) )
         {
            (var epAssembly, var targetFW) = await input.CreateAndUseRestorerAsync(
               typeof( DeployNuGetPackageTask ),
               input.LockFileCacheDirEnvName,
               input.LockFileCacheDirWithinHomeDir,
               restorer => input.DeployAsync( restorer.Restorer, token, restorer.SDKPackageID, restorer.SDKPackageVersion ),
               () => new TextWriterLogger()
               {
                  VerbosityLevel = input.LogLevel
               }
               );

            if ( !String.IsNullOrEmpty( epAssembly )
               && File.Exists( epAssembly ) )
            {
               output = new Output( epAssembly );
            }
         }
         else
         {
            await Console.Error.WriteLineAsync( $"Please specify at least {nameof( input.PackageID )} input property." );
         }
         return output ?? new Output( null );
      }



      public sealed class Input : NuGetDeploymentConfiguration, NuGetUsageConfiguration<LogLevel>
      {
         public String NuGetConfigurationFile { get; set; }

         public String RestoreFramework { get; set; }

         public String RestoreRuntimeID { get; set; }

         public String LockFileCacheDirectory { get; set; }

         public String SDKFrameworkPackageID { get; set; }

         public String SDKFrameworkPackageVersion { get; set; }

         public Boolean DisableLockFileCache { get; set; }

         public LogLevel LogLevel { get; set; }

         public Boolean DisableLogging { get; set; }

         public String PackageID { get; set; }

         public String PackageVersion { get; set; }

         public String AssemblyPath { get; set; }

         public String PackageSDKFrameworkPackageID { get; set; }

         public String PackageSDKFrameworkPackageVersion { get; set; }

         public DeploymentKind DeploymentKind { get; set; }

         public Boolean? PackageFrameworkIsPackageBased { get; set; }

         public String TargetDirectory { get; set; }

         public String LockFileCacheDirEnvName { get; set; }
         public String LockFileCacheDirWithinHomeDir { get; set; }
      }

      public sealed class Output
      {
         public Output( String epAssemblyPath )
         {
            this.EntryPointAssemblyPath = epAssemblyPath;
         }

         public String EntryPointAssemblyPath { get; }
      }
   }
}
