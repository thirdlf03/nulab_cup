using TMPro;
using UnityEngine;

namespace NulabCup.Networking
{
    /// <summary>
    /// HandMenu の Network タブに接続状態を表示する。
    /// テキスト1つに全情報をまとめて表示する。
    /// </summary>
    public class NetworkStatusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] FusionBootstrap m_FusionBootstrap;
        [SerializeField] ColocationBridge m_ColocationBridge;

        [Header("UI Elements")]
        [SerializeField] TextMeshProUGUI m_StatusText;

        void Start()
        {
            if (m_StatusText != null)
            {
                m_StatusText.fontSize = 16;
                m_StatusText.enableWordWrapping = true;
                m_StatusText.alignment = TextAlignmentOptions.TopLeft;
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
        }
    }
}
