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
using NuGetUtils.Lib.Restore;
using NuGetUtils.Lib.Tool;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetUtils.MSBuild.Exec.Runner
{
   internal static class Program
   {
      public static Task<Int32> Main( String[] args )
         => new NuGetExecutingProgram().MainAsync( args, NuGetExecutingProgram.EXEC_ARGS_SEPARATOR );

   }

   internal sealed class NuGetExecutingProgram : NuGetRestoringProgram<NuGetExecutionConfigurationImpl, ConfigurationConfigurationImpl>
   {
      internal const String EXEC_ARGS_SEPARATOR = "--";

      protected override async Task<Int32> UseRestorerAsync(
         ConfigurationInformation<NuGetExecutionConfigurationImpl> info,
         CancellationToken token,
         BoundRestoreCommandUser restorer,
         String sdkPackageID,
         String sdkPackageVersion
         )
      {
         var config = info.Configuration;
         var maybeResult = await config.ExecuteMethodUsingRestorer(
            token,
            restorer,
            sdkPackageID,
            sdkPackageVersion,
            info.GetAdditonalTypeProvider( null )
#if NET46
            , null
#endif
            );

         throw new NotImplementedException();

      }

      protected override Boolean ValidateConfiguration(
         ConfigurationInformation<NuGetExecutionConfigurationImpl> info
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
