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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec
{
   public sealed class TaskProxy
   {
      private readonly ImmutableDictionary<String, TaskPropertyHolder> _propertyInfos;
      private readonly EnvironmentValue _environment;
      private readonly InspectionValue _entrypoint;

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
            .ToImmutableDictionary( p => p.Name, p => new TaskPropertyHolder() );
      }

      // Called by generated task type
      public void Cancel()
      {
         // TODO set semaphore
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
         throw new NotImplementedException();
         // Call process, deserialize result, set output properties.
      }

      private sealed class TaskPropertyHolder
      {
         public Object Value { get; set; }
      }
   }
}
