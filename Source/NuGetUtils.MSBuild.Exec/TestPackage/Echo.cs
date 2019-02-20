using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetUtils.MSBuild.Exec.TestPackage
{
   public static partial class EntryPoints
   {
      public static EchoOutput Echo(
         EchoInput input
         )
      {
         return new EchoOutput( input );
      }
   }

   public sealed class EchoInput
   {
      public String Value { get; set; }
   }

   public sealed class EchoOutput
   {
      public EchoOutput(
         EchoInput input
         )
      {
         this.Result = input?.Value;
      }

      public String Result { get; }
   }
}
