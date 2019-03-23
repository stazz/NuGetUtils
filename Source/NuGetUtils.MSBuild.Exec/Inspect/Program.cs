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
using NuGet.Common;
using NuGet.Versioning;
using NuGetUtils.Lib.Common;
using NuGetUtils.Lib.Exec;
using NuGetUtils.Lib.Restore;
using NuGetUtils.Lib.Tool;
using NuGetUtils.Lib.Tool.Agnostic;
using NuGetUtils.MSBuild.Exec.Common;
using NuGetUtils.MSBuild.Exec.Common.NuGetDependant;
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
   using TConfiguration = InspectConfiguration<LogLevel>;

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

      protected override Boolean ValidateConfiguration(
         ConfigurationInformation<TConfiguration> info
         )
      {
         return info.Configuration.ValidateInspectConfiguration()
            && info.Configuration.ValidateConfiguration();
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

         await WriteMethodInformationToFileAsync(
            token,
            restorer,
            config.PackageID,
            config.PackageVersion,
            config.InspectFilePath,
            await config.FindMethodForExecutingWithinNuGetAssemblyAsync(
               token,
               restorer,
               ( assemblyLoader, theAssembly, suitableMethod ) => Task.FromResult( theAssembly ),
#if NET46
               null
#else
               sdkPackageID,
               sdkPackageVersion
#endif
               , getFiles: restorer.ThisFramework.CreateMSBuildExecGetFilesDelegate()
            )
            );

         return 0;
      }

      protected override String GetDocumentation()
      {
         return "";
      }

      private static async Task WriteMethodInformationToFileAsync(
         CancellationToken token,
         BoundRestoreCommandUser restorer,
         String packageID,
         String packageVersion,
         String filePath,
         Assembly assembly
         )
      {
         // Should be fast, since the lock file should already been cached by previous restore call via loadnugetassembly call
         var packageNuGetVersion = ( await restorer
            .RestoreIfNeeded( packageID, packageVersion, token ) )
            .Targets.Single()
            .Libraries
            .First( l => String.Equals( l.Name, packageID, StringComparison.CurrentCultureIgnoreCase ) )
            .Version;

         using ( var writer = filePath.OpenStreamWriter() )
         {
            await writer.WriteAsync( JsonConvert.SerializeObject( new PackageInspectionResult()
            {
               ExactPackageVersion = new VersionRange( minVersion: packageNuGetVersion, includeMinVersion: true, maxVersion: packageNuGetVersion, includeMaxVersion: true ).ToShortString(),
               SuitableMethods = assembly
                  .GetTypes()
                  .SelectMany( t => t.GetTypeInfo().FindSuitableMethodsForNuGetExec( null ) )
                  .Select( method =>
                  {
                     var returnType = method.ReturnParameter.ParameterType.GetActualTypeForPropertiesScan();
                     return new MethodInspectionResult()
                     {
                        MethodToken = method.MetadataToken,
                        MethodName = method.Name,
                        TypeName = method.DeclaringType.FullName,
                        InputParameters = method.GetParameters()
                           .Select( p => p.ParameterType )
                           .Where( t => t.IsEligibleInputOrOutputParameterType( true ) )
                           .SelectMany( t => t.GetRuntimeProperties() )
                           .Distinct()
                           .IncludeTaskProperties( false )
                           .Select( p => p.CreatePropertyInfoObject() )
                           .ToArray(),
                        OutputParameters = typeof( void ).Equals( returnType ) || !returnType.IsEligibleInputOrOutputParameterType( false ) ?
                           Empty<ExecutableParameterInfo>.Array :
                           returnType
                              .GetRuntimeProperties()
                              .IncludeTaskProperties( true )
                              .Select( p => p.CreatePropertyInfoObject() )
                              .ToArray()
                     };
                  } )
                  .ToArray()
            } ) );
         }

      }
   }

   internal static class NuGetUtilsExtensions
   {
      private static ISet<Type> AdditionalPrimitiveTypes { get; } = new HashSet<Type>()
      {
         typeof(String),
         typeof(Decimal),
         typeof(DateTime),
         typeof(DateTimeOffset),
         typeof(TimeSpan),
         typeof(Guid)
      };

      public static IEnumerable<PropertyInfo> IncludeTaskProperties(
         this IEnumerable<PropertyInfo> properties,
         Boolean isOutput
         )
      {
         // Return all non-static properties which have both getter and setter, except output parameters are ok to be getter-only
         return properties.Where( p => p.GetMethod != null && ( isOutput || p.SetMethod != null ) && !p.GetMethod.IsStatic );
      }

      public static ExecutableParameterInfo CreatePropertyInfoObject(
         this PropertyInfo property
         )
      {
         var pType = property.PropertyType;
         return new ExecutableParameterInfo()
         {
            PropertyName = property.Name,
            IsRequired = false, // TODO Required-deduction, maybe will be easier with C# 8?
            TypeName = pType.FullName,
            IsEnum = pType.IsEnum
         };
      }

      public static Boolean IsEligibleInputOrOutputParameterType(
         this Type type,
         Boolean isInput
         )
      {
         return
            !type.IsPrimitive // Boolean, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64, IntPtr, UIntPtr, Char, Double, and Single
            && !type.IsEnum // No enums
            && !AdditionalPrimitiveTypes.Contains( type ) // Other "primitives"
            && !typeof( Delegate ).IsAssignableFrom( type ) // No delegates
            && ( !isInput || !NuGetExecutionUtils.SpecialTypesForMethodArguments.Contains( type ) ) // No 'special' types
            ;
      }

      public static Type GetActualTypeForPropertiesScan(
         this Type type
         )
      {
         return type.GetTypeInfo().IsGenericTaskOrValueTask() ?
            type.GetGenericArguments()[0] :
            ( typeof( Task ).IsAssignableFrom( type ) ? typeof( void ) : type );
      }
   }
}