# NuGetUtils.Lib.Deployment

This project contains a configuration interface, named `NuGetDeploymentConfiguration`, which contains required information for deploying an assembly within NuGet package, either by copying all dependant assemblies, or by generating `.deps.json` and `.runtimeconfig.json` files.
The extension method for `NuGetDeploymentConfiguration`, called `DeployAsync` will then use the information within configuration to perform actual deployment.

# Distribution

See [NuGet package](http://www.nuget.org/packages/NuGetUtils.Lib.Deployment) for binary distribution.