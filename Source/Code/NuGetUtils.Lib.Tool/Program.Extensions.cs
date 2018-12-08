/*
 * Copyright 2018 Stanislav Muhametsin. All rights Reserved.
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
using NuGetUtils.Lib.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilPack.Documentation;

// This is in separate file to avoid clash between UtilPack Prepend and System.Linq Prepend.
// Once UtilPack newer than 1.7.0 will be used, this can be used in same file again.

/// <summary>
/// This class contains extension methods for types defined in this assembly.
/// </summary>
public static partial class E_NuGetUtils
{
   /// <summary>
   /// Creates a string which contains help text about how the command line utility is used, given the <see cref="CommandLineDocumentationInfo"/> and information about types which it was created from.
   /// </summary>
   /// <param name="docInfo">This <see cref="CommandLineDocumentationInfo"/>.</param>
   /// <param name="versionSource">The type, the assembly version of which will be used as version information.</param>
   /// <param name="commandLineConfigType">The type of the direct command line configuration.</param>
   /// <param name="configConfigType">The type of the indirect, file-based command line configuration.</param>
   /// <returns>A string which contains help text about how the command line utility is used.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="CommandLineDocumentationInfo"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If any of the <paramref name="versionSource"/>, <paramref name="commandLineConfigType"/>, or <paramref name="configConfigType"/> is <c>null</c>; or if some property of <see cref="CommandLineDocumentationInfo"/> or <see cref="DocumentationGroupInfo"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentException">If some of the <see cref="CommandLineDocumentationInfo"/> or <see cref="DocumentationGroupInfo"/> string properties are empty strings.</exception>
   public static String CreateCommandLineDocumentation(
      this CommandLineDocumentationInfo docInfo,
      Type versionSource,
      Type commandLineConfigType,
      Type configConfigType
      )
   {
      UtilPack.ArgumentValidator.ValidateNotNullReference( docInfo );
      UtilPack.ArgumentValidator.ValidateNotNull( nameof( versionSource ), versionSource );
      UtilPack.ArgumentValidator.ValidateNotNull( nameof( commandLineConfigType ), commandLineConfigType );
      UtilPack.ArgumentValidator.ValidateNotNull( nameof( configConfigType ), configConfigType );


      var execName = UtilPack.ArgumentValidator.ValidateNotEmpty( nameof( docInfo.ExecutableName ), docInfo.ExecutableName );
      var cmdLineInfo = UtilPack.ArgumentValidator.ValidateNotNull( nameof( docInfo.CommandLineGroupInfo ), docInfo.CommandLineGroupInfo );
      var configInfo = UtilPack.ArgumentValidator.ValidateNotNull( nameof( docInfo.ConfigurationFileGroupInfo ), docInfo.ConfigurationFileGroupInfo );
      var cmdLineName = cmdLineInfo.GroupName;
      if ( String.IsNullOrEmpty( cmdLineName ) )
      {
         cmdLineName = "nuget-options";
      }
      var configName = configInfo.GroupName;
      if ( String.IsNullOrEmpty( configName ) )
      {
         configName = "configuration-options";
      }

      var generator = new CommandLineArgumentsDocumentationGenerator();
      return
         $"{execName} version {versionSource.Assembly.GetName().Version} (NuGet version {typeof( NuGet.Common.ILogger ).Assembly.GetName().Version})\n" +
         generator.GenerateParametersDocumentation(
            ( cmdLineInfo.AdditionalGroups ?? UtilPack.Empty<ParameterGroupOrFixedParameter>.Enumerable ).Prepend( new NamedParameterGroup( false, cmdLineName ) ),
            commandLineConfigType,
            execName,
            UtilPack.ArgumentValidator.ValidateNotEmpty( nameof( cmdLineInfo.Purpose ), cmdLineInfo.Purpose ),
            cmdLineName
            )
            + "\n\n\n" +
         generator.GenerateParametersDocumentation( ( configInfo.AdditionalGroups ?? UtilPack.Empty<ParameterGroupOrFixedParameter>.Enumerable ).Prepend( new NamedParameterGroup( false, configName ) ),
            configConfigType,
            execName,
            UtilPack.ArgumentValidator.ValidateNotEmpty( nameof( configInfo.Purpose ), configInfo.Purpose ),
            configName
         );
   }
}
