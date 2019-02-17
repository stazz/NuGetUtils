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
using System;

namespace NuGetUtils.Lib.Tool.Agnostic
{
   /// <summary>
   /// This is data interface for configuration which describes the JSON file where the actual configuration should be read from.
   /// </summary>
   public interface ConfigurationConfiguration
   {
      /// <summary>
      /// Gets the path to the JSON file containing the actual configuration.
      /// </summary>
      /// <value>The path to the JSON file containing the actual configuration.</value>
      /// <remarks>Pass <c>-</c> as value to read from stdin.</remarks>
      String ConfigurationFileLocation { get; }
   }

   /// <summary>
   /// This class provides default, mutable, implementation of <see cref="ConfigurationConfiguration"/>.
   /// </summary>
   public class DefaultConfigurationConfiguration : ConfigurationConfiguration
   {
      /// <summary>
      /// Use this string (<c>-</c>) to mark that the file which is being read, or written to, is standard input, or output, respectively.
      /// </summary>
      public const String STANDARD_INPUT_OUR_OUTPUT_MARKER = "=";

      /// <inheritdoc />
      public String ConfigurationFileLocation { get; set; }
   }
}
