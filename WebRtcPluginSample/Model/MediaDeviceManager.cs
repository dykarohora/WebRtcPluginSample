using System;
using System.Collections.Generic;
using System.Text;

using Org.WebRtc;
using System.Threading.Tasks;
using WebRtcPluginSample.Signalling;
using System.Linq;
using WebRtcPluginSample.Utilities;

namespace WebRtcPluginSample.Model
{
    internal class MediaDeviceManager
    {
        // ===============================
        // Private Member
        // ===============================

        /// <summary>
        /// メディアデバイスへアクセスするためのオブジェクト
        /// </summary>
        private Media _media;

        // ===============================
        // Properties
        // ===============================

        // 各メディアデバイスのリスト
        public List<MediaDevice> Cameras { get; } = new List<MediaDevice>();
        public List<MediaDevice> Microphones { get; } = new List<MediaDevice>();
        public List<MediaDevice> AudioPlayoutDevices { get; } = new List<MediaDevice>();
        // 選択中のカメラデバイスがサポートする解像度とFPSのリスト
        public List<string> SupportedResolutions { get; } = new List<string>();
        // public List<Resolution> SupportedResolutions { get; } = new List<Resolution>();
        public List<CaptureCapability> SupportedFpsList { get; } = new List<CaptureCapability>();

        /// <summary>
        /// 選択中のカメラデバイス
        /// </summary>
        public MediaDevice SelectedCamera {
            get => _selectedCamera;
            set {
                if(_selectedCamera != value)
                {
                    _selectedCamera = value;
                    if (value != null)
                        _media.SelectVideoDevice(_selectedCamera);

                    SetupResolutionList().Wait();
                    SelectedResolution = SupportedResolutions.FirstOrDefault();
                    /*
                    // 解像度設定
                    SupportedResolutions.Clear();

                    var opRes = SelectedCamera.GetVideoCaptureCapabilities();
                    opRes.AsTask().ContinueWith(resolutions =>
                    {
                        if (resolutions.IsFaulted)
                        {
                            Exception ex = resolutions.Exception;
                            // TODO: Error Handling
                            return;
                        }

                        if (resolutions.Result == null)
                        {
                            // TODO: Error Handling
                            return;
                        }

                        var uniqueRes = resolutions.Result.GroupBy(test => test.ResolutionDescription).Select(grp => grp.First()).ToList();
                        foreach (var resolution in uniqueRes)
                        {
                            var w = resolution.Width;
                            var h = resolution.Height;
                            // SupportedResolutions.Add(new Resolution(w, h));
                            SupportedResolutions.Add(resolution.ResolutionDescription);
                        }
                        SelectedResolution = SupportedResolutions.FirstOrDefault();
                    });
                    */
                }
            }
        }
        private MediaDevice _selectedCamera;

        private async Task SetupResolutionList()
        {
            SupportedResolutions.Clear();
            var task = SelectedCamera.GetVideoCaptureCapabilities();
            await task.AsTask().ContinueWith(resolutions =>
            {
                if (resolutions.IsFaulted)
                {
                    Exception ex = resolutions.Exception;
                    // TODO: Error Handling
                    return;
                }

                if (resolutions.Result == null)
                {
                    // TODO: Error Handling
                    return;
                }

                var uniqueRes = resolutions.Result.GroupBy(test => test.ResolutionDescription).Select(grp => grp.First()).ToList();
                foreach (var resolution in uniqueRes)
                {
                    var w = resolution.Width;
                    var h = resolution.Height;
                    // SupportedResolutions.Add(new Resolution(w, h));
                    SupportedResolutions.Add(resolution.ResolutionDescription);
                }
            });
        }

        /// <summary>
        /// 選択中のカメラ解像度
        /// </summary>
        public string SelectedResolution {
            get => _selectedResolution;
            set {
                SupportedFpsList.Clear();

                if(SelectedCamera != null)
                {
                    var opCap = SelectedCamera.GetVideoCaptureCapabilities();
                    opCap.AsTask().ContinueWith(caps =>
                    {
                        if(caps.IsFaulted)
                        {
                            // TODO
                            return;
                        }

                        if(caps.Result == null)
                        {
                            // TODO
                            return;
                        }

                        // 設定した解像度がサポートするFPSを抽出してリスト化
                        var fpsList = from cap in caps.Result where cap.ResolutionDescription == value select cap;
                        
                        foreach(var fps in fpsList)
                        {
                            SupportedFpsList.Add(fps);
                        }
                        SelectedFps = SupportedFpsList.FirstOrDefault();
                    });
                }
                _selectedResolution = value;
            }
        }
        private string _selectedResolution;

        /// <summary>
        /// 選択中のカメラフレームレート
        /// </summary>
        public CaptureCapability SelectedFps {
            get => _selectedFps;
            set {
                if(_selectedFps != value)
                {
                    _selectedFps = value;
                    // TODO: 要リファクタ
                    Conductor.Instance.VideoCaptureProfile = value;
                    Conductor.Instance.UpdatePreferredFrameFormat();
                }
            }
        }
        private CaptureCapability _selectedFps;

        /// <summary>
        /// 選択中のマイクデバイス
        /// </summary>
        public MediaDevice SelectedMicrophone {
            get => _selectedMicrophone;
            set {
                if(_selectedMicrophone != value)
                {
                    _selectedMicrophone = value;
                    if (value != null)
                        _media.SelectAudioCaptureDevice(_selectedMicrophone);
                }
            }
        }
        private MediaDevice _selectedMicrophone;

        /// <summary>
        /// 選択中のスピーカーデバイス
        /// </summary>
        public MediaDevice SelectedAudioPlayoutDevice {
            get => _selectedAudioPlayoutDevice;
            set {
                if(_selectedAudioPlayoutDevice != value)
                {
                    _selectedAudioPlayoutDevice = value;
                    if (value != null)
                        _media.SelectAudioPlayoutDevice(_selectedAudioPlayoutDevice);
                }
            }
        }
        private MediaDevice _selectedAudioPlayoutDevice;

        // ===============================
        // Constructor
        // ===============================
        public MediaDeviceManager()
        {
            _media = Media.CreateMedia();
        }
        
        public MediaDeviceManager(Media media)
        {
            _media = media;
        }

        // ===============================
        // Public Method
        // ===============================

        /// <summary>
        /// マシンに接続されているカメラ、マイク、スピーカーデバイスを全て取得し、リストとして保持する
        /// </summary>
        /// <returns></returns>
        public async Task GetAllDeviceList()
        {
            var taskList = new Task[]
            {
                GetCameraDeviceList(),
                GetAudioCaptureDeviceList(),
                GetAudioPlayoutDeviceList()
            };

            await Task.WhenAll(taskList);
        }

        public async Task<bool> GetLowestResolution()
        {
            if (Cameras.Count > 0)
            {
                if (SelectedCamera == null)
                {
                    await Task.Run(() => SelectedCamera = Cameras.FirstOrDefault());
                }
                 
            }
            else 
            {
                return false;
            }

            return false;
        }

        public async Task<bool> GetHighestResolution()
        {
            return false;
        }

        // =============================
        // Helper Method
        // =============================

        private Task GetCameraDeviceList()
        {
            var task = Task.Run(() =>
            {
                foreach (var videoCaptureDevice in _media.GetVideoCaptureDevices())
                    Cameras.Add(videoCaptureDevice);
            });
            return task;
        }

        private Task GetAudioCaptureDeviceList()
        {
            var task = Task.Run(() =>
            {
                foreach (var audioCaptureDevice in _media.GetAudioCaptureDevices())
                    Microphones.Add(audioCaptureDevice);
            });
            return task;
        }

        private Task GetAudioPlayoutDeviceList()
        {
            var task = Task.Run(() =>
            {
                foreach (var audioPlayoutDevice in _media.GetAudioPlayoutDevices())
                    AudioPlayoutDevices.Add(audioPlayoutDevice);
            });
            return task;
        }
    }
}
