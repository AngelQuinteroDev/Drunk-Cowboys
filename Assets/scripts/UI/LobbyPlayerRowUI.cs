using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FPSMultiplayer.Lobby;

namespace FPSMultiplayer.UI
{
    public class LobbyPlayerRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _hostBadgeText;
        [SerializeField] private Button _kickButton;

        private int _playerId;
        private Action<int> _onKick;

        public void Bind(LobbyPlayerEntry entry, bool canKick, Action<int> onKick)
        {
            _playerId = entry.PlayerId;
            _onKick = onKick;

            if (_nameText != null)
                _nameText.text = entry.Name.ToString();

            if (_statusText != null)
                _statusText.text = entry.IsReady ? "Ready" : "Not Ready";

            if (_hostBadgeText != null)
                _hostBadgeText.gameObject.SetActive(entry.IsHost);

            if (_kickButton != null)
            {
                _kickButton.onClick.RemoveAllListeners();
                _kickButton.gameObject.SetActive(canKick);
                if (canKick)
                    _kickButton.onClick.AddListener(OnKickClicked);
            }
        }

        private void OnKickClicked()
        {
            _onKick?.Invoke(_playerId);
        }
    }
}
