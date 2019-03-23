using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetUtils.MSBuild.Exec.TestPackage
{
   public static partial class EntryPoints
   {
      public static EchoTaskItemWithMetaDataSpecOutput EchoTaskItemWithMetaData(
         EchoTaskItemWithMetaDataSpecInput input
         )
      {
         return new EchoTaskItemWithMetaDataSpecOutput( input );
      }
   }

   public sealed class EchoTaskItemWithMetaDataSpecInput
   {
      public TaskItemWithMetaData[] Value { get; set; }
   }

   public sealed class EchoTaskItemWithMetaDataSpecOutput
   {
      public EchoTaskItemWithMetaDataSpecOutput(
         EchoTaskItemWithMetaDataSpecInput input
         )
      {
         this.Result = input?.Value;
      }

      public TaskItemWithMetaData[] Result { get; }
   }

   public sealed class TaskItemWithMetaData
   {
      public String ItemSpec { get; set; }

      public String MetaData1 { get; set; }

      public String MetaData2 { get; set; }
   }
}
