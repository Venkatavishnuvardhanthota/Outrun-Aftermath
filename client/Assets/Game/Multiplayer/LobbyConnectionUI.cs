using UnityEngine;
using UnityEngine.UI;

namespace OutrunAftermath.Client.Multiplayer
{
    /// <summary>
    /// Interactive connection & room lobby overlay for OUTRUN: AFTERMATH.
    /// Allows launching Host, Client, Dedicated Server, or Offline Practice modes.
    /// </summary>
    public sealed class LobbyConnectionUI : MonoBehaviour
    {
        private GameObject _menuRoot;
        private Camera _lobbyCamera;
        private InputField _ipInput;
        private InputField _portInput;
        private Text _statusLabel;

        private void Start()
        {
            BuildLobbyUI();
            if (NetworkSessionController.Instance != null)
            {
                NetworkSessionController.Instance.OnStatusUpdated += HandleStatusUpdate;
            }
        }

        private void OnDestroy()
        {
            if (NetworkSessionController.Instance != null)
            {
                NetworkSessionController.Instance.OnStatusUpdated -= HandleStatusUpdate;
            }
            if (_lobbyCamera != null)
            {
                Destroy(_lobbyCamera.gameObject);
            }
        }

        private void BuildLobbyUI()
        {
            // Overview camera over Dead City Sector 4 while in lobby
            if (Camera.main == null)
            {
                var camGo = new GameObject("LobbyOverviewCamera");
                _lobbyCamera = camGo.AddComponent<Camera>();
                _lobbyCamera.transform.position = new Vector3(14f, 12f, -26f);
                _lobbyCamera.transform.rotation = Quaternion.Euler(20f, -22f, 0f);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var canvasGo = new GameObject("LobbyConnection_Canvas");
            canvasGo.transform.SetParent(transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                        Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Center dark industrial panel
            _menuRoot = CreatePanel(canvasGo.transform, "ConnectionPanel", Vector2.zero, new Vector2(580f, 540f), new Color(0.07f, 0.08f, 0.1f, 0.94f));

            CreateText(_menuRoot.transform, "Title", "OUTRUN: AFTERMATH", font, 34, TextAnchor.MiddleCenter, new Vector2(0f, 215f), new Vector2(520f, 45f), new Color(0.95f, 0.55f, 0.18f));
            CreateText(_menuRoot.transform, "Subtitle", "GROUND & CONVOY OPERATIONS // DEAD CITY SECTOR 4", font, 13, TextAnchor.MiddleCenter, new Vector2(0f, 180f), new Vector2(520f, 24f), new Color(0.75f, 0.85f, 0.92f));
            CreateText(_menuRoot.transform, "Operators", "OPERATORS: 10 PLAYABLE | VEHICLES: WASTELAND RAPTOR (4-SEAT CREW)", font, 11, TextAnchor.MiddleCenter, new Vector2(0f, 155f), new Vector2(540f, 20f), new Color(0.6f, 0.7f, 0.75f));

            // IP Field
            CreateText(_menuRoot.transform, "IpLabel", "SERVER IP", font, 13, TextAnchor.MiddleLeft, new Vector2(-130f, 110f), new Vector2(200f, 24f), Color.gray);
            _ipInput = CreateInputField(_menuRoot.transform, "IpInput", "127.0.0.1", font, new Vector2(-90f, 75f), new Vector2(260f, 40f));

            // Port Field
            CreateText(_menuRoot.transform, "PortLabel", "UDP PORT", font, 13, TextAnchor.MiddleLeft, new Vector2(130f, 110f), new Vector2(120f, 24f), Color.gray);
            _portInput = CreateInputField(_menuRoot.transform, "PortInput", "7777", font, new Vector2(140f, 75f), new Vector2(140f, 40f));

            // Action Buttons
            CreateButton(_menuRoot.transform, "HostBtn", "HOST MATCH (SERVER + PLAYER)", font, new Vector2(0f, 5f), new Vector2(440f, 52f), new Color(0.82f, 0.4f, 0.14f), OnHostClicked);
            CreateButton(_menuRoot.transform, "JoinBtn", "JOIN MATCH (CLIENT)", font, new Vector2(0f, -60f), new Vector2(440f, 52f), new Color(0.18f, 0.45f, 0.78f), OnJoinClicked);
            CreateButton(_menuRoot.transform, "ServerBtn", "DEDICATED SERVER", font, new Vector2(0f, -125f), new Vector2(440f, 46f), new Color(0.24f, 0.28f, 0.32f), OnServerClicked);
            CreateButton(_menuRoot.transform, "OfflineBtn", "OFFLINE SOLO PRACTICE", font, new Vector2(0f, -182f), new Vector2(440f, 44f), new Color(0.18f, 0.38f, 0.28f), OnOfflineClicked);

            _statusLabel = CreateText(_menuRoot.transform, "Status", "READY — SELECT DEPLOYMENT MODE", font, 13, TextAnchor.MiddleCenter, new Vector2(0f, -230f), new Vector2(500f, 28f), Color.yellow);
        }

        private (string ip, ushort port) ReadEndpoint()
        {
            string ip = string.IsNullOrWhiteSpace(_ipInput.text) ? "127.0.0.1" : _ipInput.text.Trim();
            ushort port = ushort.TryParse(_portInput.text, out ushort p) ? p : (ushort)7777;
            return (ip, port);
        }

        private void OnHostClicked()
        {
            var (ip, port) = ReadEndpoint();
            if (NetworkSessionController.Instance.StartHostSession(ip, port))
            {
                DismissMenu();
            }
        }

        private void OnJoinClicked()
        {
            var (ip, port) = ReadEndpoint();
            if (NetworkSessionController.Instance.StartClientSession(ip, port))
            {
                DismissMenu();
            }
        }

        private void OnServerClicked()
        {
            var (ip, port) = ReadEndpoint();
            if (NetworkSessionController.Instance.StartDedicatedServer(ip, port))
            {
                _menuRoot.SetActive(false);
            }
        }

        private void OnOfflineClicked()
        {
            NetworkSessionController.Instance.StartOfflinePractice();
            DismissMenu();
        }

        private void DismissMenu()
        {
            if (_lobbyCamera != null)
            {
                Destroy(_lobbyCamera.gameObject);
                _lobbyCamera = null;
            }
            if (_menuRoot != null)
            {
                Destroy(gameObject);
            }
        }

        private void HandleStatusUpdate(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = status;
            }
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text CreateText(Transform parent, string name, string content, Font font, int size, TextAnchor anchor, Vector2 pos, Vector2 box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var txt = go.GetComponent<Text>();
            txt.text = content;
            txt.font = font;
            txt.fontSize = size;
            txt.alignment = anchor;
            txt.color = color;
            return txt;
        }

        private static InputField CreateInputField(Transform parent, string name, string defaultVal, Font font, Vector2 pos, Vector2 size)
        {
            var go = CreatePanel(parent, name, pos, size, new Color(0.14f, 0.16f, 0.19f, 1f));
            var txt = CreateText(go.transform, "Text", defaultVal, font, 16, TextAnchor.MiddleLeft, new Vector2(10f, 0f), size - new Vector2(20f, 4f), Color.white);
            txt.supportRichText = false;
            var input = go.AddComponent<InputField>();
            input.textComponent = txt;
            input.text = defaultVal;
            return input;
        }

        private static void CreateButton(Transform parent, string name, string label, Font font, Vector2 pos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreatePanel(parent, name, pos, size, bgColor);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(onClick);
            CreateText(go.transform, "Label", label, font, 16, TextAnchor.MiddleCenter, Vector2.zero, size, Color.white);
        }
    }
}
