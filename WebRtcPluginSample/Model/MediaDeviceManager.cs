using System;
using System.Collections.Generic;

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

        private readonly object _resolutionLock = new object();
        private readonly object _fpsCapLock = new object();

        // ===============================
        // Properties
        // ===============================

        // 各メディアデバイスのリスト
        public List<MediaDevice> Cameras { get; } = new List<MediaDevice>();
        public List<MediaDevice> Microphones { get; } = new List<MediaDevice>();
        public List<MediaDevice> AudioPlayoutDevices { get; } = new List<MediaDevice>();
        // 選択中のカメラデバイスがサポートする解像度とFPSのリスト
        public List<Resolution> SupportedResolutions { get; } = new List<Resolution>();
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
                }
            }
        }
        private MediaDevice _selectedCamera;

        /// <summary>
        /// 選択中のカメラ解像度
        /// </summary>
        public Resolution SelectedResolution {
            get => _selectedResolution;
            set {
                if(SelectedCamera != null)
                {
                    _selectedResolution = value;
                    SetupFpsList(_selectedResolution).Wait();
                    SelectedFps = SupportedFpsList.FirstOrDefault();
                }
            }
        }
        private Resolution _selectedResolution;

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

        /// <summary>
        /// 現在選択しているカメラがサポートする最小の解像度を取得する
        /// </summary>
        /// <returns></returns>
        public async Task<Resolution> GetLowestResolution()
        {
            if (Cameras.Count > 0)
            {
                if (SelectedCamera == null)
                {
                    await Task.Run(() => SelectedCamera = Cameras.FirstOrDefault());
                }

                Resolution minimum = null;
                foreach (var resolution in SupportedResolutions)
                {
                    var pixelDensity = resolution.Width * resolution.Height;
                    if(minimum == null || minimum.Width * minimum.Height > pixelDensity)
                        minimum = resolution;
                }
                return minimum;
            }
            else 
            {
                // 例外スローの方がいいかも
                return null;
            }
        }

        /// <summary>
        /// 現在選択しているカメラがサポートする最大の解像度を取得する
        /// </summary>
        /// <returns></returns>
        public async Task<Resolution> GetHighestResolution()
        {
            if(Cameras.Count > 0)
            {
                if(SelectedCamera == null)
                {
                    await Task.Run(() => SelectedCamera = Cameras.FirstOrDefault());
                }

                Resolution maximum = null;
                foreach (var resolution in SupportedResolutions)
                {
                    var pixelDensity = resolution.Width * resolution.Height;
                    if (maximum == null || maximum.Width * maximum.Height < pixelDensity)
                        maximum = resolution;
                }
                return maximum;
            } else
            {
                // 例外スローの方がいいかも
                return null;
            }
        }

        /// <summary>
        /// 現在選択しているカメラと解像度がサポートする最小のフレームレートをもつCaptureCapabilityを取得する
        /// </summary>
        /// <returns></returns>
        public async Task<CaptureCapability> GetLowestFpsCapability()
        {
            if (Cameras.Count > 0)
            {
                if (SelectedCamera == null)
                {
                    await Task.Run(() => SelectedCamera = Cameras.FirstOrDefault());
                }

                CaptureCapability minimum = null;
                foreach (var fpsCap in SupportedFpsList)
                {
                    if (minimum == null || minimum.FrameRate > fpsCap.FrameRate)
                        minimum = fpsCap;
                }
                return minimum;
            } else
            { 
                // 例外スローの方がいいかも
                return null;
            }
        }

        /// <summary>
        /// 現在選択しているカメラと解像度がサポートする最大のフレームレートをもつCaptureCapabilityを取得する
        /// </summary>
        /// <returns></returns>
        public async Task<CaptureCapability> GetHighestFpsCapability()
        {
            if(Cameras.Count > 0)
            {
                if(SelectedCamera == null)
                {
                    await Task.Run(() => SelectedCamera = Cameras.FirstOrDefault());
                }

                CaptureCapability maximum = null;
                foreach(var fpsCap in SupportedFpsList)
                {
                    if(maximum == null || maximum.FrameRate < fpsCap.FrameRate)
                        maximum = fpsCap;
                }
                return maximum;
            } else
            {
                // 例外スローの方がいいかも
                return null;
            }
        }

        /// <summary>
        /// 指定した解像度を設定できるか試みる。選択中のカメラがサポートしていない場合はfalseを返す
        /// </summary>
        /// <param name="resolution"></param>
        /// <returns></returns>
        public async Task<bool> TrySetResolution(Resolution resolution)
        {
            if(Cameras.Count>0)
            {
                if(SelectedCamera == null)
                {
                    await Task.Run(() => SelectedCamera = Cameras.FirstOrDefault());
                }

                lock (_resolutionLock)
                {
                    foreach (var resElem in SupportedResolutions)
                    {
                        if (resElem.Equals(resolution))
                        {
                            SelectedResolution = resolution;
                            return true;
                        }
                    }
                }

                return false;
            } else
            {
                // 例外スローの方がいいかも
                return false;
            }
        }

        /// <summary>
        /// 指定したFPSが設定できるかを試みる。選択中のカメラ、解像度がサポートしていない場合はfalseを返す
        /// </summary>
        /// <param name="capability"></param>
        /// <returns></returns>
        public async Task<bool> TrySetFpsCapability(CaptureCapability capability)
        {
            if(Cameras.Count > 0)
            {
                if(SelectedCamera == null)
                {
                    await Task.Run(() => SelectedCamera = Cameras.FirstOrDefault());
                }

                lock(_fpsCapLock)
                {
                    foreach (var fpsElem in SupportedFpsList)
                    {
                        if(fpsElem.FrameRate == capability.FrameRate)
                        {
                            if(SelectedResolution.Equals(new Resolution(capability.Width, capability.Height)))
                            {
                                SelectedFps = capability;
                                return true;
                            }
                        }
                    }
                }

                return false;
            } else
            {
                return false;
            }
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

        private async Task SetupResolutionList()
        {
            lock(_resolutionLock)
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

                lock (_resolutionLock)
                {
                    foreach (var resolution in uniqueRes)
                    {
                        var w = resolution.Width;
                        var h = resolution.Height;
                        SupportedResolutions.Add(new Resolution(w, h));
                    }
                }
            }).ConfigureAwait(false);
        }

        private async Task SetupFpsList(Resolution resolution)
        {
            SupportedFpsList.Clear();
            var task = SelectedCamera.GetVideoCaptureCapabilities();
            await task.AsTask().ContinueWith(caps =>
            {
                if (caps.IsFaulted)
                {
                    Exception ex = caps.Exception;
                    // TODO
                    return;
                }

                if (caps.Result == null)
                {
                    // TODO
                    return;
                }

                var fpsList = from cap in caps.Result where cap.ResolutionDescription == resolution.ToString() select cap;
                lock (_fpsCapLock)
                {
                    foreach (var fps in fpsList)
                    {
                        SupportedFpsList.Add(fps);
                    }
                }
            }).ConfigureAwait(false);
        }
    }
}
