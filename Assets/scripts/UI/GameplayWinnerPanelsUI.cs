using TMPro;
using UnityEngine;
using FPSMultiplayer.Core;
using FPSMultiplayer.Gameplay;

namespace FPSMultiplayer.UI
{
    public class GameplayWinnerPanelsUI : MonoBehaviour
    {
        [Header("Round Winner Panel")]
        [SerializeField] private GameObject roundWinnerPanel;
        [SerializeField] private TMP_Text roundWinnerText;

        [Header("Match Winner Panel")]
        [SerializeField] private GameObject matchWinnerPanel;
        [SerializeField] private TMP_Text matchWinnerText;

        private void Awake()
        {
            ResolveTextReferences();
            HideAllPanels();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RoundStateChanged>(OnRoundStateChanged);
            EventBus.Subscribe<RoundWinnerDeclared>(OnRoundWinnerDeclared);
            EventBus.Subscribe<MatchWinnerDeclared>(OnMatchWinnerDeclared);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RoundStateChanged>(OnRoundStateChanged);
            EventBus.Unsubscribe<RoundWinnerDeclared>(OnRoundWinnerDeclared);
            EventBus.Unsubscribe<MatchWinnerDeclared>(OnMatchWinnerDeclared);
        }

        private void OnRoundStateChanged(RoundStateChanged evt)
        {
            if (evt.State == RoundState.RoundEnded || evt.State == RoundState.MatchEnded)
                return;

            HideAllPanels();
        }

        private void OnRoundWinnerDeclared(RoundWinnerDeclared evt)
        {
            ResolveTextReferences();

            if (roundWinnerPanel != null)
                roundWinnerPanel.SetActive(true);

            if (roundWinnerText != null)
                roundWinnerText.text = string.IsNullOrWhiteSpace(evt.WinnerName) ? "Sin ganador" : evt.WinnerName;

            if (matchWinnerPanel != null)
                matchWinnerPanel.SetActive(false);
        }

        private void OnMatchWinnerDeclared(MatchWinnerDeclared evt)
        {
            ResolveTextReferences();

            if (roundWinnerPanel != null)
                roundWinnerPanel.SetActive(false);

            if (matchWinnerPanel != null)
                matchWinnerPanel.SetActive(true);

            if (matchWinnerText != null)
                matchWinnerText.text = string.IsNullOrWhiteSpace(evt.WinnerName) ? "Empate" : evt.WinnerName;
        }

        private void HideAllPanels()
        {
            if (roundWinnerPanel != null)
                roundWinnerPanel.SetActive(false);

            if (matchWinnerPanel != null)
                matchWinnerPanel.SetActive(false);
        }

        private void ResolveTextReferences()
        {
            if (roundWinnerText == null && roundWinnerPanel != null)
                roundWinnerText = roundWinnerPanel.GetComponentInChildren<TMP_Text>(true);

            if (matchWinnerText == null && matchWinnerPanel != null)
                matchWinnerText = matchWinnerPanel.GetComponentInChildren<TMP_Text>(true);
        }
    }
}