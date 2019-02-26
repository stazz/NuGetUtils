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
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec
{
   public sealed class TaskProxy
   {
      private readonly ImmutableDictionary<String, TaskPropertyHolder> _propertyInfos;
      private readonly EnvironmentValue _environment;
      private readonly InspectionValue _entrypoint;

      private readonly CancellationTokenSource _cancellationTokenSource;
      private readonly NuGetUtilsExecProcessMonitor _processMonitor;

      internal TaskProxy(
         EnvironmentValue environment,
         InspectionValue entrypoint,
         TypeGenerationResult generationResult
         )
      {
         this._environment = ArgumentValidator.ValidateNotNull( nameof( environment ), environment );
         this._entrypoint = ArgumentValidator.ValidateNotNull( nameof( entrypoint ), entrypoint );
         this._propertyInfos = generationResult
            .Properties
            .ToImmutableDictionary( p => p.Name, p => new TaskPropertyHolder( p.Output ) );
         this._cancellationTokenSource = new CancellationTokenSource();
      }

      // Called by generated task type
      public void Cancel()
      {
         this._cancellationTokenSource.Cancel();
      }

      // Called by generated task type
      public Object GetProperty( String propertyName )
      {
         return this._propertyInfos.TryGetValue( propertyName, out var info ) ?
            info.Value :
            throw new InvalidOperationException( $"Property \"{propertyName}\" is not supported for this task." );
      }

      // Called by generated task type
      public void SetProperty( String propertyName, Object value )
      {
         if ( this._propertyInfos.TryGetValue( propertyName, out var info ) )
         {
            info.Value = value;
         }
         else
         {
            throw new InvalidOperationException( $"Property \"{propertyName}\" is not supported for this task." );
         }
      }

      // Called by generated task type
      public Boolean Execute( IBuildEngine be )
      {
         return this.ExecuteAsync().GetAwaiter().GetResult();
      }

      private async Task<Boolean> ExecuteAsync()
      {
         // Call process, deserialize result, set output properties.
         var tempFileLocation = Path.Combine( Path.GetTempPath(), $"NuGetUtilsExec_" + Guid.NewGuid() );

         var returnCode = await this._processMonitor.CallProcessAndStreamOutputAsync(
            "NuGetUtils.MSBuild.Exec.Perform",
            new PerformConfiguration<String>
            {
               ReturnValuePath = tempFileLocation,
            },
            this._cancellationTokenSource.Token,
            TimeSpan.FromSeconds( 1 )
            );
         if ( returnCode.HasValue )
         {
            using ( var sReader = new StreamReader( File.Open( tempFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read ), new UTF8Encoding( false, false ), false ) )
            using ( var jReader = new JsonTextReader( sReader ) )
            {
               foreach ( var jProp in ( await JObject.LoadAsync( jReader ) )
                  .Properties()
                  .Where( p => this._propertyInfos.TryGetValue( p.Name, out var prop ) && prop.IsOutput )
                  )
               {
                  var jValue = jProp.Value;
                  this._propertyInfos[jProp.Name].Value = jProp.Value is JValue jPrimitive ? jPrimitive.Value?.ToString() : jValue.ToString();
               }
            }
         }

         return returnCode.HasValue && returnCode.Value == 0;
      }

      private sealed class TaskPropertyHolder
      {
         public TaskPropertyHolder( Boolean isOutput )
         {
            this.IsOutput = isOutput;
         }

         public Boolean IsOutput { get; }
         public Object Value { get; set; }
      }
   }
}
