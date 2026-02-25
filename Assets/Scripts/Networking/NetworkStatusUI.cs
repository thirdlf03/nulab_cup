using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NulabCup.Networking
{
    /// <summary>
    /// HandMenu の Network タブに接続状態を表示する。
    /// Discovery は起動時に自動開始され、
    /// ボタンはデバッグ用の強制 Host 切り替えに使用する。
    /// </summary>
    public class NetworkStatusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] FusionBootstrap m_FusionBootstrap;
        [SerializeField] ColocationBridge m_ColocationBridge;

        [Header("UI Elements")]
        [SerializeField] TextMeshProUGUI m_StatusText;
        [SerializeField] Button m_NavigateButton;

        void Start()
        {
            if (m_StatusText != null)
            {
                m_StatusText.fontSize = 16;
                m_StatusText.enableWordWrapping = true;
                m_StatusText.alignment = TextAlignmentOptions.TopLeft;
            }

            if (m_NavigateButton != null)
            {
                m_NavigateButton.onClick.AddListener(OnNavigatePressed);
            }
        }

        void OnNavigatePressed()
        {
            if (m_ColocationBridge != null)
            {
                m_ColocationBridge.ForceHost();
            }

            if (m_NavigateButton != null)
            {
                m_NavigateButton.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (m_StatusText == null)
                return;

            var colocationState = m_ColocationBridge != null
                ? m_ColocationBridge.CurrentState.ToString()
                : "---";
            var fusionState = m_FusionBootstrap != null && m_FusionBootstrap.IsConnected
                ? "Connected"
                : "Disconnected";
            var session = m_FusionBootstrap != null ? m_FusionBootstrap.SessionName : null;
            var count = m_FusionBootstrap != null ? m_FusionBootstrap.PlayerCount : 0;

            m_StatusText.text =
                $"Colocation: {colocationState}\n" +
                $"Fusion: {fusionState}\n" +
                $"Session: {session ?? "---"}\n" +
                $"Players: {count}";

            // 接続済み or Advertising中ならボタンを非表示
            if (m_NavigateButton != null && m_ColocationBridge != null)
            {
                var state = m_ColocationBridge.CurrentState;
                var showButton = state == ColocationBridge.BridgeState.Idle
                    || state == ColocationBridge.BridgeState.Discovering;
                m_NavigateButton.gameObject.SetActive(showButton);
            }
        }

        void OnDestroy()
        {
            if (m_NavigateButton != null)
            {
                m_NavigateButton.onClick.RemoveListener(OnNavigatePressed);
            }
        }
    }
}
