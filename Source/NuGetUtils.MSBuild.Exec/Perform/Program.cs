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
using NuGet.Common;
using NuGetUtils.Lib.Exec;
using NuGetUtils.Lib.Restore;
using NuGetUtils.Lib.Tool;
using NuGetUtils.Lib.Tool.Agnostic;
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetUtils.MSBuild.Exec.Perform
{
   internal static class Program
   {
      public static Task<Int32> Main( String[] args )
         => new NuGetProgram().MainAsync( args, NuGetProgram.EXEC_ARGS_SEPARATOR );

   }

   internal sealed class NuGetProgram : NuGetRestoringProgram<DefaultNuGetExecutionConfiguration<LogLevel>, DefaultConfigurationConfiguration>
   {
      internal const String EXEC_ARGS_SEPARATOR = "--";

      protected override async Task<Int32> UseRestorerAsync(
         ConfigurationInformation<DefaultNuGetExecutionConfiguration<LogLevel>> info,
         CancellationToken token,
         BoundRestoreCommandUser restorer,
         String sdkPackageID,
         String sdkPackageVersion
         )
      {
         var config = info.Configuration;
         var maybeResult = await config.ExecuteMethodAndSerializeReturnValue(
            token,
            restorer,
            info.GetAdditonalTypeProvider( null ),
#if NET46
            null
#else
            sdkPackageID,
            sdkPackageVersion
#endif
            );

         return maybeResult.IsFirst ? 0 : -3;

      }

      protected override Boolean ValidateConfiguration(
         ConfigurationInformation<DefaultNuGetExecutionConfiguration<LogLevel>> info
         )
      {
         return info.Configuration.ValidateConfiguration();
      }

      protected override String GetDocumentation()
      {
         return "";
      }
   }
}
