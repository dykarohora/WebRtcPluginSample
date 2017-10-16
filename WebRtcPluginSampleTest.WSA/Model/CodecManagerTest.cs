using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebRtcPluginSample.Model;
using Windows.ApplicationModel.Core;

namespace WebRtcPluginSampleTest.WSA.Model
{
    [TestClass]
    public class CodecManagerTest
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
        public async Task GetAllCodecListAndSetCodecTest()
        {
            var codecManager = new CodecManager();

            await codecManager.GetAudioAndVideoCodecInfo();

            Assert.AreNotEqual(0, codecManager.AudioCodecs.Count);
            Assert.AreNotEqual(0, codecManager.VideoCodecs.Count);

            TestContext.WriteLine("-----Video Codecs-----");
            foreach(var codec in codecManager.VideoCodecs)
            {
                TestContext.WriteLine("Id:" + codec.Id + ", Name:" + codec.Name + ", ClockRate:" + codec.ClockRate);
            }

            TestContext.WriteLine("-----Audio Codecs-----");
            foreach (var codec in codecManager.AudioCodecs)
            {
                TestContext.WriteLine("Id:" + codec.Id + ", Name:" + codec.Name + ", ClockRate:" + codec.ClockRate);
            }

            codecManager.SelectedAudioCodec = codecManager.AudioCodecs.FirstOrDefault();
            codecManager.SelectedVideoCodec = codecManager.VideoCodecs.FirstOrDefault();

            Assert.IsNotNull(codecManager.SelectedAudioCodec);
            Assert.IsNotNull(codecManager.SelectedVideoCodec);
        }

        [TestMethod]
        public async Task SuccessedTrySetVideoCodec()
        {
            var codecManager = new CodecManager();
            await codecManager.GetAudioAndVideoCodecInfo();

            Assert.IsNull(codecManager.SelectedVideoCodec);

            var codecInfo = codecManager.VideoCodecs.FirstOrDefault();

            var result = await codecManager.TrySetVideoCodec(codecInfo.Name);

            Assert.IsTrue(result);
            Assert.IsNotNull(codecManager.SelectedVideoCodec);

            result = await codecManager.TrySetVideoCodec("h264");
            
            Assert.IsTrue(result);
            Assert.IsNotNull(codecManager.SelectedVideoCodec);
        }

        [TestMethod]
        public async Task FailedTrySetVideoCodec()
        {
            var codecManager = new CodecManager();
            await codecManager.GetAudioAndVideoCodecInfo();

            Assert.IsNull(codecManager.SelectedVideoCodec);

            var result = await codecManager.TrySetVideoCodec("hoge");

            Assert.IsFalse(result);
            Assert.IsNull(codecManager.SelectedVideoCodec);
        }

        [TestMethod]
        public async Task SuccessedTrySetAudioCodec()
        {
            var codecManager = new CodecManager();
            await codecManager.GetAudioAndVideoCodecInfo();

            Assert.IsNull(codecManager.SelectedAudioCodec);

            var codecInfo = codecManager.AudioCodecs.FirstOrDefault();

            var result = await codecManager.TrySetAudioCodec(codecInfo.Name, codecInfo.ClockRate);

            Assert.IsTrue(result);
            Assert.IsNotNull(codecManager.SelectedAudioCodec);

            result = await codecManager.TrySetAudioCodec("OPUS", 48000);

            Assert.IsTrue(result);
            Assert.IsNotNull(codecManager.SelectedAudioCodec);
        }

        [TestMethod]
        public async Task FailedTrySetAudioCodec()
        {
            var codecManager = new CodecManager();
            await codecManager.GetAudioAndVideoCodecInfo();

            Assert.IsNull(codecManager.SelectedAudioCodec);

            var result = await codecManager.TrySetAudioCodec("hoge", 1);

            Assert.IsFalse(result);
            Assert.IsNull(codecManager.SelectedAudioCodec);
        }
    }
}
