# NuGetUtils.Lib.AssemblyResolving

This project further expands the restoring-aspect of [NuGetUtils.Lib.Restore](../NuGetUtils.Lib.Restore) project by providing a `NuGetAssemblyResolver` interface as API to load an assembly from a NuGet package.
The instances of `NuGetAssemblyResolver` are created by `NuGetAssemblyResolverFactory`, and the method to create an instance of `NuGetAssemblyResolver` takes somewhat different arguments, depending whether you are using it in .NET Desktop or .NET Core environment.

Even though the creation of `NuGetAssemblyResolver` is not trivial (especially in .NET Desktop, because of AppDomains), once created, it is extremely easy to load an assembly from a NuGet package - and the package will be restored if it is not present on this machine, since `NuGetAssemblyResolver` uses `BoundRestoreCommandUser` class from [NuGetUtils.Lib.Restore](../NuGetUtils.Lib.Restore) project to load NuGet packages.

Furthermore, the `NuGetAssemblyResolver` will take care of also loading any other assemblies that loaded assembly depends on - effectively mapping assembly names into package paths and assemblies within packages.
The libraries using this project can thus forget about grizzly details of dynamically loading assemblies and their dependencies on-the-fly, and simply concentrate on actually using the loaded assemblies to whatever purpose is needed.

# Distribution

See [NuGet package](http://www.nuget.org/packages/NuGetUtils.Lib.AssemblyResolving) for binary distribution.