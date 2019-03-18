using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetUtils.MSBuild.Exec.TestPackage
{
   public static partial class EntryPoints
   {
      public static EchoTaskItemSpecOutput EchoTaskItemSpec(
         EchoTaskItemSpecInput input
         )
      {
         return new EchoTaskItemSpecOutput( input );
      }
   }

   public sealed class EchoTaskItemSpecInput
   {
      public String[] Value { get; set; }
   }

   public sealed class EchoTaskItemSpecOutput
   {
      public EchoTaskItemSpecOutput(
         EchoTaskItemSpecInput input
         )
      {
         this.Result = input?.Value;
      }

      public String[] Result { get; }
   }
}
