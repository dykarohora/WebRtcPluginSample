using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebRtcPluginSample.Signalling;

namespace WebRtcPluginSampleTest.WSA.Signalling
{
    [TestClass]
    public class ConductorTest
    {
        private TestContext _testContextInstance;
        public TestContext TestContext {
            get => _testContextInstance;
            set { _testContextInstance = value; }
        }

        [TestMethod]
        public async Task ConductorInitializeTest()
        {
            var conductor = Conductor.Instance;
            await conductor.Initialize();

            Assert.IsNotNull(conductor.MediaDeviceManager);
            Assert.IsNotNull(conductor.CodecManager);
            Assert.IsNotNull(conductor.IceServerManager);

            TestContext.WriteLine("Camera: " + conductor.MediaDeviceManager.Cameras.Count);
            TestContext.WriteLine("Microphoe: " + conductor.MediaDeviceManager.Microphones.Count);
            TestContext.WriteLine("Speeker: " + conductor.MediaDeviceManager.AudioPlayoutDevices.Count);

            conductor.StartLogin("localhost", "8888");
            await Task.Delay(10000);
            Assert.IsTrue(conductor.Signaller.IsConnceted());
        }
    }
}
