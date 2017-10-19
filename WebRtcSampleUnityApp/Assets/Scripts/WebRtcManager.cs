using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using WebRtcPluginSample.Signalling;
using WebRtcPluginSample;
using System.Threading.Tasks;

#if !UNITY_EDITOR
using Org.WebRtc;
#endif

namespace WebRtcSample
{
    public class WebRtcManager : MonoBehaviour
    {
        [SerializeField]
        private string signalling_host;
        [SerializeField]
        private string signalling_port;
        [SerializeField]
        private int _pluginMode = 0;

        public Renderer RenderTexture;

        public float TextureScale = 1f;

#if !UNITY_EDITOR
    [DllImport("TexturesUWP")]
    private static extern void SetTextureFromUnity(System.IntPtr texture, int w, int h);

    [DllImport("TexturesUWP")]
    private static extern void ProcessRawFrame(uint w, uint h, IntPtr yPlane, uint yStride, IntPtr uPlane, uint uStride,
        IntPtr vPlane, uint vStride);

    [DllImport("TexturesUWP")]
    private static extern void ProcessH264Frame(uint w, uint h, IntPtr data, uint dataSize);

    [DllImport("TexturesUWP")]
    private static extern IntPtr GetRenderEventFunc();

    [DllImport("TexturesUWP")]
    private static extern void SetPluginMode(int mode);

#endif

        private Conductor conductor;
        private bool _frameReadyReceive = true;

        private const int textureWidth = 640;
        private const int textureHeight = 480;

        // Use this for initialization
        private async void Start()
        {
            conductor = Conductor.Instance;
            await conductor.Initialize();
            conductor.OnEncodedVideoFrame += OnEncodedVideoStream;
#if !UNITY_EDITOR
            CreateTextureAndPassToPlugin();
            SetPluginMode(_pluginMode);
            StartCoroutine(CallPluginAtEndOfFrames());
#endif
        }

        private void OnEncodedVideoStream(uint w, uint h, byte[] data)
        {
#if !UNITY_EDITOR
        if (data.Length == 0)
            return;

        if (_frameReadyReceive)
            _frameReadyReceive = false;
        else
            return;

        GCHandle buf = GCHandle.Alloc(data, GCHandleType.Pinned);
        ProcessH264Frame(w, h, buf.AddrOfPinnedObject(), (uint)data.Length);
        buf.Free();
#endif
        }

        public async Task ConnectToServer()
        {
            await conductor.StartLogin(signalling_host, signalling_port).ConfigureAwait(false);
        }

        public async Task ConnectToPeer()
        {
            if (conductor.PeersIdList.Count > 0)
            {
                var peerId = conductor.PeersIdList.First();
                await conductor.ConnectToPeer(peerId).ConfigureAwait(false);
            }
        }

        public async Task DisconnectFromPeer()
        {
            await conductor.DisconnectFromPeer().ConfigureAwait(false);
        }

        public async Task DisconnectFromServer()
        {
            await conductor.DisconnectFromServer().ConfigureAwait(false);
        }

        private void CreateTextureAndPassToPlugin()
        {
#if !UNITY_EDITOR
        RenderTexture.transform.localScale = new Vector3(-TextureScale, (float)textureHeight / textureWidth * TextureScale, 1f);

        Texture2D tex = new Texture2D(textureWidth, textureHeight, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        RenderTexture.material.mainTexture = tex;
        SetTextureFromUnity(tex.GetNativeTexturePtr(), tex.width, tex.height);
#endif
        }


        private IEnumerator CallPluginAtEndOfFrames()
        {
            while (true)
            {
                // Wait until all frame rendering is done
                yield return new WaitForEndOfFrame();

                // Issue a plugin event with arbitrary integer identifier.
                // The plugin can distinguish between different
                // things it needs to do based on this ID.
                // For our simple plugin, it does not matter which ID we pass here.

#if !UNITY_EDITOR

            switch (_pluginMode)
            {
                case 0:
                    if (!_frameReadyReceive)
                    {
                        GL.IssuePluginEvent(GetRenderEventFunc(), 1);
                        _frameReadyReceive = true;
                    }
                    break;
                default:
                    GL.IssuePluginEvent(GetRenderEventFunc(), 1);
                    break;
            }
#endif
            }
        }
    }
}