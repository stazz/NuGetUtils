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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

// On certain platforms (e.g. alpine linux), the global named semaphores are not supported.
// This is why we have to use separate wrapping class for situation when emulating semaphore using files.

namespace NuGetUtils.MSBuild.Exec
{

   public interface ShutdownSemaphoreSignaller : IDisposable
   {
      Task SignalAsync();
   }

   public static class ShutdownSemaphoreFactory
   {
      public static ShutdownSemaphoreSignaller CreateSignaller( String semaphoreName )
      {
         try
         {
            return new SignallerByWrapper( CreateSemaphore( semaphoreName ) );
         }
         catch ( PlatformNotSupportedException )
         {
            return new SignallerByFile( GetFilePath( semaphoreName ) );
         }
      }

      private static String GetFilePath( String semaphoreName )
      {
         return Path.Combine( Path.GetTempPath(), "ShutdownFile_" + semaphoreName );
      }

      private static Semaphore CreateSemaphore( String semaphoreName )
      {
         Semaphore retVal;
         do
         {
            semaphoreName = @"Global\" + semaphoreName;
            retVal = new Semaphore( 0, Int32.MaxValue, semaphoreName, out var createdNewSemaphore );
            if ( !createdNewSemaphore )
            {
               retVal.DisposeSafely();
               retVal = null;
            }
         } while ( retVal == null );

         return retVal;
      }

      private sealed class SignallerByWrapper : AbstractDisposable, ShutdownSemaphoreSignaller
      {
         private readonly Semaphore _semaphore;

         public SignallerByWrapper( Semaphore semaphore )
         {
            this._semaphore = ArgumentValidator.ValidateNotNull( nameof( semaphore ), semaphore );
         }

         public Task SignalAsync()
         {
            this._semaphore.Release();
            return null;
         }

         protected override void Dispose( Boolean disposing )
         {
            if ( disposing )
            {
               this._semaphore.DisposeSafely();
            }
         }
      }

      private sealed class SignallerByFile : AbstractDisposable, ShutdownSemaphoreSignaller
      {
         private readonly String _filePath;
         private readonly Stream _stream;

         public SignallerByFile( String filePath )
         {
            this._filePath = ArgumentValidator.ValidateNotEmpty( nameof( filePath ), filePath );
            this._stream = File.Open( filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read );
         }

         public Task SignalAsync()
         {
            return this._stream.WriteAsync( new Byte[] { 0 }, 0, 1 );
         }

         protected override void Dispose( Boolean disposing )
         {
            if ( disposing )
            {
               this._stream.DisposeSafely();
               File.Delete( this._filePath );
            }
         }
      }
   }
}
