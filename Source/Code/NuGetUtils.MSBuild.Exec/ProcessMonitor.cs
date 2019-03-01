using Newtonsoft.Json;
using NuGetUtils.Lib.Tool.Agnostic;
using NuGetUtils.MSBuild.Exec;
using NuGetUtils.MSBuild.Exec.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace NuGetUtils.MSBuild.Exec
{
   public static class ProcessMonitor
   {

      public static (Process Process, StringBuilder StdOut, StringBuilder StdErr) CreateProcess(
         String fileName,
         String arguments
         )
      {
         var stdout = new StringBuilder();
         var stderr = new StringBuilder();

         return (CreateProcess(
            fileName,
            arguments,
            onStdOutLine: outLine => stdout.Append( outLine ).Append( '\n' ),
            onStdErrLine: errLine => stderr.Append( errLine ).Append( '\n' )
            ), stdout, stderr);
      }

      public static Process CreateProcess(
         String fileName,
         String arguments,
         Action<String> onStdOutLine = null,
         Action<String> onStdErrLine = null
         )
      {
         var startInfo = new ProcessStartInfo()
         {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = onStdOutLine != null,
            RedirectStandardError = onStdErrLine != null,
         };
         var p = new Process()
         {
            StartInfo = startInfo
         };

         if ( startInfo.RedirectStandardOutput )
         {
            p.OutputDataReceived += ( sender, args ) =>
            {
               if ( args.Data is String line ) // Will be null on process shutdown
               {
                  onStdOutLine( line );
               }
            };
         }
         if ( startInfo.RedirectStandardError )
         {
            p.ErrorDataReceived += ( sender, args ) =>
            {
               if ( args.Data is String line ) // Will be null on process shutdown
               {
                  onStdErrLine( line );
               }
            };
         }

         return p;
      }

      public static async Task StartProcessAsync(
         Process p,
         Func<StreamWriter, Task> stdinWriter = null
         )
      {
         p.StartInfo.RedirectStandardInput = stdinWriter != null;
         p.Start();
         p.BeginOutputReadLine();
         p.BeginErrorReadLine();

         // Pass serialized configuration via stdin
         if ( stdinWriter != null )
         {
            using ( var stdin = p.StandardInput )
            {
               await stdinWriter( stdin );
            }
         }
      }

      public static async Task<(Int32 exitCode, StringBuilder StdOut, StringBuilder StdErr)> ExecuteAsFileAtThisPath(
         this String fileName,
         String arguments,
         Func<StreamWriter, Task> stdinWriter = null
         )
      {
         (var p, var stdout, var stderr) = CreateProcess( fileName, arguments );
         using ( p )
         {
            await StartProcessAsync( p, stdinWriter );

            while ( !p.WaitForExit( 0 ) )
            {
               await Task.Delay( 100 );
            }

            // Process.HasExited has following documentation:
            // When standard output has been redirected to asynchronous event handlers, it is possible that output processing will
            // not have completed when this property returns true. To ensure that asynchronous event handling has been completed,
            // call the WaitForExit() overload that takes no parameter before checking HasExited.
            p.WaitForExit();
            while ( !p.HasExited )
            {
               await Task.Delay( 50 );
            }

            return (p.ExitCode, stdout, stderr);
         }
      }
   }

   public static class ProcessMonitorWithGracefulCancelability
   {
      public static async Task<Int32?> ExecuteAsFileAtThisPathWithCancelability(
         this Process process, // Typically created by  ProcessMonitor.CreateProcess(), just don't start it!
                               // String shutdownSemaphoreArgumentName,
         CancellationToken token,
         String shutdownSemaphoreName,
         TimeSpan shutdownSemaphoreMaxWaitTime,
         // Boolean cancelabilityIsOptional = false,
         Func<StreamWriter, Task> stdinWriter = null,
         Func<Task> onTick = null
         )
      {
         Int32? exitCode = null;
         using ( var shutdownSemaphore = token.CanBeCanceled ? ShutdownSemaphoreFactory.CreateSignaller( shutdownSemaphoreName ) : default )
         using ( process ) // var process = ProcessMonitor.CreateProcess( fileName, arguments, onStdOutLine, onStdErrLine ) )
         {
            process.EnableRaisingEvents = true;

            DateTime? shutdownSignalledTime = null;

            Task cancelTask = null;

            void OnCancel()
            {
               try
               {
                  if ( shutdownSemaphore == null )
                  {
                     // Kill the process
                     process.Kill();
                  }
                  else
                  {
                     // Signal the process to shut down
                     shutdownSignalledTime = DateTime.UtcNow;
                     cancelTask = shutdownSemaphore.SignalAsync();
                  }
               }
               catch
               {
                  // Make the main loop stop
                  shutdownSignalledTime = DateTime.UtcNow;
               }
            }

            try
            {
               using ( token.Register( OnCancel ) )
               {
                  await ProcessMonitor.StartProcessAsync( process, stdinWriter: stdinWriter );

                  var hasExited = false;

                  while ( !hasExited )
                  {
                     var tickTask = onTick?.Invoke();
                     if ( tickTask != null )
                     {
                        await tickTask;
                     }

                     if ( process.WaitForExit( 0 ) )
                     {
                        // The process has exited, clean up our stuff

                        // Process.HasExited has following documentation:
                        // When standard output has been redirected to asynchronous event handlers, it is possible that output processing will
                        // not have completed when this property returns true. To ensure that asynchronous event handling has been completed,
                        // call the WaitForExit() overload that takes no parameter before checking HasExited.
                        process.WaitForExit();
                        while ( !process.HasExited )
                        {
                           await Task.Delay( 50 );
                        }

                        hasExited = true;

                        // Now, check if restart semaphore has been signalled
                        // restart = restartSemaphore != null && restartSemaphore.WaitOne( 0 );
                     }
                     else if ( shutdownSignalledTime.HasValue && DateTime.UtcNow - shutdownSignalledTime.Value > shutdownSemaphoreMaxWaitTime )
                     {
                        // We have signalled shutdown, but process has not exited in time
                        try
                        {
                           process.Kill();
                        }
                        catch
                        {
                           // Nothing we can do, really
                           hasExited = true;
                        }
                     }
                     else
                     {
                        // Wait async
                        await Task.Delay( 50 );
                     }
                  }
               }
            }
            finally
            {
               if ( cancelTask != null )
               {
                  await cancelTask;
               }
            }

            try
            {
               exitCode = process.ExitCode;
            }
            catch
            {
               // Ignore
            }

         }

         return exitCode;

      }

      //private static Semaphore CreateSemaphore(
      //   //         String argumentName,
      //   String semaphoreName,
      //   // ref String argsString, 
      //   Boolean isOptional
      //   )
      //{


      //   Semaphore retVal = null;
      //   //if ( !String.IsNullOrEmpty( argumentName ) )
      //   //{
      //   //String semaName = null;
      //   try
      //   {
      //      retVal = CreateSemaphore( semaphoreName );
      //   }
      //   catch
      //   {
      //      // Certain platforms can give "The named version of this synchronization primitive is not supported on this platform." error
      //      if ( !isOptional )
      //      {
      //         throw;
      //      }
      //   }

      //   //if ( retVal != null && !String.IsNullOrEmpty( semaName ) )
      //   //{
      //   //   argsString += " " + EscapeArgumentString( String.Format( "{0}={1}", argumentName, semaName ) );
      //   //}
      //   //}

      //   return retVal;
      //}

      //private static Semaphore CreateSemaphore( String semaphoreName )
      //{
      //   Semaphore retVal;
      //   do
      //   {
      //      semaphoreName = @"Global\" + semaphoreName;
      //      retVal = new Semaphore( 0, Int32.MaxValue, semaphoreName, out var createdNewSemaphore );
      //      if ( !createdNewSemaphore )
      //      {
      //         retVal.DisposeSafely();
      //         retVal = null;
      //      }
      //   } while ( retVal == null );

      //   return retVal;
      //}

      private static String EscapeArgumentString( String argString )
      {
         if ( argString.IndexOf( "\"" ) >= 0 )
         {
            argString = "\"" + argString.Replace( "\"", "\\\"" );
         }

         return argString;
      }

      public static async Task<Int32?> ExecuteAsFileAtThisPathWithCancelabilityAndStandardRedirects(
         this String fileName,
         String arguments,
         CancellationToken token,
         String shutdownSemaphoreName,
         TimeSpan shutdownSemaphoreMaxWaitTime,
         Func<String, Boolean, Task> onStdOutOrErrLine = null,
         Func<StreamWriter, Task> stdinWriter = null
         )
      {
         var processOutput = new ConcurrentQueue<(Boolean IsError, DateTime Timestamp, String Data)>();
         try
         {
            return await ProcessMonitor.CreateProcess(
               fileName,
               arguments,
               onStdOutLine: outLine => processOutput.Enqueue( (false, DateTime.UtcNow, outLine) ),
               onStdErrLine: errLine => processOutput.Enqueue( (true, DateTime.UtcNow, errLine) ) )
               .ExecuteAsFileAtThisPathWithCancelability(
                  token,
                  shutdownSemaphoreName,
                  shutdownSemaphoreMaxWaitTime,
                  stdinWriter: stdinWriter,
                  onTick: async () => await ProcessOutput( processOutput, onStdOutOrErrLine )
               );
         }
         finally
         {
            // Flush any 'leftover' messages
            await ProcessOutput( processOutput, onStdOutOrErrLine );
         }
      }

      private static async Task ProcessOutput(
         ConcurrentQueue<(Boolean IsError, DateTime Timestamp, String Data)> processOutput,
         Func<String, Boolean, Task> onStdOutOrErrLine
         )
      {
         if ( onStdOutOrErrLine == null )
         {
            onStdOutOrErrLine = ( line, isError ) => ( isError ? Console.Error : Console.Out ).WriteLineAsync( line );
         }

         while ( processOutput.TryDequeue( out var output ) )
         {
            var t = onStdOutOrErrLine( output.Data, output.IsError );
            if ( t != null )
            {
               await t;
            }

            //if ( output.IsError )
            //{
            //   await Console.Error.WriteAsync( String.Format( "[{0}] ", output.Timestamp ) );
            //   var oldColor = Console.ForegroundColor;
            //   Console.ForegroundColor = ConsoleColor.Red;
            //   try
            //   {
            //      await Console.Error.WriteAsync( "ERROR" );
            //   }
            //   finally
            //   {
            //      Console.ForegroundColor = oldColor;
            //   }

            //   await Console.Error.WriteLineAsync( String.Format( ": {0}", output.Data ) );
            //}
            //else
            //{
            //   await Console.Out.WriteLineAsync( String.Format( "[{0}]: {1}", output.Timestamp, output.Data ) );
            //}
         }
      }

   }

   internal sealed class NuGetUtilsExecProcessMonitor
   {

      private readonly String _thisAssemblyDirectory;

      public NuGetUtilsExecProcessMonitor()
      {
         this._thisAssemblyDirectory = Path.GetDirectoryName( Path.GetFullPath( new Uri( this.GetType().GetTypeInfo().Assembly.CodeBase ).LocalPath ) );
      }

      // TODO here: similar method as this but redirect stdout and stdin to console instead of string builder

      public async Task<EitherOr<TOutput, String>> CallProcessAndGetResultAsync<TInput, TOutput>(
         String assemblyName,
         TInput input
         )
      {

         (var stdinWriter, var stdinSuccess) = GetStdInWriter( input );
         (var exitCode, var stdout, var stderr) = await
            this.GetProcessFilePath( ref assemblyName )
            .ExecuteAsFileAtThisPath(
               GetProcessArguments( assemblyName ),
               stdinWriter: stdinWriter
            );

         String GetErrorString()
         {
            var errorString = stderr.ToString();
            return String.IsNullOrEmpty( errorString ) ?
               ( exitCode == 0 ? "Unspecified error" : $"Non-zero return code of {assemblyName}" ) :
               errorString;
         }

         return stderr.Length > 0 || !stdinSuccess() || exitCode != 0 ?
            new EitherOr<TOutput, String>( GetErrorString() ) :
            JsonConvert.DeserializeObject<TOutput>( stdout.ToString() );
      }

      public async Task<Int32?> CallProcessAndStreamOutputAsync<TInput>(
         String assemblyName,
         TInput input,
         CancellationToken token,
         String shutdownSemaphoreName,
         TimeSpan shutdownSemaphoreMaxWaitTime,
         Func<String, Boolean, Task> onStdOutOrErrLine
         )
      {
         (var stdinWriter, var stdinSuccess) = GetStdInWriter( input );

         var returnCode = await this.GetProcessFilePath( ref assemblyName )
            .ExecuteAsFileAtThisPathWithCancelabilityAndStandardRedirects(
               GetProcessArguments( assemblyName ),
               token,
               shutdownSemaphoreName,
               shutdownSemaphoreMaxWaitTime,
               stdinWriter: stdinWriter,
               onStdOutOrErrLine: onStdOutOrErrLine
               );

         return stdinSuccess() ?
            returnCode :
            default;
      }

      private String GetProcessFilePath(
         ref String assemblyName
         )
      {
         assemblyName = Path.GetFullPath( Path.Combine( this._thisAssemblyDirectory, assemblyName ) + "." +
#if NET46
            "exe"
#else
            "dll"
#endif
            );

         // If we don't check this here, on .NET Core we will get rather unhelpful "pipe has been closed" error message when writing to stdin later.
         if ( !File.Exists( assemblyName ) )
         {
            throw new InvalidOperationException( $"The target executable process \"{assemblyName}\" does not exist." );
         }

         return
#if NET46
            assemblyName
#else
            "dotnet"
#endif
            ;
      }

      private static String GetProcessArguments(
         String assemblyName
         )
      {
         return
#if !NET46
               assemblyName + " " +
#endif
               $"/{nameof( ConfigurationConfiguration.ConfigurationFileLocation )}={DefaultConfigurationConfiguration.STANDARD_INPUT_OUR_OUTPUT_MARKER}";
      }

      private static (Func<StreamWriter, Task> StdInWriter, Func<Boolean> StdInSuccess) GetStdInWriter<TInput>(
         TInput input
         )
      {
         var stdinSuccess = false;
         return (
            async stdin =>
            {
               try
               {
                  await stdin.WriteAsync( JsonConvert.SerializeObject( input, Formatting.None, new JsonSerializerSettings()
                  {
                     NullValueHandling = NullValueHandling.Ignore
                  } ) );
                  stdinSuccess = true;
               }
               catch
               {
                  // Ignore
               }
            }
         ,
            () => stdinSuccess
            );
      }
   }
}