using System;

namespace NuGetUtils.Lib.Restore.Agnostic
{
   /// <summary>
   /// This is data interface for configuration which deals with usecase of creating <see cref="T:NuGetUtils.Lib.Restore.BoundRestoreCommandUser"/> and using it somehow.
   /// </summary>
   /// <typeparam name="TLogLevel">The type of <see cref="LogLevel"/> property. Typically is <see cref="T:NuGet.Common.LogLevel"/>.</typeparam>
   /// <remarks>All properties are optional.</remarks>
   public interface NuGetUsageConfiguration<out TLogLevel>
   {
      /// <summary>
      /// Gets the path to NuGet configuration file.
      /// </summary>
      /// <value>The path to NuGet configuration file.</value>
      String NuGetConfigurationFile { get; }

      /// <summary>
      /// Gets the name of the NuGet framework that restoring is performed against.
      /// </summary>
      /// <value>The name of the NuGet framework that restoring is performed against.</value>
      String RestoreFramework { get; }

      /// <summary>
      /// Gets the name of the runtime identifier to use when performing restoring.
      /// </summary>
      /// <value>The name of the runtime identifier to use when performing restoring.</value>
      String RestoreRuntimeID { get; }

      /// <summary>
      /// Gets the lock file cache directory path.
      /// </summary>
      /// <value>The lock file cache directory path.</value>
      String LockFileCacheDirectory { get; }

      /// <summary>
      /// Gets the SDK NuGet package ID.
      /// </summary>
      /// <value>The SDK NuGet package ID.</value>
      String SDKFrameworkPackageID { get; }

      /// <summary>
      /// Gets the SDK NuGet package version.
      /// </summary>
      /// <value>The SDK NuGet package version.</value>
      String SDKFrameworkPackageVersion { get; }

      /// <summary>
      /// Gets the value indicating whether to disable caching <see cref="T:NuGet.ProjectModel.LockFile"/>s to disk.
      /// </summary>
      /// <value>The value indicating whether to disable caching <see cref="T:NuGet.ProjectModel.LockFile"/>s to disk.</value>
      Boolean DisableLockFileCache { get; }

      /// <summary>
      /// Gets the <see cref="T:NuGet.Common.LogLevel"/> for NuGet logger.
      /// </summary>
      /// <value>the <see cref="T:NuGet.Common.LogLevel"/> for NuGet logger.</value>
      TLogLevel LogLevel { get; }

      /// <summary>
      /// Gets the value indicating whether to disable logging altogether.
      /// </summary>
      /// <value>The value indicating whether to disable logging altogether.</value>
      Boolean DisableLogging { get; }
   }
}
