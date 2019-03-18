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
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec
{
   internal sealed class TaskTypeGenerator
   {
      public static TaskTypeGenerator Instance { get; } = new TaskTypeGenerator();

      private TaskTypeGenerator()
      {

      }

      public TypeGenerationResult GenerateTaskType(
         Boolean isCancelable,
         IEnumerable<InspectionExecutableParameterInfo> inputs,
         IEnumerable<InspectionExecutableParameterInfo> outputs
         )
      {


         var ab = AssemblyBuilder.DefineDynamicAssembly( new AssemblyName( "NuGetTaskWrapperDynamicAssembly" ), AssemblyBuilderAccess.RunAndCollect );
         var mb = ab.DefineDynamicModule( "NuGetTaskWrapperDynamicAssembly.dll"
#if NET46
               , false
#endif
               );
         var tb = mb.DefineType( "NuGetTaskWrapper", TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Public );
         tb.AddInterfaceImplementation( typeof( ITask ) );

         var taskField = tb.DefineField( "_task", typeof( TaskProxy ), FieldAttributes.Private | FieldAttributes.InitOnly );

         // Constructor
         var ctor = tb.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
            CallingConventions.HasThis,
            new Type[] { typeof( TaskProxy ) }
            );
         var il = ctor.GetILGenerator();
         il.Emit( OpCodes.Ldarg_0 );
         il.Emit( OpCodes.Call, typeof( Object ).GetConstructor( new Type[] { } ) );

         il.Emit( OpCodes.Ldarg_0 );
         il.Emit( OpCodes.Ldarg_1 );
         il.Emit( OpCodes.Stfld, taskField );

         il.Emit( OpCodes.Ret );
         // Properties
         var taskRefGetter = typeof( TaskProxy ).GetMethod( nameof( TaskProxy.GetProperty ) ) ?? throw new Exception( "Internal error: no property getter." );
         var taskRefSetter = typeof( TaskProxy ).GetMethod( nameof( TaskProxy.SetProperty ) ) ?? throw new Exception( "Internal error: no property getter." );
         var toStringCall = typeof( Convert ).GetMethod( nameof( Convert.ToString ), new Type[] { typeof( Object ) } ) ?? throw new Exception( "Internal error: no Convert.ToString." );
         var requiredAttribute = typeof( RequiredAttribute ).GetConstructor( new Type[] { } ) ?? throw new Exception( "Internal error: no Required attribute constructor." );
         var outAttribute = typeof( OutputAttribute ).GetConstructor( new Type[] { } ) ?? throw new Exception( "Internal error: no Out attribute constructor." );
         //var beSetter = typeof( ResolverLogger ).GetMethod( nameof( ResolverLogger.TaskBuildEngineSet ) ) ?? throw new Exception( "Internal error: no log setter." );
         //var beReady = typeof( ResolverLogger ).GetMethod( nameof( ResolverLogger.TaskBuildEngineIsReady ) ) ?? throw new Exception( "Internal error: no log state updater." );

         var outPropertyInfos = new List<(String, Type, FieldBuilder)>();
         var interfacePropertyInfos = new List<(String, Type, FieldBuilder)>();

         var properties = inputs
            .Select( p => (p, new TaskPropertyInfo( p.PropertyName, p.TypeName.GetTaskPropertyType(), false, p.IsRequired )) )
            .Concat( outputs.Select( p => (p, new TaskPropertyInfo( p.PropertyName, p.TypeName.GetTaskPropertyType(), true, p.IsRequired )) ) )
            .ToImmutableArray();
         void EmitCastToCorrectType( Type propType )
         {
            if ( Equals( typeof( String ), propType ) )
            {
               il.Emit( OpCodes.Call, toStringCall );
            }
            else
            {
               // Typically ITaskItem[]
               il.Emit( OpCodes.Castclass, propType );
            }
         }

         foreach ( var property in properties
            .Concat( new (InspectionExecutableParameterInfo, TaskPropertyInfo)[] {
               (null, new TaskPropertyInfo( nameof( ITask.HostObject ), typeof( ITaskHost ), false, false )),
               (null, new TaskPropertyInfo( nameof( ITask.BuildEngine ), typeof( IBuildEngine ), false, false ))
            } ) )
         {
            var inspected = property.Item1;
            var propInfo = property.Item2;
            var propType = propInfo.PropertyType;
            var methodAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig;
            var isFromInterface = inspected == null;
            if ( isFromInterface )
            {
               // Virtual is required for class methods implementing interface methods
               methodAttributes |= MethodAttributes.Virtual;
            }
            var propName = propInfo.Name;
            var isOut = propInfo.Output;

            var getter = tb.DefineMethod(
               "get_" + propName,
               methodAttributes
               );
            getter.SetReturnType( propType );
            il = getter.GetILGenerator();

            FieldBuilder propField = null;
            if ( isFromInterface || isOut )
            {
               propField = tb.DefineField( ( isFromInterface ? "_value" : "_out" ) + propName, propType, FieldAttributes.Private );
               il.Emit( OpCodes.Ldarg_0 );
               il.Emit( OpCodes.Ldfld, propField );
               ( isFromInterface ? interfacePropertyInfos : outPropertyInfos ).Add( (propName, propType, propField) );
            }
            else
            {
               il.Emit( OpCodes.Ldarg_0 );
               il.Emit( OpCodes.Ldfld, taskField );
               il.Emit( OpCodes.Ldstr, propName );
               il.Emit( OpCodes.Callvirt, taskRefGetter );
               EmitCastToCorrectType( propType );
            }
            il.Emit( OpCodes.Ret );

            MethodBuilder setter;
            if ( isOut )
            {

               setter = null;
            }
            else
            {
               setter = tb.DefineMethod(
                  "set_" + propName,
                  methodAttributes
                  );
               setter.SetParameters( new Type[] { propType } );
               il = setter.GetILGenerator();
               il.Emit( OpCodes.Ldarg_0 );
               if ( isFromInterface )
               {
                  il.Emit( OpCodes.Ldarg_1 );
                  il.Emit( OpCodes.Stfld, propField );
               }
               else
               {
                  il.Emit( OpCodes.Ldfld, taskField );
                  il.Emit( OpCodes.Ldstr, propName );
                  il.Emit( OpCodes.Ldarg_1 );
                  il.Emit( OpCodes.Callvirt, taskRefSetter );
               }
               il.Emit( OpCodes.Ret );
            }
            var prop = tb.DefineProperty(
               propName,
               PropertyAttributes.None,
               propType,
               Empty<Type>.Array
               );
            prop.SetGetMethod( getter );
            if ( setter != null )
            {
               prop.SetSetMethod( setter );
            }


            if ( propInfo.Required )
            {
               prop.SetCustomAttribute( new CustomAttributeBuilder( requiredAttribute, new Object[] { } ) );
            }
            if ( propInfo.Output )
            {
               prop.SetCustomAttribute( new CustomAttributeBuilder( outAttribute, new Object[] { } ) );
            }
         }
         // Execute method
         var execute = tb.DefineMethod(
            nameof( ITask.Execute ),
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
            typeof( Boolean ),
            new Type[] { }
            );
         il = execute.GetILGenerator();
         var beField = interfacePropertyInfos[1].Item3;

         if ( outPropertyInfos.Count > 0 )
         {
            // try { return this._task.Execute(); } finally { this.OutProperty = this._task.GetProperty( "Out" ); }
            var retValLocal = il.DeclareLocal( typeof( Boolean ) );
            il.Emit( OpCodes.Ldc_I4_0 );
            il.Emit( OpCodes.Stloc, retValLocal );
            il.BeginExceptionBlock();
            il.Emit( OpCodes.Ldarg_0 );
            il.Emit( OpCodes.Ldfld, taskField );
            il.Emit( OpCodes.Ldarg_0 );
            il.Emit( OpCodes.Ldfld, beField );
            il.Emit( OpCodes.Callvirt, typeof( TaskProxy ).GetMethod( nameof( TaskProxy.Execute ) ) );
            il.Emit( OpCodes.Stloc, retValLocal );
            il.BeginFinallyBlock();
            foreach ( var outSetter in outPropertyInfos )
            {
               il.Emit( OpCodes.Ldarg_0 );
               il.Emit( OpCodes.Ldarg_0 );
               il.Emit( OpCodes.Ldfld, taskField );
               il.Emit( OpCodes.Ldstr, outSetter.Item1 );
               il.Emit( OpCodes.Callvirt, taskRefGetter );
               // Interesting thing is that setting some wrongly-typed value to a field works, but then reading the field just throws... I guess they changed PE verification?
               EmitCastToCorrectType( outSetter.Item2 );
               il.Emit( OpCodes.Stfld, outSetter.Item3 );
            }
            il.EndExceptionBlock();

            il.Emit( OpCodes.Ldloc, retValLocal );
         }
         else
         {
            // return this._task.Execute();
            il.Emit( OpCodes.Ldarg_0 );
            il.Emit( OpCodes.Ldfld, taskField );
            il.Emit( OpCodes.Ldarg_0 );
            il.Emit( OpCodes.Ldfld, beField );
            il.Emit( OpCodes.Tailcall );
            il.Emit( OpCodes.Callvirt, typeof( TaskProxy ).GetMethod( nameof( TaskProxy.Execute ) ) );
         }
         il.Emit( OpCodes.Ret );

         // Canceability
         if ( isCancelable )
         {
            tb.AddInterfaceImplementation( typeof( ICancelableTask ) );
            var cancel = tb.DefineMethod(
               nameof( ICancelableTask.Cancel ),
               MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
               typeof( void ),
               new Type[] { }
               );
            var cancelMethod = typeof( TaskProxy ).GetMethod( nameof( TaskProxy.Cancel ) ) ?? throw new Exception( "Internal error: no cancel." );
            il = cancel.GetILGenerator();
            // Call cancel to TaskReferenceHolder which will forward it to actual task
            il.Emit( OpCodes.Ldarg_0 );
            il.Emit( OpCodes.Ldfld, taskField );
            il.Emit( OpCodes.Tailcall );
            il.Emit( OpCodes.Callvirt, cancelMethod );
            il.Emit( OpCodes.Ret );
         }

         // We are ready
         return new TypeGenerationResult( tb.
#if NET46
            CreateType()
#else
            CreateTypeInfo().AsType()
#endif
            , properties.Select( p => p.Item2 ) );
      }
   }

   internal sealed class TypeGenerationResult
   {
      public TypeGenerationResult(
         Type generatedType,
         IEnumerable<TaskPropertyInfo> properties
         )
      {
         this.GeneratedType = ArgumentValidator.ValidateNotNull( nameof( generatedType ), generatedType );
         this.Properties = properties.ToArray();
      }

      public Type GeneratedType { get; }

      public TaskPropertyInfo[] Properties { get; } // Not ImmutableArray since the return type of ITaskFactory.GetTaskParameters is just simple array
   }
}

public static partial class E_NuGetUtils
{
   internal static Boolean IsTaskItemType( this String typeFullName )
   {
      return typeFullName.EndsWith( "[]" );
   }

   internal static Type GetTaskPropertyType( this String typeFullName )
   {
      return typeFullName.IsTaskItemType() ?
         typeof( ITaskItem[] ) :
         typeof( String );
   }
}