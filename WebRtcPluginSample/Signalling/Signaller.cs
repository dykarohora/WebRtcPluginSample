using System;
using System.Collections.Generic;
using System.Text;

namespace WebRtcPluginSample.Signalling
{
    public delegate void SignedInDelegate();
    public delegate void DisconnectedDelegate();
    public delegate void PeerConnectedDelegate(int id, string name);
    public delegate void PeerDisconnectedDelegate(int peerId);
    public delegate void PeerHangupDelegate(int peerId);
    public delegate void MessageFromPeerDelegate(int peerId, string message);
    public delegate void MessageSentDelegate(int err);
    public delegate void ServerConnectionFailureDelegate();

    public class Signaller
    {
        public event SignedInDelegate OnSignedIn;
        public event DisconnectedDelegate OnDisconnected;
        public event PeerConnectedDelegate OnPeerConnected;         // リモートユーザがシグナリングサーバに接続してきたときのイベント
        public event PeerDisconnectedDelegate OnPeerDisconnected;   // リモートユーザがシグナリングサーバからログアウトしたときのイベント
        public event PeerHangupDelegate OnPeerHangup;
        public event MessageFromPeerDelegate OnMessageFromPeer;
        public event ServerConnectionFailureDelegate OnServerConnectionFailure;
    }
}
