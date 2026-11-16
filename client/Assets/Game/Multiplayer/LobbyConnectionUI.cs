using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OutrunAftermath.Client.Runtime.Backend;

namespace OutrunAftermath.Client.Multiplayer
{
    /// <summary>
    /// Interactive connection & tactical lobby overlay for OUTRUN: AFTERMATH.
    /// Integrates backend player accounts, matchmaking queues, squad/party management,
    /// friends network with presence, room codes, leaderboards, direct Host/Client/Server modes,
    /// dedicated Authentication Modal (Login / Register), map/weather selectors, and offline practice.
    /// </summary>
    public sealed class LobbyConnectionUI : MonoBehaviour
    {
        private GameObject _menuRoot;
        private Camera _lobbyCamera;

        // Account & Status
        private Text _statusLabel;
        private Text _accountLabel;
        private Button _authModalToggleBtn;
        private Text _authModalToggleBtnLabel;

        // Navigation Tabs
        private GameObject _tabMatchmaking;
        private GameObject _tabDirectConnect;
        private Button _tabMatchmakingBtn;
        private Button _tabDirectConnectBtn;

        // Matchmaking & Environment Selection
        private string _selectedMode = "GroundOperations"; // or "ConvoyOperations"
        private string _selectedRegion = "na-east";
        private readonly string[] _availableRegions = { "na-east", "eu-central", "ap-south" };
        private int _regionIndex = 0;

        private readonly string[] _availableMaps = { "deadcity", "dustlands", "rustharbor", "ironmountain", "scrapyard", "dam", "neondistrict" };
        private int _mapIndex = 0;
        private Text _mapBtnLabel;

        private readonly string[] _availableWeathers = { "Clear", "Sandstorm", "ToxicRain", "Overcast", "Night" };
        private int _weatherIndex = 0;
        private Text _weatherBtnLabel;

        private bool _isSearchingMatch = false;
        private float _searchTimer = 0f;
        private Text _modeBtnLabel;
        private Text _regionBtnLabel;
        private GameObject _queueIdleContainer;
        private GameObject _queueActiveContainer;
        private Text _queueSearchTimerText;

        // Squad / Party Controls
        private Text _partyHeaderLabel;
        private Text _partyRosterText;
        private GameObject _partyActionsContainer;
        private GameObject _partyCreateContainer;
        private InputField _partyCodeInput;
        private Text _readyBtnLabel;
        private Button _queuePartyBtn;

        // Direct Connect & Custom Rooms
        private InputField _ipInput;
        private InputField _portInput;
        private InputField _roomCodeInput;

        // Drawers & Modals
        private GameObject _leaderboardPanel;
        private Text _leaderboardContent;
        private GameObject _friendsPanel;
        private Text _friendsContent;
        private Text _friendsRequestsContent;
        private InputField _addFriendInput;
        private Text _friendsStatusLabel;
        private List<FriendRequestItemDto> _cachedRequests = new List<FriendRequestItemDto>();

        // Dedicated Authentication Modal
        private GameObject _authModal;
        private GameObject _authLoginTab;
        private GameObject _authRegisterTab;
        private Button _authTabLoginBtn;
        private Button _authTabRegisterBtn;
        private InputField _authLoginEmailInput;
        private InputField _authLoginPassInput;
        private InputField _authRegNameInput;
        private InputField _authRegEmailInput;
        private InputField _authRegPassInput;
        private Text _authStatusLabel;
        private readonly List<(RectTransform rt, Action onClick, GameObject go)> _registeredButtons = new List<(RectTransform, Action, GameObject)>();

        private void Start()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                // Headless server - skip UI construction
                return;
            }

            EnsureEventSystem();
            BuildLobbyUI();

            if (NetworkSessionController.Instance != null)
            {
                NetworkSessionController.Instance.OnStatusUpdated += HandleStatusUpdate;
            }

            if (GameApiClient.Instance != null)
            {
                GameApiClient.Instance.OnAuthStatusChanged += HandleAuthChanged;
                GameApiClient.Instance.OnProfileUpdated += HandleProfileUpdated;
                GameApiClient.Instance.OnPartyChanged += HandlePartyChanged;
                GameApiClient.Instance.OnMatchmakingTicketUpdated += HandleTicketUpdated;
                GameApiClient.Instance.OnMatchFound += HandleMatchFound;
                RefreshAccountUI();
                RefreshPartyUI();
            }
        }

        private void Update()
        {
            // 1. Direct screen-space mouse click fallback (guarantees click execution even if EventSystem raycast misses)
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Input.mousePosition;
                for (int i = _registeredButtons.Count - 1; i >= 0; i--)
                {
                    var entry = _registeredButtons[i];
                    if (entry.go != null && entry.go.activeInHierarchy && entry.rt != null)
                    {
                        if (RectTransformUtility.RectangleContainsScreenPoint(entry.rt, mousePos, null))
                        {
                            entry.onClick?.Invoke();
                            break;
                        }
                    }
                }
            }

            // 2. Keyboard shortcuts for immediate turnkey control
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnOfflineClicked();
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                OnToggleMapClicked();
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                OnToggleWeatherClicked();
            }
            else if (Input.GetKeyDown(KeyCode.L))
            {
                OnToggleAuthModalClicked();
            }
            else if (Input.GetKeyDown(KeyCode.H))
            {
                OnHostClicked();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_authModal != null && _authModal.activeSelf) _authModal.SetActive(false);
                else if (_leaderboardPanel != null && _leaderboardPanel.activeSelf) _leaderboardPanel.SetActive(false);
                else if (_friendsPanel != null && _friendsPanel.activeSelf) _friendsPanel.SetActive(false);
                else Runtime.UI.SettingsModalUI.Instance?.ToggleOpen();
            }

            if (_isSearchingMatch)
            {
                _searchTimer += Time.deltaTime;
                if (_queueSearchTimerText != null)
                {
                    int mins = (int)(_searchTimer / 60f);
                    int secs = (int)(_searchTimer % 60f);
                    _queueSearchTimerText.text = $"SEARCHING FOR MATCH ({mins:00}:{secs:00})...";
                }
            }
        }

        private static void EnsureEventSystem()
        {
            var es = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            var inputModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            inputModule.enabled = false;
            inputModule.AssignDefaultActions();
            if (inputModule.actionsAsset != null)
            {
                inputModule.actionsAsset.Enable();
                foreach (var map in inputModule.actionsAsset.actionMaps)
                {
                    map.Enable();
                    foreach (var act in map.actions)
                    {
                        act.Enable();
                    }
                }
            }
            inputModule.enabled = true;
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
                GameApiClient.Instance.OnPartyChanged -= HandlePartyChanged;
                GameApiClient.Instance.OnMatchmakingTicketUpdated -= HandleTicketUpdated;
                GameApiClient.Instance.OnMatchFound -= HandleMatchFound;
            }

            if (_lobbyCamera != null)
            {
                Destroy(_lobbyCamera.gameObject);
            }
        }

        private void BuildLobbyUI()
        {
            if (Camera.main == null)
            {
                var camGo = new GameObject("LobbyOverviewCamera");
                _lobbyCamera = camGo.AddComponent<Camera>();
                _lobbyCamera.transform.position = new Vector3(14f, 12f, -26f);
                _lobbyCamera.transform.rotation = Quaternion.Euler(20f, -22f, 0f);
                camGo.AddComponent<LobbyCameraDrift>();
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

            // Center Dark Tactical Main Window
            _menuRoot = CreatePanel(canvasGo.transform, "ConnectionPanel", Vector2.zero, new Vector2(740f, 760f), new Color(0.06f, 0.08f, 0.12f, 0.98f));

            // Glowing Header Border
            CreatePanel(_menuRoot.transform, "HeaderAccent", new Vector2(0f, 375f), new Vector2(740f, 6f), new Color(0.96f, 0.52f, 0.12f));

            CreateText(_menuRoot.transform, "Title", "OUTRUN: AFTERMATH", font, 32, TextAnchor.MiddleCenter, new Vector2(0f, 335f), new Vector2(680f, 38f), new Color(0.98f, 0.58f, 0.14f));
            CreateText(_menuRoot.transform, "Subtitle", "TACTICAL MULTIPLAYER OPERATIONS // CROSS-PLATFORM NETWORK", font, 11, TextAnchor.MiddleCenter, new Vector2(0f, 306f), new Vector2(680f, 20f), new Color(0.68f, 0.84f, 0.95f));

            // Top Account & Presence Bar
            var accountBar = CreatePanel(_menuRoot.transform, "AccountBar", new Vector2(0f, 264f), new Vector2(680f, 44f), new Color(0.10f, 0.13f, 0.18f, 0.98f));
            _accountLabel = CreateText(accountBar.transform, "AccountText", "GUEST MODE (UNAUTHENTICATED)", font, 12, TextAnchor.MiddleLeft, new Vector2(-120f, 0f), new Vector2(380f, 34f), Color.white);

            CreateButton(accountBar.transform, "SettingsBtn", "SETTINGS", font, new Vector2(145f, 0f), new Vector2(100f, 32f), new Color(0.22f, 0.30f, 0.40f), () =>
            {
                if (Runtime.UI.SettingsModalUI.Instance != null)
                {
                    Runtime.UI.SettingsModalUI.Instance.ToggleOpen();
                }
            });

            _authModalToggleBtn = CreateButton(accountBar.transform, "AuthBtn", "LOGIN / REGISTER", font, new Vector2(265f, 0f), new Vector2(125f, 32f), new Color(0.18f, 0.48f, 0.32f), OnToggleAuthModalClicked);
            _authModalToggleBtnLabel = _authModalToggleBtn.GetComponentInChildren<Text>();

            // Tab Navigation Bar
            var tabNavBar = CreatePanel(_menuRoot.transform, "TabNavBar", new Vector2(0f, 214f), new Vector2(680f, 40f), new Color(0.08f, 0.10f, 0.14f, 1f));
            _tabMatchmakingBtn = CreateButton(tabNavBar.transform, "TabMmBtn", "MATCHMAKING & SQUADS", font, new Vector2(-170f, 0f), new Vector2(330f, 34f), new Color(0.85f, 0.45f, 0.12f), () => SwitchTab(0));
            _tabDirectConnectBtn = CreateButton(tabNavBar.transform, "TabDcBtn", "DIRECT CONNECT & ROOMS", font, new Vector2(170f, 0f), new Vector2(330f, 34f), new Color(0.18f, 0.22f, 0.28f), () => SwitchTab(1));

            // ==========================================
            // TAB 0: MATCHMAKING & SQUADS
            // ==========================================
            _tabMatchmaking = CreatePanel(_menuRoot.transform, "TabMatchmaking", new Vector2(0f, -5f), new Vector2(680f, 370f), Color.clear);

            // Mode & Region Selector Bar
            CreateText(_tabMatchmaking.transform, "ModeHeader", "OPERATION MODE & REGION", font, 11, TextAnchor.MiddleLeft, new Vector2(-180f, 160f), new Vector2(280f, 20f), Color.gray);
            var modeBtn = CreateButton(_tabMatchmaking.transform, "ToggleModeBtn", "MODE: GROUND OPERATIONS", font, new Vector2(-155f, 126f), new Vector2(310f, 36f), new Color(0.16f, 0.26f, 0.38f), OnToggleModeClicked);
            _modeBtnLabel = modeBtn.GetComponentInChildren<Text>();

            var regBtn = CreateButton(_tabMatchmaking.transform, "ToggleRegionBtn", "REGION: NA-EAST", font, new Vector2(165f, 126f), new Vector2(290f, 36f), new Color(0.16f, 0.26f, 0.38f), OnToggleRegionClicked);
            _regionBtnLabel = regBtn.GetComponentInChildren<Text>();

            // Queue Section (Idle vs Searching)
            _queueIdleContainer = CreatePanel(_tabMatchmaking.transform, "QueueIdle", new Vector2(0f, 65f), new Vector2(640f, 54f), Color.clear);
            CreateButton(_queueIdleContainer.transform, "FindMatchBtn", "FIND MATCH", font, Vector2.zero, new Vector2(480f, 48f), new Color(0.92f, 0.48f, 0.14f), OnFindMatchClicked);

            _queueActiveContainer = CreatePanel(_tabMatchmaking.transform, "QueueActive", new Vector2(0f, 65f), new Vector2(640f, 54f), Color.clear);
            var queueBox = CreatePanel(_queueActiveContainer.transform, "QueueBox", new Vector2(-75f, 0f), new Vector2(420f, 48f), new Color(0.12f, 0.24f, 0.36f, 0.95f));
            _queueSearchTimerText = CreateText(queueBox.transform, "QueueTimer", "SEARCHING FOR MATCH (00:00)...", font, 13, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(400f, 40f), Color.cyan);
            CreateButton(_queueActiveContainer.transform, "CancelMatchBtn", "CANCEL", font, new Vector2(195f, 0f), new Vector2(150f, 48f), new Color(0.68f, 0.2f, 0.2f), OnCancelMatchClicked);
            _queueActiveContainer.SetActive(false);

            // Squad / Party Sub-panel
            var partyBox = CreatePanel(_tabMatchmaking.transform, "PartyBox", new Vector2(0f, -75f), new Vector2(640f, 185f), new Color(0.09f, 0.12f, 0.16f, 0.95f));
            _partyHeaderLabel = CreateText(partyBox.transform, "PartyHeader", "SURVIVOR SQUAD (NOT IN SQUAD)", font, 12, TextAnchor.MiddleLeft, new Vector2(-150f, 72f), new Vector2(320f, 22f), new Color(0.95f, 0.65f, 0.2f));

            // Squad Roster Display
            _partyRosterText = CreateText(partyBox.transform, "PartyRoster", "Create a squad or enter a 6-character squad code to deploy together.", font, 11, TextAnchor.UpperLeft, new Vector2(-60f, 2f), new Vector2(480f, 90f), Color.white);

            // Party Create/Join Container (shown when NOT in party)
            _partyCreateContainer = CreatePanel(partyBox.transform, "PartyCreateContainer", new Vector2(0f, -50f), new Vector2(620f, 44f), Color.clear);
            CreateButton(_partyCreateContainer.transform, "CreatePartyBtn", "CREATE SQUAD", font, new Vector2(-180f, 0f), new Vector2(190f, 36f), new Color(0.22f, 0.46f, 0.25f), OnCreatePartyClicked);
            _partyCodeInput = CreateInputField(_partyCreateContainer.transform, "PartyCodeInput", "PTY...", font, new Vector2(35f, 0f), new Vector2(160f, 36f));
            CreateButton(_partyCreateContainer.transform, "JoinPartyBtn", "JOIN CODE", font, new Vector2(195f, 0f), new Vector2(140f, 36f), new Color(0.18f, 0.4f, 0.65f), OnJoinPartyClicked);

            // Party In-Party Actions (shown when IN party)
            _partyActionsContainer = CreatePanel(partyBox.transform, "PartyActionsContainer", new Vector2(0f, -52f), new Vector2(620f, 38f), Color.clear);
            var readyBtn = CreateButton(_partyActionsContainer.transform, "ToggleReadyBtn", "READY", font, new Vector2(-200f, 0f), new Vector2(140f, 36f), new Color(0.2f, 0.55f, 0.25f), OnToggleReadyClicked);
            _readyBtnLabel = readyBtn.GetComponentInChildren<Text>();
            _queuePartyBtn = CreateButton(_partyActionsContainer.transform, "QueuePartyBtn", "QUEUE SQUAD", font, new Vector2(-35f, 0f), new Vector2(170f, 36f), new Color(0.85f, 0.45f, 0.12f), OnFindMatchClicked);
            CreateButton(_partyActionsContainer.transform, "LeavePartyBtn", "LEAVE SQUAD", font, new Vector2(175f, 0f), new Vector2(170f, 36f), new Color(0.55f, 0.2f, 0.2f), OnLeavePartyClicked);
            _partyActionsContainer.SetActive(false);

            // ==========================================
            // TAB 1: DIRECT CONNECT & CUSTOM ROOMS
            // ==========================================
            _tabDirectConnect = CreatePanel(_menuRoot.transform, "TabDirectConnect", new Vector2(0f, -5f), new Vector2(680f, 370f), Color.clear);

            // Room Code Section
            CreateText(_tabDirectConnect.transform, "RoomLabel", "ONLINE ROOM CODE", font, 11, TextAnchor.MiddleLeft, new Vector2(-160f, 150f), new Vector2(190f, 20f), Color.gray);
            _roomCodeInput = CreateInputField(_tabDirectConnect.transform, "RoomCodeInput", "DEAD66", font, new Vector2(-120f, 122f), new Vector2(250f, 36f));
            CreateButton(_tabDirectConnect.transform, "CreateRoomBtn", "CREATE ROOM", font, new Vector2(70f, 122f), new Vector2(140f, 36f), new Color(0.75f, 0.45f, 0.1f), OnCreateRoomClicked);
            CreateButton(_tabDirectConnect.transform, "JoinRoomBtn", "JOIN CODE", font, new Vector2(225f, 122f), new Vector2(130f, 36f), new Color(0.15f, 0.45f, 0.75f), OnJoinRoomClicked);

            // Direct IP & Port Field
            CreateText(_tabDirectConnect.transform, "IpLabel", "SERVER IP", font, 11, TextAnchor.MiddleLeft, new Vector2(-160f, 72f), new Vector2(190f, 20f), Color.gray);
            _ipInput = CreateInputField(_tabDirectConnect.transform, "IpInput", "127.0.0.1", font, new Vector2(-120f, 44f), new Vector2(250f, 36f));

            CreateText(_tabDirectConnect.transform, "PortLabel", "UDP PORT", font, 11, TextAnchor.MiddleLeft, new Vector2(130f, 72f), new Vector2(110f, 20f), Color.gray);
            _portInput = CreateInputField(_tabDirectConnect.transform, "PortInput", "7777", font, new Vector2(150f, 44f), new Vector2(150f, 36f));

            // Core Action Buttons
            CreateButton(_tabDirectConnect.transform, "HostBtn", "HOST MATCH (SERVER + PLAYER)", font, new Vector2(0f, -16f), new Vector2(500f, 44f), new Color(0.82f, 0.4f, 0.14f), OnHostClicked);
            CreateButton(_tabDirectConnect.transform, "JoinBtn", "DIRECT JOIN (CLIENT)", font, new Vector2(0f, -68f), new Vector2(500f, 44f), new Color(0.18f, 0.45f, 0.78f), OnJoinClicked);
            CreateButton(_tabDirectConnect.transform, "ServerBtn", "DEDICATED SERVER MODE", font, new Vector2(0f, -120f), new Vector2(500f, 40f), new Color(0.24f, 0.28f, 0.32f), OnServerClicked);

            _tabDirectConnect.SetActive(false);

            // ==========================================
            // MAP & WEATHER SELECTOR BAR
            // ==========================================
            var mapWeatherBar = CreatePanel(_menuRoot.transform, "MapWeatherBar", new Vector2(0f, -210f), new Vector2(680f, 42f), new Color(0.09f, 0.11f, 0.15f, 1f));
            var mapBtn = CreateButton(mapWeatherBar.transform, "ToggleMapBtn", "MAP: DEADCITY", font, new Vector2(-170f, 0f), new Vector2(310f, 34f), new Color(0.18f, 0.24f, 0.32f), OnToggleMapClicked);
            _mapBtnLabel = mapBtn.GetComponentInChildren<Text>();

            var weatherBtn = CreateButton(mapWeatherBar.transform, "ToggleWeatherBtn", "WEATHER: CLEAR", font, new Vector2(170f, 0f), new Vector2(310f, 34f), new Color(0.18f, 0.24f, 0.32f), OnToggleWeatherClicked);
            _weatherBtnLabel = weatherBtn.GetComponentInChildren<Text>();

            // ==========================================
            // BOTTOM UTILITY & SOCIAL BUTTONS
            // ==========================================
            var bottomBar = CreatePanel(_menuRoot.transform, "BottomBar", new Vector2(0f, -265f), new Vector2(680f, 44f), Color.clear);
            CreateButton(bottomBar.transform, "FriendsBtn", "SQUAD NETWORK", font, new Vector2(-215f, 0f), new Vector2(210f, 38f), new Color(0.22f, 0.42f, 0.62f), OnFriendsToggleClicked);
            CreateButton(bottomBar.transform, "LeaderboardBtn", "LEADERBOARDS", font, new Vector2(0f, 0f), new Vector2(180f, 38f), new Color(0.35f, 0.3f, 0.45f), OnLeaderboardToggleClicked);
            CreateButton(bottomBar.transform, "OfflineBtn", "OFFLINE PRACTICE", font, new Vector2(215f, 0f), new Vector2(210f, 38f), new Color(0.18f, 0.45f, 0.28f), OnOfflineClicked);

            _statusLabel = CreateText(_menuRoot.transform, "Status", "READY — SELECT DEPLOYMENT OR FIND MATCH", font, 12, TextAnchor.MiddleCenter, new Vector2(0f, -330f), new Vector2(680f, 32f), Color.yellow);

            // ==========================================
            // SIDE DRAWER: FRIENDS & SOCIAL PANEL
            // ==========================================
            _friendsPanel = CreatePanel(canvasGo.transform, "FriendsPanel", new Vector2(560f, 0f), new Vector2(370f, 760f), new Color(0.08f, 0.1f, 0.14f, 0.98f));
            CreateText(_friendsPanel.transform, "FrTitle", "SQUAD NETWORK", font, 20, TextAnchor.MiddleCenter, new Vector2(0f, 330f), new Vector2(330f, 30f), new Color(0.3f, 0.7f, 1f));
            CreateText(_friendsPanel.transform, "FrSub", "ONLINE PRESENCE & SQUAD INVITES", font, 10, TextAnchor.MiddleCenter, new Vector2(0f, 306f), new Vector2(330f, 18f), Color.gray);

            _friendsContent = CreateText(_friendsPanel.transform, "FrList", "Sign in to view online squadmates...", font, 11, TextAnchor.UpperLeft, new Vector2(0f, 150f), new Vector2(330f, 270f), Color.white);
            CreateText(_friendsPanel.transform, "FrReqHeader", "PENDING SQUAD REQUESTS", font, 11, TextAnchor.MiddleLeft, new Vector2(-15f, 5f), new Vector2(300f, 20f), Color.yellow);
            _friendsRequestsContent = CreateText(_friendsPanel.transform, "FrReqList", "No pending invites.", font, 11, TextAnchor.UpperLeft, new Vector2(0f, -50f), new Vector2(330f, 80f), Color.gray);

            _addFriendInput = CreateInputField(_friendsPanel.transform, "AddFriendInput", "Callsign...", font, new Vector2(-60f, -120f), new Vector2(200f, 34f));
            CreateButton(_friendsPanel.transform, "SendReqBtn", "ADD SQUADMATE", font, new Vector2(85f, -120f), new Vector2(130f, 34f), new Color(0.2f, 0.5f, 0.3f), OnSendFriendRequestClicked);

            _friendsStatusLabel = CreateText(_friendsPanel.transform, "FrStatus", "", font, 11, TextAnchor.MiddleCenter, new Vector2(0f, -170f), new Vector2(330f, 30f), Color.yellow);
            CreateButton(_friendsPanel.transform, "CloseFrBtn", "CLOSE PANEL", font, new Vector2(0f, -230f), new Vector2(280f, 36f), new Color(0.4f, 0.2f, 0.2f), () => _friendsPanel.SetActive(false));
            _friendsPanel.SetActive(false);

            // ==========================================
            // SIDE DRAWER: LEADERBOARDS PANEL
            // ==========================================
            _leaderboardPanel = CreatePanel(canvasGo.transform, "LeaderboardPanel", new Vector2(-560f, 0f), new Vector2(370f, 760f), new Color(0.08f, 0.1f, 0.14f, 0.98f));
            CreateText(_leaderboardPanel.transform, "LbTitle", "TOP SURVIVORS", font, 20, TextAnchor.MiddleCenter, new Vector2(0f, 330f), new Vector2(330f, 30f), new Color(0.95f, 0.7f, 0.2f));
            CreateText(_leaderboardPanel.transform, "LbSub", "GLOBAL OUTRUN OPERATOR STANDINGS", font, 10, TextAnchor.MiddleCenter, new Vector2(0f, 306f), new Vector2(330f, 18f), Color.gray);

            _leaderboardContent = CreateText(_leaderboardPanel.transform, "LbContent", "Loading standings from backend...", font, 12, TextAnchor.UpperLeft, new Vector2(0f, 50f), new Vector2(330f, 460f), Color.white);
            CreateButton(_leaderboardPanel.transform, "CloseLbBtn", "CLOSE STANDINGS", font, new Vector2(0f, -230f), new Vector2(280f, 36f), new Color(0.4f, 0.2f, 0.2f), () => _leaderboardPanel.SetActive(false));
            _leaderboardPanel.SetActive(false);

            // ==========================================
            // AUTHENTICATION TERMINAL MODAL (LOGIN / REGISTER)
            // ==========================================
            BuildAuthModal(canvasGo.transform, font);
        }

        private void BuildAuthModal(Transform parent, Font font)
        {
            _authModal = CreatePanel(parent, "AuthModal", Vector2.zero, new Vector2(580f, 520f), new Color(0.08f, 0.10f, 0.15f, 0.99f));
            CreatePanel(_authModal.transform, "AuthHeaderGlow", new Vector2(0f, 257f), new Vector2(580f, 6f), new Color(0.2f, 0.65f, 0.45f));

            CreateText(_authModal.transform, "AuthTitle", "OUTRUN: ACCESS CONTROL TERMINAL", font, 22, TextAnchor.MiddleCenter, new Vector2(0f, 225f), new Vector2(540f, 32f), new Color(0.28f, 0.85f, 0.55f));
            CreateText(_authModal.transform, "AuthSub", "AUTHENTICATE OPERATOR OR COMMISSION NEW PERSONNEL", font, 10, TextAnchor.MiddleCenter, new Vector2(0f, 198f), new Vector2(540f, 20f), Color.gray);

            // Switch Tabs (Sign In vs Register)
            var authNav = CreatePanel(_authModal.transform, "AuthNav", new Vector2(0f, 160f), new Vector2(520f, 38f), new Color(0.12f, 0.15f, 0.20f));
            _authTabLoginBtn = CreateButton(authNav.transform, "AuthTabLogin", "OPERATOR SIGN IN", font, new Vector2(-130f, 0f), new Vector2(250f, 32f), new Color(0.2f, 0.55f, 0.35f), () => SwitchAuthTab(0));
            _authTabRegisterBtn = CreateButton(authNav.transform, "AuthTabReg", "RECRUIT NEW OPERATOR", font, new Vector2(130f, 0f), new Vector2(250f, 32f), new Color(0.18f, 0.22f, 0.28f), () => SwitchAuthTab(1));

            // ==========================================
            // AUTH TAB 0: SIGN IN
            // ==========================================
            _authLoginTab = CreatePanel(_authModal.transform, "AuthLoginTab", new Vector2(0f, -10f), new Vector2(520f, 260f), Color.clear);

            CreateText(_authLoginTab.transform, "LoginCallsignLabel", "OPERATOR CALLSIGN / EMAIL:", font, 11, TextAnchor.MiddleLeft, new Vector2(0f, 85f), new Vector2(480f, 20f), Color.white);
            _authLoginEmailInput = CreateInputField(_authLoginTab.transform, "LoginCallsignInput", "Vanguard_1", font, new Vector2(0f, 55f), new Vector2(480f, 36f));

            CreateText(_authLoginTab.transform, "LoginPassLabel", "SECURITY PASSKEY:", font, 11, TextAnchor.MiddleLeft, new Vector2(0f, 15f), new Vector2(480f, 20f), Color.white);
            _authLoginPassInput = CreateInputField(_authLoginTab.transform, "LoginPassInput", "Pass123!", font, new Vector2(0f, -15f), new Vector2(480f, 36f), isPassword: true);

            CreateButton(_authLoginTab.transform, "SubmitLoginBtn", "AUTHORIZE & DEPLOY", font, new Vector2(-120f, -75f), new Vector2(230f, 42f), new Color(0.22f, 0.62f, 0.35f), OnSubmitLoginClicked);
            CreateButton(_authLoginTab.transform, "QuickDevBtn", "QUICK GUEST / DEV", font, new Vector2(120f, -75f), new Vector2(230f, 42f), new Color(0.20f, 0.35f, 0.48f), OnQuickDevAuthClicked);

            // ==========================================
            // AUTH TAB 1: RECRUIT (REGISTER)
            // ==========================================
            _authRegisterTab = CreatePanel(_authModal.transform, "AuthRegisterTab", new Vector2(0f, -10f), new Vector2(520f, 260f), Color.clear);

            CreateText(_authRegisterTab.transform, "RegNameLabel", "NEW OPERATOR CALLSIGN:", font, 11, TextAnchor.MiddleLeft, new Vector2(0f, 95f), new Vector2(480f, 20f), Color.white);
            _authRegNameInput = CreateInputField(_authRegisterTab.transform, "RegNameInput", "Shadow_99", font, new Vector2(0f, 70f), new Vector2(480f, 34f));

            CreateText(_authRegisterTab.transform, "RegEmailLabel", "COMMUNICATION EMAIL:", font, 11, TextAnchor.MiddleLeft, new Vector2(0f, 35f), new Vector2(480f, 20f), Color.white);
            _authRegEmailInput = CreateInputField(_authRegisterTab.transform, "RegEmailInput", "operator@outrun.test", font, new Vector2(0f, 10f), new Vector2(480f, 34f));

            CreateText(_authRegisterTab.transform, "RegPassLabel", "SECURITY PASSKEY:", font, 11, TextAnchor.MiddleLeft, new Vector2(0f, -25f), new Vector2(480f, 20f), Color.white);
            _authRegPassInput = CreateInputField(_authRegisterTab.transform, "RegPassInput", "Pass123!", font, new Vector2(0f, -50f), new Vector2(480f, 34f), isPassword: true);

            CreateButton(_authRegisterTab.transform, "SubmitRegBtn", "COMMISSION & DEPLOY", font, new Vector2(0f, -95f), new Vector2(480f, 40f), new Color(0.85f, 0.45f, 0.12f), OnSubmitRegisterClicked);
            _authRegisterTab.SetActive(false);

            // Auth Status & Close
            _authStatusLabel = CreateText(_authModal.transform, "AuthStatus", "Awaiting operator credentials...", font, 11, TextAnchor.MiddleCenter, new Vector2(0f, -195f), new Vector2(520f, 28f), Color.yellow);
            CreateButton(_authModal.transform, "CloseAuthBtn", "DISMISS TERMINAL", font, new Vector2(0f, -230f), new Vector2(260f, 32f), new Color(0.35f, 0.20f, 0.22f), () => _authModal.SetActive(false));

            _authModal.SetActive(false);
        }

        private void SwitchTab(int tabIndex)
        {
            _tabMatchmaking.SetActive(tabIndex == 0);
            _tabDirectConnect.SetActive(tabIndex == 1);

            _tabMatchmakingBtn.GetComponent<Image>().color = tabIndex == 0 ? new Color(0.85f, 0.45f, 0.12f) : new Color(0.18f, 0.22f, 0.28f);
            _tabDirectConnectBtn.GetComponent<Image>().color = tabIndex == 1 ? new Color(0.85f, 0.45f, 0.12f) : new Color(0.18f, 0.22f, 0.28f);
        }

        private void SwitchAuthTab(int tabIndex)
        {
            _authLoginTab.SetActive(tabIndex == 0);
            _authRegisterTab.SetActive(tabIndex == 1);

            _authTabLoginBtn.GetComponent<Image>().color = tabIndex == 0 ? new Color(0.2f, 0.55f, 0.35f) : new Color(0.18f, 0.22f, 0.28f);
            _authTabRegisterBtn.GetComponent<Image>().color = tabIndex == 1 ? new Color(0.85f, 0.45f, 0.12f) : new Color(0.18f, 0.22f, 0.28f);
        }

        private void OnToggleAuthModalClicked()
        {
            if (GameApiClient.Instance != null && GameApiClient.Instance.IsAuthenticated)
            {
                // If authenticated, clicking acts as Logout
                GameApiClient.Instance.Logout();
                RefreshAccountUI();
                RefreshPartyUI();
                _statusLabel.text = "Logged out of operator profile. Guest mode active.";
                return;
            }

            _authModal.SetActive(!_authModal.activeSelf);
        }

        private async void OnSubmitLoginClicked()
        {
            if (GameApiClient.Instance == null) return;
            string input = _authLoginEmailInput.text?.Trim();
            string pass = _authLoginPassInput.text?.Trim();

            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(pass))
            {
                _authStatusLabel.text = "Error: Callsign and password cannot be empty.";
                return;
            }

            string email = input.Contains("@") ? input : $"{input.ToLowerInvariant()}@outrun.local";
            _authStatusLabel.text = "Authorizing credentials with central database...";

            var (ok, msg) = await GameApiClient.Instance.LoginAsync(email, pass);
            if (ok)
            {
                _authStatusLabel.text = "Authorization confirmed! Operator active.";
                _statusLabel.text = $"Authenticated as {GameApiClient.Instance.CurrentDisplayName}!";
                RefreshAccountUI();
                RefreshPartyUI();
                _authModal.SetActive(false);
            }
            else
            {
                _authStatusLabel.text = $"Authorization rejected: {msg}";
            }
        }

        private async void OnSubmitRegisterClicked()
        {
            if (GameApiClient.Instance == null) return;
            string name = _authRegNameInput.text?.Trim();
            string email = _authRegEmailInput.text?.Trim();
            string pass = _authRegPassInput.text?.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
            {
                _authStatusLabel.text = "Error: All fields are mandatory for commission.";
                return;
            }

            _authStatusLabel.text = "Registering new operator profile with database...";
            var (ok, msg) = await GameApiClient.Instance.RegisterAsync(email, pass, name);
            if (ok)
            {
                _authStatusLabel.text = "Operator commissioned! Welcome to the division.";
                _statusLabel.text = $"Registered & Authenticated as {name}!";
                RefreshAccountUI();
                RefreshPartyUI();
                _authModal.SetActive(false);
            }
            else
            {
                _authStatusLabel.text = $"Commission failed: {msg}";
            }
        }

        private void OnToggleModeClicked()
        {
            _selectedMode = _selectedMode == "GroundOperations" ? "ConvoyOperations" : "GroundOperations";
            string modeName = _selectedMode == "GroundOperations" ? "GROUND OPERATIONS" : "CONVOY OPERATIONS (CREWED)";
            _modeBtnLabel.text = $"MODE: {modeName}";
        }

        private void OnToggleRegionClicked()
        {
            _regionIndex = (_regionIndex + 1) % _availableRegions.Length;
            _selectedRegion = _availableRegions[_regionIndex];
            _regionBtnLabel.text = $"REGION: {_selectedRegion.ToUpper()}";
        }

        private void OnToggleMapClicked()
        {
            _mapIndex = (_mapIndex + 1) % _availableMaps.Length;
            string mapId = _availableMaps[_mapIndex];
            _mapBtnLabel.text = $"MAP: {mapId.ToUpper()}";
            if (Runtime.Environment.MapEnvironmentDirector.Instance != null)
            {
                Runtime.Environment.MapEnvironmentDirector.Instance.LoadMap(mapId);
            }
        }

        private void OnToggleWeatherClicked()
        {
            _weatherIndex = (_weatherIndex + 1) % _availableWeathers.Length;
            string weatherStr = _availableWeathers[_weatherIndex];
            _weatherBtnLabel.text = $"WEATHER: {weatherStr.ToUpper()}";
            if (Runtime.Environment.AtmosphereDirector.Instance != null &&
                Enum.TryParse<Runtime.Environment.WeatherCondition>(weatherStr, out var cond))
            {
                Runtime.Environment.AtmosphereDirector.Instance.ApplyWeather(cond);
            }
        }

        private async void OnFindMatchClicked()
        {
            if (GameApiClient.Instance == null) return;
            if (!GameApiClient.Instance.IsAuthenticated)
            {
                _statusLabel.text = "Authentication required for matchmaking. Opening terminal...";
                _authModal.SetActive(true);
                return;
            }

            _isSearchingMatch = true;
            _searchTimer = 0f;
            _queueIdleContainer.SetActive(false);
            _queueActiveContainer.SetActive(true);

            _statusLabel.text = $"Queuing for {_selectedMode} in {_selectedRegion}...";
            var (ok, ticket, msg) = await GameApiClient.Instance.EnqueueMatchmakingAsync(_selectedMode, _selectedRegion, false, "StandardPursuit");
            if (!ok)
            {
                _statusLabel.text = $"Matchmaking error: {msg}";
                _isSearchingMatch = false;
                _queueIdleContainer.SetActive(true);
                _queueActiveContainer.SetActive(false);
            }
        }

        private async void OnCancelMatchClicked()
        {
            _isSearchingMatch = false;
            _queueIdleContainer.SetActive(true);
            _queueActiveContainer.SetActive(false);
            _statusLabel.text = "Matchmaking search canceled.";

            if (GameApiClient.Instance != null && GameApiClient.Instance.IsAuthenticated)
            {
                await GameApiClient.Instance.CancelMatchmakingAsync();
            }
        }

        private void HandleTicketUpdated(MatchmakingTicketDto ticket)
        {
            if (ticket == null) return;
            _statusLabel.text = $"Queue status: {ticket.status.ToUpper()} (Ticket {ticket.ticketId.Substring(0, 8)}...)";
        }

        private void HandleMatchFound(MatchedAssignmentDto assignment)
        {
            _isSearchingMatch = false;
            _queueIdleContainer.SetActive(true);
            _queueActiveContainer.SetActive(false);

            _statusLabel.text = $"MATCH FOUND! Dispatching to {assignment.serverIp}:{assignment.serverPort} ({assignment.mapId})...";
            if (NetworkSessionController.Instance != null)
            {
                NetworkSessionController.Instance.StartClientSession(assignment.serverIp, (ushort)assignment.serverPort);
                DismissMenu();
            }
        }

        private async void OnCreatePartyClicked()
        {
            if (GameApiClient.Instance == null) return;
            if (!GameApiClient.Instance.IsAuthenticated)
            {
                _statusLabel.text = "Please log in first to create a squad.";
                _authModal.SetActive(true);
                return;
            }

            _statusLabel.text = "Registering new survivor squad...";
            var (ok, party, msg) = await GameApiClient.Instance.CreatePartyAsync();
            if (ok)
            {
                _statusLabel.text = "Squad created successfully!";
                RefreshPartyUI();
            }
            else
            {
                _statusLabel.text = $"Squad creation error: {msg}";
            }
        }

        private async void OnJoinPartyClicked()
        {
            if (GameApiClient.Instance == null) return;
            if (!GameApiClient.Instance.IsAuthenticated)
            {
                _statusLabel.text = "Please log in first to join a squad.";
                _authModal.SetActive(true);
                return;
            }

            string code = _partyCodeInput.text?.Trim().ToUpper();
            if (string.IsNullOrEmpty(code) || code.Length != 6)
            {
                _statusLabel.text = "Please enter a valid 6-character squad code.";
                return;
            }

            _statusLabel.text = $"Joining squad {code}...";
            var (ok, party, msg) = await GameApiClient.Instance.JoinPartyByCodeAsync(code);
            if (ok)
            {
                _statusLabel.text = $"Joined squad {code}!";
                RefreshPartyUI();
            }
            else
            {
                _statusLabel.text = $"Squad join error: {msg}";
            }
        }

        private async void OnToggleReadyClicked()
        {
            if (GameApiClient.Instance == null) return;
            var party = GameApiClient.Instance.CurrentParty;
            if (party == null) return;

            bool currentReady = false;
            foreach (var m in party.members)
            {
                if (m.userId == GameApiClient.Instance.CurrentUserId.ToString())
                {
                    currentReady = m.isReady;
                    break;
                }
            }

            bool newReady = !currentReady;
            var (ok, msg) = await GameApiClient.Instance.SetPartyReadyAsync(newReady);
            if (ok)
            {
                _readyBtnLabel.text = newReady ? "READY (WAITING)" : "NOT READY";
                _readyBtnLabel.color = newReady ? Color.yellow : Color.white;
            }
        }

        private async void OnLeavePartyClicked()
        {
            if (GameApiClient.Instance == null) return;
            var (ok, msg) = await GameApiClient.Instance.LeavePartyAsync();
            if (ok)
            {
                _statusLabel.text = "Left survivor squad.";
                RefreshPartyUI();
            }
        }

        private void HandlePartyChanged(PartyDetailsDto party)
        {
            RefreshPartyUI();
        }

        private void RefreshPartyUI()
        {
            if (GameApiClient.Instance == null || !GameApiClient.Instance.IsAuthenticated || GameApiClient.Instance.CurrentParty == null)
            {
                _partyHeaderLabel.text = "SURVIVOR SQUAD (NOT IN SQUAD)";
                _partyRosterText.text = "Create a squad or enter a 6-character squad code to deploy together.\nSquadmates are guaranteed to deploy on the same tactical team.";
                _partyCreateContainer.SetActive(true);
                _partyActionsContainer.SetActive(false);
                return;
            }

            var party = GameApiClient.Instance.CurrentParty;
            _partyHeaderLabel.text = $"SURVIVOR SQUAD // CODE: {party.code}";
            _partyCreateContainer.SetActive(false);
            _partyActionsContainer.SetActive(true);

            string roster = $"MEMBERS ({party.members.Count}/4):\n";
            bool isLeader = party.leaderUserId == GameApiClient.Instance.CurrentUserId.ToString();
            _queuePartyBtn.gameObject.SetActive(isLeader);

            foreach (var m in party.members)
            {
                string leadTag = m.isLeader ? "[LEADER] " : "";
                string readyTag = m.isReady ? "[READY]" : "[WAITING]";
                roster += $"• {leadTag}{m.displayName} (Rank {m.rank}) — {readyTag}\n";
            }
            _partyRosterText.text = roster;
        }

        private void OnFriendsToggleClicked()
        {
            _friendsPanel.SetActive(!_friendsPanel.activeSelf);
            if (_friendsPanel.activeSelf)
            {
                RefreshFriendsUI();
            }
        }

        private async void RefreshFriendsUI()
        {
            if (GameApiClient.Instance == null || !GameApiClient.Instance.IsAuthenticated)
            {
                _friendsContent.text = "Please log in to access your squad network.";
                return;
            }

            _friendsContent.text = "Loading squadmates...";
            var friends = await GameApiClient.Instance.FetchFriendsAsync();
            if (friends != null)
            {
                if (friends.Count == 0)
                {
                    _friendsContent.text = "No registered squadmates yet.\nEnter a callsign below to expand your network.";
                }
                else
                {
                    string list = "";
                    foreach (var f in friends)
                    {
                        string statusColor = f.presenceStatus == "online" ? "lime" : (f.presenceStatus == "offline" ? "grey" : "yellow");
                        list += $"• <b>{f.displayName}</b> (Rank {f.survivorRank})\n  <color={statusColor}>● {f.presenceStatus.ToUpper()}</color> // {f.xp} XP\n\n";
                    }
                    _friendsContent.text = list;
                }
            }

            var reqs = await GameApiClient.Instance.FetchFriendRequestsAsync();
            if (reqs != null && reqs.incoming != null)
            {
                _cachedRequests = reqs.incoming;
                if (reqs.incoming.Count == 0)
                {
                    _friendsRequestsContent.text = "No pending invites.";
                }
                else
                {
                    string rText = "";
                    foreach (var r in reqs.incoming)
                    {
                        rText += $"• Invite from <b>{r.requesterName}</b> (Rank {r.requesterRank})\n";
                    }
                    _friendsRequestsContent.text = rText;
                }
            }
        }

        private async void OnSendFriendRequestClicked()
        {
            if (GameApiClient.Instance == null || !GameApiClient.Instance.IsAuthenticated) return;
            string targetName = _addFriendInput.text?.Trim();
            if (string.IsNullOrEmpty(targetName)) return;

            _friendsStatusLabel.text = $"Sending invite to {targetName}...";
            var (ok, msg) = await GameApiClient.Instance.SendFriendRequestAsync(targetName);
            _friendsStatusLabel.text = ok ? "Squad invite dispatched!" : $"Invite failed: {msg}";
            if (ok)
            {
                _addFriendInput.text = "";
                RefreshFriendsUI();
            }
        }

        private void OnLeaderboardToggleClicked()
        {
            _leaderboardPanel.SetActive(!_leaderboardPanel.activeSelf);
            if (_leaderboardPanel.activeSelf)
            {
                RefreshLeaderboardUI();
            }
        }

        private async void RefreshLeaderboardUI()
        {
            if (GameApiClient.Instance == null) return;
            _leaderboardContent.text = "Retrieving world standings...";
            var leaders = await GameApiClient.Instance.FetchLeaderboardAsync();
            if (leaders != null && leaders.Count > 0)
            {
                string text = "RANK   CALLSIGN              XP      WINS\n";
                text += "----------------------------------------\n";
                int rank = 1;
                foreach (var l in leaders)
                {
                    string name = l.displayName.PadRight(18).Substring(0, 18);
                    text += $"{rank,3}.  {name} {l.xp,7}  {l.wins,4}W\n";
                    rank++;
                }
                _leaderboardContent.text = text;
            }
            else
            {
                _leaderboardContent.text = "Failed to load leaderboards.";
            }
        }

        private void RefreshAccountUI()
        {
            if (_accountLabel == null) return;

            if (GameApiClient.Instance != null && GameApiClient.Instance.IsAuthenticated)
            {
                var prof = GameApiClient.Instance.CurrentProfile;
                if (prof != null)
                {
                    _accountLabel.text = $"★ {prof.displayName.ToUpper()} [ONLINE // LVL {prof.survivorRank}]";
                }
                else
                {
                    _accountLabel.text = $"★ {GameApiClient.Instance.CurrentDisplayName.ToUpper()} [ONLINE]";
                }

                if (_authModalToggleBtnLabel != null)
                {
                    _authModalToggleBtnLabel.text = "LOGOUT";
                }
                if (_authModalToggleBtn != null)
                {
                    _authModalToggleBtn.GetComponent<Image>().color = new Color(0.48f, 0.22f, 0.22f);
                }
            }
            else
            {
                _accountLabel.text = "GUEST MODE (UNAUTHENTICATED)";
                if (_authModalToggleBtnLabel != null)
                {
                    _authModalToggleBtnLabel.text = "LOGIN / REGISTER";
                }
                if (_authModalToggleBtn != null)
                {
                    _authModalToggleBtn.GetComponent<Image>().color = new Color(0.18f, 0.48f, 0.32f);
                }
            }
        }

        private void HandleAuthChanged(bool isAuthed, string name)
        {
            RefreshAccountUI();
            RefreshPartyUI();
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

        // ==========================================
        // DIRECT CONNECT & ROOMS
        // ==========================================
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
            string chosenMap = _availableMaps[_mapIndex];
            string chosenWeather = _availableWeathers[_weatherIndex];

            if (Runtime.Environment.MapEnvironmentDirector.Instance != null)
            {
                Runtime.Environment.MapEnvironmentDirector.Instance.LoadMap(chosenMap);
            }
            if (Runtime.Environment.AtmosphereDirector.Instance != null &&
                Enum.TryParse<Runtime.Environment.WeatherCondition>(chosenWeather, out var wc))
            {
                Runtime.Environment.AtmosphereDirector.Instance.ApplyWeather(wc);
            }

            if (NetworkSessionController.Instance != null)
            {
                NetworkSessionController.Instance.StartOfflinePractice();
            }
            DismissMenu();
        }

        private async void OnQuickDevAuthClicked()
        {
            if (GameApiClient.Instance == null) return;

            _statusLabel.text = "Authenticating test account with backend...";
            string devName = "Pilot_" + UnityEngine.Random.Range(100, 999);
            var (ok, msg) = await GameApiClient.Instance.QuickDevLoginAsync(devName);
            if (ok)
            {
                _statusLabel.text = $"Authenticated as {GameApiClient.Instance.CurrentDisplayName}!";
                RefreshAccountUI();
                RefreshPartyUI();
                _authModal.SetActive(false);
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
                _authModal.SetActive(true);
                return;
            }

            _statusLabel.text = "Requesting new room code from backend API...";
            var options = new CreateRoomOptions
            {
                mode = _selectedMode,
                matchType = "StandardPursuit",
                mapId = _availableMaps[_mapIndex]
            };

            var (ok, room, err) = await GameApiClient.Instance.CreateRoomAsync(options);
            if (ok && room != null)
            {
                _roomCodeInput.text = room.code;
                _statusLabel.text = $"Room created: {room.code}. Waiting for deployment...";
            }
            else
            {
                _statusLabel.text = "Failed to create room.";
            }
        }

        private async void OnJoinRoomClicked()
        {
            if (GameApiClient.Instance == null) return;
            if (!GameApiClient.Instance.IsAuthenticated)
            {
                _statusLabel.text = "Please log in first before joining a room.";
                _authModal.SetActive(true);
                return;
            }

            string code = _roomCodeInput.text?.Trim().ToUpper();
            if (string.IsNullOrEmpty(code) || code.Length != 6)
            {
                _statusLabel.text = "Invalid room code. Must be 6 characters.";
                return;
            }

            _statusLabel.text = $"Resolving room {code} from backend...";
            var (ok, room, err) = await GameApiClient.Instance.JoinRoomAsync(code);
            if (ok && room != null)
            {
                _statusLabel.text = $"Room {code} verified! Connecting to host...";
                var (ip, port) = ReadEndpoint();
                if (NetworkSessionController.Instance != null && NetworkSessionController.Instance.StartClientSession(ip, port))
                {
                    DismissMenu();
                }
            }
            else
            {
                _statusLabel.text = $"Could not join room {code}. Room may not exist.";
            }
        }

        // ==========================================
        // UI BUILDER HELPERS
        // ==========================================
        private static GameObject CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = (color.a > 0.05f);
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
            txt.raycastTarget = false; // Never block pointer events on child text labels
            return txt;
        }

        private static InputField CreateInputField(Transform parent, string name, string defaultVal, Font font, Vector2 pos, Vector2 size, bool isPassword = false)
        {
            var go = CreatePanel(parent, name, pos, size, new Color(0.12f, 0.16f, 0.22f, 1f));
            var img = go.GetComponent<Image>();
            img.raycastTarget = true;

            var txt = CreateText(go.transform, "Text", defaultVal, font, 13, TextAnchor.MiddleLeft, new Vector2(10f, 0f), size - new Vector2(20f, 4f), Color.white);
            txt.supportRichText = false;
            txt.raycastTarget = false;

            var input = go.AddComponent<InputField>();
            input.targetGraphic = img;
            input.textComponent = txt;
            if (isPassword)
            {
                input.inputType = InputField.InputType.Password;
            }
            input.text = defaultVal;
            return input;
        }

        private Button CreateButton(Transform parent, string name, string label, Font font, Vector2 pos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreatePanel(parent, name, pos, size, bgColor);
            var btn = go.AddComponent<Button>();
            var img = go.GetComponent<Image>();
            img.raycastTarget = true;
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = new Color(Mathf.Min(bgColor.r + 0.20f, 1f), Mathf.Min(bgColor.g + 0.20f, 1f), Mathf.Min(bgColor.b + 0.20f, 1f), bgColor.a);
            colors.pressedColor = new Color(bgColor.r * 0.65f, bgColor.g * 0.65f, bgColor.b * 0.65f, bgColor.a);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            Action wrappedClick = () =>
            {
                Runtime.Audio.AudioManager.Instance?.PlayHitMarker(false, true);
                onClick?.Invoke();
            };
            btn.onClick.AddListener(() => wrappedClick());
            _registeredButtons.Add((go.GetComponent<RectTransform>(), wrappedClick, go));

            var lbl = CreateText(go.transform, "Label", label, font, 13, TextAnchor.MiddleCenter, Vector2.zero, size, Color.white);
            lbl.raycastTarget = false; // Label will NOT intercept clicks
            return btn;
        }
    }

    /// <summary>
    /// Smooth orbital camera drift for atmospheric lobby presentation.
    /// </summary>
    public sealed class LobbyCameraDrift : MonoBehaviour
    {
        private float _time;
        private Vector3 _initialPos;

        private void Awake()
        {
            _initialPos = transform.position;
        }

        private void Update()
        {
            _time += Time.deltaTime * 0.08f;
            float x = Mathf.Sin(_time) * 12f;
            float z = Mathf.Cos(_time) * 12f - 26f;
            transform.position = new Vector3(_initialPos.x + x, _initialPos.y + Mathf.Sin(_time * 0.5f) * 1.5f, z);
            transform.LookAt(Vector3.up * 2f);
        }
    }
}
