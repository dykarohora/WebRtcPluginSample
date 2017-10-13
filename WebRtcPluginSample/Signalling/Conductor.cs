using System;
using System.Collections.Generic;
using System.Text;
using WebRtcPluginSample.Model;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

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

        CancellationTokenSource _connectToPeerCancelationTokenSource;
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
            if (_signaller.IsConnceted()) return;
            _signaller.Connect(server, port, peerName == string.Empty ? GetLocalPeerName() : peerName);
        }

        /// <summary>
        /// リモートユーザとの通話を開始する
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public async Task ConnectToPeer(Peer peer)
        {
            Debug.Assert(peer != null);
            Debug.Assert(_peerId == -1);

            // すでに通話を実施している
            if (_peerConnection != null)
            {
                Debug.WriteLine("[Error] Conductor: We only support connecting to one peer at a time");
                return;
            }

            _connectToPeerCancelationTokenSource = new System.Threading.CancellationTokenSource();
            // Peerコネクションの生成、成否が返ってくる
            _connectToPeerTask = CreatePeerConnection(_connectToPeerCancelationTokenSource.Token);
            bool connectResult = await _connectToPeerTask;
            _connectToPeerTask = null;
            _connectToPeerCancelationTokenSource.Dispose();

            if(connectResult)
            {
                _peerId = peer.Id;
                // SDPオファーの作成
                var offer = await _peerConnection.CreateOffer();

                string newSdp = offer.Sdp;

            }
            
        }

        /// <summary>
        /// Peerコネクションをつくる
        /// </summary>
        /// <param name="cancelationToken"></param>
        /// <returns></returns>
        private async Task<bool> CreatePeerConnection(CancellationToken cancelationToken)
        {
            Debug.Assert(_peerConnection == null);
            // タスクがキャンセルされていないか
            if (cancelationToken.IsCancellationRequested) return false;
            // Peerコネクションの設定オブジェクトを作る
            var config = new RTCConfiguration()
            {
                // ICE関連の設定？？
                BundlePolicy = RTCBundlePolicy.Balanced,
                IceTransportPolicy = RTCIceTransportPolicy.All,
                IceServers = _iceServers
            };

            Debug.WriteLine("Conductor: Creating peer connection.");
            _peerConnection = new RTCPeerConnection(config);

            if (_peerConnection == null)
                throw new NullReferenceException("Peer connection is not creating.");

            _peerConnection.EtwStatsEnabled = _etwStatsEnabled;
            _peerConnection.ConnectionHealthStatsEnabled = _peerConnectionStatsEnabled;

            // タスクがキャンセルされていないか
            if (cancelationToken.IsCancellationRequested) return false;

            OnPeerConnectionCreated?.Invoke();

            // Peerコネクションのイベントハンドラを設定

            // 新しいICE候補が見つかった時のハンドラ
            _peerConnection.OnIceCandidate += PeerConnection_OnIceCandidate;
            // リモートユーザのメディアストリームがPeerコネクションに追加されたときのハンドラ
            _peerConnection.OnAddStream += PeerConnection_OnAddStream;
            // リモートユーザのメディアストリームがPeerコネクションから外されたときのハンドラ
            _peerConnection.OnRemoveStream += PeerConnection_OnRemoveStream;
            // 
            _peerConnection.OnConnectionHealthStats += PeerConnection_OnConnectionHealthStats;

            // データチャネルのセットアップ
            _peerSendDataChannel = _peerConnection.CreateDataChannel(
                "SendDataChannel", new RTCDataChannelInit() { Ordered = true });
            // データチャネルがオープンされたときのイベント
            _peerSendDataChannel.OnOpen += PeerSendDataChannel_OnOpen;
            // データチャネルがクローズされたときのイベント
            _peerSendDataChannel.OnClose += PeerDataChannel_OnClose;
            // データチャネル上でエラーが発生したときのイベント
            _peerSendDataChannel.OnError += PeerSendDataChannel_OnError;
            // リモート側でデータチャネルがオープンしたときのイベント
            _peerConnection.OnDataChannel += PeerConnection_OnDataChannel;

            Debug.WriteLine("Conductor+ Getting user media.");
            RTCMediaStreamConstraints mediaStreamConstraints = new RTCMediaStreamConstraints
            {
                audioEnabled = true,
                videoEnabled = true
            };
            // タスクがキャンセルされていないか
            if (cancelationToken.IsCancellationRequested) return false;
            // ローカルメディアストリームの作成
            _mediaStream = await _media.GetUserMedia(mediaStreamConstraints);
            // タスクがキャンセルされていないか
            if (cancelationToken.IsCancellationRequested) return false;
            // Peerコネクションにローカルストリーム
            _peerConnection.AddStream(_mediaStream);
            // イベント発火
            OnAddLocalStream?.Invoke(new MediaStreamEvent() { Stream = _mediaStream });
            // タスクがキャンセルされていないか
            if (cancelationToken.IsCancellationRequested) return false;
            return true;
        }

        // ===========================
        // Constructor
        // ===========================
        private Conductor()
        {
            _signaller = new Signaller();
            _media = Media.CreateMedia();

            Signaller.OnDisconnected += Signaller_OnDisconnected;
            Signaller.OnMessageFromPeer += Signaller_OnMeesageFromPeer;
            Signaller.OnPeerConnected += Signaller_OnPeerConnected;
            Signaller.OnPeerHangup += Signaller_OnPeerHangup;
            Signaller.OnPeerDisconnected += Signaller_OnPeerDisconnected;
            Signaller.OnServerConnectionFailure += Signaller_OnServerConnectionFailed;
            Signaller.OnSignedIn += Signaller_OnSignedIn;
        }

        private void Signaller_OnPeerHangup(int peerId)
        {
            if (peerId != _peerId) return;

            Debug.WriteLine("Conductor: Our peer hung up.");
            ClosePeerConnection();
        }

        private void Signaller_OnSignedIn() { }

        private void Signaller_OnDisconnected()
        {
            ClosePeerConnection();
        }

        private void Signaller_OnPeerConnected(int id, string name) { }

        private void Signaller_OnPeerDisconnected(int peerId)
        {
            if (peerId != _peerId && peerId != 0) return;
            Debug.WriteLine("Conductor: Our peer disconnected.");
            ClosePeerConnection();
        }

        private void Signaller_OnServerConnectionFailed()
        {
            Debug.WriteLine("[Error]: Connection to server failed!");
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
