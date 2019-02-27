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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetUtils.MSBuild.Exec;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Tests.NuGetUtils.MSBuild.Exec
{
   [TestClass]
   public class TaskFactoryTests
   {
      [ClassInitialize]
      public static void SetUpOtherProcesses(
         TestContext unused // Must be present, otherwise exception is thrown indicating wrong method signature.
         )
      {
         // When running non-self-contained processes via dotnet, we must make sure that .runtimeconfig.json file is present
         // Having projects as references does not do that, so we have to do it other way.
         // One such way is just simply copy the .runtimeconfig.json file of this project, and rename as appropriate
         var thisAssemblyPath = Path.GetFullPath( new Uri( typeof( TaskFactoryTests ).GetTypeInfo().Assembly.CodeBase ).LocalPath );
         var thisAssemblyDirectory = Path.GetDirectoryName( thisAssemblyPath );
         var thisAssemblyRuntimeConfig = Path.ChangeExtension( thisAssemblyPath, ".runtimeconfig.json" );
         foreach ( var execName in new[] { "Discover", "Inspect", "Perform" } )
         {
            File.Copy( thisAssemblyRuntimeConfig, Path.Combine( thisAssemblyDirectory, "NuGetUtils.MSBuild.Exec." + execName + ".runtimeconfig.json" ), true );
         }
      }

      [TestMethod]
      public void TestSimpleUseCase()
      {
         //global::NuGetUtils.MSBuild.Exec.Discover.Program.Main( new[] { "/ConfigurationFileLocation=-" } ).GetAwaiter().GetResult();

         var factory = new NuGetExecutionTaskFactory();
         Assert.IsTrue( factory.Initialize(
            null,
            null,
            new XElement( "TaskBody",
               new XElement( "PackageID", "NuGetUtils.MSBuild.Exec.TestPackage" ),
               new XElement( "PackageVersion", "1.0.0" ),
               new XElement( "EntryPointTypeName", "NuGetUtils.MSBuild.Exec.TestPackage.EntryPoints" ),
               new XElement( "EntryPointMethodName", "Echo" )
               ).ToString(),
            null
            ) );

         const String TEST_VALUE = "Testing";
         var task = factory.CreateTask( null );
         var property = task.GetType().GetRuntimeProperty( "Value" );
         property.SetMethod.Invoke( task, new[] { TEST_VALUE } );
         Assert.IsTrue( task.Execute() );
         Assert.AreEqual( TEST_VALUE, property.GetMethod.Invoke( task, null ) );

      }
   }
}
