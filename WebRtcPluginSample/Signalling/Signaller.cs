using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using WebRtcPluginSample.Model;

#if NETFX_CORE
using Windows.Data.Json;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
#endif

namespace WebRtcPluginSample.Signalling
{
    public delegate void SignedInDelegate();
    public delegate void DisconnectedDelegate();
    public delegate void PeerConnectedDelegate(int id, string name);
    public delegate void PeerDisconnectedDelegate(int peerId);
    public delegate void PeerHangupDelegate(int peerId);
    public delegate void MessageFromPeerDelegate(int peerId, string message);
    public delegate void MessageSentDelegate(int err);
    public delegate void ServerConnectionFailureDelegate();

    internal class Signaller
    {
        // ===============================
        // Private Member
        // ===============================

        public enum State
        {
            NOT_CONNECTED,      // 未接続
            RESOLVING,
            SIGNING_IN,         // サーバへの接続試行中
            CONNECTED,          // 接続中
            SIGNING_OUT_WAITING,
            SIGNING_OUT,
        };
        /// <summary>
        /// シグナリングサーバへのせつぞくステータス
        /// </summary>
        private State _state;

#if NETFX_CORE
        private StreamSocket _hangingGetSocket;
        // シグナリングサーバの接続情報
        private HostName _server;
#endif

        private string _port;
        // 自分のクライアント名
        private string _clientName;
        // 自分のPeerID
        private int _myId;
        // リモートユーザの一覧
        // private Dictionary<int, string> _peers = new Dictionary<int, string>();
        private List<Peer> _peers = new List<Peer>();

        // ===============================
        // Properties
        // ===============================

        /// <summary>
        /// シグナリングサーバへ接続しているかどうか
        /// </summary>
        /// <returns></returns>
        public bool IsConnceted()
        {
            return _myId != -1;
        }

        /// <summary>
        /// リモートユーザのリスト
        /// </summary>
        public List<Peer> Peers { get => _peers; }

        // ===============================
        // Constructor
        // ===============================

        public Signaller()
        {
            _state = State.NOT_CONNECTED;
            _myId = -1;
        }

        // ===============================
        // Event
        // ===============================

        /// <summary>
        /// シグナリングサーバへの接続(ログイン)が完了したときのイベント
        /// </summary>
        public event SignedInDelegate OnSignedIn;

        /// <summary>
        /// シグナリングサーバから切断(ログアウト)が完了したときのイベント
        /// </summary>
        public event DisconnectedDelegate OnDisconnected;

        /// <summary>
        /// リモートユーザがシグナリングサーバに接続してきたときのイベント
        /// </summary>
        public event PeerConnectedDelegate OnPeerConnected;

        /// <summary>
        /// リモートユーザがシグナリングサーバからログアウトしたときのイベント 
        /// </summary>
        public event PeerDisconnectedDelegate OnPeerDisconnected;

        /// <summary>
        /// 通話から通話終了シグナルを受け取ったときのイベント
        /// </summary>
        public event PeerHangupDelegate OnPeerHangup;

        /// <summary>
        /// 通話相手からメッセージを受信したときのイベント
        /// </summary>
        public event MessageFromPeerDelegate OnMessageFromPeer;

        /// <summary>
        /// シグナリングサーバへの接続(ログイン)が失敗したときのイベント
        /// </summary>
        public event ServerConnectionFailureDelegate OnServerConnectionFailure; // シグナリングサーバへの接続が失敗したときのイベント

        // ===============================
        // Public Method
        // ===============================

        /// <summary>
        /// シグナリングサーバへの接続
        /// </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="client_name"></param>
        public async Task Connect(string server, string port, string client_name)
        {
            try
            {
                // すでにサーバへ接続済み、もしくは接続中
                if(_state != State.NOT_CONNECTED)
                {
                    OnServerConnectionFailure?.Invoke();
                    return;
                }

                #if NETFX_CORE
                _server = new HostName(server);
                #endif

                _port = port;
                _clientName = client_name;

                _state = State.SIGNING_IN;      // ステートをサーバへの接続試行中に変更
                // ログインリクエストの送信、そのまま後続の処理もやってしまう
                await ControlSocketRequestAsync(string.Format("GET /sign_in?{0} HTTP/1.0\r\n\r\n", client_name)).ConfigureAwait(false);
                // ログイン成功
                if(_state == State.CONNECTED)
                {
                    var task = HangingGetReadLoopAsync();
                } else
                // ログイン失敗
                {
                    _state = State.NOT_CONNECTED;
                    OnServerConnectionFailure?.Invoke();
                }
            } catch(Exception ex)
            {
                Debug.WriteLine("[Error] Signaling: Failed to connect to server: " + ex.Message);
            }
        }

        /// <summary>
        /// サインアウト処理
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SignOut()
        {
            if (_state == State.NOT_CONNECTED || _state == State.SIGNING_OUT) return true;
#if NETFX_CORE
            if(_hangingGetSocket != null)
            {
                _hangingGetSocket.Dispose();
                _hangingGetSocket = null;
            }
#endif
            _state = State.SIGNING_OUT;

            if(_myId != -1)
            {
                // サインアウトリクエストを送る
                await ControlSocketRequestAsync(string.Format("GET /sign_out?peer_id={0} HTTP/1.0\r\n\r\n", _myId)).ConfigureAwait(false);
            } else
            {
                return true;
            }

            _myId = -1;
            _state = State.NOT_CONNECTED;
            return true;
        }
        
        /// <summary>
        /// 指定したPeer(リモートユーザ)にメッセージを送信する
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<bool> SendToPeer(int peerId, string message)
        {
            // シグナリングサーバに接続していない場合はfalse
            if (_state != State.CONNECTED) return false;
            Debug.Assert(IsConnceted());
            // 引数のpeerIDが不正でもfalse
            if (!IsConnceted() || peerId == -1) return false;

            string buffer = string.Format(
                "POST /message?peer_id={0}&to={1} HTTP/1.0\r\n" +
                "Content-Length: {2}\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n" +
                "{3}",
                _myId, peerId, message.Length, message);

            return await ControlSocketRequestAsync(buffer);
        }

#if NETFX_CORE
        /// <summary>
        /// 指定したPeer(リモートユーザ)にJson形式のメッセージを送信する
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        public async Task<bool> SendToPeer(int peerId, IJsonValue json)
        {
            string message = json.Stringify();
            return await SendToPeer(peerId, message);
        }
#endif

        // ===============================
        // Helper Method
        // ===============================

        /// <summary>
        /// シグナリングサーバ接続後のポーリングリクエスト
        /// </summary>
        /// <returns></returns>
        private async Task HangingGetReadLoopAsync()
        {
#if NETFX_CORE
            while(_state != State.NOT_CONNECTED)
            {
                using (_hangingGetSocket = new StreamSocket())
                {
                    try
                    {
                        // シグナリングサーバへ接続
                        await _hangingGetSocket.ConnectAsync(_server, _port);
                        if (_hangingGetSocket == null) return;

                        await _hangingGetSocket.WriteStringAsync(string.Format("GET /wait?peer_id={0} HTTP/1.0\r\n\r\n", _myId));
                        var readResult = await ReadIntoBufferAsync(_hangingGetSocket);

                        if (readResult == null) continue;

                        string buffer = readResult.Item1;       // レスポンス
                        int content_length = readResult.Item2;  // レスポンスボディサイズ

                        int peer_id, eoh;
                        if (!ParseServerResponse(buffer, out peer_id, out eoh)) continue;

                        int pos = eoh + 4;      // レスポンスボディの開始位置

                        if(_myId == peer_id)
                        {
                            int id = 0;                 // リモートユーザのID
                            string name = "";           // リモートユーザのクライアント名
                            bool connected = false;     // trueはログイン, falseはログアウト
                            // レスポンスボディをパースしてリモートユーザのログイン/ログアウトを処理
                            if(ParseEntry(buffer.Substring(pos), ref name, ref id, ref connected))
                            {
                                if(connected)
                                {
                                    // _peers[id] = name;
                                    _peers.Add(new Peer { Id = id, Name = name });
                                    OnPeerConnected?.Invoke(id, name);
                                } else
                                {
                                    // _peers.Remove(id);
                                    var peerToRemove = _peers.Find(peer => peer.Id == id);
                                    _peers.Remove(peerToRemove);
                                    OnPeerDisconnected?.Invoke(id);
                                }
                            }
                        } else
                        {
                            //
                            string message = buffer.Substring(pos);
                            if(message == "BYE")
                            {
                                OnPeerHangup?.Invoke(peer_id);
                            } else
                            {
                                OnMessageFromPeer?.Invoke(peer_id, message);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("[Error] Signaling: Long-polling exception: {0}", e.Message);
                    }
                }
            }
#else
            await Task.Run(() => { });
#endif
        }

        /// <summary>
        /// シグナリングサーバへリクエストを送信
        /// </summary>
        /// <param name="sendBuffer">リクエスト</param>
        /// <returns></returns>
        private async Task<bool> ControlSocketRequestAsync(string sendBuffer)
        {
#if NETFX_CORE
            using (var socket = new StreamSocket())
            {
                try
                {
                    await socket.ConnectAsync(_server, _port);
                } catch (Exception e)
                {
                    // サーバへの接続が失敗
                    Debug.WriteLine("[Error] Signaling: Failed to connect to " + _server + ":" + _port + " : " + e.Message);
                    return false;
                }
                // 疑似HTTP通信
                // 自分のクライアント名をGETで送信
                await socket.WriteStringAsync(sendBuffer);
                // レスポンスの読み取り
                var readResult = await ReadIntoBufferAsync(socket);
                if(readResult == null)
                {
                    // レスポンス読み取り失敗
                    return false;
                }

                string buffer = readResult.Item1;       // レスポンス
                int content_length = readResult.Item2;  // レスポンスボディのサイズ

                int peer_id, eoh;
                // Pragmaヘッダの値(自分のPeerID)とヘッダの終わり位置を取得する
                if(!ParseServerResponse(buffer, out peer_id, out eoh))
                {
                    return false;
                }

                // シグナリングサーバへの接続試行中だった場合
                if(_myId == -1)
                {
                    Debug.Assert(_state == State.SIGNING_IN);
                    _myId = peer_id;
                    Debug.Assert(_myId != -1);

                    // レスポンスボディにすでにシグナリングサーバへ接続しているユーザのIDとクライアント名が記載されているので、
                    // Peerリストに追加する
                    if(content_length > 0)
                    {
                        int pos = eoh + 4;  // レスポンスボディの開始位置
                        while(pos < buffer.Length)
                        {
                            int eol = buffer.IndexOf('\n', pos);
                            if (eol == -1) break;

                            int id = 0;
                            string name = "";
                            bool connected = false;
                            if(ParseEntry(buffer.Substring(pos, eol-pos), ref name, ref id, ref connected) && id != _myId)
                            {
                                // _peers[id] = name;
                                _peers.Add(new Peer { Id = id, Name = name });
                                OnPeerConnected(id, name);
                            }
                            pos = eol + 1;
                        }
                        OnSignedIn?.Invoke();       // シグナリングサーバへの接続完了イベント発火
                    }
                }

                else if(_state == State.SIGNING_OUT)
                {
                    Close();
                    OnDisconnected?.Invoke();
                } else if(_state == State.SIGNING_OUT_WAITING)
                {
                    await SignOut();
                }

                if(_state == State.SIGNING_IN)
                {
                    _state = State.CONNECTED;       // 接続完了状態に遷移
                }
            }
            return true;
#else
            await Task.Run(() => { });
            return false;
#endif
        }

        /// <summary>
        /// シグナリングサーバから切断
        /// </summary>
        private void Close()
        {
#if NETFX_CORE
            if(_hangingGetSocket != null)
            {
                _hangingGetSocket.Dispose();
                _hangingGetSocket = null;
            }
#endif

            // _peers.Clear();
            _peers.Clear();
            _state = State.NOT_CONNECTED;
        }

        /// <summary>
        /// レスポンスからPragmaヘッダ(自分/通話相手のPeerID)の値とヘッダの終わり位置を取得する
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="peerId"></param>
        /// <param name="eoh"></param>
        /// <returns></returns>
        private bool ParseServerResponse(string buffer, out int peerId, out int eoh)
        {
            peerId = -1;
            eoh = -1;

            try
            {
                int index = buffer.IndexOf(' ') + 1;
                int status = int.Parse(buffer.Substring(index, 3)); // レスポンスステータスコード

                if(status != 200)
                {
                    if (status == 500 && buffer.Contains("Peer most likely gone."))
                    {
                        Debug.WriteLine("Peer most likely gone. Closing peer connection.");
                        // ???
                        OnPeerDisconnected(0);
                        return false;
                    }
                    Close();
                    OnDisconnected?.Invoke();
                    _myId = -1;
                    return false;
                }
                // ヘッダの終わりの位置
                eoh = buffer.IndexOf("\r\n\r\n");
                if(eoh == -1)
                {
                    Debug.WriteLine("[Error] Failed to parse server response (end of header not found)! Buffer(" + buffer.Length + ")=<" + buffer + ">");
                    return false;
                }
                // Pragmaヘッダの値を取り出す
                GetHeaderValue(buffer, true, "\r\nPragma: ", out peerId);
                return true;
            } catch(Exception ex)
            {
                Debug.WriteLine("[Error] Failed to parse server response (ex=" + ex.Message + ")! Buffer(" + buffer.Length + ")=<" + buffer + ">");
                return false;
            }
        }

#if NETFX_CORE
#pragma warning disable 1998
        /// <summary>
        /// サーバからのレスポンスを受信し、レスポンス全体とコンテンツ長さ(レスポンスボディのサイズ)のタプルを返す
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        private async Task<Tuple<string, int>> ReadIntoBufferAsync(StreamSocket socket)
        {
            DataReaderLoadOperation loadTask = null;
            string data;
            try
            {
                var reader = new DataReader(socket.InputStream);
                reader.InputStreamOptions = InputStreamOptions.Partial;
                // ストリームから64Mbyte読み取り
                loadTask = reader.LoadAsync(0xffff);
                // レスポンスの読み取り
                bool succeeded = loadTask.AsTask().Wait(20000);
                if (!succeeded)
                {
                    throw new TimeoutException("Timed out long polling, re-trying.");
                }
                
                var count = loadTask.GetResults();
                if (count == 0)
                {
                    throw new Exception("No results loaded from reader.");
                }
                // レスポンスを取得
                data = reader.ReadString(count);
                if (data == null)
                {
                    throw new Exception("ReadString operation failed.");
                }
            }
            catch (TimeoutException ex)
            {
                Debug.WriteLine(ex.Message);
                if (loadTask != null && loadTask.Status == Windows.Foundation.AsyncStatus.Started)
                {
                    loadTask.Cancel();  // タスクのキャンセル
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Error] Signaling: Failed to read from socket. " + ex.Message);
                if (loadTask != null && loadTask.Status == Windows.Foundation.AsyncStatus.Started)
                {
                    loadTask.Cancel();  // タスクのキャンセル
                }
                return null;
            }

            int content_length = 0;
            bool ret = false;
            // レスポンスボディの開始位置を取得
            int i = data.IndexOf("\r\n\r\n");
            if (i != -1)
            {
                Debug.WriteLine("Signaling: Headers received [i=" + i + " data(" + data.Length + ")"/*=" + data*/ + "]");
                // Content-Lengthヘッダのヘッダ値を取得する
                if (GetHeaderValue(data, false, "\r\nContent-Length: ", out content_length))
                {
                    // レスポンスの全体長を計算
                    int total_response_size = (i + 4) + content_length;
                    // ストリームから読み取ったデータ長と比較
                    if (data.Length >= total_response_size)
                    {
                        ret = true;
                    }
                    else
                    {
                        Debug.WriteLine("[Error] Singaling: Incomplete response; expected to receive " + total_response_size + ", received" + data.Length);
                    }
                }
                else
                {
                    Debug.WriteLine("[Error] Signaling: No content length field specified by the server.");
                }
            }
            return ret ? Tuple.Create(data, content_length) : null;
        }
#pragma warning restore 1998
#endif

        /// <summary>
        /// レスポンスボディをパースし、ユーザ名、PeerID、接続状態を取り出す
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="name"></param>
        /// <param name="id"></param>
        /// <param name="connected"></param>
        /// <returns></returns>
        private static bool ParseEntry(string entry, ref string name, ref int id, ref bool connected)
        {
            connected = false;
            int separator = entry.IndexOf(',');
            if(separator != -1)
            {
                id = entry.Substring(separator + 1).ParseReadingInt();
                name = entry.Substring(0, separator);
                separator = entry.IndexOf(',', separator + 1);
                if(separator != -1)
                {
                    connected = entry.Substring(separator + 1).ParseReadingInt() > 0 ? true : false;
                }
            }
            return name.Length > 0;
        }

        /// <summary>
        /// レスポンスから指定したヘッダの値を取り出す(int)
        /// </summary>
        /// <param name="buffer">レスポンス</param>
        /// <param name="optional"></param>
        /// <param name="header">ヘッダ名</param>
        /// <param name="value">ヘッダ値</param>
        /// <returns></returns>
        private static bool GetHeaderValue(string buffer, bool optional, string header, out int value)
        {
            try
            {
                // ヘッダが登場するレスポンスの位置
                int index = buffer.IndexOf(header);
                // 見つからなかった
                if(index == -1)
                {
                    if(optional)
                    {
                        value = -1;
                        return true;
                    }
                    throw new KeyNotFoundException();
                }
                // ヘッダの長さだけ加算
                index += header.Length;
                // ヘッダ値の取り出し
                value = buffer.Substring(index).ParseReadingInt();
                return true;
            } catch
            {
                value = -1;
                if(!optional)
                {
                    Debug.WriteLine("[Error] Failed to find header <" + header + "> in buffer(" + buffer.Length + ")=<" + buffer + ">");
                    return false;
                } else
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// レスポンスから指定したヘッダの値を取り出す(string)
        /// </summary>
        /// <param name="buffer">レスポンス</param>
        /// <param name="header">ヘッダ名</param>
        /// <param name="value">ヘッダ値</param>
        /// <returns></returns>
        private static bool GetHeaderValue(string buffer, string header, out string value)
        {
            try
            {
                // ヘッダが登場するレスポンスの位置
                int startIndex = buffer.IndexOf(header);
                // 見つからなかった
                if(startIndex == -1)
                {
                    value = null;
                    return false;
                }
                // ヘッダの長さ分加算
                startIndex += header.Length;
                // 改行まで
                int endIndex = buffer.IndexOf("\r\n", startIndex);
                // ヘッダ値を取り出す
                value = buffer.Substring(startIndex, endIndex - startIndex);
                return true;
            } catch
            {
                value = null;
                return false;
            }
        }
    }

    // ===============================
    // Extention
    // ===============================

    public static class Extentions
    {
#if NETFX_CORE
        /// <summary>
        /// ソケットを介してデータを送信
        /// </summary>
        /// <param name="socket">ソケット</param>
        /// <param name="str">送信データ</param>
        /// <returns></returns>
        public static async Task WriteStringAsync(this StreamSocket socket, string str)
        {
            try
            {
                var write = new DataWriter(socket.OutputStream);
                write.WriteString(str);
                await write.StoreAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Error] Singnaling: Couldn't write to socket : " + ex.Message);
            }
        }
#endif

        public static int ParseReadingInt(this string str)
        {
            return int.Parse(Regex.Match(str, "\\d+").Value);
        }
    }
}
