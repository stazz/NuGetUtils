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
using NuGet.Common;
using NuGetUtils.Lib.Exec;
using NuGetUtils.Lib.Restore;
using NuGetUtils.Lib.Tool;
using NuGetUtils.Lib.Tool.Agnostic;
using NuGetUtils.MSBuild.Exec.Common;
using NuGetUtils.MSBuild.Exec.Common.NuGetDependant;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec.Perform
{
   using TConfiguration = PerformConfiguration<LogLevel>;

   internal static class Program
   {
      public static Task<Int32> Main( String[] args )
         => new NuGetProgram().MainAsync( args, NuGetProgram.EXEC_ARGS_SEPARATOR );

   }


   internal sealed class NuGetProgram : NuGetRestoringProgramWithShutdownCancellation<TConfiguration, DefaultConfigurationConfiguration>
   {
      internal const String EXEC_ARGS_SEPARATOR = "--";

      public NuGetProgram()
         : base( config => config.ShutdownSemaphoreName )
      {

      }

      protected override async Task<Int32> UseRestorerInParallelWithCancellationWatchingAsync(
         ConfigurationInformation<TConfiguration> info,
         CancellationToken token,
         BoundRestoreCommandUser restorer,
         String sdkPackageID,
         String sdkPackageVersion
         )
      {

         var config = info.Configuration;
         var specialValues = new Dictionary<Type, Func<Object>>()
         {
            { typeof(Func<String>), () => config.ProjectFilePath }
         };

         var maybeResult = await config.ExecuteMethodAndSerializeReturnValue(
               token,
               restorer,
               type =>
               {
                  return specialValues.TryGetValue( type, out var factory ) ?
                     factory() :
                     JsonConvert.DeserializeObject( config.InputProperties, type )
                     ;
               },
#if NET46
               null
#else
               sdkPackageID,
               sdkPackageVersion
#endif
               , getFiles: restorer.ThisFramework.CreateMSBuildExecGetFilesDelegate()
               );

         return maybeResult.IsFirst ? 0 : -4;

      }

      protected override Boolean ValidateConfiguration(
         ConfigurationInformation<TConfiguration> info
         )
      {
         return info.Configuration.ValidateConfiguration()
            && info.Configuration.ValidatePerformConfiguration();
      }

      protected override String GetDocumentation()
      {
         return "";
      }
   }
}
