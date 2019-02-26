using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetUtils.MSBuild.Exec.Common
{
   public class PerformConfiguration<TLogLevel> : DefaultNuGetExecutionConfiguration<TLogLevel>
   {
      public String ShutdownSemaphoreName { get; set; }
   }
}
