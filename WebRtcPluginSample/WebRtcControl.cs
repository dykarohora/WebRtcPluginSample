using System;
using System.Collections.Generic;
using System.Text;

using Windows.ApplicationModel.Core;
using Windows.UI.Core;

using Org.WebRtc;
using WebRtcPluginSample.Signalling;
using System.Linq;

namespace WebRtcPluginSample
{
    public class WebRtcControl
    {
        public event Action<string> OnStatusMessageUpdate;      // ステータス変更を通知するイベント

        private readonly CoreDispatcher _uiDispathcer;


        public WebRtcControl()
        {
            _uiDispathcer = CoreApplication.MainView.CoreWindow.Dispatcher;
        }

        public void Initialize()
        {
            // WebRTCライブラリの初期化
            WebRTC.Initialize(_uiDispathcer);
            Conductor.Instance.ETWStatsEnabled = false;

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




        }

        /// <summary>
        /// マシン上のカメラデバイスのリスト
        /// </summary>
        public List<MediaDevice> Cameras { get; set; }

        /// <summary>
        /// マイクデバイスのリスト
        /// </summary>
        public List<MediaDevice> Microphones { get; set; }

        /// <summary>
        /// スピーカーデバイスのリスト
        /// </summary>
        public List<MediaDevice> AudioPlayoutDevices { get; set; }

        /// <summary>
        /// 現在選択中のカメラ
        /// </summary>
        public MediaDevice SelectedCamera {
            get => _selectedCamera;
            set {
                _selectedCamera = value;
                if (value == null) return;
                // ビデオキャプチャに使用するビデオデバイスを変更する
                Conductor.Instance.Media.SelectVideoDevice(_selectedCamera);
                if (_allCapRes == null) _allCapRes = new List<string>();
                else _allCapRes.Clear();
                // 選択したデバイスがサポートする解像度とFPSの一覧を取得する(非同期)
                var opRes = value.GetVideoCaptureCapabilities();
                // 取得が完了したあとの処理(非同期)
                opRes.AsTask().ContinueWith(resolutions =>
                {
                    RunOnUiThread(() =>
                    {
                        // opResで例外が発生した場合
                        if (resolutions.IsFaulted)
                        {
                            Exception ex = resolutions.Exception;
                            while (ex is AggregateException && ex.InnerException != null)
                                ex = ex.InnerException;
                            string errorMsg = "SetSelectedCamera: Failed to GetVideoCaptureCapabilities (Error: " + ex.Message + ")";
                            OnStatusMessageUpdate?.Invoke(errorMsg);
                            return;
                        }
                        // 結果が返ってこない
                        if (resolutions.Result == null)
                        {
                            string errorMsg = "SetSelectedCamera: Failed to GetVideoCaptureCapabilities (Result is null)";
                            OnStatusMessageUpdate?.Invoke(errorMsg);
                            return;
                        }
                        // 解像度でグルーピングし、グループの先頭の要素を集めてリスト化
                        var uniqueRes = resolutions.Result.GroupBy(test => test.ResolutionDescription).Select(grp => grp.First()).ToList();
                        // デバイスが640x480の解像度をサポートしていれば、それをデフォルトとする
                        CaptureCapability defaultResolution = null;
                        foreach (var resolution in uniqueRes)
                        {
                            if (defaultResolution == null)
                            {
                                defaultResolution = resolution;
                            }
                            _allCapRes.Add(resolution.ResolutionDescription);
                            if ((resolution.Width == 640) && (resolution.Height == 480))
                            {
                                defaultResolution = resolution;
                            }
                        }
                        SelectedCapResItem = defaultResolution.ResolutionDescription;
                    });
                });
            }
        }
        private MediaDevice _selectedCamera;

        /// <summary>
        /// カメラデバイスがサポートする解像度のリスト(string表現)
        /// </summary>
        public List<string> AllCapRes {
            get => _allCapRes ?? (_allCapRes = new List<string>());
            set => _allCapRes = value;
        }
        private List<string> _allCapRes;

        /// <summary>
        /// 現在選択中のカメラ解像度
        /// </summary>
        public string SelectedCapResItem {
            get => _selectedCapResItem;
            set {
                if (AllCapFps == null) AllCapFps = new List<CaptureCapability>();
                else AllCapFps.Clear();

                if(SelectedCamera != null)
                {
                    // 選択したデバイスから解像度とFPSの一覧を取得する(非同期)
                    var opCap = SelectedCamera.GetVideoCaptureCapabilities();
                    opCap.AsTask().ContinueWith(caps =>
                    {
                        // 設定した解像度がサポートするFPSを抽出してリスト化
                        var fpsList = from cap in caps.Result where cap.ResolutionDescription == value select cap;
                        RunOnUiThread(() =>
                        {
                            CaptureCapability defaultFps = null;
                            uint selectedCapFpsFrameRate = 0;
                            // FPSを設定
                            foreach(var fps in fpsList)
                            {
                                AllCapFps.Add(fps);
                                if(defaultFps == null)
                                {
                                    defaultFps = fps;
                                }
                                SelectedCapFpsItem = defaultFps;
                            }
                        });
                    });
                }
                _selectedCapResItem = value;
            }
        }
        private string _selectedCapResItem = null;

        /// <summary>
        /// カメラデバイスがサポートするFPSのリスト(解像度に依存する)
        /// </summary>
        public List<CaptureCapability> AllCapFps {
            get => _allCapFps ?? (_allCapFps = new List<CaptureCapability>());
            set => _allCapFps = value;
        }
        private List<CaptureCapability> _allCapFps;

        /// <summary>
        /// 現在選択中のビデオFPS
        /// </summary>
        public CaptureCapability SelectedCapFpsItem {
            get => _selectedCapFpsItem;
            set {
                if(_selectedCapFpsItem != value)
                {
                    _selectedCapFpsItem = value;
                    Conductor.Instance.VideoCaptureProfile = value;
                    Conductor.Instance.UpdatePreferredFrameFormat();
                }
            }
        }
        private CaptureCapability _selectedCapFpsItem;


        private void RunOnUiThread(Action fn)
        {
            var asyncOp = _uiDispathcer.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(fn));
        }
    }
}
