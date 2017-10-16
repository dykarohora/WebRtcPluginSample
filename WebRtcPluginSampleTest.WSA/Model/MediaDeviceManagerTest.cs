using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;

using WebRtcPluginSample.Model;
using System.Diagnostics;

namespace WebRtcPluginSampleTest.WSA.Model
{
    [TestClass]
    public class MediaDeviceManagerTest
    {
        [TestMethod]
        public async Task GetAllDeviceTest()
        {
            WebRTC.Initialize(CoreApplication.MainView.CoreWindow.Dispatcher);
            var media = Media.CreateMedia();

            var mediaDeviceManager = new MediaDeviceManager(media);

            await mediaDeviceManager.GetAllDeviceList();

            await Task.Run(() => { mediaDeviceManager.SelectedCamera = mediaDeviceManager.Cameras.FirstOrDefault(); });
            
            Assert.AreNotEqual(0, mediaDeviceManager.Cameras.Count);
            Assert.AreNotEqual(0, mediaDeviceManager.Microphones.Count);
            Assert.AreNotEqual(0, mediaDeviceManager.AudioPlayoutDevices.Count);
            Assert.AreNotEqual(0, mediaDeviceManager.SupportedResolutions.Count);
            Assert.AreNotEqual(0, mediaDeviceManager.SupportedFpsList.Count);

            Assert.IsNotNull(mediaDeviceManager.SelectedCamera);
            Assert.IsNotNull(mediaDeviceManager.SelectedResolution);
            Assert.IsNotNull(mediaDeviceManager.SelectedFps);


        }
    }
}
