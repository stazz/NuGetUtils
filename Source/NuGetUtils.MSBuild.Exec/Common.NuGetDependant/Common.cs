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
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGetUtils.Lib.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGetUtils.MSBuild.Exec.Common.NuGetDependant
{
   public static class NuGetUtilsCommon
   {
      public static GetFileItemsDelegate CreateMSBuildExecGetFilesDelegate( this NuGetFramework thisFW )
         => ( rGraph, rid, lib, libs ) => GetSuitableFiles( thisFW, rGraph, rid, lib, libs );

      private static IEnumerable<String> GetSuitableFiles(
         NuGetFramework thisFramework,
         Lazy<RuntimeGraph> runtimeGraph,
         String runtimeIdentifier,
         LockFileTargetLibrary targetLibrary,
         Lazy<IDictionary<String, LockFileLibrary>> libraries
         )
      {
         var retVal = NuGetUtility.GetRuntimeAssembliesDelegate( runtimeGraph, runtimeIdentifier, targetLibrary, libraries );
         if ( !retVal.Any() && libraries.Value.TryGetValue( targetLibrary.Name, out var lib ) )
         {

            // targetLibrary does not list stuff like build/net45/someassembly.dll
            // So let's do manual matching
            var fwGroups = lib.Files.Where( f =>
            {
               return f.StartsWith( PackagingConstants.Folders.Build, StringComparison.OrdinalIgnoreCase )
                      && PackageHelper.IsAssembly( f )
                      && Path.GetDirectoryName( f ).Length > PackagingConstants.Folders.Build.Length + 1;
            } ).GroupBy( f =>
            {
               try
               {
                  return NuGetFramework.ParseFolder( f.Split( '/' )[1] );
               }
               catch
               {
                  return null;
               }
            } )
           .Where( g => g.Key != null )
           .Select( g => new FrameworkSpecificGroup( g.Key, g ) );

            var matchingGroup = NuGetFrameworkUtility.GetNearest(
               fwGroups,
               thisFramework,
               g => g.TargetFramework );
            retVal = matchingGroup?.Items;
         }

         return retVal;
      }
   }
}