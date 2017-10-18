using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebRtcPluginSample.Signalling;
using Windows.ApplicationModel.Core;

namespace WebRtcPluginSampleTest.WSA.Signalling
{
    [TestClass]
    public class SignallerTest
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
        public async Task SuccessedConnectTest()
        {
            var signaller = new Signaller();

            await signaller.Connect("192.168.0.12", "8888", "Test");
            Assert.IsTrue(signaller.IsConnceted);
        }

        [TestMethod]
        public async Task FailedConnectTest()
        {
            var signaller = new Signaller();

            await signaller.Connect("hoge.hoge", "20000", "Test");
            Assert.IsFalse(signaller.IsConnceted);
        }

        [TestMethod]
        public async Task SuccessedSignOutTest()
        {
            var signaller = new Signaller();

            await signaller.Connect("192.168.0.12", "8888", "Test");
            Assert.IsTrue(signaller.IsConnceted);

            await signaller.SignOut();
            Assert.IsFalse(signaller.IsConnceted);
        }

        // 以降のテストは別のマシン上でPeerCCなどを使ってシグナリングサーバへログインしてから実行する
        
        [TestMethod]
        public async Task ConfirmPeerListTest()
        {
            var signaller = new Signaller();

            await signaller.Connect("192.168.0.12", "8888", "Test");
            Assert.IsTrue(signaller.IsConnceted);

            Assert.AreNotEqual(0, signaller.Peers.Count);
            TestContext.WriteLine("Peer num: " + signaller.Peers.Count);

            foreach(var peer in signaller.Peers)
            {
                Assert.AreNotEqual(-1, peer.Id);
            }

            await signaller.SignOut();
            Assert.IsFalse(signaller.IsConnceted);
        }

        [TestMethod]
        public async Task SendMessageTest()
        {
            var signaller = new Signaller();

            await signaller.Connect("192.168.0.12", "8888", "Test");
            Assert.IsTrue(signaller.IsConnceted);

            Assert.AreNotEqual(0, signaller.Peers.Count);

            var peer = signaller.Peers.FirstOrDefault();
            
            var result = await signaller.SendToPeer(peer.Id, "testmessage");

            Assert.IsTrue(result);

            await signaller.SignOut();
            Assert.IsFalse(signaller.IsConnceted);
        }
    }
}
