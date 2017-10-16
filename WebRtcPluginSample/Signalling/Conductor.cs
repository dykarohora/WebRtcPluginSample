using System;
using System.Collections.Generic;
using System.Text;
using WebRtcPluginSample.Model;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;
using WebRtcPluginSample.Manager;

#if NETFX_CORE
using Org.WebRtc;
using Windows.Data.Json;
using Windows.Networking.Connectivity;
using Windows.Networking;
using WebRtcPluginSample.Utilities;
#endif

namespace WebRtcPluginSample.Signalling
{
    public class Conductor
    {
        // ===========================
        // Private Member
        // ===========================

        private static readonly object _lock = new object();
        private static Conductor _instance;

        private readonly CoreDispatcher _coreDispatcher;

        private MediaDeviceManager _mediaDeviceManager;
        private CodecManager _codecManager;
        private IceServerManager _iceServerManager;

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

            _coreDispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;

            // _iceServers = new List<RTCIceServer>();

        }

        /// <summary>
        /// 初期化
        /// </summary>
        public async Task Initialize()
        {
            WebRTC.Initialize(_coreDispatcher);

            _mediaDeviceManager = new MediaDeviceManager(_media);
            _codecManager = new CodecManager();
            _iceServerManager = new IceServerManager();

            await Task.WhenAll(
                _mediaDeviceManager.GetAllDeviceList(),
                _codecManager.GetAudioAndVideoCodecInfo());

            var selectCameraTask = Task.Run(() => { _mediaDeviceManager.SelectedCamera = _mediaDeviceManager.Cameras.FirstOrDefault(); });
            _mediaDeviceManager.SelectedMicrophone = _mediaDeviceManager.Microphones.FirstOrDefault();
            _mediaDeviceManager.SelectedAudioPlayoutDevice = _mediaDeviceManager.AudioPlayoutDevices.FirstOrDefault();
            _codecManager.SelectedAudioCodec = _codecManager.AudioCodecs.FirstOrDefault();
            _codecManager.SelectedVideoCodec = _codecManager.VideoCodecs.FirstOrDefault();
            await selectCameraTask;



        }

        /// <summary>
        /// シグナリング用クライアント
        /// </summary>
        public Signaller Signaller => _signaller;
        private readonly Signaller _signaller;


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
        /// ローカルのメディアストリーム
        /// </summary>
        private MediaStream _mediaStream;

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

        public object MediaLock { get; set; } = new object();

        CancellationTokenSource _connectToPeerCancelationTokenSource;
        Task<bool> _connectToPeerTask;

        #region Event
        /// <summary>
        /// Peerコネクションにローカルストリームをセットしたときのイベント
        /// </summary>
        public event Action<MediaStreamEvent> OnAddLocalStream;
        /// <summary>
        /// Peerコネクションを作成したときのイベント
        /// </summary>
        public event Action OnPeerConnectionCreated;
        /// <summary>
        /// 通話を終了してPeerコネクションを破棄したときのイベント
        /// </summary>
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
        /// クライアント名を取得
        /// </summary>
        /// <returns></returns>
        private string GetLocalPeerName()
        {
            var hostname = NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);
            string ret = hostname?.CanonicalName ?? "<unknown host>";
            return ret;
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
                // TODO: Error Handling
                return;
            }

            _connectToPeerCancelationTokenSource = new CancellationTokenSource();
            // Peerコネクションの生成、成否が返ってくる
            _connectToPeerTask = CreatePeerConnection(_connectToPeerCancelationTokenSource.Token);
            bool connectResult = await _connectToPeerTask;
            _connectToPeerTask = null;
            _connectToPeerCancelationTokenSource.Dispose();

            if(connectResult)
            {
                // SDPオファーを作成し、Peerに送信する
                _peerId = peer.Id;
                var offer = await _peerConnection.CreateOffer();
                string newSdp = offer.Sdp;
                SdpUtils.SelectCodecs(ref newSdp, _codecManager.SelectedAudioCodec, _codecManager.SelectedVideoCodec);
                offer.Sdp = newSdp;
                await _peerConnection.SetLocalDescription(offer);
                SendSdp(offer);
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
                // ICEサーバ関連の設定
                BundlePolicy = RTCBundlePolicy.Balanced,
                IceTransportPolicy = RTCIceTransportPolicy.All,
                IceServers = await _iceServerManager.ConvertIceServersToRTCIceServers()
            };

            _peerConnection = new RTCPeerConnection(config);

            if (_peerConnection == null)
                throw new NullReferenceException("Peer connection is not creating.");

            _peerConnection.EtwStatsEnabled = false;
            _peerConnection.ConnectionHealthStatsEnabled = false;

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

        #region PeerConnections EventHandler

        /// <summary>
        /// 新しいICE候補が見つかった時のイベントハンドラ
        /// </summary>
        /// <param name="evt"></param>
        private void PeerConnection_OnIceCandidate(RTCPeerConnectionIceEvent evt)
        {
            if (evt.Candidate == null) return;

            double index = null != evt.Candidate.SdpMLineIndex ? (double)evt.Candidate.SdpMLineIndex : -1;
            JsonObject json;

            json = new JsonObject
            {
                {kCandidateSdpMidName, JsonValue.CreateStringValue(evt.Candidate.SdpMid) },
                {kCandidateSdpMlineIndexName, JsonValue.CreateNumberValue(index) },
                {kCandidateSdpName, JsonValue.CreateStringValue(evt.Candidate.Candidate) }
            };

            Debug.WriteLine("Conductor: Sending ice candidate.\n" + json.Stringify());
            SendMessage(json);
        }

        /// <summary>
        /// リモートユーザのメディアストリームがPeerコネクションに追加されたときのハンドラ
        /// </summary>
        /// <param name="evt"></param>
        private void PeerConnection_OnAddStream(MediaStreamEvent evt)
        {
            OnAddRemoteStream?.Invoke(evt);
        }

        /// <summary>
        /// リモートユーザのメディアストリームがPeerコネクションから外されたときのハンドラ
        /// </summary>
        /// <param name="evt"></param>
        private void PeerConnection_OnRemoveStream(MediaStreamEvent evt)
        {
            OnRemoveRemoteStream?.Invoke(evt);
        }

        #endregion

        /// <summary>
        /// SDPを送信する
        /// </summary>
        /// <param name="description"></param>
        private void SendSdp(RTCSessionDescription description)
        {
            JsonObject json = null;
            json = new JsonObject();
            json.Add(kSessionDescriptionTypeName, JsonValue.CreateStringValue(description.Type.GetValueOrDefault().ToString().ToLower()));
            json.Add(kSessionDescriptionSdpName, JsonValue.CreateStringValue(description.Sdp));

            SendMessage(json);
        }

        /// <summary>
        /// JSON形式のメッセージを送信する
        /// </summary>
        /// <param name="json"></param>
        private void SendMessage(IJsonValue json)
        {
            var task = _signaller.SendToPeer(_peerId, json);
        }

        /// <summary>
        /// 相手との通話を終了する
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectFromPeer() {
            await SendHangupMessage();
            ClosePeerConnection();
        }

        private void ClosePeerConnection()
        {
            lock(MediaLock)
            {
                if(_peerConnection != null)
                {
                    _peerId = -1;
                    if(_mediaStream != null)
                    {
                        foreach(var track in _mediaStream.GetTracks())
                        {
                            if(track != null)
                            {
                                if (track.Enabled)
                                {
                                    track.Stop();
                                }
                                _mediaStream.RemoveTrack(track);
                            }
                        }
                    }
                    _mediaStream = null;

                    if(_peerSendDataChannel != null)
                    {
                        _peerSendDataChannel.Close();
                        _peerSendDataChannel = null;
                    }

                    if(_peerReceiveDataChannel != null)
                    {
                        _peerReceiveDataChannel.Close();
                        _peerReceiveDataChannel = null;
                    }

                    OnPeerConnectionClosed?.Invoke();
                    _peerConnection.Close();

                    SessionId = null;
                    _peerConnection = null;

                    OnReadyToConnect?.Invoke();
                }
            }
        }

        private async Task SendHangupMessage()
        {
            await _signaller.SendToPeer(_peerId, "BYE");
        }


        // ===========================
        // EventHandler
        // ===========================


        #region Signalling client's EventHandler
        /// <summary>
        /// リモートユーザからメッセージを受信したときのハンドラ
        /// SDPオファー/アンサーの処理、ICE受信時の処理を行う
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="message"></param>
        private void Signaller_OnMeesageFromPeer(int peerId, string message)
        {
            Task.Run(async () =>
            {
                Debug.Assert(_peerId == peerId || _peerId == -1);
                Debug.Assert(message.Length > 0);

                // 通話相手からのメッセージではない、かつPeerコネクション生成済みなら何もしない
                if (_peerId != peerId && _peerId != -1)
                {
                    Debug.WriteLine("[Error] Conductor: Received a message from unknown peer while already in a conversation with a different peer.");
                    return;
                }

                JsonObject jMessage;
                // Jsonへのパースが失敗
                if (!JsonObject.TryParse(message, out jMessage))
                {
                    Debug.WriteLine("[Error] Conductor: Received unknown message." + message);
                    return;
                }

                string type = jMessage.ContainsKey(kSessionDescriptionTypeName) ? jMessage.GetNamedString(kSessionDescriptionTypeName) : null;
                // Peerコネクションが生成されていない = まだ通話相手が決まっていない場合
                if (_peerConnection == null)
                {
                    // Peerコネクションを生成する
                    if (!string.IsNullOrEmpty(type))
                    {
                        if (type == "offer" || type == "answer" || type == "json")
                        {
                            Debug.Assert(_peerId == -1);
                            _peerId = peerId;

                            IEnumerable<Peer> enumerablePeer = Peers.Where(x => x.Id == peerId);
                            Peer = enumerablePeer.First();

                            _connectToPeerCancelationTokenSource = new CancellationTokenSource();
                            _connectToPeerTask = CreatePeerConnection(_connectToPeerCancelationTokenSource.Token);
                            bool connectResult = await _connectToPeerTask;
                            _connectToPeerTask = null;
                            _connectToPeerCancelationTokenSource.Dispose();

                            if (!connectResult)
                            {
                                Debug.WriteLine("[Error] Conductor: Failed to initialize our PeerConnection instance");
                                await Signaller.SignOut();
                                return;
                            }
                            else if (_peerId != peerId)
                            {
                                Debug.WriteLine("[Error] Conductor: Received a message from unknown peer while already in a conversation with a different peer.");
                                return;
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[Warn] Conductor: Received an untyped message after closing peer connection.");
                        return;
                    }
                }
                // SDPを受信
                if (_peerConnection != null && !string.IsNullOrEmpty(type))
                {
                    if (type == kMessageDataType)
                    {
                        OnPeerMessageDataReceived?.Invoke(peerId, message);
                    }

                    if (type == "offer-loopback")
                    {
                        Debug.Assert(false);
                    }

                    string sdp = null;
                    // JSONメッセージからSDPを取得する
                    sdp = jMessage.ContainsKey(kSessionDescriptionSdpName) ? jMessage.GetNamedString(kSessionDescriptionSdpName) : null;
                    if (string.IsNullOrEmpty(sdp))
                    {
                        Debug.WriteLine("[Error] Conductor: Can't parse received session description message.");
                        return;
                    }

                    RTCSdpType messageType = RTCSdpType.Offer;
                    switch (type)
                    {
                        case "offer": messageType = RTCSdpType.Offer; break;
                        case "answer": messageType = RTCSdpType.Answer; break;
                        case "preanswer": messageType = RTCSdpType.Pranswer; break;
                        default: Debug.Assert(false, type); break;
                    }

                    Debug.WriteLine("Conductor: Received session description: " + message);
                    // Peerコネクションに受信したSDPをリモートSDPとしてセット
                    await _peerConnection.SetRemoteDescription(new RTCSessionDescription(messageType, sdp));
                    // 受信したメッセージがOfferであった場合はSDPアンサーを返信する
                    if (messageType == RTCSdpType.Offer)
                    {
                        var answer = await _peerConnection.CreateAnswer();
                        await _peerConnection.SetLocalDescription(answer);
                        SendSdp(answer);
                    }
                }
                else
                // ICEを受信
                {
                    RTCIceCandidate candidate = null;
                    var sdpMid = jMessage.ContainsKey(kCandidateSdpMidName)
                            ? jMessage.GetNamedString(kCandidateSdpMidName)
                            : null;
                    var sdpMlineIndex = jMessage.ContainsKey(kCandidateSdpMlineIndexName)
                        ? jMessage.GetNamedNumber(kCandidateSdpMlineIndexName)
                        : -1;
                    var sdp = jMessage.ContainsKey(kCandidateSdpName)
                        ? jMessage.GetNamedString(kCandidateSdpName)
                        : null;
                    if (string.IsNullOrEmpty(sdpMid) || sdpMlineIndex == -1 || string.IsNullOrEmpty(sdp))
                    {
                        Debug.WriteLine("[Error] Conductor: Can't parse received message.\n" + message);
                        return;
                    }
                    candidate = new RTCIceCandidate(sdp, sdpMid, (ushort)sdpMlineIndex);
                    await _peerConnection.AddIceCandidate(candidate);
                    Debug.WriteLine("Conductor: Received candidate : " + message);

                }
            }).Wait();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peerId"></param>
        private void Signaller_OnPeerHangup(int peerId)
        {
            if (peerId != _peerId) return;

            Debug.WriteLine("Conductor: Our peer hung up.");
            ClosePeerConnection();
        }

        /// <summary>
        /// シグナリングサーバへの接続が完了したときのイベントハンドラ
        /// </summary>
        private void Signaller_OnSignedIn() { }

        /// <summary>
        /// シグナリングサーバからログアウトしたときのイベントハンドラ
        /// </summary>
        private void Signaller_OnDisconnected()
        {
            ClosePeerConnection();
        }

        /// <summary>
        /// リモートユーザがシグナリングサーバに接続してきたときのイベントハンドラ
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        private void Signaller_OnPeerConnected(int id, string name) { }

        /// <summary>
        /// リモートユーザがシグナリングサーバからログアウトしたときのイベントハンドラ
        /// </summary>
        /// <param name="peerId"></param>
        private void Signaller_OnPeerDisconnected(int peerId)
        {
            if (peerId != _peerId && peerId != 0) return;
            Debug.WriteLine("Conductor: Our peer disconnected.");
            ClosePeerConnection();
        }

        /// <summary>
        /// シグナリングサーバへのせつぞくが失敗したときのイベントハンドラ
        /// </summary>
        private void Signaller_OnServerConnectionFailed()
        {
            Debug.WriteLine("[Error]: Connection to server failed!");
        }

        #endregion

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

        public void EnableLocalVideoStream()
        {
            lock(MediaLock)
            {
                if(_mediaStream != null)
                {
                    foreach(MediaVideoTrack videoTrack in _mediaStream.GetVideoTracks())
                    {
                        videoTrack.Enabled = true;
                    }
                }
                VideoEnabled = true;
            }
        }

        public void DisableLocalVideoStream()
        {
            lock(MediaLock)
            {
                if(_mediaStream != null)
                {
                    foreach(MediaVideoTrack videoTrack in _mediaStream.GetVideoTracks())
                    {
                        videoTrack.Enabled = false;
                    }
                }
                VideoEnabled = false;
            }
        }

        public void UnmuteMicrophone()
        {
            if(_mediaStream != null)
            {
                foreach(MediaAudioTrack audioTrack in _mediaStream.GetAudioTracks())
                {
                    audioTrack.Enabled = true;
                }
            }
            AudioEnabled = true;
        }

        public void MuteMicrophone()
        {
            if(_mediaStream != null)
            {
                foreach(MediaAudioTrack audioTrack in _mediaStream.GetAudioTracks())
                {
                    audioTrack.Enabled = false;
                }
            } AudioEnabled = false;
        }
    }
}
