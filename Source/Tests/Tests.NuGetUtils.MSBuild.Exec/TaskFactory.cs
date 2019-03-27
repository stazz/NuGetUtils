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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using UtilPack;
using ITaskItem = Microsoft.Build.Framework.ITaskItem;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

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

         // But first, copy everything under 'tools' folder, since that's how it is in actual production environment
         var thisAssemblyPath = Path.GetFullPath( new Uri( typeof( TaskFactoryTests ).GetTypeInfo().Assembly.CodeBase ).LocalPath );
         var thisAssemblyDirectory = Path.GetDirectoryName( thisAssemblyPath );
         var toolsDir = Path.Combine( thisAssemblyDirectory, NuGetExecutionTaskFactory.TOOLS_DIR );
         if ( !Directory.Exists( toolsDir ) )
         {
            Directory.CreateDirectory( toolsDir );
         }
         foreach ( var fn in Directory.EnumerateFiles( thisAssemblyDirectory, "*.dll", SearchOption.TopDirectoryOnly ) )
         {
            File.Copy( fn, Path.Combine( toolsDir, Path.GetFileName( fn ) ), true );
         }

         var thisAssemblyRuntimeConfig = Path.ChangeExtension( thisAssemblyPath, ".runtimeconfig.json" );
         foreach ( var execName in new[] { "Discover", "Inspect", "Perform" } )
         {
            File.Copy( thisAssemblyRuntimeConfig, Path.Combine( toolsDir, "NuGetUtils.MSBuild.Exec." + execName + ".runtimeconfig.json" ), true );
         }
      }
      const String TEST_VALUE = "Testing";

      [TestMethod]
      public void TestSimpleUseCase()
      {
         PerformTestPackageTest(
            "Echo",
            ( task, ignored ) =>
            {
               task.GetType().GetRuntimeProperty( "Value" ).SetMethod.Invoke( task, new[] { TEST_VALUE } );
               Assert.IsTrue( task.Execute() );
               var outputProperty = task.GetType().GetRuntimeProperty( "Result" );
               Assert.AreEqual( TEST_VALUE, outputProperty.GetMethod.Invoke( task, null ) );
            } );
      }

      [TestMethod]
      public void TestTaskItemSpecEcho()
      {
         PerformTestPackageTest(
            "EchoTaskItemSpec",
            ( task, ignored ) =>
            {
               var input = new TaskItem[] { new TaskItem( TEST_VALUE ) };
               task.GetType().GetRuntimeProperty( "Value" ).SetMethod.Invoke( task, new Object[] { input } );
               Assert.IsTrue( task.Execute() );
               var outputProperty = task.GetType().GetRuntimeProperty( "Result" );
               Assert.IsTrue( ArrayEqualityComparer<ITaskItem>.ArrayEquality( input, (ITaskItem[]) outputProperty.GetMethod.Invoke( task, null ), ( t1, t2 ) => String.Equals( t1.ItemSpec, t2.ItemSpec ) ) );
            } );
      }

      [TestMethod]
      public void TestTaskItemWithMetaDataEcho()
      {
         PerformTestPackageTest(
            "EchoTaskItemWithMetaData",
            ( task, ignored ) =>
            {
               const String MD1 = "MD1";
               const String MD2 = "MD2";

               var input = new TaskItem[] { new TaskItem( TEST_VALUE, new Dictionary<String, String>() { { "MetaData1", MD1 }, { "MetaData2", MD2 } } ) };
               task.GetType().GetRuntimeProperty( "Value" ).SetMethod.Invoke( task, new Object[] { input } );
               Assert.IsTrue( task.Execute() );
               var outputProperty = task.GetType().GetRuntimeProperty( "Result" );
               Assert.IsTrue( ArrayEqualityComparer<ITaskItem>.ArrayEquality( input, (ITaskItem[]) outputProperty.GetMethod.Invoke( task, null ), TaskItemEquality ) );
            } );
      }

      [TestMethod, Timeout( 30000 )]
      public async Task TestCancellation()
      {
         await PerformTestPackageTest(
            "Neverending",
            async ( task, taskProxy ) =>
            {
               var cancellationTokenSource = (CancellationTokenSource) taskProxy.GetType().GetTypeInfo().DeclaredFields.First( f => String.Equals( f.Name, "_cancellationTokenSource" ) ).GetValue( taskProxy );
               var executeTask = taskProxy.ExecuteAsync( null );
               cancellationTokenSource.CancelAfter( 10000 );
               Assert.IsFalse( await executeTask );
            } );
      }

      private static void PerformTestPackageTest(
         String entrypointMethod,
         Action<Microsoft.Build.Framework.ITask, TaskProxy> useTask
         ) => PerformTestPackageTest<Object>( entrypointMethod, ( task, proxy ) => { useTask( task, proxy ); return null; } );


      private static T PerformTestPackageTest<T>(
         String entrypointMethod,
         Func<Microsoft.Build.Framework.ITask, TaskProxy, T> useTask
         )
      {
         var factory = new NuGetExecutionTaskFactory();
         Assert.IsTrue( factory.Initialize(
            null,
            null,
            new XElement( "TaskBody",
               new XElement( "PackageID", "NuGetUtils.MSBuild.Exec.TestPackage" ),
               new XElement( "PackageVersion", "1.0.0" ),
               new XElement( "EntryPointTypeName", "NuGetUtils.MSBuild.Exec.TestPackage.EntryPoints" ),
               new XElement( "EntryPointMethodName", entrypointMethod )
               ).ToString(),
            null
            ) );

         var task = factory.CreateTask( null );
         return useTask( task, (TaskProxy) task.GetType().GetTypeInfo().DeclaredFields.First( f => String.Equals( f.Name, "_task" ) ).GetValue( task ) );
      }

      private static Boolean TaskItemEquality( Microsoft.Build.Framework.ITaskItem t1, Microsoft.Build.Framework.ITaskItem t2 )
      {
         return String.Equals( t1.ItemSpec, t2.ItemSpec )
            && DictionaryEqualityComparer<String, String>.DefaultEqualityComparer.Equals(
               t1.MetadataNames.OfType<String>().ToDictionary( m1 => m1, m1 => t1.GetMetadata( m1 ) ),
               t2.MetadataNames.OfType<String>().ToDictionary( m1 => m1, m1 => t2.GetMetadata( m1 ) )
               );
      }
   }
}
