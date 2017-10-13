using System;
using System.Collections.Generic;
using System.Text;
using WebRtcPluginSample.Model;
using System.Threading;
using System.Threading.Tasks;

#if NETFX_CORE
using Org.WebRtc;
#endif

namespace WebRtcPluginSample.Signalling
{
    public class Conductor
    {
        private static readonly object _lock = new object();
        private static Conductor _instance;

        public static Conductor Instance {
            get {
                if(_instance == null)
                {
                    lock (_lock)
                    {
                        if(_instance == null)
                        {
                            _instance = new Conductor();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// シグナリング用クライアント
        /// </summary>
        public Signaller Signaller => _signaller;
        private readonly Signaller _signaller;

        /// <summary>
        /// 映像伝送用のコーデック
        /// </summary>
        public CodecInfo VideoCodec { get; set; }

        /// <summary>
        /// 音声伝送用のコーデック
        /// </summary>
        public CodecInfo AudioCodec { get; set; }

        /// <summary>
        /// 使用するカメラの情報
        /// </summary>
        public CaptureCapability VideoCaptureProfile;
        
        // SDPネゴシエーション用の属性
        private static readonly string kCandidateSdpMidName = "sdpMid";
        private static readonly string kCandidateSdpMlineIndexName = "sdpMLineIndex";
        private static readonly string kCandidateSdpName = "candidate";
        private static readonly string kSessionDescriptionTypeName = "type";
        private static readonly string kSessionDescriptionSdpName = "sdp";

        private static readonly string kMessageDataType = "message";

        /// <summary>
        /// Peerコネクションオブジェクト
        /// </summary>
        private RTCPeerConnection _peerConnection;

        /// <summary>
        /// 送信用データチャネル
        /// </summary>
        private RTCDataChannel _peerSendDataChannel;

        /// <summary>
        /// 受信用データチャネル
        /// </summary>
        private RTCDataChannel _peerReceiveDataChannel;

        /// <summary>
        /// メディアデバイスを操作するためのオブジェクト
        /// </summary>
        public Media Media => _media;
        private readonly Media _media;

        public Peer Peer;

        /// <summary>
        /// シグナリングサーバに接続しているリモートユーザのリスト
        /// </summary>
        public List<Peer> Peers;

        /// <summary>
        /// 
        /// </summary>
        private MediaStream _mediaStream;

        /// <summary>
        /// Iceサーバのリスト
        /// </summary>
        private readonly List<RTCIceServer> _iceServers;

        /// <summary>
        /// 通信相手のID
        /// </summary>
        private int _peerId = -1;

        /// <summary>
        /// 自カメラのビデオフレームを送信するかどうか
        /// </summary>
        private bool VideoEnabled;

        /// <summary>
        /// 自マイクの音声を送信するかどうか
        /// </summary>
        private bool AudioEnabled;

        /// <summary>
        /// 
        /// </summary>
        private string SessionId;

        /// <summary>
        /// 
        /// </summary>
        public bool ETWStatsEnabled {
            get => _etwStatsEnabled;
            set {
                _etwStatsEnabled = value;
                if (_peerConnection != null)
                    _peerConnection.EtwStatsEnabled = value;
            }
        }
        private bool _etwStatsEnabled;

        /// <summary>
        /// 
        /// </summary>
        public bool PeerConnectionStatsEnabled {
            get => _peerConnectionStatsEnabled;
            set {
                _peerConnectionStatsEnabled = value;
                if(_peerConnection != null)
                {
                    _peerConnection.ConnectionHealthStatsEnabled = value;
                }
            }
        }
        public bool _peerConnectionStatsEnabled;

        public object MediaLock { get; set; } = new object();

        CancellationTokenSource _connectToPeerCancelationTokeSource;
        Task<bool> _connectToPeerTask;

        #region Event
        /// <summary>
        /// 
        /// </summary>
        public event Action<MediaStreamEvent> OnAddLocalStream;

        public event Action OnPeerConnectionCreated;
        public event Action OnPeerConnectionClosed;
        public event Action OnReadyToConnect;

        public event Action<int, string> OnPeerMessageDataReceived;
        public event Action<int, string> OnPeerDataChannelReceived;

        public event Action<MediaStreamEvent> OnAddRemoteStream;
        public event Action<MediaStreamEvent> OnRemoveRemoteStream;

        public event Action<RTCPeerConnectionHealthStats> OnConnectionHealthStats;
        #endregion

        /// <summary>
        /// シグナリングサーバへの接続
        /// </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="peerName"></param>
        public void StartLogin(string server, string port, string peerName ="")
        {
            
        }

        /// <summary>
        /// WebRTCライブラリにカメラデバイスが使用する解像度とFPSを設定する
        /// </summary>
        public void UpdatePreferredFrameFormat()
        {
            if(VideoCaptureProfile != null)
            {
                WebRTC.SetPreferredVideoCaptureFormat(
                    (int)VideoCaptureProfile.Width, (int)VideoCaptureProfile.Height, (int)VideoCaptureProfile.FrameRate);
            }
        }

        /// <summary>
        /// Iceサーバのリストを更新
        /// </summary>
        /// <param name="iceServers"></param>
        public void ConfigureIceServers(IList<IceServer> iceServers)
        {
            _iceServers.Clear();
            foreach(IceServer iceServer in iceServers)
            {
                string url = "stun:";
                if (iceServer.Type == IceServer.ServerType.TURN) url = "turn:";
                RTCIceServer server = null;
                url += iceServer.Host;
                server = new RTCIceServer { Url = url };
                if (iceServer.Credential != null) server.Credential = iceServer.Credential;
                if (iceServer.Username != null) server.Username = iceServer.Username;
                _iceServers.Add(server);
            }
        }
    }
}
