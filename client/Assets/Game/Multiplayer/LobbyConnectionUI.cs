using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OutrunAftermath.Client.Runtime.Backend;

namespace OutrunAftermath.Client.Multiplayer
{
    /// <summary>
    /// Interactive connection & room lobby overlay for OUTRUN: AFTERMATH.
    /// Integrates backend player accounts, room codes, leaderboards, and
    /// direct Host/Client/Dedicated Server/Offline deployment modes.
    /// </summary>
    public sealed class LobbyConnectionUI : MonoBehaviour
    {
        private GameObject _menuRoot;
        private Camera _lobbyCamera;
        private InputField _ipInput;
        private InputField _portInput;
        private InputField _roomCodeInput;
        private Text _statusLabel;
        private Text _accountLabel;
        private GameObject _leaderboardPanel;
        private Text _leaderboardContent;

        private void Start()
        {
            BuildLobbyUI();
            if (NetworkSessionController.Instance != null)
            {
                NetworkSessionController.Instance.OnStatusUpdated += HandleStatusUpdate;
            }
            if (GameApiClient.Instance != null)
            {
                GameApiClient.Instance.OnAuthStatusChanged += HandleAuthChanged;
                GameApiClient.Instance.OnProfileUpdated += HandleProfileUpdated;
                RefreshAccountUI();
            }
        }

        private void OnDestroy()
        {
            if (NetworkSessionController.Instance != null)
            {
                NetworkSessionController.Instance.OnStatusUpdated -= HandleStatusUpdate;
            }
            if (GameApiClient.Instance != null)
            {
                GameApiClient.Instance.OnAuthStatusChanged -= HandleAuthChanged;
                GameApiClient.Instance.OnProfileUpdated -= HandleProfileUpdated;
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
            _menuRoot = CreatePanel(canvasGo.transform, "ConnectionPanel", Vector2.zero, new Vector2(620f, 620f), new Color(0.07f, 0.08f, 0.1f, 0.95f));

            CreateText(_menuRoot.transform, "Title", "OUTRUN: AFTERMATH", font, 32, TextAnchor.MiddleCenter, new Vector2(0f, 260f), new Vector2(560f, 40f), new Color(0.95f, 0.55f, 0.18f));
            CreateText(_menuRoot.transform, "Subtitle", "GROUND & CONVOY OPERATIONS // PERSISTENT BACKEND ACTIVE", font, 12, TextAnchor.MiddleCenter, new Vector2(0f, 230f), new Vector2(560f, 22f), new Color(0.75f, 0.85f, 0.92f));

            // Top Account Status Bar
            var accountBar = CreatePanel(_menuRoot.transform, "AccountBar", new Vector2(0f, 192f), new Vector2(560f, 40f), new Color(0.12f, 0.14f, 0.18f, 0.9f));
            _accountLabel = CreateText(accountBar.transform, "AccountText", "GUEST MODE (UNAUTHENTICATED)", font, 12, TextAnchor.MiddleLeft, new Vector2(-60f, 0f), new Vector2(400f, 32f), Color.white);
            CreateButton(accountBar.transform, "QuickAuthBtn", "DEV LOGIN", font, new Vector2(215f, 0f), new Vector2(110f, 30f), new Color(0.2f, 0.5f, 0.3f), OnQuickDevAuthClicked);

            // API Room Code Section
            CreateText(_menuRoot.transform, "RoomLabel", "ONLINE ROOM CODE", font, 12, TextAnchor.MiddleLeft, new Vector2(-150f, 142f), new Vector2(180f, 22f), Color.gray);
            _roomCodeInput = CreateInputField(_menuRoot.transform, "RoomCodeInput", "DEAD66", font, new Vector2(-110f, 112f), new Vector2(240f, 36f));
            CreateButton(_menuRoot.transform, "CreateRoomBtn", "CREATE ROOM", font, new Vector2(65f, 112f), new Vector2(130f, 36f), new Color(0.75f, 0.45f, 0.1f), OnCreateRoomClicked);
            CreateButton(_menuRoot.transform, "JoinRoomBtn", "JOIN CODE", font, new Vector2(210f, 112f), new Vector2(120f, 36f), new Color(0.15f, 0.45f, 0.75f), OnJoinRoomClicked);

            // Direct IP & Port Field (for LAN / Direct connect)
            CreateText(_menuRoot.transform, "IpLabel", "SERVER IP", font, 11, TextAnchor.MiddleLeft, new Vector2(-150f, 68f), new Vector2(180f, 20f), Color.gray);
            _ipInput = CreateInputField(_menuRoot.transform, "IpInput", "127.0.0.1", font, new Vector2(-110f, 42f), new Vector2(240f, 34f));

            CreateText(_menuRoot.transform, "PortLabel", "UDP PORT", font, 11, TextAnchor.MiddleLeft, new Vector2(120f, 68f), new Vector2(100f, 20f), Color.gray);
            _portInput = CreateInputField(_menuRoot.transform, "PortInput", "7777", font, new Vector2(140f, 42f), new Vector2(140f, 34f));

            // Core Action Buttons
            CreateButton(_menuRoot.transform, "HostBtn", "HOST MATCH (SERVER + PLAYER)", font, new Vector2(0f, -12f), new Vector2(480f, 46f), new Color(0.82f, 0.4f, 0.14f), OnHostClicked);
            CreateButton(_menuRoot.transform, "JoinBtn", "DIRECT JOIN (CLIENT)", font, new Vector2(0f, -66f), new Vector2(480f, 46f), new Color(0.18f, 0.45f, 0.78f), OnJoinClicked);
            CreateButton(_menuRoot.transform, "ServerBtn", "DEDICATED SERVER", font, new Vector2(-125f, -122f), new Vector2(230f, 42f), new Color(0.24f, 0.28f, 0.32f), OnServerClicked);
            CreateButton(_menuRoot.transform, "OfflineBtn", "OFFLINE PRACTICE", font, new Vector2(125f, -122f), new Vector2(230f, 42f), new Color(0.18f, 0.38f, 0.28f), OnOfflineClicked);

            CreateButton(_menuRoot.transform, "LeaderboardBtn", "VIEW LEADERBOARDS", font, new Vector2(0f, -174f), new Vector2(480f, 38f), new Color(0.35f, 0.3f, 0.45f), OnLeaderboardToggleClicked);

            _statusLabel = CreateText(_menuRoot.transform, "Status", "READY — SELECT DEPLOYMENT MODE", font, 12, TextAnchor.MiddleCenter, new Vector2(0f, -230f), new Vector2(560f, 28f), Color.yellow);

            // Side Leaderboard Panel (initially hidden)
            _leaderboardPanel = CreatePanel(canvasGo.transform, "LeaderboardPanel", new Vector2(420f, 0f), new Vector2(340f, 620f), new Color(0.08f, 0.09f, 0.12f, 0.96f));
            CreateText(_leaderboardPanel.transform, "LbTitle", "TOP SURVIVORS", font, 20, TextAnchor.MiddleCenter, new Vector2(0f, 260f), new Vector2(300f, 30f), new Color(0.95f, 0.65f, 0.2f));
            _leaderboardContent = CreateText(_leaderboardPanel.transform, "LbContent", "Loading records from backend...", font, 12, TextAnchor.UpperLeft, new Vector2(0f, -10f), new Vector2(300f, 480f), Color.white);
            CreateButton(_leaderboardPanel.transform, "CloseLbBtn", "CLOSE", font, new Vector2(0f, -270f), new Vector2(140f, 32f), new Color(0.4f, 0.2f, 0.2f), () => _leaderboardPanel.SetActive(false));
            _leaderboardPanel.SetActive(false);
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
            if (NetworkSessionController.Instance != null && NetworkSessionController.Instance.StartHostSession(ip, port))
            {
                DismissMenu();
            }
        }

        private void OnJoinClicked()
        {
            var (ip, port) = ReadEndpoint();
            if (NetworkSessionController.Instance != null && NetworkSessionController.Instance.StartClientSession(ip, port))
            {
                DismissMenu();
            }
        }

        private void OnServerClicked()
        {
            var (ip, port) = ReadEndpoint();
            if (NetworkSessionController.Instance != null && NetworkSessionController.Instance.StartDedicatedServer(ip, port))
            {
                _menuRoot.SetActive(false);
            }
        }

        private void OnOfflineClicked()
        {
            if (NetworkSessionController.Instance != null)
            {
                NetworkSessionController.Instance.StartOfflinePractice();
            }
            DismissMenu();
        }

        private async void OnQuickDevAuthClicked()
        {
            if (GameApiClient.Instance == null) return;

            if (GameApiClient.Instance.IsAuthenticated)
            {
                GameApiClient.Instance.Logout();
                RefreshAccountUI();
                return;
            }

            _statusLabel.text = "Authenticating test account with backend...";
            string devName = "Pilot_" + Random.Range(100, 999);
            var (ok, msg) = await GameApiClient.Instance.QuickDevLoginAsync(devName);
            if (ok)
            {
                _statusLabel.text = $"Authenticated as {GameApiClient.Instance.CurrentDisplayName}!";
                RefreshAccountUI();
            }
            else
            {
                _statusLabel.text = $"Auth error: {msg}";
            }
        }

        private async void OnCreateRoomClicked()
        {
            if (GameApiClient.Instance == null) return;
            if (!GameApiClient.Instance.IsAuthenticated)
            {
                _statusLabel.text = "Please log in first before creating an online room.";
                return;
            }

            _statusLabel.text = "Requesting new room code from backend API...";
            var options = new CreateRoomOptions
            {
                mode = NetworkSessionController.Instance != null && NetworkSessionController.Instance.ActiveGameMode == 1 ? "ConvoyOperations" : "GroundOperations",
                matchType = "StandardPursuit",
                mapId = "deadcity"
            };

            var (ok, room, err) = await GameApiClient.Instance.CreateRoomAsync(options);
            if (ok && room != null)
            {
                _roomCodeInput.text = room.code;
                _statusLabel.text = $"ROOM CREATED: CODE [{room.code}] — STARTING HOST...";
                OnHostClicked();
            }
            else
            {
                _statusLabel.text = $"Failed to create room: {err}";
            }
        }

        private async void OnJoinRoomClicked()
        {
            if (GameApiClient.Instance == null) return;
            if (!GameApiClient.Instance.IsAuthenticated)
            {
                _statusLabel.text = "Please log in first before joining a room.";
                return;
            }

            string code = _roomCodeInput.text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                _statusLabel.text = "Enter a valid 6-character room code.";
                return;
            }

            _statusLabel.text = $"Joining room [{code}]...";
            var (ok, join, err) = await GameApiClient.Instance.JoinRoomAsync(code);
            if (ok && join != null)
            {
                _statusLabel.text = $"JOINED ROOM [{code}] AS TEAM {join.team.ToUpper()}! CONNECTING...";
                OnJoinClicked();
            }
            else
            {
                _statusLabel.text = $"Join failed: {err}";
            }
        }

        private async void OnLeaderboardToggleClicked()
        {
            if (_leaderboardPanel == null) return;

            bool willShow = !_leaderboardPanel.activeSelf;
            _leaderboardPanel.SetActive(willShow);

            if (willShow && GameApiClient.Instance != null)
            {
                _leaderboardContent.text = "Querying survivors from backend API...";
                var list = await GameApiClient.Instance.FetchLeaderboardAsync();
                if (list != null && list.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < list.Count && i < 12; i++)
                    {
                        var e = list[i];
                        sb.AppendLine($"#{i + 1:00} {e.displayName,-14}  R{e.survivorRank}  {e.xp,6} XP  ({e.wins}W/{e.losses}L)");
                    }
                    _leaderboardContent.text = sb.ToString();
                }
                else
                {
                    _leaderboardContent.text = "No recorded matches found on backend.";
                }
            }
        }

        private void RefreshAccountUI()
        {
            if (_accountLabel == null) return;

            if (GameApiClient.Instance != null && GameApiClient.Instance.IsAuthenticated)
            {
                var p = GameApiClient.Instance.CurrentProfile;
                if (p != null)
                {
                    _accountLabel.text = $"★ {p.displayName} | RANK {p.survivorRank} | {p.xp} XP | {p.wins}W - {p.losses}L";
                }
                else
                {
                    _accountLabel.text = $"★ {GameApiClient.Instance.CurrentDisplayName} (ID: {GameApiClient.Instance.CurrentUserId.Substring(0, 8)}...)";
                }
            }
            else
            {
                _accountLabel.text = "GUEST MODE (UNAUTHENTICATED)";
            }
        }

        private void HandleAuthChanged(bool isAuthed, string name)
        {
            RefreshAccountUI();
        }

        private void HandleProfileUpdated(PlayerProfileDto profile)
        {
            RefreshAccountUI();
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
            var txt = CreateText(go.transform, "Text", defaultVal, font, 14, TextAnchor.MiddleLeft, new Vector2(10f, 0f), size - new Vector2(20f, 4f), Color.white);
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
            CreateText(go.transform, "Label", label, font, 14, TextAnchor.MiddleCenter, Vector2.zero, size, Color.white);
        }
    }
}
