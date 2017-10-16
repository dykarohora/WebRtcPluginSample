using System;
using System.Collections.Generic;
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
        private object _mediaLock = new object();

        private static Conductor _instance;

        private readonly CoreDispatcher _coreDispatcher;

        // 各種マネージャ
        private MediaDeviceManager _mediaDeviceManager;
        private CodecManager _codecManager;
        private IceServerManager _iceServerManager;

        /// <summary>
        /// Peerコネクションオブジェクト
        /// </summary>
        private RTCPeerConnection _peerConnection;

        /// <summary>
        /// ローカルのメディアストリーム
        /// </summary>
        private MediaStream _mediaStream;

        /// <summary>
        /// 通信相手のID
        /// </summary>
        private int _peerId = -1;

        /// <summary>
        /// メディアデバイスを操作するためのオブジェクト
        /// </summary>
        private readonly Media _media;

        // SDPネゴシエーション用の属性
        private static readonly string kCandidateSdpMidName = "sdpMid";
        private static readonly string kCandidateSdpMlineIndexName = "sdpMLineIndex";
        private static readonly string kCandidateSdpName = "candidate";
        private static readonly string kSessionDescriptionTypeName = "type";
        private static readonly string kSessionDescriptionSdpName = "sdp";
        private static readonly string kMessageDataType = "message";

        private CancellationTokenSource _connectToPeerCancelationTokenSource;
        private Task<bool> _connectToPeerTask;

        // ===========================
        // Properties
        // ===========================

        /// <summary>
        /// シングルトン
        /// </summary>
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
        internal Signaller Signaller => _signaller;
        private readonly Signaller _signaller;

        /// <summary>
        /// ローカルのビデオストリームを有効化するかどうか
        /// </summary>
        public bool IsEnableVideo {
            get => _isEnabledVideo;
            set {
                if(_isEnabledVideo != value)
                {
                    lock(_mediaLock)
                    {
                        if(_mediaStream != null)
                        {
                            foreach(var videoTrack in _mediaStream.GetVideoTracks())
                            {
                                videoTrack.Enabled = value;
                            }
                        }
                    }
                    _isEnabledVideo = value;
                }
            }
        }
        private bool _isEnabledVideo = false;

        /// <summary>
        /// ローカルのオーディオストリームを有効化するかどうか
        /// </summary>
        public bool IsEnabledAudio {
            get => _isEnabledAudio;
            set {
                if(_isEnabledAudio != value)
                {
                    lock(_mediaLock)
                    {
                        if(_mediaStream != null)
                        {
                            foreach(var audioTrack in _mediaStream.GetAudioTracks())
                            {
                                audioTrack.Enabled = value;
                            }
                        }
                    }
                    _isEnabledAudio = value;
                }
            }
        }
        private bool _isEnabledAudio = false;

        // ===========================
        // Constructor
        // ===========================
        private Conductor()
        {
            _signaller = new Signaller();
            _media = Media.CreateMedia();

            Signaller.OnDisconnected += Signaller_OnDisconnected;
            Signaller.OnMessageFromPeer += Signaller_OnMeesageFromPeer;
            Signaller.OnPeerHangup += Signaller_OnPeerHangup;
            Signaller.OnPeerDisconnected += Signaller_OnPeerDisconnected;

            _coreDispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
        }

        internal Peer Peer;

        /// <summary>
        /// シグナリングサーバに接続しているリモートユーザのリスト
        /// </summary>
        internal List<Peer> Peers;

        // ===========================
        // Event
        // ===========================
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

        /// <summary>
        /// Peerとの通話する準備がととのったときのイベント
        /// </summary>
        public event Action OnReadyToConnect;

        public event Action<int, string> OnPeerMessageDataReceived;

        public event Action<MediaStreamEvent> OnAddRemoteStream;
        public event Action<MediaStreamEvent> OnRemoveRemoteStream;
        #endregion

        // ===========================
        // Public Method
        // ===========================

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
        /// 相手との通話を終了する
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectFromPeer() {
            await _signaller.SendToPeer(_peerId, "BYE");
            ClosePeerConnection();
        }

        // ===========================
        // Helper Method
        // ===========================

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
            IsEnableVideo = true;
            IsEnabledAudio = true;
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
        /// Peerコネクションの破棄
        /// </summary>
        private void ClosePeerConnection()
        {
            lock(_mediaLock)
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

                    OnPeerConnectionClosed?.Invoke();
                    _peerConnection.Close();
                    _peerConnection = null;

                    OnReadyToConnect?.Invoke();
                }
            }
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
                    // TODO: Error Handling
                    return;
                }

                JsonObject jMessage;
                // Jsonへのパースが失敗
                if (!JsonObject.TryParse(message, out jMessage))
                {
                    // TODO: Error Handling
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
                            bool connectResult = await _connectToPeerTask.ConfigureAwait(false);
                            _connectToPeerTask = null;
                            _connectToPeerCancelationTokenSource.Dispose();

                            if (!connectResult)
                            {
                                await Signaller.SignOut();
                                return;
                            }
                            else if (_peerId != peerId)
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
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
                    await _peerConnection.SetRemoteDescription(new RTCSessionDescription(messageType, sdp)).AsTask().ConfigureAwait(false);
                    // 受信したメッセージがOfferであった場合はSDPアンサーを返信する
                    if (messageType == RTCSdpType.Offer)
                    {
                        var answer = await _peerConnection.CreateAnswer().AsTask().ConfigureAwait(false);
                        await _peerConnection.SetLocalDescription(answer).AsTask().ConfigureAwait(false);
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
                    await _peerConnection.AddIceCandidate(candidate).AsTask().ConfigureAwait(false);
                    Debug.WriteLine("Conductor: Received candidate : " + message);

                }
            }).Wait();
        }

        /// <summary>
        /// Peerから通話終了のシグナルを受信したときのハンドラ
        /// </summary>
        /// <param name="peerId"></param>
        private void Signaller_OnPeerHangup(int peerId)
        {
            if (peerId != _peerId) return;

            Debug.WriteLine("Conductor: Our peer hung up.");
            ClosePeerConnection();
        }

        /// <summary>
        /// シグナリングサーバからログアウトしたときのイベントハンドラ
        /// </summary>
        private void Signaller_OnDisconnected()
        {
            ClosePeerConnection();
        }

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

        #endregion

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

    }
}
