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
using System;

namespace NuGetUtils.MSBuild.Exec.Runner
{
   internal sealed class NuGetExecutionConfigurationImpl : NuGetExecutionConfiguration, NuGetUsageConfiguration
   {
      public String NuGetConfigurationFile { get; set; }

      public String RestoreFramework { get; set; }

      public String LockFileCacheDirectory { get; set; }

      public String SDKFrameworkPackageID { get; set; }

      public String SDKFrameworkPackageVersion { get; set; }

      public Boolean DisableLockFileCache { get; set; }

      public LogLevel LogLevel { get; set; }

      public Boolean DisableLogging { get; set; }

      public String PackageID { get; set; }

      public String PackageVersion { get; set; }

      public String AssemblyPath { get; set; }

      public String EntrypointTypeName { get; set; }

      public String EntrypointMethodName { get; set; }

      public Boolean RestoreSDKPackage { get; set; }
   }

   internal class ConfigurationConfigurationImpl : ConfigurationConfiguration
   {
      public String ConfigurationFileLocation { get; set; }
   }
}
