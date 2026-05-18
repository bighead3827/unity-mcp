using System.Linq;
using MCPForUnity.Editor.Services;
using NUnit.Framework;

namespace MCPForUnityTests.Editor.Services
{
    [TestFixture]
    public class ConfigureDetectedClientsTests
    {
        [Test]
        public void Summary_ContainsOnlyInstalledClients()
        {
            var svc = new ClientConfigurationService();
            var summary = svc.ConfigureAllDetectedClients();
            int installedCount = svc.GetAllClients().Count(c => c.IsInstalled);
            Assert.AreEqual(installedCount, summary.SuccessCount + summary.FailureCount,
                "Only installed clients should appear in success/failure totals");
        }

        [Test]
        public void Summary_SkippedCountTracksUninstalled()
        {
            var svc = new ClientConfigurationService();
            var summary = svc.ConfigureAllDetectedClients();
            int uninstalledCount = svc.GetAllClients().Count(c => !c.IsInstalled);
            Assert.AreEqual(uninstalledCount, summary.SkippedCount);
        }
    }
}
