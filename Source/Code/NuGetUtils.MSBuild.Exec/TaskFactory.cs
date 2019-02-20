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
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGetUtils.Lib.Tool.Agnostic;
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec
{
   public sealed class NuGetExecutionTaskFactory : ITaskFactory
   {
      private const String PACKAGE_ID = "PackageID";
      private const String PACKAGE_ID_IS_SELF = "PackageIDIsSelf";
      private const String PACKAGE_VERSION = "PackageVersion";
      private const String ASSEMBLY_PATH = "AssemblyPath";
      private const String NUGET_FW = "NuGetFramework";
      private const String NUGET_FW_PACKAGE_ID = "NuGetFrameworkPackageID";
      private const String NUGET_FW_PACKAGE_VERSION = "NuGetFrameworkPackageVersion";
      private const String NUGET_RID = "NuGetPlatformRID";
      private const String NUGET_RID_CATALOG_PACKAGE_ID = "NuGetPlatformRIDCatalogPackageID";
      private const String NUGET_CONFIG_FILE = "NuGetConfigurationFile";
      private const String COPY_TO_TEMPORARY_FOlDER_BEFORE_LOAD = "CopyToFolderBeforeLoad";
      private const String TASK_NAME = "EntryPointTypeName";
      private const String TASK_METHOD_NAME = "EntryPointMethodName";
      private const String UNMANAGED_ASSEMBLIES_MAP = "UnmanagedAssemblyReferenceMap";

      // Static in order to share state between task factory usage in different build files.
      private static readonly NuGetExecutionCache _cache = new NuGetExecutionCache();

      private InitializationResult _initResult;

      public Boolean Initialize(
         String taskName,
         IDictionary<String, TaskPropertyInfo> parameterGroup,
         String taskBody,
         IBuildEngine taskFactoryLoggingHost
         )
      {
         InitializationResult initResult = null;
         try
         {
            var taskBodyElement = XElement.Parse( taskBody );

            initResult = InitializeAsync(
               new InitializationArgs(
                  taskBodyElement.ElementAnyNS( NUGET_FW )?.Value,
                  taskBodyElement.ElementAnyNS( NUGET_RID )?.Value,
                  taskBodyElement.ElementAnyNS( NUGET_FW_PACKAGE_ID )?.Value,
                  taskBodyElement.ElementAnyNS( NUGET_FW_PACKAGE_VERSION )?.Value,
                  false,
                  taskBodyElement.ElementAnyNS( NUGET_CONFIG_FILE )?.Value,
                  taskBodyElement.ElementAnyNS( PACKAGE_ID )?.Value,
                  taskBodyElement.ElementAnyNS( PACKAGE_VERSION )?.Value,
                  taskBodyElement.ElementAnyNS( PACKAGE_ID_IS_SELF )?.Value?.ParseAsBooleanSafe() ?? false,
                  taskBodyElement.ElementAnyNS( ASSEMBLY_PATH )?.Value,
                  ( taskBodyElement.ElementAnyNS( TASK_NAME )?.Value ).DefaultIfNullOrEmpty( taskName ),
                  taskBodyElement.ElementAnyNS( TASK_METHOD_NAME )?.Value,
                  taskFactoryLoggingHost
                  )
               ).GetAwaiter().GetResult();
         }
         catch ( Exception exc )
         {
            taskFactoryLoggingHost?.LogErrorEvent( new BuildErrorEventArgs(
               "subcat",
               "code",
               "file",
               0,
               0,
               0,
               0,
               $"Internal error: {exc.Message}",
               "helpKeyword",
               "senderName"
               ) );
         }

         this._initResult = initResult;

         return initResult != null;
      }

      public String FactoryName => nameof( NuGetExecutionTaskFactory );

      public TaskPropertyInfo[] GetTaskParameters()
      {
         return this._initResult?.TypeGenerationResult?.Properties;
      }

      public Type TaskType => this._initResult?.TypeGenerationResult?.GeneratedType;

      public void CleanupTask( ITask task )
      {

      }

      public ITask CreateTask( IBuildEngine taskFactoryLoggingHost )
      {
         return this._initResult?.CreateTaskInstance();
      }

      private static async Task<InitializationResult> InitializeAsync(
         InitializationArgs args
         )
      {
         var be = args.BuildEngine;
         var projectFilePath = be?.ProjectFileOfTaskNode;
         var env = await _cache.DetectEnvironmentAsync( new EnvironmentKeyInfo(
            new EnvironmentKey(
               args.Framework,
               args.RuntimeID,
               args.SDKPackageID,
               args.SDKPackageVersion,
               args.SettingsLocation,
               args.PackageIDIsSelf ? projectFilePath : args.PackageID,
               args.PackageVersion
            ),
            args.PackageIDIsSelf,
            projectFilePath
            ) );
         InitializationResult initializationResult = null;
         if ( env.Errors.Length > 0 )
         {
            if ( be != null )
            {
               // TODO log based on error codes

            }
            initializationResult = null;
         }
         else
         {
            var inspection = await _cache.InspectPackageAsync( env, new InspectionKey(
               env.ThisFramework,
               args.SettingsLocation,
               env.PackageID,
               env.PackageVersion,
               args.AssemblyPath,
               args.TypeName,
               args.MethodName
               ),
               args.RestoreSDKPackage
               );
            var typeGenResult = TaskTypeGenerator.Instance.GenerateTaskType(
               true,
               inspection.InputParameters,
               inspection.OutputParameters
               );

            initializationResult = new InitializationResult(
               typeGenResult,
               () => (ITask) typeGenResult.GeneratedType.GetTypeInfo().DeclaredConstructors.First().Invoke( new[]
               {
                  new TaskProxy( env, inspection, typeGenResult)
               } )
               );
         }

         return initializationResult;
      }


      private sealed class InitializationArgs
      {
         public InitializationArgs(
            String framework,
            String runtimeID,
            String sdkPackageID,
            String sdkPackageVersion,
            Boolean restoreSDKPackage,
            String settingsLocation,
            String packageID,
            String packageVersion,
            Boolean packageIDIsSelf,
            String assemblyPath,
            String typeName,
            String methodName,
            IBuildEngine buildEngine
            )
         {
            this.Framework = framework;
            this.RuntimeID = runtimeID;
            this.SDKPackageID = sdkPackageID;
            this.SDKPackageVersion = sdkPackageVersion;
            this.RestoreSDKPackage = restoreSDKPackage;
            this.SettingsLocation = settingsLocation;
            this.PackageID = packageID;
            this.PackageVersion = packageVersion;
            this.PackageIDIsSelf = packageIDIsSelf;
            this.AssemblyPath = assemblyPath;
            this.TypeName = typeName;
            this.MethodName = methodName;
            this.BuildEngine = buildEngine;
         }

         public String Framework { get; }
         public String RuntimeID { get; }
         public String SDKPackageID { get; }
         public String SDKPackageVersion { get; }
         public Boolean RestoreSDKPackage { get; }
         public String SettingsLocation { get; }
         public String PackageID { get; }
         public String PackageVersion { get; }
         public Boolean PackageIDIsSelf { get; }
         public String AssemblyPath { get; }
         public String TypeName { get; }
         public String MethodName { get; }
         public IBuildEngine BuildEngine { get; }
      }

      private sealed class InitializationResult
      {
         public InitializationResult(
            TypeGenerationResult typeGenerationResult,
            Func<ITask> createTaskInstance
            )
         {
            this.TypeGenerationResult = ArgumentValidator.ValidateNotNull( nameof( typeGenerationResult ), typeGenerationResult );
            this.CreateTaskInstance = ArgumentValidator.ValidateNotNull( nameof( createTaskInstance ), createTaskInstance );
         }

         public TypeGenerationResult TypeGenerationResult { get; }

         public Func<ITask> CreateTaskInstance { get; }
      }
   }

   public static partial class NuGetUtilsExtensions
   {
      // From https://stackoverflow.com/questions/1145659/ignore-namespaces-in-linq-to-xml
      internal static IEnumerable<XElement> ElementsAnyNS<T>( this IEnumerable<T> source, String localName )
         where T : XContainer
      {
         return source.Elements().Where( e => e.Name.LocalName == localName );
      }

      internal static XElement ElementAnyNS<T>( this IEnumerable<T> source, String localName )
         where T : XContainer
      {
         return source.ElementsAnyNS( localName ).FirstOrDefault();
      }

      internal static IEnumerable<XElement> ElementsAnyNS( this XContainer source, String localName )
      {
         return source.Elements().Where( e => e.Name.LocalName == localName );
      }

      internal static XElement ElementAnyNS( this XContainer source, String localName )
      {
         return source.ElementsAnyNS( localName ).FirstOrDefault();
      }
   }
}