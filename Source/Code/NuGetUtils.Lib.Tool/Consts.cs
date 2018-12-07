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
using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetUtils.Lib.Tool
{
   /// <summary>
   /// This is type parameterless class to hold some string constants used by <see cref="NuGetRestoringProgram{TCommandLineConfiguration, TConfigurationConfiguration}"/>.
   /// </summary>
   public static class NuGetRestoringProgramConsts
   {
      /// <summary>
      /// This is the default environment variable name that is used when trying to deduce lock file cache directory.
      /// </summary>
      public const String LOCK_FILE_CACHE_DIR_ENV_NAME = "NUGET_UTILS_CACHE_DIR";

      /// <summary>
      /// This is the default directory name within home directory which will hold the lock file cache directory.
      /// </summary>
      public const String LOCK_FILE_CACHE_DIR_WITHIN_HOME_DIR = ".nuget-utils-cache";
   }
}
