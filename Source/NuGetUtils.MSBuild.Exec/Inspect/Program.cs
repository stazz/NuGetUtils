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
using NuGet.Versioning;
using NuGetUtils.Lib.Exec;
using NuGetUtils.Lib.Restore;
using NuGetUtils.Lib.Tool;
using NuGetUtils.Lib.Tool.Agnostic;
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec.Inspect
{
   internal static class Program
   {
      public static Task<Int32> Main( String[] args )
         => new NuGetProgram().MainAsync( args, NuGetProgram.EXEC_ARGS_SEPARATOR );

   }

   internal sealed class NuGetProgram : NuGetRestoringProgram<InspectConfiguration, DefaultConfigurationConfiguration>
   {
      internal const String EXEC_ARGS_SEPARATOR = "--";

      protected override Boolean ValidateConfiguration(
         ConfigurationInformation<InspectConfiguration> info
         )
      {
         return info.Configuration.ValidateInspectConfiguration()
            && info.Configuration.ValidateConfiguration();
      }

      protected override async Task<Int32> UseRestorerAsync(
         ConfigurationInformation<InspectConfiguration> info,
         CancellationToken token,
         BoundRestoreCommandUser restorer,
         String sdkPackageID,
         String sdkPackageVersion
         )
      {
         var config = info.Configuration;

         var methodOrNull = await config.FindMethodForExecutingWithinNuGetAssemblyAsync(
            token,
            restorer,
            ( assemblyLoader, suitableMethod ) => Task.FromResult( suitableMethod ),
#if NET46
            null
#else
            sdkPackageID,
            sdkPackageVersion
#endif
            );

         var retVal = methodOrNull == null ?
            -3 :
            0;

         if ( retVal == 0 )
         {
            await WriteMethodInformationToFileAsync( config.InspectFilePath, methodOrNull );
         }

         return retVal;
      }

      protected override String GetDocumentation()
      {
         return "";
      }

      private static async Task WriteMethodInformationToFileAsync(
         String filePath,
         MethodInfo method
         )
      {
         var returnType = method.ReturnParameter.ParameterType;
         using ( var writer = filePath.OpenStreamWriter() )
         {
            await writer.WriteAsync( JsonConvert.SerializeObject( new PackageInspectionResult()
            {
               MethodToken = method.MetadataToken,
               InputParameters = method.GetParameters()
                  .Select( p => p.ParameterType )
                  .Where( t => !NuGetExecutionUtils.SpecialTypesForMethodArguments.Contains( t ) )
                  .SelectMany( t => t.GetRuntimeProperties() )
                  .Distinct()
                  .Select( p => p.CreatePropertyInfoObject() )
                  .ToArray(),
               OutputParameters = typeof( void ).Equals( returnType ) ?
                  Empty<ExecutableParameterInfo>.Array :
                  returnType
                     .GetRuntimeProperties()
                     .Select( p => p.CreatePropertyInfoObject() )
                     .ToArray()
            } ) );
         }

      }


   }

   internal static class NuGetUtilsExtensions
   {
      public static ExecutableParameterInfo CreatePropertyInfoObject(
         this PropertyInfo property
         )
      {
         var pType = property.PropertyType;
         return new ExecutableParameterInfo()
         {
            PropertyName = property.Name,
            IsRequired = false,
            TypeName = pType.FullName,
            IsEnum = pType.IsEnum
         };
      }
   }
}