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
using NuGetUtils.Lib.Exec.Agnostic;
using NuGetUtils.Lib.Restore.Agnostic;
using NuGetUtils.Lib.Tool.Agnostic;
using System;
using System.IO;
using System.Text;

namespace NuGetUtils.MSBuild.Exec.Common
{
   /// <summary>
   /// This provides default, writeable, implementation for <see cref="NuGetExecutionConfiguration"/> and <see cref="NuGetUsageConfiguration{TLogLevel}"/>.
   /// </summary>
   public class DefaultNuGetExecutionConfiguration<TLogLevel> : NuGetExecutionConfiguration, NuGetUsageConfiguration<TLogLevel>
   {
      /// <inheritdoc />
      public String NuGetConfigurationFile { get; set; }

      /// <inheritdoc />
      public String RestoreFramework { get; set; }

      /// <inheritdoc />
      public String RestoreRuntimeID { get; set; }

      /// <inheritdoc />
      public String LockFileCacheDirectory { get; set; }

      /// <inheritdoc />
      public String SDKFrameworkPackageID { get; set; }

      /// <inheritdoc />
      public String SDKFrameworkPackageVersion { get; set; }

      /// <inheritdoc />
      public Boolean DisableLockFileCache { get; set; }

      /// <inheritdoc />
      public TLogLevel LogLevel { get; set; }

      /// <inheritdoc />
      public Boolean DisableLogging { get; set; }

      /// <inheritdoc />
      public String PackageID { get; set; }

      /// <inheritdoc />
      public String PackageVersion { get; set; }

      /// <inheritdoc />
      public String AssemblyPath { get; set; }

      /// <inheritdoc />
      public String EntrypointTypeName { get; set; }

      /// <inheritdoc />
      public String EntrypointMethodName { get; set; }

      /// <inheritdoc />
      public String ReturnValuePath { get; set; }

#if !NET46

      /// <inheritdoc />
      public Boolean RestoreSDKPackage { get; set; }

#endif
   }

   public static class NuGetUtilsExtensions
   {
      public static Stream OpenFileToWriteOrStandardOutput( this String path )
      {
         return String.Equals( DefaultConfigurationConfiguration.STANDARD_INPUT_OUR_OUTPUT_MARKER, path ) ?
            Console.OpenStandardOutput() :
            File.Open( path, FileMode.Create, FileAccess.Write, FileShare.None );
      }

      public static TextWriter OpenStreamWriter( this String path )
      {
         return new StreamWriter( path.OpenFileToWriteOrStandardOutput(), new UTF8Encoding( false, false ) );
      }
   }
}
