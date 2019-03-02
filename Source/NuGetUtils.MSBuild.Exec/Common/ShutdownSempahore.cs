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

namespace NuGetUtils.MSBuild.Exec.Common
{

   public interface ShutdownSemaphoreSignaller : IDisposable
   {
      Task SignalAsync();
   }

   public interface ShutdownSemaphoreAwaiter : IDisposable
   {
      Task WaitForShutdownSignal( CancellationToken token );
   }

   public static class ShutdownSemaphoreFactory
   {
      private const String SEMAPHORE_PREFIX = @"Global\";

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

      public static ShutdownSemaphoreAwaiter CreateAwaiter( String semaphoreName )
      {
         Semaphore semaphore = null;
         try
         {
            Semaphore.TryOpenExisting( SEMAPHORE_PREFIX + semaphoreName, out semaphore );
         }
         catch ( PlatformNotSupportedException )
         {

         }
         return semaphore == null ?
            (ShutdownSemaphoreAwaiter) new AwaiterByFile( GetFilePath( semaphoreName ) ) :
            new AwaiterByWrapper( semaphore );
      }

      private static String GetFilePath( String semaphoreName )
      {
         return Path.Combine( Path.GetTempPath(), "ShutdownFile_" + semaphoreName );
      }

      private static Semaphore CreateSemaphore( String semaphoreName )
      {
         semaphoreName = SEMAPHORE_PREFIX + semaphoreName;
         var retVal = new Semaphore( 0, Int32.MaxValue, semaphoreName, out var createdNewSemaphore );
         if ( !createdNewSemaphore )
         {
            retVal.DisposeSafely();
            throw new ArgumentException( "Semaphore name " + semaphoreName + " already existed." );
         }
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

      private sealed class AwaiterByWrapper : AbstractDisposable, ShutdownSemaphoreAwaiter
      {
         private readonly Semaphore _semaphore;

         public AwaiterByWrapper( Semaphore semaphore )
         {
            this._semaphore = ArgumentValidator.ValidateNotNull( nameof( semaphore ), semaphore );
         }

         public async Task WaitForShutdownSignal( CancellationToken token )
         {
            var sema = this._semaphore;
            while (
               !token.IsCancellationRequested
               && !sema.WaitOne( 0 )
               )
            {
               await Task.Delay( 100 );
            }
         }

         protected override void Dispose( Boolean disposing )
         {
            this._semaphore.DisposeSafely();
         }
      }

      private sealed class AwaiterByFile : AbstractDisposable, ShutdownSemaphoreAwaiter
      {
         private readonly Stream _stream;

         public AwaiterByFile( String filePath )
         {
            this._stream = File.Open( filePath, FileMode.Open, FileAccess.Read, FileShare.Read );
         }

         public Task WaitForShutdownSignal( CancellationToken token )
         {
            // Passing token to stream only checks for cancellation on .Read method entrypoint.
            // Once it goes into native code, the cancellation token cancel request is not registered.
            // So, use our own cancellation mechanism.
            using ( token.Register( () => this.DisposeSafely() ) )
            {
               return this._stream.ReadAtLeastAsync( new Byte[1], 0, 1, 1 );
            }

         }

         protected override void Dispose( Boolean disposing )
         {
            this._stream.DisposeSafely();
         }
      }
   }
}
