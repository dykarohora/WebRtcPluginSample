using System;
using System.Collections.Generic;
using System.Text;

using Windows.ApplicationModel.Core;
using Windows.UI.Core;

using Org.WebRtc;
using WebRtcPluginSample.Signalling;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using WebRtcPluginSample.Model;
using System.Threading.Tasks;

namespace WebRtcPluginSample
{
    public class WebRtcControl
    {
        public event Action OnInitialized;                                          // 初期化完了時のイベント
        public event Action<int, string> OnPeerMessageDataReceived;                 // メッセージを受信したときのイベント
        public event Action<string> OnStatusMessageUpdate;                          // ステータス変更を通知するイベント
        public event Action<int, IDataChannelMessage> OnPeerDataChannelReceived;    // データチャネルからデータを受信したときのイベント

        #region Private Member

        private readonly CoreDispatcher _uiDispathcer;

        private MediaVideoTrack _peerVideoTrack;
        private MediaVideoTrack _selfVideoTrack;
        #endregion

        public WebRtcControl()
        {
            // _uiDispathcer = CoreApplication.MainView.CoreWindow.Dispatcher;
        }

        public void Initialize()
        {
            // WebRTCライブラリの初期化
            // WebRTC.Initialize(_uiDispathcer);
            // Conductor.Instance.ETWStatsEnabled = false;

            /*
            Cameras = new List<MediaDevice>();
            Microphones = new List<MediaDevice>();
            AudioPlayoutDevices = new List<MediaDevice>();
            // マシン上で使用できるメディアデバイスをすべて取得する
            foreach(var videoCaptureDevice in Conductor.Instance.Media.GetVideoCaptureDevices())
            {
                Cameras.Add(videoCaptureDevice);
            }
            foreach(var audioCaptureDevice in Conductor.Instance.Media.GetAudioCaptureDevices())
            {
                Microphones.Add(audioCaptureDevice);
            }
            foreach(var audioPlayoutDevice in Conductor.Instance.Media.GetAudioPlayoutDevices())
            {
                AudioPlayoutDevices.Add(audioPlayoutDevice);
            }
            */

            // 各種メディアデバイスはリストの先頭のものを使用する
            // Holoはいいけど、Immersiveの場合は考え直すべきです
            /*
            if(SelectedCamera == null && Cameras.Count > 0)
            {
                SelectedCamera = Cameras.First();
            }
            
            if(SelectedMicrophone == null && Microphones.Count > 0)
            {
                SelectedMicrophone = Microphones.First();
            }

            if(SelectedAudioPlayoutDevice == null && AudioPlayoutDevices.Count >0)
            {
                SelectedAudioPlayoutDevice = AudioPlayoutDevices.First();
            }
            */

            // ================================
            // シグナリング関連のイベントハンドラ
            // ================================

            // マシンに接続されたメディアデバイスが変更されたときのイベントハンドラ
            // Conductor.Instance.Media.OnMediaDevicesChanged += OnMediaDeviceChanged;
            // リモートユーザがシグナリングサーバに接続してきたときのハンドラ
            // 自分の初回ログイン、ポーリング時の新規ユーザ追加時にコールされる
            // TODO 接続ユーザの選択方法を工夫したいところ
            Conductor.Instance.Signaller.OnPeerConnected += (peerId, peerName) =>
            {
                // リモートユーザのリストを行進する
                if (Peers == null)
                {
                    Peers = new List<Peer>();
                    Conductor.Instance.Peers = Peers;
                }
                Peers.Add(new Peer { Id = peerId, Name = peerName });
                // 接続してきたリモートユーザをPeer候補とする
                SelectedPeer = Peers.First(x => x.Id == peerId);
            };
            // リモートユーザがシグナリングサーバからログアウトしたときのハンドラ
            Conductor.Instance.Signaller.OnPeerDisconnected += (peerId) =>
            {
                var peerToRemove = Peers?.FirstOrDefault(p => p.Id == peerId);
                if (peerToRemove != null)
                    Peers.Remove(peerToRemove);
            };
            // シグナリングサーバへの接続が完了したときのハンドラ
            Conductor.Instance.Signaller.OnSignedIn += () =>
            {
                IsConnected = true;
                IsMicrophoneEnabled = false;
                IsCameraEnabled = false;
                IsConnecting = false;

                OnStatusMessageUpdate?.Invoke("Signed in");
            };
            // シグナリングサーバへの接続が失敗したときのハンドラ
            Conductor.Instance.Signaller.OnServerConnectionFailure += () =>
            {
                IsConnecting = false;

                OnStatusMessageUpdate?.Invoke("Server Connection Failure");
            };
            // シグナリングサーバからログアウトしたときのハンドラ
            Conductor.Instance.Signaller.OnDisconnected += () =>
            {
                IsConnected = false;
                IsMicrophoneEnabled = false;
                IsCameraEnabled = false;
                IsDisconnecting = false;
                Peers?.Clear();

                OnStatusMessageUpdate?.Invoke("Disconnected");
            };
            // 
            Conductor.Instance.OnReadyToConnect += () =>
            {
                IsReadyToConnect = true;
            };


            // =============================
            // Peerコネクション関連のイベントハンドラ
            // =============================

            // Peerコネクションに自身のメディアストリームがセットされたときのイベントハンドラ
            Conductor.Instance.OnAddLocalStream += Conductor_OnAddLocalStream;
            // Peerコネクションからリモートユーザのメディアストリームが削除されたときのイベントハンドラ
            Conductor.Instance.OnRemoveRemoteStream += Conductor_OnRemoveRemoteStream;
            // 
            Conductor.Instance.OnConnectionHealthStats += Conductor_OnPeerConnectionHealthStats;
            // Peerコネクションが生成されたときのイベントハンドラ(通話開始)
            Conductor.Instance.OnPeerConnectionCreated += () =>
            {
                IsReadyToConnect = false;
                IsConnectedToPeer = true;
                IsReadyToDisconnect = false;

                IsCameraEnabled = true;
                IsMicrophoneEnabled = true; // ??

                OnStatusMessageUpdate?.Invoke("Peer Connection Created");
            };
            // Peerコネクションが破棄されたときのイベントハンドラ
            Conductor.Instance.OnPeerConnectionClosed += () =>
            {
                IsConnectedToPeer = false;
                _peerVideoTrack = null;
                _selfVideoTrack = null;
                IsMicrophoneEnabled = false;
                IsCameraEnabled = false;
            };
            // Peer(リモートユーザ)からメッセージを受信したときのハンドラ
            Conductor.Instance.OnPeerMessageDataReceived += (peerId, message) =>
            {
                OnPeerMessageDataReceived?.Invoke(peerId, message);
            };

            // =============================
            // コーデック設定
            // =============================
            /*
            // オーディオコーデックの設定
            AudioCodecs = new List<CodecInfo>();
            var audioCodecList = WebRTC.GetAudioCodecs();
            string[] incompatibleAudioCodecs = new string[] { "CN32000", "CN16000", "CN8000", "red8000", "telephone-event8000" };

            foreach (var audioCodec in audioCodecList)
            {
                if (!incompatibleAudioCodecs.Contains(audioCodec.Name + audioCodec.ClockRate))
                {
                    AudioCodecs.Add(audioCodec);
                }
            }
            if (AudioCodecs.Count > 0)
            {
                SelectedAudioCodec = AudioCodecs.First();
            }

            // ビデオコーデックの設定。デフォルトはH.264を使う
            VideoCodecs = new List<CodecInfo>();
            var videoCodecList = WebRTC.GetVideoCodecs().OrderBy(codec =>
            {
                switch (codec.Name)
                {
                    case "VP8": return 1;
                    case "VP9": return 2;
                    case "H264": return 3;
                    default: return 99;
                }
            });

            foreach (var videoCodec in videoCodecList)
            {
                VideoCodecs.Add(videoCodec);
            }
            if (VideoCodecs.Count > 0)
            {
                SelectedVideoCodec = VideoCodecs.FirstOrDefault(codec => codec.Name.Contains("H264"));
            }
            */
            /*
            // =============================
            // Iceサーバの設定
            // =============================
            IceServers = new List<IceServer>();
            NewIceServer = new IceServer();

            IceServers.Add(new IceServer("stun.l.google.com:19302", IceServer.ServerType.STUN));
            IceServers.Add(new IceServer("stun1.l.google.com:19302", IceServer.ServerType.STUN));
            IceServers.Add(new IceServer("stun2.l.google.com:19302", IceServer.ServerType.STUN));
            IceServers.Add(new IceServer("stun3.l.google.com:19302", IceServer.ServerType.STUN));
            IceServers.Add(new IceServer("stun4.l.google.com:19302", IceServer.ServerType.STUN));

            Conductor.Instance.ConfigureIceServers(IceServers);
            */
            OnInitialized?.Invoke();
        }

        #region Event Handlers

        /// <summary>
        /// ローカルストリームがPeerコネクションに追加されたときのハンドラ
        /// </summary>
        /// <param name="evt"></param>
        private void Conductor_OnAddLocalStream(MediaStreamEvent evt)
        {
            if(evt == null)
            {
                var msg = "Conductor_OnAddLocalStream--media stream NULL";
                Debug.WriteLine(msg);
                OnStatusMessageUpdate.Invoke(msg);
            }
            _selfVideoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
            if(_selfVideoTrack != null)
            {
                if (IsCameraEnabled) Conductor.Instance.EnableLocalVideoStream();
                else Conductor.Instance.DisableLocalVideoStream();

                if (IsMicrophoneEnabled) Conductor.Instance.UnmuteMicrophone();
                else Conductor.Instance.MuteMicrophone();
            }
        }

        private void Conductor_OnRemoveRemoteStream(MediaStreamEvent evt)
        {

        }

        private void Conductor_OnPeerConnectionHealthStats(RTCPeerConnectionHealthStats stats)
        {
            
        }
        #endregion

        /// <summary>
        /// リモートユーザのリスト
        /// </summary>
        public List<Peer> Peers { get; set; }

        /// <summary>
        /// シグナリングサーバへの接続が完了しているかどうか
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// マイクの使用可否
        /// </summary>
        public bool IsMicrophoneEnabled { get; set; }

        /// <summary>
        /// カメラの使用可否
        /// </summary>
        public bool IsCameraEnabled { get; set; }

        /// <summary>
        /// シグナリングサーバへ接続中かどうか
        /// </summary>
        public bool IsConnecting { get; set; }

        /// <summary>
        /// シグナリングサーバから切断中かどうか
        /// </summary>
        public bool IsDisconnecting { get; set; }

        /// <summary>
        /// 選択中のリモートユーザ
        /// </summary>
        public Peer SelectedPeer { get; set; }

        /// <summary>
        /// Peerへの接続準備が完了し、通信開始待ちかどうか
        /// </summary>
        public bool IsReadyToConnect { get; set; }

        /// <summary>
        /// Peerと通話中かどうか
        /// </summary>
        public bool IsConnectedToPeer { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsReadyToDisconnect { get; set; }
        
        /// <summary>
        /// シグナリングサーバへの接続
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="peerName"></param>
        public void ConnectToServer(string host, string port, string peerName)
        {
            Task.Run(() =>
            {
                IsConnecting = true;
                Conductor.Instance.StartLogin(host, port, peerName);
            });
        }
        
        /// <summary>
        /// 通話の開始
        /// </summary>
        public void ConnectToPeer()
        {
            if(SelectedPeer != null)
            {
                Task.Run(() =>
                {
                    Conductor.Instance.ConnectToPeer(SelectedPeer);
                });
            } else
            {
                OnStatusMessageUpdate?.Invoke("SelectedPeer not  set");
            }
        }
    }
}