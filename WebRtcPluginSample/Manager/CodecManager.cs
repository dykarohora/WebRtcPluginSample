using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebRtcPluginSample.Manager
{
    internal class CodecManager
    {
        // ===============================
        // Private Member
        // ===============================

        private readonly object _audioLock = new object();
        private readonly object _videoLock = new object();

        // ===============================
        // Properties
        // ===============================

        /// <summary>
        /// デバイス上でサポートしているオーディオコーデックの一覧
        /// </summary>
        public List<CodecInfo> AudioCodecs { get; } = new List<CodecInfo>();

        /// <summary>
        /// マシン上でサポートしているビデオコーデックの一覧
        /// </summary>
        public List<CodecInfo> VideoCodecs { get; } = new List<CodecInfo>();

        public CodecInfo SelectedAudioCodec { get; set; }

        public CodecInfo SelectedVideoCodec { get; set; }

        // ===============================
        // Public Method
        // ===============================
        
        /// <summary>
        /// マシン上でサポートされているコーデック(オーディオ、ビデオの両方)を全て取得する
        /// </summary>
        /// <returns></returns>
        public async Task GetAudioAndVideoCodecInfo()
        {
            var taskList = new Task[]
            {
                GetAudioCodecs(),
                GetVideoCodecs()
            };

            await Task.WhenAll(taskList);
        }

        /// <summary>
        /// 指定したビデオコーデックの設定を試みる。コーデックがサポートされていない場合はfalseを返す。
        /// </summary>
        /// <param name="codecName"></param>
        /// <returns></returns>
        public Task<bool> TrySetVideoCodec(string codecName)
        {
            var task = Task.Run(() =>
            {
                if (VideoCodecs.Count > 0)
                {
                    lock (_videoLock)
                    {
                        foreach (var codec in VideoCodecs)
                        {
                            if (codec.Name.Equals(codecName, StringComparison.OrdinalIgnoreCase))
                            {
                                SelectedVideoCodec = codec;
                                return true;
                            }
                        }
                    }

                    return false;
                }
                else
                {
                    return false;
                }
            });
            return task;
        }

        /// <summary>
        /// 指定したオーディオコーデックの設定を試みる。コーデックがサポートされていない場合はfalseを返す。
        /// </summary>
        /// <param name="codecName"></param>
        /// <param name="clockRate"></param>
        /// <returns></returns>
        public Task<bool> TrySetAudioCodec(string codecName,int clockRate)
        {
            var task = Task.Run(() =>
            {
                if(AudioCodecs.Count > 0)
                {
                    lock(_audioLock)
                    {
                        foreach(var codec in AudioCodecs)
                        {
                            if(codec.Name.Equals(codecName, StringComparison.OrdinalIgnoreCase) && codec.ClockRate == clockRate)
                            {
                                SelectedAudioCodec = codec;
                                return true;
                            }
                        }
                    }
                    return false;
                }
                else
                {
                    return false;
                }
            });
            return task;
        }


        // ===============================
        // Helper Method
        // ===============================

        private Task GetAudioCodecs()
        {
            var task = Task.Run(() =>
            {
                lock (_audioLock)
                {
                    foreach (var codec in WebRTC.GetAudioCodecs())
                        AudioCodecs.Add(codec);
                }
            });
            return task;
        }

        private Task GetVideoCodecs()
        {
            var task = Task.Run(() =>
            {
                var videoCodecList = WebRTC.GetVideoCodecs().OrderBy(CodecInfo =>
                {
                    switch (CodecInfo.Name)
                    {
                        case "VP8": return 1;
                        case "VP9": return 2;
                        case "H264": return 3;
                        default: return 99;
                    }
                });
                lock (_videoLock)
                {
                    foreach (var videoCodec in videoCodecList)
                        VideoCodecs.Add(videoCodec);
                }
            });
            return task;
        }
    }
}
