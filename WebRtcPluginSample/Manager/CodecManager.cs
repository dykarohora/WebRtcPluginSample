using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if NETFX_CORE
using Org.WebRtc;
#endif

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

        #if NETFX_CORE
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
        #endif
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
                #if NETFX_CORE
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
                #else

                return false;

                #endif
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
#if NETFX_CORE
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
                #else

                return false;

                #endif
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
                #if NETFX_CORE
                lock (_audioLock)
                {
                    foreach (var codec in WebRTC.GetAudioCodecs())
                        AudioCodecs.Add(codec);
                }
                #endif
            });
            return task;
        }

        private Task GetVideoCodecs()
        {
            var task = Task.Run(() =>
            {
                #if NETFX_CORE
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
                #endif
            });
            return task;
        }
    }
}
