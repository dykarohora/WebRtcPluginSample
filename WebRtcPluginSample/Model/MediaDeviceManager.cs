using System;
using System.Collections.Generic;
using System.Text;

using Org.WebRtc;
using System.Threading.Tasks;

namespace WebRtcPluginSample.Model
{
    internal class MediaDeviceManager
    {
        // ===============================
        // Private Member
        // ===============================

        /// <summary>
        /// メディアデバイスへアクセスするためのオブジェくト
        /// </summary>
        private Media _media;

        // ===============================
        // Properties
        // ===============================

        // 各メディアデバイスのリスト
        public List<MediaDevice> Cameras { get; } = new List<MediaDevice>();
        public List<MediaDevice> Microphones { get; } = new List<MediaDevice>();
        public List<MediaDevice> AudioPlayoutDevices { get; } = new List<MediaDevice>();

        // 選択中のメディアデバイス
        private MediaDevice _selectedCamera;
        public MediaDevice SelectedCamera { get; set; }

        public string SelectedResolution { get; set; }
        public CaptureCapability SelectedFps { get; set; }

        private MediaDevice _selectedMicrophone;
        public MediaDevice SelectedMicrophone {
            get;
            set;
        }

        private MediaDevice _selectedAudioPlayoutDevice;
        public MediaDevice SelectedAudioPlayoutDevice { get; set; }

        // ===============================
        //
        // ===============================
        
        public MediaDeviceManager(Media media)
        {
            _media = media;
        }

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
