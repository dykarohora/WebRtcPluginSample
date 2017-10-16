using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebRtcPluginSample.Manager;
using Windows.ApplicationModel.Core;

namespace WebRtcPluginSampleTest.WSA.Manager
{
    [TestClass]
    public class IceServerManagerTest
    {
        private TestContext _testContextInstance;
        public TestContext TestContext {
            get => _testContextInstance;
            set { _testContextInstance = value; }
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext test)
        {
            WebRTC.Initialize(CoreApplication.MainView.CoreWindow.Dispatcher);
        }

        [TestMethod]
        public async Task ConvertIceServersToRTCIceServersTest()
        {
            var iceServerManager = new IceServerManager();

            Assert.IsNotNull(iceServerManager.IceServers);
            Assert.AreEqual(5, iceServerManager.IceServers.Count);

            var result = await iceServerManager.ConvertIceServersToRTCIceServers();

            Assert.AreEqual(iceServerManager.IceServers.Count, result.Count);

            for(int i =0; i<result.Count; ++i)
            {
                var ice = iceServerManager.IceServers[i];
                var rtcIce = result[i];
                Assert.AreEqual("stun:" + ice.Host, rtcIce.Url);
            }
        }
    }
}
