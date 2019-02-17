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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGetUtils.Lib.Exec;
using NuGetUtils.Lib.Restore;
using NuGetUtils.Lib.Tool;
using NuGetUtils.Lib.Tool.Agnostic;
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec.Discover
{
   internal static class Program
   {
      public static Task<Int32> Main( String[] args )
         => new NuGetProgram().MainAsync( args, NuGetProgram.EXEC_ARGS_SEPARATOR );

   }

   internal sealed class NuGetProgram : NuGetRestoringProgram<DiscoverConfiguration, DefaultConfigurationConfiguration>
   {
      internal const String EXEC_ARGS_SEPARATOR = "--";

      protected override Boolean ValidateConfiguration(
         ConfigurationInformation<DiscoverConfiguration> info
         )
      {
         return info.Configuration.ValidateDiscoverConfiguration()
            && ( info.Configuration.PackageIDIsSelf || info.Configuration.ValidateConfiguration() );
      }

      protected override async Task<Int32> UseRestorerAsync(
         ConfigurationInformation<DiscoverConfiguration> info,
         CancellationToken token,
         BoundRestoreCommandUser restorer,
         String sdkPackageID,
         String sdkPackageVersion
         )
      {
         var config = info.Configuration;
         await WriteEnvironmentInformationToFileAsync(
            config.DiscoverFilePath,
            new EnvironmentInspector(
               config.PackageID,
               config.PackageVersion,
               config.PackageIDIsSelf,
               config.ProjectFilePath
               ).InspectEnvironment( restorer, sdkPackageID, sdkPackageVersion )
            );

         return 0;
      }

      protected override String GetDocumentation()
      {
         return "";
      }

      private static async Task WriteEnvironmentInformationToFileAsync(
         String filePath,
         EnvironmentInspectionResult environment
         )
      {
         using ( var writer = filePath.OpenStreamWriter() )
         {
            await writer.WriteAsync( JsonConvert.SerializeObject( environment ) );
         }

      }
   }
}