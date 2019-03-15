# NuGetUtils.Lib.Deployment

This project contains a configuration interface, named `NuGetDeploymentConfiguration`, which contains required information for deploying an assembly within NuGet package, either by copying all dependant assemblies, or by generating `.deps.json` and `.runtimeconfig.json` files.
The extension method for `NuGetDeploymentConfiguration`, called `DeployAsync` will then use the information within configuration to perform actual deployment.

# Distribution

See [NuGet package](http://www.nuget.org/packages/NuGetUtils.Lib.Deployment) for binary distribution.

# TODO

This project needs a refactor: Use the ExtractAssemblyPaths extension method on restorer to find out all assembly paths of the package to be restored.
The current deployment code is quite a mess.
