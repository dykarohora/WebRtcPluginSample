using System;
using System.Collections.Generic;
using System.Text;

namespace WebRtcPluginSample.Model
{
    public class IceServer
    {
        public enum ServerType { STUN, TURN }

        // =======================
        // private member
        // =======================

        private ServerType _type;
        private string _typeStr;

        private string _host;
        private string _port;

        private string _username;
        private string _credential;

        private bool _valid;

        // =======================
        // property
        // =======================

        public ServerType Type {
            get => _type;
            set {
                switch (value)
                {
                    case ServerType.STUN:
                        _typeStr = "stun";
                        break;
                    case ServerType.TURN:
                        _typeStr = "turn";
                        break;
                    default:
                        _typeStr = "unknown";
                        break;
                }
                _type = value;
            }
        }

        public string TypeStr {
            get => _typeStr;
        }

        public string Host {
            get => _host;
            set => _host = value;
        }

        public string Port {
            get => _port;
            set => _port = value;
        }

        public string Username {
            get => _username;
            set => _username = value;
        }

        public string Credential {
            get => _credential;
            set => _credential = value;
        }

        public bool Valid {
            get => _valid;
            set => _valid = value;
        }

        // =======================
        // constructor
        // =======================
        public IceServer() : this(string.Empty, ServerType.STUN) { }

        public IceServer(string host, ServerType type)
        {
            Host = host;
            Type = type;
        }
    }
}
