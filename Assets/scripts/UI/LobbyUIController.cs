using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using FPSMultiplayer.Core;
using FPSMultiplayer.Lobby;
using FPSMultiplayer.Networking;

namespace FPSMultiplayer.UI
{
    public class LobbyUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LobbyManager _lobbyManager;
        [SerializeField] private RectTransform _playersRoot;
        [SerializeField] private LobbyPlayerRowUI _playerRowPrefab;

        [Header("Controls")]
        [SerializeField] private Button _readyButton;
        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _roomNameText;
        [SerializeField] private TMP_Text _statusText;

        private ISessionManager _sessionManager;
        private readonly List<LobbyPlayerRowUI> _rows = new();
        private bool _nameSynced;

        private void Awake()
        {
            if (_readyButton != null)
                _readyButton.onClick.AddListener(OnReadyClicked);
            if (_startButton != null)
                _startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnEnable()
        {
            EnsureCursorUnlocked();
            EventBus.Subscribe<LobbyListUpdated>(OnLobbyListUpdated);
            ResolveServices();
            RefreshList();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LobbyListUpdated>(OnLobbyListUpdated);
        }

        private void OnDestroy()
        {
            if (_readyButton != null)
                _readyButton.onClick.RemoveListener(OnReadyClicked);
            if (_startButton != null)
                _startButton.onClick.RemoveListener(OnStartClicked);
        }

        private void ResolveServices()
        {
            ServiceLocator.TryGet<ISessionManager>(out _sessionManager);
            if (_lobbyManager == null)
                ServiceLocator.TryGet<LobbyManager>(out _lobbyManager);
        }

        private void OnLobbyListUpdated(LobbyListUpdated _)
        {
            RefreshList();
        }

        private void RefreshList()
        {
            ResolveServices();
            UpdateRoomName();

            if (!IsLobbyReady() || _playersRoot == null || _playerRowPrefab == null)
                return;

            ClearRows();

            bool isHost = _sessionManager != null && _sessionManager.IsHost;

            foreach (var entry in _lobbyManager.Players)
            {
                var row = Instantiate(_playerRowPrefab, _playersRoot, false);
                bool canKick = isHost && !entry.IsHost;
                row.Bind(entry, canKick, OnKickClicked);
                _rows.Add(row);
            }

            UpdateButtons(isHost);
            TrySyncLocalName();
        }

        private void UpdateButtons(bool isHost)
        {
            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(isHost);
                _startButton.interactable = isHost && AreAllNonHostReady();
            }
        }

        private bool AreAllNonHostReady()
        {
            if (!IsLobbyReady()) return false;

            foreach (var entry in _lobbyManager.Players)
            {
                if (!entry.IsHost && !entry.IsReady)
                    return false;
            }

            return true;
        }

        private int GetLocalPlayerId()
        {
            var runner = _sessionManager?.Runner;
            return runner != null ? runner.LocalPlayer.PlayerId : -1;
        }

        private void OnReadyClicked()
        {
            if (!IsLobbyReady()) return;

            int localId = GetLocalPlayerId();
            if (localId < 0) return;

            bool isReady = false;
            foreach (var entry in _lobbyManager.Players)
            {
                if (entry.PlayerId == localId)
                {
                    isReady = entry.IsReady;
                    break;
                }
            }

            _lobbyManager.RPC_SetReady(localId, !isReady);
        }

        private void OnStartClicked()
        {
            _lobbyManager?.RPC_StartMatch();
        }

        private void OnKickClicked(int playerId)
        {
            _lobbyManager?.RPC_KickPlayer(playerId);
        }

        private void TrySyncLocalName()
        {
            if (_nameSynced || !IsLobbyReady()) return;

            int localId = GetLocalPlayerId();
            if (localId < 0) return;

            string name = PlayerPrefs.GetString("PlayerName", $"Player_{localId}");
            _lobbyManager.RPC_SetPlayerName(localId, name);
            _nameSynced = true;
        }

        private void UpdateRoomName()
        {
            if (_roomNameText == null) return;

            if (_sessionManager?.Runner != null && _sessionManager.Runner.SessionInfo.IsValid)
                _roomNameText.text = _sessionManager.Runner.SessionInfo.Name;
        }
        private void ClearRows()
        {
            foreach (Transform child in _playersRoot)
            {
                Destroy(child.gameObject);
            }

            _rows.Clear();
        }

        private bool IsLobbyReady()
        {
            if (_lobbyManager == null) return false;
            var obj = _lobbyManager.Object;
            return obj != null && obj.IsValid;
        }

        private static void EnsureCursorUnlocked()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
