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
    /// friends network with presence, room codes, leaderboards, and direct Host/Client/Server modes.
    /// </summary>
    public sealed class LobbyConnectionUI : MonoBehaviour
    {
        private GameObject _menuRoot;
        private Camera _lobbyCamera;

        // Account & Status
        private Text _statusLabel;
        private Text _accountLabel;

        // Navigation Tabs
        private GameObject _tabMatchmaking;
        private GameObject _tabDirectConnect;
        private Button _tabMatchmakingBtn;
        private Button _tabDirectConnectBtn;

        // Matchmaking Controls
        private string _selectedMode = "GroundOperations"; // or "ConvoyOperations"
        private string _selectedRegion = "na-east";
        private readonly string[] _availableRegions = { "na-east", "eu-central", "ap-south" };
        private int _regionIndex = 0;
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

        // Drawers
        private GameObject _leaderboardPanel;
        private Text _leaderboardContent;
        private GameObject _friendsPanel;
        private Text _friendsContent;
        private Text _friendsRequestsContent;
        private InputField _addFriendInput;
        private Text _friendsStatusLabel;
        private List<FriendRequestItemDto> _cachedRequests = new List<FriendRequestItemDto>();

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
                GameApiClient.Instance.OnPartyChanged += HandlePartyChanged;
                GameApiClient.Instance.OnMatchmakingTicketUpdated += HandleTicketUpdated;
                GameApiClient.Instance.OnMatchFound += HandleMatchFound;
                RefreshAccountUI();
                RefreshPartyUI();
            }
        }

        private void Update()
        {
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
            _menuRoot = CreatePanel(canvasGo.transform, "ConnectionPanel", Vector2.zero, new Vector2(680f, 740f), new Color(0.06f, 0.08f, 0.11f, 0.96f));

            CreateText(_menuRoot.transform, "Title", "OUTRUN: AFTERMATH", font, 30, TextAnchor.MiddleCenter, new Vector2(0f, 320f), new Vector2(620f, 36f), new Color(0.96f, 0.58f, 0.16f));
            CreateText(_menuRoot.transform, "Subtitle", "GROUND & CONVOY TACTICAL OPERATIONS // MATCHMAKING ONLINE", font, 11, TextAnchor.MiddleCenter, new Vector2(0f, 292f), new Vector2(620f, 20f), new Color(0.72f, 0.86f, 0.94f));

            // Top Account & Presence Bar
            var accountBar = CreatePanel(_menuRoot.transform, "AccountBar", new Vector2(0f, 252f), new Vector2(620f, 40f), new Color(0.11f, 0.14f, 0.18f, 0.95f));
            _accountLabel = CreateText(accountBar.transform, "AccountText", "GUEST MODE (UNAUTHENTICATED)", font, 12, TextAnchor.MiddleLeft, new Vector2(-110f, 0f), new Vector2(360f, 32f), Color.white);
            CreateButton(accountBar.transform, "SettingsBtn", "SETTINGS", font, new Vector2(140f, 0f), new Vector2(95f, 30f), new Color(0.25f, 0.32f, 0.40f), () =>
            {
                if (Runtime.UI.SettingsModalUI.Instance != null)
                {
                    Runtime.UI.SettingsModalUI.Instance.ToggleOpen();
                }
            });
            CreateButton(accountBar.transform, "QuickAuthBtn", "DEV LOGIN", font, new Vector2(250f, 0f), new Vector2(100f, 30f), new Color(0.2f, 0.52f, 0.32f), OnQuickDevAuthClicked);

            // Tab Navigation Bar
            var tabNavBar = CreatePanel(_menuRoot.transform, "TabNavBar", new Vector2(0f, 205f), new Vector2(620f, 38f), new Color(0.09f, 0.11f, 0.14f, 1f));
            _tabMatchmakingBtn = CreateButton(tabNavBar.transform, "TabMmBtn", "MATCHMAKING & SQUADS", font, new Vector2(-155f, 0f), new Vector2(300f, 32f), new Color(0.85f, 0.45f, 0.12f), () => SwitchTab(0));
            _tabDirectConnectBtn = CreateButton(tabNavBar.transform, "TabDcBtn", "DIRECT CONNECT & ROOMS", font, new Vector2(155f, 0f), new Vector2(300f, 32f), new Color(0.18f, 0.22f, 0.28f), () => SwitchTab(1));

            // ==========================================
            // TAB 0: MATCHMAKING & SQUADS
            // ==========================================
            _tabMatchmaking = CreatePanel(_menuRoot.transform, "TabMatchmaking", new Vector2(0f, -10f), new Vector2(640f, 380f), Color.clear);

            // Mode & Region Selector Bar
            CreateText(_tabMatchmaking.transform, "ModeHeader", "OPERATION MODE & REGION", font, 11, TextAnchor.MiddleLeft, new Vector2(-170f, 160f), new Vector2(260f, 20f), Color.gray);
            var modeBtn = CreateButton(_tabMatchmaking.transform, "ToggleModeBtn", "MODE: GROUND OPERATIONS", font, new Vector2(-140f, 128f), new Vector2(280f, 36f), new Color(0.16f, 0.26f, 0.38f), OnToggleModeClicked);
            _modeBtnLabel = modeBtn.GetComponentInChildren<Text>();

            var regBtn = CreateButton(_tabMatchmaking.transform, "ToggleRegionBtn", "REGION: NA-EAST", font, new Vector2(160f, 128f), new Vector2(260f, 36f), new Color(0.16f, 0.26f, 0.38f), OnToggleRegionClicked);
            _regionBtnLabel = regBtn.GetComponentInChildren<Text>();

            // Queue Section (Idle vs Searching)
            _queueIdleContainer = CreatePanel(_tabMatchmaking.transform, "QueueIdle", new Vector2(0f, 68f), new Vector2(580f, 54f), Color.clear);
            CreateButton(_queueIdleContainer.transform, "FindMatchBtn", "FIND MATCH", font, Vector2.zero, new Vector2(440f, 48f), new Color(0.92f, 0.48f, 0.14f), OnFindMatchClicked);

            _queueActiveContainer = CreatePanel(_tabMatchmaking.transform, "QueueActive", new Vector2(0f, 68f), new Vector2(580f, 54f), Color.clear);
            var queueBox = CreatePanel(_queueActiveContainer.transform, "QueueBox", new Vector2(-70f, 0f), new Vector2(380f, 48f), new Color(0.12f, 0.24f, 0.36f, 0.95f));
            _queueSearchTimerText = CreateText(queueBox.transform, "QueueTimer", "SEARCHING FOR MATCH (00:00)...", font, 13, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(360f, 40f), Color.cyan);
            CreateButton(_queueActiveContainer.transform, "CancelMatchBtn", "CANCEL", font, new Vector2(170f, 0f), new Vector2(140f, 48f), new Color(0.68f, 0.2f, 0.2f), OnCancelMatchClicked);
            _queueActiveContainer.SetActive(false);

            // Squad / Party Sub-panel
            var partyBox = CreatePanel(_tabMatchmaking.transform, "PartyBox", new Vector2(0f, -70f), new Vector2(580f, 180f), new Color(0.09f, 0.12f, 0.16f, 0.95f));
            _partyHeaderLabel = CreateText(partyBox.transform, "PartyHeader", "SURVIVOR SQUAD (NOT IN SQUAD)", font, 12, TextAnchor.MiddleLeft, new Vector2(-120f, 70f), new Vector2(300f, 22f), new Color(0.95f, 0.65f, 0.2f));

            // Squad Roster Display
            _partyRosterText = CreateText(partyBox.transform, "PartyRoster", "Create a squad or enter a 6-character squad code to deploy together.", font, 11, TextAnchor.UpperLeft, new Vector2(-50f, 0f), new Vector2(420f, 90f), Color.white);

            // Party Create/Join Container (shown when NOT in party)
            _partyCreateContainer = CreatePanel(partyBox.transform, "PartyCreateContainer", new Vector2(0f, -48f), new Vector2(560f, 42f), Color.clear);
            CreateButton(_partyCreateContainer.transform, "CreatePartyBtn", "CREATE SQUAD", font, new Vector2(-160f, 0f), new Vector2(180f, 34f), new Color(0.25f, 0.45f, 0.25f), OnCreatePartyClicked);
            _partyCodeInput = CreateInputField(_partyCreateContainer.transform, "PartyCodeInput", "PTY...", font, new Vector2(40f, 0f), new Vector2(140f, 34f));
            CreateButton(_partyCreateContainer.transform, "JoinPartyBtn", "JOIN CODE", font, new Vector2(180f, 0f), new Vector2(120f, 34f), new Color(0.18f, 0.4f, 0.65f), OnJoinPartyClicked);

            // Party In-Party Actions (shown when IN party)
            _partyActionsContainer = CreatePanel(partyBox.transform, "PartyActionsContainer", new Vector2(0f, -52f), new Vector2(560f, 38f), Color.clear);
            var readyBtn = CreateButton(_partyActionsContainer.transform, "ToggleReadyBtn", "READY", font, new Vector2(-180f, 0f), new Vector2(130f, 34f), new Color(0.2f, 0.55f, 0.25f), OnToggleReadyClicked);
            _readyBtnLabel = readyBtn.GetComponentInChildren<Text>();
            _queuePartyBtn = CreateButton(_partyActionsContainer.transform, "QueuePartyBtn", "QUEUE SQUAD", font, new Vector2(-30f, 0f), new Vector2(150f, 34f), new Color(0.85f, 0.45f, 0.12f), OnFindMatchClicked);
            CreateButton(_partyActionsContainer.transform, "LeavePartyBtn", "LEAVE SQUAD", font, new Vector2(160f, 0f), new Vector2(150f, 34f), new Color(0.55f, 0.2f, 0.2f), OnLeavePartyClicked);
            _partyActionsContainer.SetActive(false);

            // ==========================================
            // TAB 1: DIRECT CONNECT & CUSTOM ROOMS
            // ==========================================
            _tabDirectConnect = CreatePanel(_menuRoot.transform, "TabDirectConnect", new Vector2(0f, -10f), new Vector2(640f, 380f), Color.clear);

            // Room Code Section
            CreateText(_tabDirectConnect.transform, "RoomLabel", "ONLINE ROOM CODE", font, 11, TextAnchor.MiddleLeft, new Vector2(-150f, 150f), new Vector2(180f, 20f), Color.gray);
            _roomCodeInput = CreateInputField(_tabDirectConnect.transform, "RoomCodeInput", "DEAD66", font, new Vector2(-110f, 122f), new Vector2(240f, 34f));
            CreateButton(_tabDirectConnect.transform, "CreateRoomBtn", "CREATE ROOM", font, new Vector2(65f, 122f), new Vector2(130f, 34f), new Color(0.75f, 0.45f, 0.1f), OnCreateRoomClicked);
            CreateButton(_tabDirectConnect.transform, "JoinRoomBtn", "JOIN CODE", font, new Vector2(210f, 122f), new Vector2(120f, 34f), new Color(0.15f, 0.45f, 0.75f), OnJoinRoomClicked);

            // Direct IP & Port Field
            CreateText(_tabDirectConnect.transform, "IpLabel", "SERVER IP", font, 11, TextAnchor.MiddleLeft, new Vector2(-150f, 72f), new Vector2(180f, 20f), Color.gray);
            _ipInput = CreateInputField(_tabDirectConnect.transform, "IpInput", "127.0.0.1", font, new Vector2(-110f, 44f), new Vector2(240f, 34f));

            CreateText(_tabDirectConnect.transform, "PortLabel", "UDP PORT", font, 11, TextAnchor.MiddleLeft, new Vector2(120f, 72f), new Vector2(100f, 20f), Color.gray);
            _portInput = CreateInputField(_tabDirectConnect.transform, "PortInput", "7777", font, new Vector2(140f, 44f), new Vector2(140f, 34f));

            // Core Action Buttons
            CreateButton(_tabDirectConnect.transform, "HostBtn", "HOST MATCH (SERVER + PLAYER)", font, new Vector2(0f, -16f), new Vector2(460f, 44f), new Color(0.82f, 0.4f, 0.14f), OnHostClicked);
            CreateButton(_tabDirectConnect.transform, "JoinBtn", "DIRECT JOIN (CLIENT)", font, new Vector2(0f, -68f), new Vector2(460f, 44f), new Color(0.18f, 0.45f, 0.78f), OnJoinClicked);
            CreateButton(_tabDirectConnect.transform, "ServerBtn", "DEDICATED SERVER MODE", font, new Vector2(0f, -120f), new Vector2(460f, 40f), new Color(0.24f, 0.28f, 0.32f), OnServerClicked);

            _tabDirectConnect.SetActive(false);

            // ==========================================
            // BOTTOM UTILITY & SOCIAL BUTTONS
            // ==========================================
            var bottomBar = CreatePanel(_menuRoot.transform, "BottomBar", new Vector2(0f, -245f), new Vector2(620f, 40f), Color.clear);
            CreateButton(bottomBar.transform, "FriendsBtn", "SQUAD NETWORK (FRIENDS)", font, new Vector2(-190f, 0f), new Vector2(200f, 36f), new Color(0.22f, 0.42f, 0.62f), OnFriendsToggleClicked);
            CreateButton(bottomBar.transform, "LeaderboardBtn", "LEADERBOARDS", font, new Vector2(10f, 0f), new Vector2(160f, 36f), new Color(0.35f, 0.3f, 0.45f), OnLeaderboardToggleClicked);
            CreateButton(bottomBar.transform, "OfflineBtn", "OFFLINE PRACTICE", font, new Vector2(195f, 0f), new Vector2(180f, 36f), new Color(0.18f, 0.38f, 0.28f), OnOfflineClicked);

            _statusLabel = CreateText(_menuRoot.transform, "Status", "READY — SELECT DEPLOYMENT OR FIND MATCH", font, 12, TextAnchor.MiddleCenter, new Vector2(0f, -315f), new Vector2(620f, 32f), Color.yellow);

            // ==========================================
            // SIDE DRAWER: FRIENDS & SOCIAL PANEL
            // ==========================================
            _friendsPanel = CreatePanel(canvasGo.transform, "FriendsPanel", new Vector2(540f, 0f), new Vector2(360f, 740f), new Color(0.08f, 0.1f, 0.14f, 0.98f));
            CreateText(_friendsPanel.transform, "FrTitle", "SQUAD NETWORK", font, 20, TextAnchor.MiddleCenter, new Vector2(0f, 320f), new Vector2(320f, 30f), new Color(0.3f, 0.7f, 1f));
            CreateText(_friendsPanel.transform, "FrSub", "ONLINE PRESENCE & SQUAD INVITES", font, 10, TextAnchor.MiddleCenter, new Vector2(0f, 296f), new Vector2(320f, 18f), Color.gray);

            _friendsContent = CreateText(_friendsPanel.transform, "FrList", "Sign in to view online squadmates...", font, 11, TextAnchor.UpperLeft, new Vector2(0f, 140f), new Vector2(320f, 270f), Color.white);

            // Pending Friend Requests
            CreateText(_friendsPanel.transform, "ReqTitle", "INCOMING INVITES", font, 12, TextAnchor.MiddleLeft, new Vector2(-30f, -20f), new Vector2(260f, 20f), Color.yellow);
            _friendsRequestsContent = CreateText(_friendsPanel.transform, "ReqList", "No pending requests.", font, 11, TextAnchor.UpperLeft, new Vector2(0f, -65f), new Vector2(320f, 60f), Color.white);
            CreateButton(_friendsPanel.transform, "AcceptFirstReqBtn", "ACCEPT ALL", font, new Vector2(100f, -20f), new Vector2(110f, 24f), new Color(0.2f, 0.5f, 0.25f), OnAcceptAllRequestsClicked);

            // Add Friend Input
            CreateText(_friendsPanel.transform, "AddFrLabel", "ADD SQUADMATE", font, 11, TextAnchor.MiddleLeft, new Vector2(-50f, -125f), new Vector2(220f, 20f), Color.gray);
            _addFriendInput = CreateInputField(_friendsPanel.transform, "AddFriendInput", "Username or ID", font, new Vector2(-40f, -155f), new Vector2(220f, 32f));
            CreateButton(_friendsPanel.transform, "SendReqBtn", "SEND", font, new Vector2(110f, -155f), new Vector2(70f, 32f), new Color(0.2f, 0.52f, 0.32f), OnSendFriendRequestClicked);

            _friendsStatusLabel = CreateText(_friendsPanel.transform, "FrStatus", "", font, 10, TextAnchor.MiddleCenter, new Vector2(0f, -210f), new Vector2(320f, 40f), Color.yellow);

            CreateButton(_friendsPanel.transform, "RefreshFrBtn", "REFRESH", font, new Vector2(-75f, -290f), new Vector2(130f, 32f), new Color(0.2f, 0.3f, 0.42f), OnRefreshFriendsClicked);
            CreateButton(_friendsPanel.transform, "CloseFrBtn", "CLOSE", font, new Vector2(75f, -290f), new Vector2(130f, 32f), new Color(0.45f, 0.2f, 0.2f), () => _friendsPanel.SetActive(false));
            _friendsPanel.SetActive(false);

            // ==========================================
            // SIDE DRAWER: LEADERBOARDS PANEL
            // ==========================================
            _leaderboardPanel = CreatePanel(canvasGo.transform, "LeaderboardPanel", new Vector2(-540f, 0f), new Vector2(360f, 740f), new Color(0.08f, 0.09f, 0.12f, 0.98f));
            CreateText(_leaderboardPanel.transform, "LbTitle", "TOP SURVIVORS", font, 20, TextAnchor.MiddleCenter, new Vector2(0f, 320f), new Vector2(320f, 30f), new Color(0.95f, 0.65f, 0.2f));
            _leaderboardContent = CreateText(_leaderboardPanel.transform, "LbContent", "Loading records from backend...", font, 11, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(320f, 540f), Color.white);
            CreateButton(_leaderboardPanel.transform, "CloseLbBtn", "CLOSE", font, new Vector2(0f, -310f), new Vector2(140f, 32f), new Color(0.4f, 0.2f, 0.2f), () => _leaderboardPanel.SetActive(false));
            _leaderboardPanel.SetActive(false);
        }

        private void SwitchTab(int tabIndex)
        {
            bool isMm = tabIndex == 0;
            _tabMatchmaking.SetActive(isMm);
            _tabDirectConnect.SetActive(!isMm);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            _tabMatchmakingBtn.GetComponent<Image>().color = isMm ? new Color(0.85f, 0.45f, 0.12f) : new Color(0.18f, 0.22f, 0.28f);
            _tabDirectConnectBtn.GetComponent<Image>().color = !isMm ? new Color(0.85f, 0.45f, 0.12f) : new Color(0.18f, 0.22f, 0.28f);
        }

        private void OnToggleModeClicked()
        {
            _selectedMode = (_selectedMode == "GroundOperations") ? "ConvoyOperations" : "GroundOperations";
            if (_modeBtnLabel != null)
            {
                _modeBtnLabel.text = (_selectedMode == "GroundOperations") ? "MODE: GROUND OPERATIONS" : "MODE: CONVOY OPERATIONS";
            }
        }

        private void OnToggleRegionClicked()
        {
            _regionIndex = (_regionIndex + 1) % _availableRegions.Length;
            _selectedRegion = _availableRegions[_regionIndex];
            if (_regionBtnLabel != null)
            {
                _regionBtnLabel.text = $"REGION: {_selectedRegion.ToUpper()}";
            }
        }

        private async void OnFindMatchClicked()
        {
            if (GameApiClient.Instance == null) return;
            if (!GameApiClient.Instance.IsAuthenticated)
            {
                _statusLabel.text = "Please log in first before queueing for matchmaking.";
                return;
            }

            bool isParty = GameApiClient.Instance.CurrentParty != null && GameApiClient.Instance.CurrentParty.inParty;
            _statusLabel.text = isParty ? "Queueing squad for matchmaking..." : "Queueing solo for matchmaking...";

            var (ok, ticket, err) = await GameApiClient.Instance.EnqueueMatchmakingAsync(_selectedMode, _selectedRegion, isParty);
            if (ok && ticket != null)
            {
                _isSearchingMatch = true;
                _searchTimer = 0f;
                _queueIdleContainer.SetActive(false);
                _queueActiveContainer.SetActive(true);
                _statusLabel.text = $"ENQUEUED TICKET [{ticket.ticketId.Substring(0, 8)}...] - SEARCHING...";
            }
            else
            {
                _statusLabel.text = $"Matchmaking error: {err}";
            }
        }

        private async void OnCancelMatchClicked()
        {
            if (GameApiClient.Instance == null) return;
            _statusLabel.text = "Cancelling matchmaking search...";
            bool ok = await GameApiClient.Instance.CancelMatchmakingAsync();
            _isSearchingMatch = false;
            _queueIdleContainer.SetActive(true);
            _queueActiveContainer.SetActive(false);
            _statusLabel.text = ok ? "Matchmaking search cancelled." : "Failed to cancel search.";
        }

        private void HandleTicketUpdated(MatchmakingTicketDto ticket)
        {
            if (ticket == null || ticket.status != "queued")
            {
                _isSearchingMatch = false;
                _queueIdleContainer.SetActive(true);
                _queueActiveContainer.SetActive(false);
            }
        }

        private void HandleMatchFound(MatchedAssignmentDto assignment)
        {
            if (assignment == null) return;
            _isSearchingMatch = false;
            _queueIdleContainer.SetActive(true);
            _queueActiveContainer.SetActive(false);

            _statusLabel.text = $"MATCH FOUND! SERVER: {assignment.serverIp}:{assignment.serverPort} | TEAM: {assignment.team.ToUpper()}";

            if (NetworkSessionController.Instance != null)
            {
                Debug.Log($"[LobbyConnectionUI] Auto-dispatching to dedicated match server: {assignment.serverIp}:{assignment.serverPort}");
                NetworkSessionController.Instance.StartClientSession(assignment.serverIp, (ushort)assignment.serverPort);
                DismissMenu();
            }
        }

        // ==========================================
        // SQUAD / PARTY MANAGEMENT
        // ==========================================
        private async void OnCreatePartyClicked()
        {
            if (GameApiClient.Instance == null) return;
            if (!GameApiClient.Instance.IsAuthenticated)
            {
                _statusLabel.text = "Log in first to create a squad.";
                return;
            }

            _statusLabel.text = "Creating squad...";
            var (ok, party, err) = await GameApiClient.Instance.CreatePartyAsync();
            if (ok && party != null)
            {
                _statusLabel.text = $"SQUAD CREATED! CODE: [{party.code}]";
                RefreshPartyUI();
            }
            else
            {
                _statusLabel.text = $"Failed to create squad: {err}";
            }
        }

        private async void OnJoinPartyClicked()
        {
            if (GameApiClient.Instance == null) return;
            if (!GameApiClient.Instance.IsAuthenticated)
            {
                _statusLabel.text = "Log in first to join a squad.";
                return;
            }

            string code = _partyCodeInput != null ? _partyCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                _statusLabel.text = "Enter squad code to join.";
                return;
            }

            _statusLabel.text = $"Joining squad [{code}]...";
            var (ok, party, err) = await GameApiClient.Instance.JoinPartyByCodeAsync(code);
            if (ok && party != null)
            {
                _statusLabel.text = $"JOINED SQUAD [{party.code}]!";
                RefreshPartyUI();
            }
            else
            {
                _statusLabel.text = $"Join squad failed: {err}";
            }
        }

        private async void OnLeavePartyClicked()
        {
            if (GameApiClient.Instance == null) return;
            _statusLabel.text = "Leaving squad...";
            var (ok, msg) = await GameApiClient.Instance.LeavePartyAsync();
            _statusLabel.text = ok ? "Left squad." : $"Leave failed: {msg}";
            RefreshPartyUI();
        }

        private async void OnToggleReadyClicked()
        {
            if (GameApiClient.Instance == null) return;
            var party = GameApiClient.Instance.CurrentParty;
            if (party == null || !party.inParty) return;

            var me = party.members.Find(m => m.userId == GameApiClient.Instance.CurrentUserId);
            bool newReady = me == null || !me.isReady;
            _statusLabel.text = newReady ? "Marking READY..." : "Marking UNREADY...";
            var (ok, err) = await GameApiClient.Instance.SetPartyReadyAsync(newReady);
            if (ok)
            {
                RefreshPartyUI();
                _statusLabel.text = newReady ? "SQUAD STATUS: READY" : "SQUAD STATUS: NOT READY";
            }
            else
            {
                _statusLabel.text = $"Ready update failed: {err}";
            }
        }

        private void RefreshPartyUI()
        {
            if (_partyHeaderLabel == null) return;

            var party = GameApiClient.Instance != null ? GameApiClient.Instance.CurrentParty : null;
            bool inParty = party != null && party.inParty;

            _partyCreateContainer.SetActive(!inParty);
            _partyActionsContainer.SetActive(inParty);

            if (!inParty)
            {
                _partyHeaderLabel.text = "SURVIVOR SQUAD (NOT IN SQUAD)";
                _partyRosterText.text = "Create a squad or enter a 6-character squad code to deploy together.\nSquadmates are guaranteed to stay on the same team.";
                return;
            }

            _partyHeaderLabel.text = $"SURVIVOR SQUAD: [{party.code}] (MEMBERS: {party.members.Count}/4)";

            var sb = new System.Text.StringBuilder();
            bool allReady = true;
            bool isMeLeader = party.leaderUserId == GameApiClient.Instance.CurrentUserId;
            bool amIReady = false;

            for (int i = 0; i < party.members.Count; i++)
            {
                var m = party.members[i];
                string crown = m.isLeader ? "[LEADER] " : "         ";
                string readyTag = m.isReady ? "[READY]" : "[NOT READY]";
                sb.AppendLine($"{crown}{m.displayName,-14} (Rank {m.rank})  {readyTag}");

                if (!m.isReady) allReady = false;
                if (m.userId == GameApiClient.Instance.CurrentUserId && m.isReady) amIReady = true;
            }

            _partyRosterText.text = sb.ToString();

            if (_readyBtnLabel != null)
            {
                _readyBtnLabel.text = amIReady ? "UNREADY" : "READY";
            }

            if (_queuePartyBtn != null)
            {
                _queuePartyBtn.gameObject.SetActive(isMeLeader);
                _queuePartyBtn.interactable = allReady;
            }
        }

        private void HandlePartyChanged(PartyDetailsDto party)
        {
            RefreshPartyUI();
        }

        // ==========================================
        // SQUAD NETWORK (FRIENDS & SOCIAL)
        // ==========================================
        private void OnFriendsToggleClicked()
        {
            if (_friendsPanel == null) return;
            bool willShow = !_friendsPanel.activeSelf;
            _friendsPanel.SetActive(willShow);

            if (willShow)
            {
                OnRefreshFriendsClicked();
            }
        }

        private async void OnRefreshFriendsClicked()
        {
            if (GameApiClient.Instance == null || !GameApiClient.Instance.IsAuthenticated)
            {
                if (_friendsContent != null) _friendsContent.text = "Sign in to view online squadmates.";
                return;
            }

            if (_friendsStatusLabel != null) _friendsStatusLabel.text = "Refreshing squad network...";

            var friends = await GameApiClient.Instance.FetchFriendsAsync();
            var reqContainer = await GameApiClient.Instance.FetchFriendRequestsAsync();
            _cachedRequests = (reqContainer != null && reqContainer.incoming != null) ? reqContainer.incoming : new List<FriendRequestItemDto>();

            // Render Friends List
            if (friends != null && friends.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < friends.Count; i++)
                {
                    var f = friends[i];
                    string statusColor = f.presenceStatus == "online" || f.presenceStatus == "in-lobby" ? "[ONLINE]" :
                                         f.presenceStatus == "in-match" ? "[IN-MATCH]" : "[OFFLINE]";
                    sb.AppendLine($"{statusColor} {f.displayName,-14} (R{f.survivorRank})");
                }
                _friendsContent.text = sb.ToString();
            }
            else
            {
                _friendsContent.text = "No squadmates found.\nEnter a username below to add allies!";
            }

            // Render Pending Requests
            if (_cachedRequests.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _cachedRequests.Count; i++)
                {
                    var r = _cachedRequests[i];
                    sb.AppendLine($"From {r.requesterName} (Rank {r.requesterRank})");
                }
                _friendsRequestsContent.text = sb.ToString();
            }
            else
            {
                _friendsRequestsContent.text = "No pending invites.";
            }

            if (_friendsStatusLabel != null) _friendsStatusLabel.text = "";
        }

        private async void OnSendFriendRequestClicked()
        {
            if (GameApiClient.Instance == null || !GameApiClient.Instance.IsAuthenticated) return;

            string target = _addFriendInput != null ? _addFriendInput.text.Trim() : "";
            if (string.IsNullOrEmpty(target))
            {
                if (_friendsStatusLabel != null) _friendsStatusLabel.text = "Enter a username or ID.";
                return;
            }

            if (_friendsStatusLabel != null) _friendsStatusLabel.text = $"Sending invite to {target}...";
            var (ok, msg) = await GameApiClient.Instance.SendFriendRequestAsync(target);
            if (ok)
            {
                if (_friendsStatusLabel != null) _friendsStatusLabel.text = "Squad invite sent!";
                if (_addFriendInput != null) _addFriendInput.text = "";
                OnRefreshFriendsClicked();
            }
            else
            {
                if (_friendsStatusLabel != null) _friendsStatusLabel.text = $"Invite failed: {msg}";
            }
        }

        private async void OnAcceptAllRequestsClicked()
        {
            if (GameApiClient.Instance == null || _cachedRequests.Count == 0) return;

            if (_friendsStatusLabel != null) _friendsStatusLabel.text = "Accepting invites...";
            for (int i = 0; i < _cachedRequests.Count; i++)
            {
                await GameApiClient.Instance.RespondFriendRequestAsync(_cachedRequests[i].id, true);
            }
            OnRefreshFriendsClicked();
        }

        // ==========================================
        // LEADERBOARDS
        // ==========================================
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
                    for (int i = 0; i < list.Count && i < 14; i++)
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
                RefreshPartyUI();
                return;
            }

            _statusLabel.text = "Authenticating test account with backend...";
            string devName = "Pilot_" + UnityEngine.Random.Range(100, 999);
            var (ok, msg) = await GameApiClient.Instance.QuickDevLoginAsync(devName);
            if (ok)
            {
                _statusLabel.text = $"Authenticated as {GameApiClient.Instance.CurrentDisplayName}!";
                RefreshAccountUI();
                RefreshPartyUI();
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
                mode = _selectedMode,
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

        private void RefreshAccountUI()
        {
            if (_accountLabel == null) return;

            if (GameApiClient.Instance != null && GameApiClient.Instance.IsAuthenticated)
            {
                var p = GameApiClient.Instance.CurrentProfile;
                if (p != null)
                {
                    _accountLabel.text = $"★ {p.displayName} | RANK {p.survivorRank} | {p.xp} XP | {p.wins}W - {p.losses}L [ONLINE]";
                }
                else
                {
                    _accountLabel.text = $"★ {GameApiClient.Instance.CurrentDisplayName} [ONLINE]";
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
        // UI BUILDER HELPERS
        // ==========================================
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
            var go = CreatePanel(parent, name, pos, size, new Color(0.14f, 0.16f, 0.20f, 1f));
            var txt = CreateText(go.transform, "Text", defaultVal, font, 13, TextAnchor.MiddleLeft, new Vector2(10f, 0f), size - new Vector2(20f, 4f), Color.white);
            txt.supportRichText = false;
            var input = go.AddComponent<InputField>();
            input.textComponent = txt;
            input.text = defaultVal;
            return input;
        }

        private static Button CreateButton(Transform parent, string name, string label, Font font, Vector2 pos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreatePanel(parent, name, pos, size, bgColor);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(onClick);
            CreateText(go.transform, "Label", label, font, 13, TextAnchor.MiddleCenter, Vector2.zero, size, Color.white);
            return btn;
        }
    }
}
