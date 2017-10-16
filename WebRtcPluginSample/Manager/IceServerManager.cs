using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebRtcPluginSample.Model;

#if NETFX_CORE
using Org.WebRtc;
#endif

namespace WebRtcPluginSample.Manager
{
    internal class IceServerManager
    {
        // ===============================
        // Properties
        // ===============================

        public List<IceServer> IceServers { get; } = new List<IceServer>();

        // ===============================
        // Constructor
        // ===============================

        public IceServerManager()
        {
            IceServers.Add(new IceServer("stun.l.google.com:19302", IceServer.ServerType.STUN));
            IceServers.Add(new IceServer("stun1.l.google.com:19302", IceServer.ServerType.STUN));
            IceServers.Add(new IceServer("stun2.l.google.com:19302", IceServer.ServerType.STUN));
            IceServers.Add(new IceServer("stun3.l.google.com:19302", IceServer.ServerType.STUN));
            IceServers.Add(new IceServer("stun4.l.google.com:19302", IceServer.ServerType.STUN));
        }

        // ===============================
        // Public Method
        // ===============================

/// <summary>
/// IceServerのリストをRTCIceServerのリストに変換する
/// </summary>
/// <returns></returns>
        #if NETFX_CORE
        public Task<List<RTCIceServer>> ConvertIceServersToRTCIceServers()
        {
            var task = Task.Run(() =>
            {
                List<RTCIceServer> result = new List<RTCIceServer>();
                foreach (IceServer iceServer in IceServers)
                {
                    string url = "stun:";
                    if (iceServer.Type == IceServer.ServerType.TURN) url = "turn:";
                    RTCIceServer server = null;
                    url += iceServer.Host;
                    server = new RTCIceServer { Url = url };
                    if (iceServer.Credential != null) server.Credential = iceServer.Credential;
                    if (iceServer.Username != null) server.Username = iceServer.Username;
                    result.Add(server);
                }
                return result;
            });

            return task;
        }
        #endif
    }
}
