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
   public class InspectConfiguration<TLogLevel> : DefaultNuGetExecutionConfiguration<TLogLevel>
   {
      public String InspectFilePath { get; set; }
   }

   public sealed class PackageInspectionResult
   {
      public String ExactPackageVersion { get; set; }
      public MethodInspectionResult[] SuitableMethods { get; set; }
   }

   public sealed class MethodInspectionResult
   {
      public Int32 MethodToken { get; set; }
      public String TypeName { get; set; }
      public String MethodName { get; set; }
      public ExecutableParameterInfo[] InputParameters { get; set; }
      public ExecutableParameterInfo[] OutputParameters { get; set; }
   }

   public sealed class ExecutableParameterInfo
   {
      public String PropertyName { get; set; }
      public String TypeName { get; set; }
      public Boolean IsRequired { get; set; }
      public Boolean IsEnum { get; set; }
      // TODO public { String TypeName; { String Name; String Type; }[] Properties; } ArrayElementTypeInfo
   }
}

public static partial class E_NuGetUtils
{
   public static Boolean ValidateInspectConfiguration<TLogLevel>(
      this InspectConfiguration<TLogLevel> configuration
      )
   {
      return configuration.ValidateDefaultNuGetExecutionConfiguration()
         && !String.IsNullOrEmpty( configuration.InspectFilePath );
   }
}