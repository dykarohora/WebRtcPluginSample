using HoloToolkit.Unity.InputModule;
using UnityEngine;

namespace WebRtcSample
{
    public class GestureHandler : MonoBehaviour, IInputClickHandler
    {
        public enum HandlerType
        {
            ConnectToServer,
            ConnectToPeer,
            DisconnectFromPeer,
            DisconnectFromServer,
        }

        [SerializeField]
        private HandlerType type;
        [SerializeField]
        private WebRtcManager manager;

        public void OnInputClicked(InputClickedEventData eventData)
        {
            HandleEvent();
        }

        private void Update()
        {
            if(Input.GetButtonDown("XboxA"))
            {
                if(GazeManager.Instance.HitObject == gameObject)
                {
                    HandleEvent();
                }
            }
        }

        private void HandleEvent()
        {
            switch (type)
            {
                case HandlerType.ConnectToServer:
                    manager?.ConnectToServer();
                    break;
                case HandlerType.ConnectToPeer:
                    manager?.ConnectToPeer();
                    break;
                case HandlerType.DisconnectFromPeer:
                    manager?.DisconnectFromPeer();
                    break;
                case HandlerType.DisconnectFromServer:
                    manager?.DisconnectFromServer();
                    break;
            }
        }
    }
}