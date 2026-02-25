using System;
using System.Text;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Meta;

namespace NulabCup.Networking
{
    /// <summary>
    /// Colocation Discovery と Fusion セッション参加を橋渡しする。
    /// 起動時に Discovery を開始し、タイムアウト内に他のデバイスを発見できれば
    /// そのセッションに参加、できなければ Host としてセッションを作成し Advertisement を開始する。
    /// </summary>
    public class ColocationBridge : MonoBehaviour
    {
        const string Tag = "[ColocationBridge]";
        const string SessionPrefix = "NulabCup_";
        const float DiscoveryTimeout = 5f;

        [SerializeField] FusionBootstrap m_FusionBootstrap;

        ColocationDiscoveryFeature m_Feature;
        float m_DiscoveryStartTime;
        bool m_Resolved;

        public enum BridgeState
        {
            Idle,
            Discovering,
            Advertising,
            Connected
        }

        public BridgeState CurrentState { get; private set; } = BridgeState.Idle;

        async void Start()
        {
            if (m_FusionBootstrap == null)
            {
                m_FusionBootstrap = GetComponent<FusionBootstrap>();
                if (m_FusionBootstrap == null)
                {
                    Debug.LogError($"{Tag} FusionBootstrap not found.");
                    return;
                }
            }

            m_FusionBootstrap.OnJoinedSession += () => CurrentState = BridgeState.Connected;

            m_Feature = OpenXRSettings.Instance?.GetFeature<ColocationDiscoveryFeature>();
            if (m_Feature == null || !m_Feature.enabled)
            {
                Debug.LogWarning($"{Tag} ColocationDiscoveryFeature not available. Starting as host without colocation.");
                BecomeHost();
                return;
            }

            m_Feature.messageDiscovered += OnMessageDiscovered;

            Debug.Log($"{Tag} Starting colocation discovery...");
            CurrentState = BridgeState.Discovering;
            m_DiscoveryStartTime = Time.time;

            var result = await m_Feature.TryStartDiscoveryAsync();
            if (!result.IsSuccess())
            {
                Debug.LogWarning($"{Tag} Failed to start discovery. Starting as host.");
                BecomeHost();
            }
        }

        void Update()
        {
            if (CurrentState != BridgeState.Discovering || m_Resolved)
                return;

            if (Time.time - m_DiscoveryStartTime >= DiscoveryTimeout)
            {
                Debug.Log($"{Tag} Discovery timed out, becoming host.");
                m_Resolved = true;
                StopDiscoveryAndBecomeHost();
            }
        }

        void OnMessageDiscovered(object sender, ColocationDiscoveryMessage message)
        {
            if (m_Resolved)
                return;

            m_Resolved = true;

            var sessionName = Encoding.UTF8.GetString(message.data);
            Debug.Log($"{Tag} Discovered session: {sessionName}");

            CurrentState = BridgeState.Connected;
            StopDiscoveryThenJoin(sessionName);
        }

        async void StopDiscoveryThenJoin(string sessionName)
        {
            if (m_Feature != null && m_Feature.discoveryState == ColocationState.Active)
                await m_Feature.TryStopDiscoveryAsync();

            m_FusionBootstrap.StartSession(sessionName);
        }

        async void StopDiscoveryAndBecomeHost()
        {
            if (m_Feature != null && m_Feature.discoveryState == ColocationState.Active)
                await m_Feature.TryStopDiscoveryAsync();

            BecomeHost();
        }

        void BecomeHost()
        {
            var sessionName = SessionPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
            Debug.Log($"{Tag} Creating session as host: {sessionName}");

            m_FusionBootstrap.OnJoinedSession += () => StartAdvertising(sessionName);
            m_FusionBootstrap.StartSession(sessionName);
        }

        async void StartAdvertising(string sessionName)
        {
            if (m_Feature == null || !m_Feature.enabled)
                return;

            CurrentState = BridgeState.Advertising;
            var bytes = Encoding.UTF8.GetBytes(sessionName);
            Debug.Log($"{Tag} Starting advertisement: {sessionName}");

            var result = await m_Feature.TryStartAdvertisementAsync(bytes.AsSpan());
            if (result.status.IsSuccess())
            {
                Debug.Log($"{Tag} Advertisement active.");
            }
            else
            {
                Debug.LogWarning($"{Tag} Failed to start advertisement.");
            }
        }

        void OnDestroy()
        {
            if (m_Feature != null)
            {
                m_Feature.messageDiscovered -= OnMessageDiscovered;

                if (m_Feature.discoveryState == ColocationState.Active)
                    _ = m_Feature.TryStopDiscoveryAsync();

                if (m_Feature.advertisementState == ColocationState.Active)
                    _ = m_Feature.TryStopAdvertisementAsync();
            }
        }
    }
}
