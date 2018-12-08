using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Frameworks;
using NuGetUtils.Lib.Common;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace NuGetUtils.Common.Tests
{
   [TestClass]
   public class AutomaticFrameworkDetectionTests
   {
      [TestMethod]
      public void TestDetectingNewestFramework()
      {
         var autoDetectedFW = NuGetUtility.TryAutoDetectThisProcessFramework();
         var specifiedFW = NuGetFramework.ParseFolder( Environment.GetEnvironmentVariable( "THIS_TFM" ) );
         //XDocument csProj;
         //using ( var fs = File.Open( Path.GetFullPath( Path.Combine( Environment.GetEnvironmentVariable( "GIT_DIR" ), "Source", "Tests", "Tests.NuGetUtils.Lib.Common", "Tests.NuGetUtils.Lib.Common.csproj" ) ), FileMode.Open, FileAccess.Read, FileShare.Read ) )
         //{
         //   csProj = await XDocument.LoadAsync( fs, LoadOptions.None, default );
         //}

         //var csProjFW = NuGetFramework.ParseFolder( csProj.XPathSelectElement( "/Project/PropertyGroup/TargetFramework" ).Value );

         Assert.AreEqual( specifiedFW, autoDetectedFW, "CSProj and process frameworks must match." );
      }
   }
}
