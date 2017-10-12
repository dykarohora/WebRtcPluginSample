using System;
using System.Collections.Generic;
using System.Text;
using WebRtcPluginSample.Model;

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
        public List<Peer> Peers;

        private MediaStream _mediaStream;

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
        private bool _etwStatsEnabled;
        public bool ETWStatsEnabled {
            get => _etwStatsEnabled;
            set {
                _etwStatsEnabled = value;
                if (_peerConnection != null)
                    _peerConnection.EtwStatsEnabled = value;
            }
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
    }
}
