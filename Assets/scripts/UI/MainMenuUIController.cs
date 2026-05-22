using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FPSMultiplayer.Core;
using FPSMultiplayer.Infrastructure;
using FPSMultiplayer.Networking;
using FPSMultiplayer.Shared;

namespace FPSMultiplayer.UI
{
    public class MainMenuUIController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _initPanel;
        [SerializeField] private GameObject _roomsPanel;

        [Header("Init Panel")]
        [SerializeField] private TMP_InputField _playerNameInput;
        [SerializeField] private Button _playButton;

        [Header("Rooms Panel")]
        [SerializeField] private TMP_InputField _joinRoomInput;
        [SerializeField] private TMP_InputField _createRoomInput;
        [SerializeField] private Button _joinButton;
        [SerializeField] private Button _createButton;
        [SerializeField] private TMP_Text _statusText;

        [Header("Defaults")]
        [SerializeField] private string _playerNameKey = "PlayerName";
        [SerializeField] private string _roomPrefix = "Room_";

        private bool _isBusy;

        private void Awake()
        {
            if (_playButton != null)
                _playButton.onClick.AddListener(OnPlayClicked);
            if (_joinButton != null)
                _joinButton.onClick.AddListener(OnJoinClicked);
            if (_createButton != null)
                _createButton.onClick.AddListener(OnCreateClicked);
        }

        private void Start()
        {
            ShowInitPanel();

            if (_playerNameInput != null)
            {
                var saved = PlayerPrefs.GetString(_playerNameKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(saved))
                    _playerNameInput.text = saved;
            }
        }

        private void OnDestroy()
        {
            if (_playButton != null)
                _playButton.onClick.RemoveListener(OnPlayClicked);
            if (_joinButton != null)
                _joinButton.onClick.RemoveListener(OnJoinClicked);
            if (_createButton != null)
                _createButton.onClick.RemoveListener(OnCreateClicked);
        }

        private void OnPlayClicked()
        {
            string name = _playerNameInput != null ? _playerNameInput.text : string.Empty;
            name = SanitizePlayerName(name);

            PlayerPrefs.SetString(_playerNameKey, name);
            PlayerPrefs.Save();

            SetStatus(string.Empty);
            ShowRoomsPanel();
        }

        private async void OnCreateClicked()
        {
            if (_isBusy) return;

            string roomName = _createRoomInput != null ? _createRoomInput.text : string.Empty;
            roomName = SanitizeRoomName(roomName, true);

            if (string.IsNullOrWhiteSpace(roomName))
            {
                SetStatus("Nombre de sala requerido.");
                return;
            }

            await StartSession(roomName, true);
        }

        private async void OnJoinClicked()
        {
            if (_isBusy) return;

            string roomName = _joinRoomInput != null ? _joinRoomInput.text : string.Empty;
            roomName = SanitizeRoomName(roomName, false);

            if (string.IsNullOrWhiteSpace(roomName))
            {
                SetStatus("Nombre de sala requerido.");
                return;
            }

            await StartSession(roomName, false);
        }

        private async Task StartSession(string roomName, bool create)
        {
            SetBusy(true);

            if (!ServiceLocator.TryGet<ISessionManager>(out var sessionManager))
            {
                SetStatus("SessionManager no disponible.");
                SetBusy(false);
                return;
            }

            if (create)
                await sessionManager.CreateRoom(roomName, GameConstants.Network.MaxPlayers);
            else
                await sessionManager.JoinRoom(roomName);

            if (create)
                ServiceLocator.Get<ISceneFlowManager>()?.LoadLobbyScene();

            SetBusy(false);
        }

        private void ShowInitPanel()
        {
            if (_initPanel != null) _initPanel.SetActive(true);
            if (_roomsPanel != null) _roomsPanel.SetActive(false);
        }

        private void ShowRoomsPanel()
        {
            if (_initPanel != null) _initPanel.SetActive(false);
            if (_roomsPanel != null) _roomsPanel.SetActive(true);
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;

            if (_playButton != null) _playButton.interactable = !busy;
            if (_joinButton != null) _joinButton.interactable = !busy;
            if (_createButton != null) _createButton.interactable = !busy;
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        private string SanitizePlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return $"Player_{Random.Range(1000, 9999)}";

            name = name.Trim();
            if (name.Length > 24)
                name = name.Substring(0, 24);

            return name;
        }

        private string SanitizeRoomName(string roomName, bool allowAuto)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                return allowAuto ? $"{_roomPrefix}{Random.Range(1000, 9999)}" : string.Empty;

            roomName = roomName.Trim();
            if (roomName.Length > 32)
                roomName = roomName.Substring(0, 32);

            return roomName;
        }
    }
}
