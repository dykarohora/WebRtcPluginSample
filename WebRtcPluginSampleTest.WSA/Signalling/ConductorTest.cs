using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebRtcPluginSample.Signalling;
using WebRtcPluginSample.Utilities;

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
        public async Task InitializeTest()
        {
            var conductor = Conductor.Instance;
            await conductor.Initialize();

            var mediaDeviceManager = conductor.MediaDeviceManager;

            Assert.IsNotNull(mediaDeviceManager.SelectedCamera);
            Assert.IsNotNull(mediaDeviceManager.SelectedResolution);
            Assert.IsNotNull(mediaDeviceManager.SelectedFps);
            Assert.IsNotNull(mediaDeviceManager.SelectedMicrophone);
            Assert.IsNotNull(mediaDeviceManager.SelectedAudioPlayoutDevice);

            var selectedResolution = mediaDeviceManager.SelectedResolution;
            Assert.AreEqual(new Resolution(640, 480), selectedResolution);

            var selectedFPS = mediaDeviceManager.SelectedFps;
            var highestFPS = await mediaDeviceManager.GetHighestFpsCapability();
            Assert.AreEqual(selectedFPS, highestFPS);

            var codecManager = conductor.CodecManager;

            var audioCodec = codecManager.SelectedAudioCodec;
            var videoCodec = codecManager.SelectedVideoCodec;

            Assert.AreEqual(audioCodec.Name, "opus");
            Assert.AreEqual(videoCodec.Name, "H264");
        }

        [TestMethod]
        public async Task LoginLogOutTest()
        {
            var conductor = Conductor.Instance;
            await conductor.Initialize();

            await conductor.StartLogin("192.168.0.12", "8888");

            var signaller = conductor.Signaller;

            Assert.IsTrue(signaller.IsConnceted);

            await conductor.DisconnectFromServer();

            Assert.IsFalse(signaller.IsConnceted);

        }
    }
}
