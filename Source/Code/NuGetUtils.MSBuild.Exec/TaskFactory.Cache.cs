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
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGetUtils.Lib.Tool.Agnostic;
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec
{
   internal sealed class NuGetExecutionCache
   {
      private readonly ConcurrentDictionary<EnvironmentKeyInfo, AsyncLazy<EnvironmentValue>> _environments;
      private readonly ConcurrentDictionary<InspectionKey, AsyncLazy<InspectionValue>> _inspections;

      public NuGetExecutionCache()
      {
         this.ProcessMonitor = new NuGetUtilsExecProcessMonitor();
         this._environments = new ConcurrentDictionary<EnvironmentKeyInfo, AsyncLazy<EnvironmentValue>>( ComparerFromFunctions.NewEqualityComparer<EnvironmentKeyInfo>(
            ( xInfo, yInfo ) =>
            {
               var x = xInfo?.Key;
               var y = yInfo?.Key;
               return ReferenceEquals( xInfo, yInfo ) || ( x != null && y != null
                  && String.Equals( x.PackageID, y.PackageID, StringComparison.CurrentCultureIgnoreCase )
                  && String.Equals( x.NuGetFramework, y.NuGetFramework, StringComparison.CurrentCultureIgnoreCase )
                  && String.Equals( x.PackageVersion, y.PackageVersion, StringComparison.CurrentCultureIgnoreCase )
                  && String.Equals( x.SDKPackageID, y.SDKPackageID, StringComparison.CurrentCultureIgnoreCase )
                  && String.Equals( x.SDKPackageVersion, y.SDKPackageVersion, StringComparison.CurrentCultureIgnoreCase )
                  && String.Equals( x.NuGetRuntimeID, y.NuGetRuntimeID, StringComparison.CurrentCultureIgnoreCase )
                  && String.Equals( x.SettingsLocation, y.SettingsLocation, StringComparison.CurrentCulture ) // No ignore case in settings location for case-sensitive file systems
                  );
            },
            x => x?.Key?.PackageID?.ToLower()?.GetHashCode() ?? 0
            ) );
         this._inspections = new ConcurrentDictionary<InspectionKey, AsyncLazy<InspectionValue>>( ComparerFromFunctions.NewEqualityComparer<InspectionKey>(
            ( x, y ) => ReferenceEquals( x, y ) || ( x != null && y != null
            && String.Equals( x.PackageID, y.PackageID, StringComparison.CurrentCultureIgnoreCase )
            && String.Equals( x.NuGetFramework, y.NuGetFramework, StringComparison.CurrentCultureIgnoreCase )
            && String.Equals( x.PackageVersion, y.PackageVersion, StringComparison.CurrentCultureIgnoreCase )
            && String.Equals( x.SettingsLocation, y.SettingsLocation, StringComparison.CurrentCulture ) // No ignore case in settings location for case-sensitive file systems
            && String.Equals( x.AssemblyPath, y.AssemblyPath, StringComparison.CurrentCulture ) // No ignore case in settings location for case-sensitive file systems
            ),
            x => x?.PackageID?.ToLower()?.GetHashCode() ?? 0
            ) );
      }

      public NuGetUtilsExecProcessMonitor ProcessMonitor { get; }

      public async Task<EnvironmentValue> DetectEnvironmentAsync(
         EnvironmentKeyInfo keyInfo,
         IBuildEngine be,
         CancellationToken token
         )
      {
         const String PROCESS = "NuGetUtils.MSBuild.Exec.Discover";
         return await this._environments.GetOrAdd( keyInfo, theKeyInfo => new UtilPack.AsyncLazy<EnvironmentValue>( async () =>
         {
            var key = theKeyInfo.Key;
            var packageIDIsSelf = theKeyInfo.PackageIDIsProjectPath;

            await be.LogProcessInvocationMessage( PROCESS );
            var startTime = DateTime.UtcNow;

            var env = await this.ProcessMonitor.CallProcessAndGetResultAsync<DiscoverConfiguration<String>, EnvironmentInspectionResult>(
               PROCESS,
               new DiscoverConfiguration<String>()
               {
                  DisableLogging = true,
                  DiscoverFilePath = DefaultConfigurationConfiguration.STANDARD_INPUT_OUR_OUTPUT_MARKER,
                  NuGetConfigurationFile = key.SettingsLocation,
                  RestoreFramework = key.NuGetFramework,
                  RestoreRuntimeID = key.NuGetRuntimeID,
                  SDKFrameworkPackageID = key.SDKPackageID,
                  SDKFrameworkPackageVersion = key.SDKPackageVersion,
                  PackageID = packageIDIsSelf ? null : key.PackageID,
                  PackageVersion = key.PackageVersion,
                  PackageIDIsSelf = packageIDIsSelf,
                  ProjectFilePath = theKeyInfo.ProjectFilePath,

                  ShutdownSemaphoreName = NuGetUtilsExecProcessMonitor.CreateNewShutdownSemaphoreName(),
                  // ReturnValuePath, RestoreSDKPackage is not used by Discover program.
               },
               token
               );
            await be.LogProcessEndMessage( PROCESS, startTime );

            var result = env.GetFirstOrDefault();
            if ( result == null )
            {
               throw new Exception( $"Errors occurred during environment detection: { env.GetSecondOrDefault() }." );
            }

            return new EnvironmentValue( result );
         } ) );
      }

      public async Task<InspectionValue> InspectPackageAsync(
         EnvironmentValue environment,
         InspectionKey key,
         Boolean restoreSDKPackage,
         IBuildEngine be,
         CancellationToken token
         )
      {
         const String PROCESS = "NuGetUtils.MSBuild.Exec.Inspect";

         return await this._inspections.GetOrAdd( key, theKey => new UtilPack.AsyncLazy<InspectionValue>( async () =>
         {
            await be.LogProcessInvocationMessage( PROCESS );

            var startTime = DateTime.UtcNow;
            var env = await this.ProcessMonitor.CallProcessAndGetResultAsync<InspectConfiguration<String>, PackageInspectionResult>(
               PROCESS,
               new InspectConfiguration<String>()
               {
                  DisableLogging = true,
                  InspectFilePath = DefaultConfigurationConfiguration.STANDARD_INPUT_OUR_OUTPUT_MARKER,
                  NuGetConfigurationFile = key.SettingsLocation,
                  RestoreFramework = key.NuGetFramework,
                  RestoreRuntimeID = environment.ThisRuntimeID,
                  SDKFrameworkPackageID = environment.SDKPackageID,
                  SDKFrameworkPackageVersion = environment.SDKPackageVersion,
                  PackageID = key.PackageID,
                  PackageVersion = key.PackageVersion,
                  AssemblyPath = key.AssemblyPath,

                  ShutdownSemaphoreName = NuGetUtilsExecProcessMonitor.CreateNewShutdownSemaphoreName(),
                  // ReturnValuePath is not used by Inspect program
#if !NET46
                  RestoreSDKPackage = restoreSDKPackage
#endif
               },
               token
               );
            await be.LogProcessEndMessage( PROCESS, startTime );

            var result = env.GetFirstOrDefault();
            if ( result == null )
            {
               throw new Exception( $"Errors occurred during package inspection: { env.GetSecondOrDefault() }." );
            }

            return new InspectionValue( result );
         } ) );
      }
   }

   // When detecting NuGet environment, the result depends on (givenFramework, givenRuntimeID, givenSettingsLocation, (if packageIDIsSelf true then projectFilePath else givenPackageID), givenPackageVersion)
   // The result is (actualFramework, actualRuntimeID, actualPackageID, actualPackageVersion)
   // Package ID and version are path of the environment because if packageIDIsSelf is specified, we need to sort out the packageID and version ourselves
   // Technically speaking, the environment does not depend on package id and version, however, to minimize the amount of process invocations, we detect package id and version along with the environment.
   // During typical usage of this task factory by MSBuild tasks, all of these parameters are the same.
   internal sealed class EnvironmentKey
   {
      public EnvironmentKey(
         String framework,
         String runtimeID,
         String sdkPackageID,
         String sdkPackageVersion,
         String settingsLocation,
         String packageID,
         String packageVersion
         )
      {
         this.NuGetFramework = framework.DefaultIfNullOrEmpty();
         this.NuGetRuntimeID = runtimeID.DefaultIfNullOrEmpty();
         this.SDKPackageID = sdkPackageID.DefaultIfNullOrEmpty();
         this.SDKPackageVersion = sdkPackageVersion.DefaultIfNullOrEmpty();
         this.SettingsLocation = settingsLocation.DefaultIfNullOrEmpty();
         this.PackageID = ArgumentValidator.ValidateNotEmpty( nameof( packageID ), packageID );
         this.PackageVersion = packageVersion;
      }

      public String NuGetFramework { get; }
      public String NuGetRuntimeID { get; }
      public String SDKPackageID { get; }
      public String SDKPackageVersion { get; }
      public String SettingsLocation { get; }
      public String PackageID { get; }
      public String PackageVersion { get; }
   }

   internal sealed class EnvironmentKeyInfo
   {

      public EnvironmentKeyInfo(
         EnvironmentKey key,
         Boolean packageIDIsProjectPath,
         String projectFilePath
         )
      {
         this.Key = ArgumentValidator.ValidateNotNull( nameof( key ), key );
         this.PackageIDIsProjectPath = packageIDIsProjectPath;
         this.ProjectFilePath = packageIDIsProjectPath ? key.PackageID : projectFilePath.DefaultIfNullOrEmpty();
      }

      public EnvironmentKey Key { get; }

      public Boolean PackageIDIsProjectPath { get; }

      public String ProjectFilePath { get; }
   }

   internal sealed class EnvironmentValue
   {
      public EnvironmentValue(
         EnvironmentInspectionResult result
         )
      {
         var hasErrors = result.Errors.Length > 0;
         this.ThisFramework = NotEmptyIfNoErrors( hasErrors, nameof( EnvironmentInspectionResult.ThisFramework ), result.ThisFramework );
         this.ThisRuntimeID = NotEmptyIfNoErrors( hasErrors, nameof( EnvironmentInspectionResult.ThisRuntimeID ), result.ThisRuntimeID );
         this.PackageID = NotEmptyIfNoErrors( hasErrors, nameof( EnvironmentInspectionResult.PackageID ), result.PackageID );
         this.PackageVersion = NotEmptyIfNoErrors( hasErrors, nameof( EnvironmentInspectionResult.PackageVersion ), result.PackageVersion );

#if !NET46
         this.SDKPackageID = NotEmptyIfNoErrors( hasErrors, nameof( EnvironmentInspectionResult.SDKPackageID ), result.SDKPackageID );
         this.SDKPackageVersion = NotEmptyIfNoErrors( hasErrors, nameof( EnvironmentInspectionResult.SDKPackageVersion ), result.SDKPackageVersion );
#endif

         this.Errors = result.Errors?.ToImmutableArray() ?? ImmutableArray<String>.Empty;
      }

      public String ThisFramework { get; }
      public String ThisRuntimeID { get; }
      public String SDKPackageID { get; }
      public String SDKPackageVersion { get; }
      public String PackageID { get; }
      public String PackageVersion { get; }
      public ImmutableArray<String> Errors { get; }

      private static String NotEmptyIfNoErrors( Boolean hasErrors, String paramName, String paramValue )
      {
         return hasErrors ? paramValue : ArgumentValidator.ValidateNotEmpty( paramName, paramValue );
      }
   }


   // (thisFramework, settingsLocation, packageID, packageVersion, entrypointTypeName, entrypointMethodName)
   // ->
   // readonly version of InspectionResult
   internal sealed class InspectionKey
   {
      public InspectionKey(
         String nuGetFramework,
         String settingsLocation,
         String packageID,
         String packageVersion,
         String assemblyPath
         )
      {
         this.NuGetFramework = ArgumentValidator.ValidateNotEmpty( nameof( nuGetFramework ), nuGetFramework );
         this.SettingsLocation = settingsLocation.DefaultIfNullOrEmpty();
         this.PackageID = ArgumentValidator.ValidateNotEmpty( nameof( packageID ), packageID );
         this.PackageVersion = ArgumentValidator.ValidateNotEmpty( nameof( packageVersion ), packageVersion );
         this.AssemblyPath = assemblyPath.DefaultIfNullOrEmpty();
      }

      public String NuGetFramework { get; }
      public String SettingsLocation { get; }
      public String PackageID { get; }
      public String PackageVersion { get; }
      public String AssemblyPath { get; }
   }

   internal sealed class InspectionValue
   {
      public InspectionValue(
         PackageInspectionResult result
         )
      {
         this.ExactPackageVersion = ArgumentValidator.ValidateNotEmpty( nameof( PackageInspectionResult.ExactPackageVersion ), result.ExactPackageVersion );
         this.SuitableMethods = result.SuitableMethods
            .Where( m => m.MethodToken != 0 )
            .Select( m => new MethodInspectionInfo( m ) )
            .ToImmutableArray();
      }

      public String ExactPackageVersion { get; }
      public ImmutableArray<MethodInspectionInfo> SuitableMethods { get; set; }
   }

   internal sealed class MethodInspectionInfo
   {
      public MethodInspectionInfo(
         MethodInspectionResult result
         )
      {
         var methodToken = result.MethodToken;
         this.MethodToken = methodToken;
         this.TypeName = ArgumentValidator.ValidateNotEmpty( nameof( result.TypeName ), result.TypeName );
         this.MethodName = ArgumentValidator.ValidateNotEmpty( nameof( result.MethodName ), result.MethodName );
         this.InputParameters = result.InputParameters.Select( p => new InspectionExecutableParameterInfo( p ) ).ToImmutableArray();
         this.OutputParameters = result.OutputParameters.Select( p => new InspectionExecutableParameterInfo( p ) ).ToImmutableArray();
      }

      public Int32 MethodToken { get; }
      public String TypeName { get; set; }
      public String MethodName { get; set; }
      public ImmutableArray<InspectionExecutableParameterInfo> InputParameters { get; }
      public ImmutableArray<InspectionExecutableParameterInfo> OutputParameters { get; }
   }

   internal sealed class InspectionExecutableParameterInfo
   {
      public InspectionExecutableParameterInfo(
         ExecutableParameterInfo result
         )
      {
         var pName = result.PropertyName;
         var tName = result.TypeName;
         this.PropertyName = ArgumentValidator.ValidateNotEmpty( nameof( pName ), pName );
         this.TypeName = ArgumentValidator.ValidateNotEmpty( nameof( tName ), tName );
         this.IsRequired = result.IsRequired;
         this.IsEnum = result.IsEnum;
      }

      public String PropertyName { get; }
      public String TypeName { get; }
      public Boolean IsRequired { get; }
      public Boolean IsEnum { get; }
   }

   public static partial class NuGetUtilsExtensions
   {
      internal static String DefaultIfNullOrEmpty( this String value, String defaultValue = null )
      {
         return String.IsNullOrEmpty( value ) ? defaultValue : value;
      }

      internal static String DefaultIfNullOrEmpty( this String value, Func<String> defaultValueFactory )
      {
         return String.IsNullOrEmpty( value ) ? defaultValueFactory() : value;
      }

      internal static Task LogProcessInvocationMessage( this IBuildEngine be, String process )
         => be.LogMessageOrWriteToConsoleOut( $"Invoking process { process }." );

      internal static Task LogProcessEndMessage( this IBuildEngine be, String process, DateTime startTime )
         => be.LogMessageOrWriteToConsoleOut( $"Process { process } exited, duration { ( DateTime.UtcNow - startTime ).ToString( "c" ) }." );

      internal static async Task LogMessageOrWriteToConsoleOut( this IBuildEngine be, String message )
      {
         if ( be == null )
         {
            await Console.Out.WriteLineAsync( message );
         }
         else
         {
            be.LogMessageEvent( new BuildMessageEventArgs(
               message,
               null,
               null,
               MessageImportance.Normal
               ) );
         }
      }
   }
}

