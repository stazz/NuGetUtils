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
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetUtils.MSBuild.Exec.Common
{
   public class DiscoverConfiguration<TLogLevel> : DefaultNuGetExecutionConfiguration<TLogLevel>
   {
      public String DiscoverFilePath { get; set; }
      public Boolean PackageIDIsSelf { get; set; }
      public String ProjectFilePath { get; set; }
   }

   public sealed class EnvironmentInspectionResult
   {
      public String ThisFramework { get; set; }
      public String ThisRuntimeID { get; set; }
      public String PackageID { get; set; }
      public String PackageVersion { get; set; }
      public String SDKPackageID { get; set; }
      public String SDKPackageVersion { get; set; }
      public String[] Errors { get; set; }
   }
}

public static partial class E_NuGetUtils
{
   public static Boolean ValidateDiscoverConfiguration<TLogLevel>(
      this DiscoverConfiguration<TLogLevel> configuration
      )
   {
      return !String.IsNullOrEmpty( configuration.DiscoverFilePath );
   }
}
