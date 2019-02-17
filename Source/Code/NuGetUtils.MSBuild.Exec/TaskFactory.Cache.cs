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
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec
{
   internal sealed class NuGetExecutionCache
   {
      private readonly String _thisAssemblyDirectory;

      private readonly ConcurrentDictionary<EnvironmentKeyInfo, AsyncLazy<EnvironmentValue>> _environments;
      private readonly ConcurrentDictionary<InspectionKey, AsyncLazy<InspectionValue>> _inspections;

      public NuGetExecutionCache()
      {
         this._thisAssemblyDirectory = Path.GetFullPath( new Uri( this.GetType().GetTypeInfo().Assembly.CodeBase ).LocalPath );
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
            && String.Equals( x.EntrypointTypeName, y.EntrypointTypeName, StringComparison.CurrentCulture ) // No ignore case because name of type
            && String.Equals( x.EntrypointMethodName, y.EntrypointMethodName, StringComparison.CurrentCulture ) // No ignore case because name of method
            ),
            x => x?.PackageID?.ToLower()?.GetHashCode() ?? 0
            ) );
      }

      public async Task<EnvironmentValue> DetectEnvironmentAsync(
         EnvironmentKeyInfo keyInfo
         )
      {
         return await this._environments.GetOrAdd( keyInfo, theKeyInfo => new UtilPack.AsyncLazy<EnvironmentValue>( async () =>
         {
            var key = theKeyInfo.Key;
            var packageIDIsSelf = theKeyInfo.PackageIDIsProjectPath;

            var env = await this.CallProcessAndGetResultAsync<DiscoverConfiguration<String>, EnvironmentInspectionResult>(
               "NuGetUtils.MSBuild.Exec.Discover",
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
                  ProjectFilePath = theKeyInfo.ProjectFilePath
                  // ReturnValuePath, RestoreSDKPackage is not used by Discover program.
               } );
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
         Boolean restoreSDKPackage
         )
      {
         return await this._inspections.GetOrAdd( key, theKey => new UtilPack.AsyncLazy<InspectionValue>( async () =>
         {

            var env = await this.CallProcessAndGetResultAsync<InspectConfiguration<String>, PackageInspectionResult>(
               "NuGetUtils.MSBuild.Exec.Inspect",
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
                  EntrypointTypeName = key.EntrypointTypeName,
                  EntrypointMethodName = key.EntrypointMethodName,
                  // ReturnValuePath is not used by Inspect program
#if !NET46
                  RestoreSDKPackage = restoreSDKPackage
#endif
               } );
            var result = env.GetFirstOrDefault();
            if ( result == null )
            {
               throw new Exception( $"Errors occurred during environment detection: { env.GetSecondOrDefault() }." );
            }

            return new InspectionValue( result );
         } ) );
      }

      private async Task<EitherOr<TOutput, String>> CallProcessAndGetResultAsync<TInput, TOutput>(
         String assemblyName,
         TInput input
         )
      {
         assemblyName = Path.Combine( this._thisAssemblyDirectory, assemblyName );
         var p = new Process()
         {
            StartInfo = new ProcessStartInfo()
            {
               FileName =
#if NET46
               assemblyName + ".exe"
#else
               "dotnet"
#endif
               ,
               Arguments =
#if !NET46
               assemblyName + ".dll " +
#endif
               $"/{nameof( ConfigurationConfiguration.ConfigurationFileLocation )}:-",
               UseShellExecute = false,
               CreateNoWindow = true,
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               RedirectStandardInput = true,
            }
         };

         var stdout = new StringBuilder();
         p.OutputDataReceived += ( sender, args ) =>
         {
            if ( args.Data is String line ) // Will be null on process shutdown
            {
               stdout.Append( line );
            }
         };
         var stderr = new StringBuilder();
         p.ErrorDataReceived += ( sender, args ) =>
         {
            if ( args.Data is String line ) // Will be null on process shutdown
            {
               stderr.Append( line );
            }
         };

         p.Start();
         p.BeginOutputReadLine();
         p.BeginErrorReadLine();

         // Pass serialized configuration via stdin
         using ( var stdin = p.StandardInput )
         {
            await stdin.WriteAsync( JsonConvert.SerializeObject( input ) );
         }


         while ( !p.WaitForExit( 0 ) )
         {
            await Task.Delay( 100 );
         }

         // Process.HasExited has following documentation:
         // When standard output has been redirected to asynchronous event handlers, it is possible that output processing will
         // not have completed when this property returns true. To ensure that asynchronous event handling has been completed,
         // call the WaitForExit() overload that takes no parameter before checking HasExited.
         p.WaitForExit();
         while ( !p.HasExited )
         {
            await Task.Delay( 50 );
         }

         return stderr.Length > 0 ?
            new EitherOr<TOutput, String>( stderr.ToString() ) :
            JsonConvert.DeserializeObject<TOutput>( stdout.ToString() );
      }
   }

   // When detecting NuGet environment, the result depends on (givenFramework, givenRuntimeID, givenSettingsLocation, (if packageIDIsSelf true then projectFilePath else givenPackageID), givenPackageVersion)
   // The result is (actualFramework, actualRuntimeID, actualPackageID, actualPackageVersion)
   // Package ID and version are path of the environment because if packageIDIsSelf is specified, we need to sort out the packageID and version ourselves
   // Technically speaking, the environment could just be the (framework, runtime), however, to minimize the amount of process invocations, we detect package id and version along with the environment.
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
         this.ProjectFilePath = packageIDIsProjectPath ? key.PackageID : ArgumentValidator.ValidateNotEmpty( nameof( projectFilePath ), projectFilePath );
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
         this.ThisFramework = ArgumentValidator.ValidateNotEmpty( nameof( EnvironmentInspectionResult.ThisFramework ), result.ThisFramework );
         this.ThisRuntimeID = ArgumentValidator.ValidateNotEmpty( nameof( EnvironmentInspectionResult.ThisRuntimeID ), result.ThisRuntimeID );
         this.PackageID = ArgumentValidator.ValidateNotEmpty( nameof( EnvironmentInspectionResult.PackageID ), result.PackageID );
         this.PackageVersion = ArgumentValidator.ValidateNotEmpty( nameof( EnvironmentInspectionResult.PackageVersion ), result.PackageVersion );
         this.SDKPackageID = ArgumentValidator.ValidateNotEmpty( nameof( EnvironmentInspectionResult.SDKPackageID ), result.SDKPackageID );
         this.SDKPackageVersion = ArgumentValidator.ValidateNotEmpty( nameof( EnvironmentInspectionResult.SDKPackageVersion ), result.SDKPackageVersion );
         this.Errors = result.Errors?.ToImmutableArray() ?? ImmutableArray<String>.Empty;
      }

      public String ThisFramework { get; }
      public String ThisRuntimeID { get; }
      public String SDKPackageID { get; }
      public String SDKPackageVersion { get; }
      public String PackageID { get; }
      public String PackageVersion { get; }
      public ImmutableArray<String> Errors { get; }
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
         String assemblyPath,
         String entrypointTypeName,
         String entrypointMethodName
         )
      {
         this.NuGetFramework = ArgumentValidator.ValidateNotEmpty( nameof( nuGetFramework ), nuGetFramework );
         this.SettingsLocation = settingsLocation.DefaultIfNullOrEmpty();
         this.PackageID = ArgumentValidator.ValidateNotEmpty( nameof( packageID ), packageID );
         this.PackageVersion = ArgumentValidator.ValidateNotEmpty( nameof( packageVersion ), packageVersion );
         this.AssemblyPath = assemblyPath.DefaultIfNullOrEmpty();
         this.EntrypointTypeName = entrypointTypeName.DefaultIfNullOrEmpty();
         this.EntrypointMethodName = entrypointMethodName.DefaultIfNullOrEmpty();
      }

      public String NuGetFramework { get; }
      public String SettingsLocation { get; }
      public String PackageID { get; }
      public String PackageVersion { get; }
      public String AssemblyPath { get; }
      public String EntrypointTypeName { get; }
      public String EntrypointMethodName { get; }
   }

   internal sealed class InspectionValue
   {
      public InspectionValue(
         PackageInspectionResult result
         )
      {
         var methodToken = result.MethodToken;
         if ( methodToken == 0 )
         {
            throw new ArgumentException( nameof( methodToken ) );
         }

         this.MethodToken = methodToken;
         this.InputParameters = result.InputParameters.Select( p => new InspectionExecutableParameterInfo( p ) ).ToImmutableArray();
         this.OutputParameters = result.OutputParameters.Select( p => new InspectionExecutableParameterInfo( p ) ).ToImmutableArray();
      }

      public Int32 MethodToken { get; }
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
   }
}

