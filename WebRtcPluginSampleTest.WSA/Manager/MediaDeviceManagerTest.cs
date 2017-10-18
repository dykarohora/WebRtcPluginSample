using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.WebRtc;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;

using WebRtcPluginSample.Manager;
using WebRtcPluginSample.Utilities;

namespace WebRtcPluginSampleTest.WSA.Manager
{
    [TestClass]
    public class MediaDeviceManagerTest
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
        public async Task GetAllDeviceListAndSetDeviceTest()
        {
            var mediaDeviceManager = new MediaDeviceManager();

            await mediaDeviceManager.GetAllDeviceList();

            await Task.Run(() => { mediaDeviceManager.SelectedCamera = mediaDeviceManager.Cameras.FirstOrDefault(); });
            mediaDeviceManager.SelectedMicrophone = mediaDeviceManager.Microphones.FirstOrDefault();
            mediaDeviceManager.SelectedAudioPlayoutDevice = mediaDeviceManager.AudioPlayoutDevices.FirstOrDefault();

            Assert.AreNotEqual(0, mediaDeviceManager.Cameras.Count);
            Assert.AreNotEqual(0, mediaDeviceManager.Microphones.Count);
            Assert.AreNotEqual(0, mediaDeviceManager.AudioPlayoutDevices.Count);
            Assert.AreNotEqual(0, mediaDeviceManager.SupportedResolutions.Count);
            Assert.AreNotEqual(0, mediaDeviceManager.SupportedFpsList.Count);

            Assert.IsNotNull(mediaDeviceManager.SelectedCamera);
            Assert.IsNotNull(mediaDeviceManager.SelectedResolution);
            Assert.IsNotNull(mediaDeviceManager.SelectedFps);
            Assert.IsNotNull(mediaDeviceManager.SelectedMicrophone);
            Assert.IsNotNull(mediaDeviceManager.SelectedAudioPlayoutDevice);
        }

        [TestMethod]
        public async Task GetResolutionTest()
        {
            var mediaDeviceManager = new MediaDeviceManager();

            await mediaDeviceManager.GetAllDeviceList();
            await Task.Run(() => { mediaDeviceManager.SelectedCamera = mediaDeviceManager.Cameras.FirstOrDefault(); });

            var lowestRes = await mediaDeviceManager.GetLowestResolution();
            var highestRes = await mediaDeviceManager.GetHighestResolution();

            Assert.IsNotNull(lowestRes);
            Assert.IsNotNull(highestRes);

            TestContext.WriteLine(lowestRes.ToString());
            TestContext.WriteLine(highestRes.ToString());

            var lowPixelDensity = lowestRes.Width * lowestRes.Height;
            var highPixelDensity = highestRes.Width * highestRes.Height;
            
            Assert.IsTrue(lowPixelDensity <= highPixelDensity);
        }

        [TestMethod]
        public async Task GetResolutionWithoutExpresslySetCamera()
        {
            var mediaDeviceManager = new MediaDeviceManager();

            await mediaDeviceManager.GetAllDeviceList();

            var lowestRes = await mediaDeviceManager.GetLowestResolution();
            var highestRes = await mediaDeviceManager.GetHighestResolution();

            Assert.IsNotNull(lowestRes);
            Assert.IsNotNull(highestRes);

            TestContext.WriteLine(lowestRes.ToString());
            TestContext.WriteLine(highestRes.ToString());

            var lowPixelDensity = lowestRes.Width * lowestRes.Height;
            var highPixelDensity = highestRes.Width * highestRes.Height;

            Assert.IsTrue(lowPixelDensity <= highPixelDensity);
        }

        [TestMethod]
        public async Task FailedGetResoultionTest()
        {
            var mediaDeviceManager = new MediaDeviceManager();

            var lowestRes = await mediaDeviceManager.GetLowestResolution();
            var highestRes = await mediaDeviceManager.GetHighestResolution();

            Assert.IsNull(lowestRes);
            Assert.IsNull(highestRes);
        }

        [TestMethod]
        public async Task GetFpsCapabilityTest()
        {
            var mediaDeviceManager = new MediaDeviceManager();

            await mediaDeviceManager.GetAllDeviceList();
            await Task.Run(() => { mediaDeviceManager.SelectedCamera = mediaDeviceManager.Cameras.FirstOrDefault(); });

            var lowestFps = await mediaDeviceManager.GetLowestFpsCapability();
            var highestFps = await mediaDeviceManager.GetHighestFpsCapability();

            Assert.IsNotNull(lowestFps);
            Assert.IsNotNull(highestFps);

            TestContext.WriteLine(lowestFps.FullDescription);
            TestContext.WriteLine(highestFps.FullDescription);

            Assert.IsTrue(lowestFps.FrameRate <= highestFps.FrameRate);
        }

        [TestMethod]
        public async Task GetFpsCapabilityWithoutExpresslySetCamera()
        {
            var mediaDeviceManager = new MediaDeviceManager();

            await mediaDeviceManager.GetAllDeviceList();

            var lowestFps = await mediaDeviceManager.GetLowestFpsCapability();
            var highestFps = await mediaDeviceManager.GetHighestFpsCapability();

            Assert.IsNotNull(lowestFps);
            Assert.IsNotNull(highestFps);

            TestContext.WriteLine(lowestFps.FullDescription);
            TestContext.WriteLine(highestFps.FullDescription);

            Assert.IsTrue(lowestFps.FrameRate <= highestFps.FrameRate);
        }

        [TestMethod]
        public async Task FailedGetFpsCapabilityTest()
        {
            var mediaDeviceManager = new MediaDeviceManager();

            var lowestFps = await mediaDeviceManager.GetLowestFpsCapability();
            var highestFps = await mediaDeviceManager.GetHighestFpsCapability();

            Assert.IsNull(lowestFps);
            Assert.IsNull(highestFps);
        }

        [TestMethod]
        public async Task SuccessedTrySetResolutionTest()
        {
            var mediaDeviceManager = new MediaDeviceManager();

            await mediaDeviceManager.GetAllDeviceList();
            await Task.Run(() => { mediaDeviceManager.SelectedCamera = mediaDeviceManager.Cameras.FirstOrDefault(); });

            var highestRes = await mediaDeviceManager.GetHighestResolution();

            TestContext.WriteLine("Set Resolution: " + mediaDeviceManager.SelectedResolution.ToString());
            TestContext.WriteLine("Argument Resolution: " + highestRes.ToString());

            var result = await mediaDeviceManager.TrySetResolution(highestRes);

            TestContext.WriteLine("Set Resolution: " + mediaDeviceManager.SelectedResolution.ToString());

            Assert.IsTrue(result);
            Assert.AreEqual(highestRes, mediaDeviceManager.SelectedResolution);
        }

        [TestMethod]
        public async Task FailedTrySetResolutionTest()
        {
            var mediaDeviceManager = new MediaDeviceManager();

            await mediaDeviceManager.GetAllDeviceList();
            await Task.Run(() => { mediaDeviceManager.SelectedCamera = mediaDeviceManager.Cameras.FirstOrDefault(); });

            var expectedRes = mediaDeviceManager.SelectedResolution;
            expectedRes = new Resolution(expectedRes.Width, expectedRes.Height);

            var invalidRes = new Resolution(1, 1);

            TestContext.WriteLine("Set Resolution: " + mediaDeviceManager.SelectedResolution.ToString());

            var result = await mediaDeviceManager.TrySetResolution(invalidRes);

            TestContext.WriteLine("Set Resolution: " + mediaDeviceManager.SelectedResolution.ToString());

            Assert.IsFalse(result);
            Assert.AreEqual(expectedRes, mediaDeviceManager.SelectedResolution);
        }

        [TestMethod]
        public async Task SuccessedTrySetFpsCapabilityTest()
        {
            var mediaDeviceManager = new MediaDeviceManager();
            await mediaDeviceManager.GetAllDeviceList();
            
            await Task.Run(() => { mediaDeviceManager.SelectedCamera = mediaDeviceManager.Cameras.FirstOrDefault(); });

            var res = await mediaDeviceManager.GetLowestResolution();
            await mediaDeviceManager.TrySetResolution(res);

            var lowestFps = await mediaDeviceManager.GetLowestFpsCapability();

            TestContext.WriteLine("Set Fps: " + mediaDeviceManager.SelectedFps.FrameRate + "fps");
            TestContext.WriteLine("Argument Fps: " + lowestFps.FrameRate + "fps");

            var result = await mediaDeviceManager.TrySetFpsCapability(lowestFps);

            TestContext.WriteLine("Set Fps: " + mediaDeviceManager.SelectedFps.FrameRate + "fps");

            Assert.IsTrue(result);
            Assert.AreEqual(lowestFps, mediaDeviceManager.SelectedFps);
        }

        [TestMethod]
        public async Task FailedTrySetFpsCapabilityBecauseDifferenceResolutionTest()
        {
            var mediaDeviceManager = new MediaDeviceManager();
            await mediaDeviceManager.GetAllDeviceList();

            await Task.Run(() => { mediaDeviceManager.SelectedCamera = mediaDeviceManager.Cameras.FirstOrDefault(); });

            var higestFps = await mediaDeviceManager.GetHighestFpsCapability();

            var lowestRes = await mediaDeviceManager.GetLowestResolution();
            await mediaDeviceManager.TrySetResolution(lowestRes);

            var task = await mediaDeviceManager.TrySetFpsCapability(higestFps);

            Assert.IsFalse(task);
        }
    }
}
