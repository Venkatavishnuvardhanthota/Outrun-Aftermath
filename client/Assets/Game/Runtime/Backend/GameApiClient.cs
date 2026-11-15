using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace OutrunAftermath.Client.Runtime.Backend
{
    [Serializable]
    public sealed class AuthResponseDto
    {
        public string accessToken;
        public string refreshToken;
        public string userId;
        public string displayName;
    }

    [Serializable]
    public sealed class PlayerProfileDto
    {
        public string userId;
        public string displayName;
        public int survivorRank;
        public long xp;
        public string selectedOperatorId;
        public string platform;
        public int matchesPlayed;
        public int wins;
        public int losses;
    }

    [Serializable]
    public sealed class CreateRoomOptions
    {
        public string mode = "GroundOperations";
        public string matchType = "StandardPursuit";
        public string mapId = "deadcity";
        public string password = "";
        public bool randomMap = false;
        public int rogueSlots = 4;
        public int sentinelSlots = 4;
        public int matchMinutes = 10;
        public bool friendlyFire = false;
        public int difficulty = 2;
    }

    [Serializable]
    public sealed class RoomResponseDto
    {
        public string id;
        public string code;
        public string mode;
        public string matchType;
        public string mapId;
        public string summary;
    }

    [Serializable]
    public sealed class JoinRoomResponseDto
    {
        public string id;
        public string code;
        public string team;
        public bool already;
    }

    [Serializable]
    public sealed class RoomMemberDto
    {
        public string id;
        public string roomId;
        public string userId;
        public string team;
        public bool ready;
        public string operatorId;
    }

    [Serializable]
    public sealed class RoomDetailsDto
    {
        public string id;
        public string code;
        public string hostUserId;
        public string mode;
        public string matchType;
        public string mapId;
        public string settingsJson;
        public string status;
        public List<RoomMemberDto> members = new List<RoomMemberDto>();
    }

    [Serializable]
    public sealed class LeaderboardEntryDto
    {
        public string userId;
        public string displayName;
        public int survivorRank;
        public long xp;
        public int matchesPlayed;
        public int wins;
        public int losses;
    }

    [Serializable]
    public sealed class MatchHistoryItemDto
    {
        public string id;
        public string mode;
        public string matchType;
        public string mapId;
        public string startedAtUtc;
        public string endedAtUtc;
        public string winnerFaction;
        public string faction;
        public string operatorId;
        public int xpAwarded;
        public string result;
    }

    [Serializable]
    public sealed class CompleteMatchPlayerDto
    {
        public string userId;
        public string faction;
        public string operatorId;
        public int kills;
        public int revives;
        public int objectiveProgress;
        public bool sideComplete;
    }

    [Serializable]
    public sealed class CompleteMatchRequestDto
    {
        public string matchId;
        public string mode;
        public string matchType;
        public string mapId;
        public string startedAtUtc;
        public string winnerFaction;
        public int seed;
        public List<CompleteMatchPlayerDto> players = new List<CompleteMatchPlayerDto>();
    }

    [Serializable]
    public sealed class FriendDto
    {
        public string userId;
        public string displayName;
        public int survivorRank;
        public long xp;
        public string presenceStatus;
        public string lastSeenUtc;
        public int wins;
        public int losses;
    }

    [Serializable]
    public sealed class FriendRequestItemDto
    {
        public string id;
        public string requesterId;
        public string requesterName;
        public int requesterRank;
        public string createdAtUtc;
    }

    [Serializable]
    public sealed class FriendRequestsContainerDto
    {
        public List<FriendRequestItemDto> incoming = new List<FriendRequestItemDto>();
    }

    [Serializable]
    public sealed class PartyMemberItemDto
    {
        public string userId;
        public string displayName;
        public int rank;
        public bool isLeader;
        public bool isReady;
        public string joinedAtUtc;
    }

    [Serializable]
    public sealed class PartyDetailsDto
    {
        public bool inParty;
        public string partyId;
        public string code;
        public string leaderUserId;
        public List<PartyMemberItemDto> members = new List<PartyMemberItemDto>();
    }

    [Serializable]
    public sealed class MatchedAssignmentDto
    {
        public string matchId;
        public string serverIp;
        public int serverPort;
        public string mode;
        public string mapId;
        public string team;
    }

    [Serializable]
    public sealed class MatchmakingTicketDto
    {
        public string ticketId;
        public string status; // "queued", "matched", "cancelled"
        public string enqueuedUtc;
        public MatchedAssignmentDto assignment;
    }

    /// <summary>
    /// Master client runtime bridge to the ASP.NET Core backend API.
    /// Handles JWT authentication, token caching, player profile, rooms, and match reporting.
    /// </summary>
    public sealed class GameApiClient : MonoBehaviour
    {
        private const string PrefKeyAccessToken = "outrun_jwt_access";
        private const string PrefKeyRefreshToken = "outrun_jwt_refresh";
        private const string PrefKeyUserId = "outrun_user_id";
        private const string PrefKeyDisplayName = "outrun_display_name";

        public static GameApiClient Instance { get; private set; }

        [Header("Backend Configuration")]
        [SerializeField] private string defaultApiUrl = "http://127.0.0.1:5000";
        public string BaseUrl { get; set; } = "http://127.0.0.1:5000";

        public string AccessToken { get; private set; } = "";
        public string RefreshToken { get; private set; } = "";
        public string CurrentUserId { get; private set; } = "";
        public string CurrentDisplayName { get; private set; } = "";
        public PlayerProfileDto CurrentProfile { get; private set; }
        public PartyDetailsDto CurrentParty { get; private set; }
        public MatchmakingTicketDto CurrentTicket { get; private set; }
        public List<FriendDto> CachedFriends { get; private set; } = new List<FriendDto>();
        public bool IsSearchingMatch => CurrentTicket != null && CurrentTicket.status == "queued";

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(CurrentUserId);

        public event Action<bool, string> OnAuthStatusChanged;
        public event Action<PlayerProfileDto> OnProfileUpdated;
        public event Action<PartyDetailsDto> OnPartyChanged;
        public event Action<MatchmakingTicketDto> OnMatchmakingTicketUpdated;
        public event Action<MatchedAssignmentDto> OnMatchFound;
        public event Action<List<FriendDto>> OnFriendsListUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BaseUrl = defaultApiUrl;
            LoadCachedCredentials();
            ParseCommandLineArgs();
        }

        private void Start()
        {
            if (IsAuthenticated)
            {
                _ = RefreshProfileAsync();
            }
        }

        private void ParseCommandLineArgs()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            bool hasDevGuest = false;
            string guestName = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("-api-url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    BaseUrl = args[++i];
                }
                else if (arg.Equals("-auth-name", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    guestName = args[++i];
                }
                else if (arg.Equals("-dev-guest", StringComparison.OrdinalIgnoreCase))
                {
                    hasDevGuest = true;
                }
            }

            if (hasDevGuest || !string.IsNullOrEmpty(guestName))
            {
                // In automated testing or explicit dev guest mode, reset shared PlayerPrefs cache
                Logout();
                string targetName = !string.IsNullOrEmpty(guestName) ? guestName : ("Pilot_" + UnityEngine.Random.Range(100, 999));
                _ = QuickDevLoginAsync(targetName);
            }
        }

        private void LoadCachedCredentials()
        {
            AccessToken = PlayerPrefs.GetString(PrefKeyAccessToken, "");
            RefreshToken = PlayerPrefs.GetString(PrefKeyRefreshToken, "");
            CurrentUserId = PlayerPrefs.GetString(PrefKeyUserId, "");
            CurrentDisplayName = PlayerPrefs.GetString(PrefKeyDisplayName, "");

            if (IsAuthenticated)
            {
                Debug.Log($"[ApiClient] Loaded cached session for {CurrentDisplayName} ({CurrentUserId})");
            }
        }

        private void SaveCredentials(AuthResponseDto auth)
        {
            AccessToken = auth.accessToken ?? "";
            RefreshToken = auth.refreshToken ?? "";
            CurrentUserId = auth.userId ?? "";
            CurrentDisplayName = auth.displayName ?? "";

            PlayerPrefs.SetString(PrefKeyAccessToken, AccessToken);
            PlayerPrefs.SetString(PrefKeyRefreshToken, RefreshToken);
            PlayerPrefs.SetString(PrefKeyUserId, CurrentUserId);
            PlayerPrefs.SetString(PrefKeyDisplayName, CurrentDisplayName);
            PlayerPrefs.Save();

            OnAuthStatusChanged?.Invoke(true, CurrentDisplayName);
            Debug.Log($"[ApiClient] Authenticated as {CurrentDisplayName} (ID: {CurrentUserId})");
        }

        public void Logout()
        {
            AccessToken = "";
            RefreshToken = "";
            CurrentUserId = "";
            CurrentDisplayName = "";
            CurrentProfile = null;

            PlayerPrefs.DeleteKey(PrefKeyAccessToken);
            PlayerPrefs.DeleteKey(PrefKeyRefreshToken);
            PlayerPrefs.DeleteKey(PrefKeyUserId);
            PlayerPrefs.DeleteKey(PrefKeyDisplayName);
            PlayerPrefs.Save();

            OnAuthStatusChanged?.Invoke(false, "Logged out");
        }

        #region Authentication Operations

        public async Task<(bool ok, string message)> RegisterAsync(string email, string password, string displayName)
        {
            string url = $"{BaseUrl}/api/auth/register";
            string payload = JsonUtility.ToJson(new RegisterPayload { email = email, password = password, displayName = displayName });

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: false);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var auth = JsonUtility.FromJson<AuthResponseDto>(req.downloadHandler.text);
                SaveCredentials(auth);
                await RefreshProfileAsync();
                return (true, "Registration successful");
            }

            string err = req.downloadHandler != null ? req.downloadHandler.text : req.error;
            Debug.LogWarning($"[ApiClient] Registration failed: {err}");
            return (false, err);
        }

        public async Task<(bool ok, string message)> LoginAsync(string email, string password)
        {
            string url = $"{BaseUrl}/api/auth/login";
            string payload = JsonUtility.ToJson(new LoginPayload { email = email, password = password });

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: false);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var auth = JsonUtility.FromJson<AuthResponseDto>(req.downloadHandler.text);
                SaveCredentials(auth);
                await RefreshProfileAsync();
                return (true, "Login successful");
            }

            string err = req.downloadHandler != null ? req.downloadHandler.text : req.error;
            Debug.LogWarning($"[ApiClient] Login failed: {err}");
            return (false, err);
        }

        public async Task<(bool ok, string message)> QuickDevLoginAsync(string preferredName = "DevPilot")
        {
            string cleanName = string.IsNullOrWhiteSpace(preferredName) ? "Pilot" : preferredName.Trim();
            string email = $"{cleanName.ToLowerInvariant().Replace(" ", "_")}@outrun.local";
            string password = "Password123!";

            // Attempt login first
            var (loginOk, _) = await LoginAsync(email, password);
            if (loginOk) return (true, $"Logged in as {CurrentDisplayName}");

            // If account doesn't exist, register
            var (regOk, regErr) = await RegisterAsync(email, password, cleanName);
            if (regOk) return (true, $"Registered & logged in as {CurrentDisplayName}");

            // Fallback unique email if taken
            string uniqueEmail = $"{cleanName.ToLowerInvariant()}_{UnityEngine.Random.Range(1000, 9999)}@outrun.local";
            string uniqueName = $"{cleanName}_{UnityEngine.Random.Range(100, 999)}";
            return await RegisterAsync(uniqueEmail, password, uniqueName);
        }

        #endregion

        #region Profile & Social Operations

        public async Task<PlayerProfileDto> RefreshProfileAsync()
        {
            if (!IsAuthenticated) return null;

            string url = $"{BaseUrl}/api/me";
            using var req = CreateJsonRequest(url, "GET", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var profile = JsonUtility.FromJson<PlayerProfileDto>(req.downloadHandler.text);
                CurrentProfile = profile;
                OnProfileUpdated?.Invoke(profile);
                return profile;
            }

            return null;
        }

        public async Task<List<MatchHistoryItemDto>> FetchRecentMatchesAsync()
        {
            if (!IsAuthenticated) return new List<MatchHistoryItemDto>();

            string url = $"{BaseUrl}/api/me/matches";
            using var req = CreateJsonRequest(url, "GET", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                return JsonHelper.FromJsonList<MatchHistoryItemDto>(json);
            }

            return new List<MatchHistoryItemDto>();
        }

        public async Task<List<LeaderboardEntryDto>> FetchLeaderboardAsync()
        {
            string url = $"{BaseUrl}/api/leaderboards";
            using var req = CreateJsonRequest(url, "GET", null, authRequired: false);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                return JsonHelper.FromJsonList<LeaderboardEntryDto>(json);
            }

            return new List<LeaderboardEntryDto>();
        }

        #endregion

        #region Room & Matchmaking Operations

        public async Task<(bool ok, RoomResponseDto room, string error)> CreateRoomAsync(CreateRoomOptions options)
        {
            if (!IsAuthenticated) return (false, null, "Authentication required to create room.");

            string url = $"{BaseUrl}/api/rooms";
            string payload = JsonUtility.ToJson(options ?? new CreateRoomOptions());

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var room = JsonUtility.FromJson<RoomResponseDto>(req.downloadHandler.text);
                return (true, room, null);
            }

            string err = req.downloadHandler != null ? req.downloadHandler.text : req.error;
            return (false, null, err);
        }

        public async Task<(bool ok, JoinRoomResponseDto join, string error)> JoinRoomAsync(string roomCode, string password = "")
        {
            if (!IsAuthenticated) return (false, null, "Authentication required to join room.");
            if (string.IsNullOrWhiteSpace(roomCode)) return (false, null, "Room code cannot be empty.");

            string url = $"{BaseUrl}/api/rooms/{roomCode.ToUpperInvariant().Trim()}/join";
            string payload = JsonUtility.ToJson(new JoinRoomPayload { password = password ?? "" });

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var join = JsonUtility.FromJson<JoinRoomResponseDto>(req.downloadHandler.text);
                return (true, join, null);
            }

            string err = req.downloadHandler != null ? req.downloadHandler.text : req.error;
            return (false, null, err);
        }

        public async Task<RoomDetailsDto> FetchRoomDetailsAsync(string roomCode)
        {
            if (!IsAuthenticated || string.IsNullOrWhiteSpace(roomCode)) return null;

            string url = $"{BaseUrl}/api/rooms/{roomCode.ToUpperInvariant().Trim()}";
            using var req = CreateJsonRequest(url, "GET", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                return JsonUtility.FromJson<RoomDetailsDto>(req.downloadHandler.text);
            }

            return null;
        }

        #endregion

        #region Presence & Social Operations

        public async Task<bool> UpdatePresenceAsync(string status)
        {
            if (!IsAuthenticated) return false;
            string url = $"{BaseUrl}/api/me/presence";
            string payload = JsonUtility.ToJson(new UpdatePresencePayload { presenceStatus = status });

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            return req.result == UnityWebRequest.Result.Success;
        }

        public async Task<List<FriendDto>> FetchFriendsAsync()
        {
            if (!IsAuthenticated) return new List<FriendDto>();
            string url = $"{BaseUrl}/api/friends";
            using var req = CreateJsonRequest(url, "GET", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var list = JsonHelper.FromJsonList<FriendDto>(req.downloadHandler.text);
                CachedFriends = list != null ? new List<FriendDto>(list) : new List<FriendDto>();
                OnFriendsListUpdated?.Invoke(CachedFriends);
                return CachedFriends;
            }
            return CachedFriends;
        }

        public async Task<FriendRequestsContainerDto> FetchFriendRequestsAsync()
        {
            if (!IsAuthenticated) return new FriendRequestsContainerDto();
            string url = $"{BaseUrl}/api/friends/requests";
            using var req = CreateJsonRequest(url, "GET", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                return JsonUtility.FromJson<FriendRequestsContainerDto>(req.downloadHandler.text);
            }
            return new FriendRequestsContainerDto();
        }

        public async Task<(bool ok, string message)> SendFriendRequestAsync(string targetDisplayName)
        {
            if (!IsAuthenticated || string.IsNullOrWhiteSpace(targetDisplayName)) return (false, "Invalid username.");
            string url = $"{BaseUrl}/api/friends/request";
            string payload = JsonUtility.ToJson(new SendFriendRequestPayload { addresseeUsernameOrId = targetDisplayName.Trim() });

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                return (true, "Friend request sent!");
            }
            string err = req.downloadHandler != null ? req.downloadHandler.text : req.error;
            return (false, err);
        }

        public async Task<(bool ok, string message)> RespondFriendRequestAsync(string requestId, bool accept)
        {
            if (!IsAuthenticated || string.IsNullOrEmpty(requestId)) return (false, "Not authenticated or invalid request.");
            string url = $"{BaseUrl}/api/friends/respond";
            string payload = JsonUtility.ToJson(new RespondFriendRequestPayload { requestId = requestId, accept = accept });

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                _ = FetchFriendsAsync();
                return (true, accept ? "Request accepted." : "Request declined.");
            }
            return (false, req.downloadHandler != null ? req.downloadHandler.text : req.error);
        }

        public async Task<bool> RemoveFriendAsync(string friendUserId)
        {
            if (!IsAuthenticated || string.IsNullOrEmpty(friendUserId)) return false;
            string url = $"{BaseUrl}/api/friends/{friendUserId}";
            using var req = CreateJsonRequest(url, "DELETE", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                _ = FetchFriendsAsync();
                return true;
            }
            return false;
        }

        #endregion

        #region Party Operations

        public async Task<(bool ok, PartyDetailsDto party, string error)> CreatePartyAsync()
        {
            if (!IsAuthenticated) return (false, null, "Not authenticated");
            string url = $"{BaseUrl}/api/parties";
            using var req = CreateJsonRequest(url, "POST", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                return await FetchMyPartyAsync();
            }
            return (false, null, req.downloadHandler != null ? req.downloadHandler.text : req.error);
        }

        public async Task<(bool ok, PartyDetailsDto party, string error)> FetchMyPartyAsync()
        {
            if (!IsAuthenticated) return (false, null, "Not authenticated");
            string url = $"{BaseUrl}/api/parties/me";
            using var req = CreateJsonRequest(url, "GET", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var party = JsonUtility.FromJson<PartyDetailsDto>(req.downloadHandler.text);
                CurrentParty = party;
                OnPartyChanged?.Invoke(CurrentParty);
                return (true, CurrentParty, null);
            }
            return (false, null, req.error);
        }

        public async Task<(bool ok, PartyDetailsDto party, string error)> JoinPartyByCodeAsync(string code)
        {
            if (!IsAuthenticated || string.IsNullOrWhiteSpace(code)) return (false, null, "Invalid party code.");
            string url = $"{BaseUrl}/api/parties/join";
            string payload = JsonUtility.ToJson(new JoinPartyPayload { code = code.Trim().ToUpperInvariant() });

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var (pOk, pDetails, pErr) = await FetchMyPartyAsync();
                return (true, pDetails ?? CurrentParty, null);
            }
            return (false, null, req.downloadHandler != null ? req.downloadHandler.text : req.error);
        }

        public async Task<(bool ok, string error)> LeavePartyAsync()
        {
            if (!IsAuthenticated) return (false, "Not authenticated");
            string url = $"{BaseUrl}/api/parties/leave";
            using var req = CreateJsonRequest(url, "POST", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            CurrentParty = null;
            OnPartyChanged?.Invoke(null);
            if (req.result == UnityWebRequest.Result.Success)
            {
                return (true, null);
            }
            return (false, req.downloadHandler != null ? req.downloadHandler.text : req.error);
        }

        public async Task<(bool ok, string error)> SetPartyReadyAsync(bool isReady)
        {
            if (!IsAuthenticated) return (false, "Not authenticated");
            string url = $"{BaseUrl}/api/parties/ready";
            string payload = JsonUtility.ToJson(new SetReadyPayload { isReady = isReady });

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                await FetchMyPartyAsync();
                return (true, null);
            }
            return (false, req.downloadHandler != null ? req.downloadHandler.text : req.error);
        }

        #endregion

        #region Matchmaking Operations

        private Coroutine _pollMatchmakingCoroutine;

        public async Task<(bool ok, MatchmakingTicketDto ticket, string error)> EnqueueMatchmakingAsync(
            string mode = "GroundOperations",
            string region = "na-east",
            bool isParty = false,
            string matchType = "StandardPursuit",
            bool crossplay = true)
        {
            if (!IsAuthenticated) return (false, null, "Not authenticated");

            bool usePartyUrl = isParty || (CurrentParty != null && CurrentParty.inParty);
            string url = usePartyUrl
                ? $"{BaseUrl}/api/parties/queue"
                : $"{BaseUrl}/api/matchmaking/ticket";

            string payload = JsonUtility.ToJson(new QueueMatchmakingPayload
            {
                mode = mode,
                matchType = matchType,
                region = region,
                isParty = usePartyUrl,
                crossplay = crossplay
            });

            using var req = CreateJsonRequest(url, "POST", payload, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var ticket = JsonUtility.FromJson<MatchmakingTicketDto>(req.downloadHandler.text);
                CurrentTicket = ticket;
                OnMatchmakingTicketUpdated?.Invoke(CurrentTicket);

                if (ticket.status == "matched" && ticket.assignment != null)
                {
                    OnMatchFound?.Invoke(ticket.assignment);
                }
                else
                {
                    if (_pollMatchmakingCoroutine != null) StopCoroutine(_pollMatchmakingCoroutine);
                    _pollMatchmakingCoroutine = StartCoroutine(PollMatchmakingRoutine(ticket.ticketId));
                }

                return (true, ticket, null);
            }

            return (false, null, req.downloadHandler != null ? req.downloadHandler.text : req.error);
        }

        private IEnumerator PollMatchmakingRoutine(string ticketId)
        {
            float elapsed = 0f;
            while (CurrentTicket != null && CurrentTicket.status == "queued" && elapsed < 120f)
            {
                yield return new WaitForSeconds(1.0f);
                elapsed += 1.0f;

                string url = $"{BaseUrl}/api/matchmaking/ticket/{ticketId}";
                using var req = CreateJsonRequest(url, "GET", null, authRequired: true);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var updated = JsonUtility.FromJson<MatchmakingTicketDto>(req.downloadHandler.text);
                    if (updated != null)
                    {
                        CurrentTicket = updated;
                        OnMatchmakingTicketUpdated?.Invoke(CurrentTicket);

                        if (updated.status == "matched" && updated.assignment != null)
                        {
                            Debug.Log($"[ApiClient] Match found! Connecting to {updated.assignment.serverIp}:{updated.assignment.serverPort} ({updated.assignment.team})");
                            OnMatchFound?.Invoke(updated.assignment);
                            yield break;
                        }
                    }
                }
            }
        }

        public async Task<bool> CancelMatchmakingAsync()
        {
            if (!IsAuthenticated || CurrentTicket == null || string.IsNullOrEmpty(CurrentTicket.ticketId)) return false;

            if (_pollMatchmakingCoroutine != null)
            {
                StopCoroutine(_pollMatchmakingCoroutine);
                _pollMatchmakingCoroutine = null;
            }

            string ticketId = CurrentTicket.ticketId;
            string url = $"{BaseUrl}/api/matchmaking/ticket/{ticketId}";

            using var req = CreateJsonRequest(url, "DELETE", null, authRequired: true);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            CurrentTicket = null;
            OnMatchmakingTicketUpdated?.Invoke(null);
            return req.result == UnityWebRequest.Result.Success;
        }

        #endregion

        #region Match Completion Reporting (Server-Side)

        public async Task<(bool ok, string message)> CompleteMatchServerAsync(CompleteMatchRequestDto matchData, string serverKey = "DEV_MATCH_SERVER_KEY_CHANGE")
        {
            string url = $"{BaseUrl}/internal/matches/complete";
            string payload = JsonUtility.ToJson(matchData);

            using var req = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Match-Server-Key", serverKey);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[ApiClient] Match {matchData.matchId} completed and awarded via backend: {req.downloadHandler.text}");
                return (true, req.downloadHandler.text);
            }

            string err = req.downloadHandler != null ? req.downloadHandler.text : req.error;
            Debug.LogError($"[ApiClient] Failed to report match completion to backend: {err}");
            return (false, err);
        }

        #endregion

        private UnityWebRequest CreateJsonRequest(string url, string method, string jsonPayload, bool authRequired)
        {
            var req = new UnityWebRequest(url, method);
            if (!string.IsNullOrEmpty(jsonPayload))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            if (authRequired && !string.IsNullOrEmpty(AccessToken))
            {
                req.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            }

            return req;
        }

        #region Payload Wrappers

        [Serializable]
        private struct RegisterPayload
        {
            public string email;
            public string password;
            public string displayName;
        }

        [Serializable]
        private struct LoginPayload
        {
            public string email;
            public string password;
        }

        [Serializable]
        private struct JoinRoomPayload
        {
            public string password;
        }

        [Serializable]
        private struct UpdatePresencePayload
        {
            public string presenceStatus;
        }

        [Serializable]
        private struct SendFriendRequestPayload
        {
            public string addresseeUsernameOrId;
        }

        [Serializable]
        private struct RespondFriendRequestPayload
        {
            public string requestId;
            public bool accept;
        }

        [Serializable]
        private struct JoinPartyPayload
        {
            public string code;
        }

        [Serializable]
        private struct SetReadyPayload
        {
            public bool isReady;
        }

        [Serializable]
        private struct QueueMatchmakingPayload
        {
            public string mode;
            public string matchType;
            public string region;
            public bool isParty;
            public bool crossplay;
        }

        #endregion
    }

    /// <summary>
    /// Helper to parse top-level JSON arrays using Unity's JsonUtility.
    /// </summary>
    public static class JsonHelper
    {
        [Serializable]
        private class Wrapper<T>
        {
            public List<T> items;
        }

        public static List<T> FromJsonList<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<T>();
            string wrapped = "{\"items\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrapped);
            return wrapper != null && wrapper.items != null ? wrapper.items : new List<T>();
        }
    }
}
