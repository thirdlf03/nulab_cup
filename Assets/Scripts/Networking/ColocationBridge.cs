using System;
using System.Text;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Meta;
using NulabCup.Debugging;

namespace NulabCup.Networking
{
    /// <summary>
    /// Colocation Discovery と Fusion セッション参加を橋渡しする。
    /// 起動時に自動で Discovery を開始し、他デバイスを発見すればそのセッションに参加する。
    /// デバッグ用に ForceHost() で強制的に Host としてセッションを作成できる。
    /// </summary>
    public class ColocationBridge : MonoBehaviour
    {
        const string Tag = "[ColocationBridge]";
        const string SessionPrefix = "NulabCup_";
        [SerializeField] FusionBootstrap m_FusionBootstrap;

        ColocationDiscoveryFeature m_Feature;
        bool m_Resolved;
        bool m_Initialized;

        public enum BridgeState
        {
            Idle,
            Discovering,
            Advertising,
            Connected
        }

        public BridgeState CurrentState { get; private set; } = BridgeState.Idle;

        void Start()
        {
            StartupProfiler.LogMilestone("ColocationBridge", "Start() BEGIN");

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

            StartupProfiler.LogMilestone("ColocationBridge", "OpenXRSettings.GetFeature BEGIN");
            m_Feature = OpenXRSettings.Instance?.GetFeature<ColocationDiscoveryFeature>();
            StartupProfiler.LogMilestone("ColocationBridge", $"OpenXRSettings.GetFeature END (found={m_Feature != null}, enabled={m_Feature?.enabled})");

            if (m_Feature != null && m_Feature.enabled)
            {
                m_Feature.messageDiscovered += OnMessageDiscovered;
            }

            m_Initialized = true;
            Debug.Log($"{Tag} Initialized. Auto-starting discovery.");
            StartupProfiler.LogMilestone("ColocationBridge", "Start() END — auto-starting discovery");

            BeginDiscovery();
        }

        /// <summary>
        /// Discovery を開始する。Start() から自動呼び出しされる。
        /// </summary>
        public async void BeginDiscovery()
        {
            if (!m_Initialized)
            {
                Debug.LogWarning($"{Tag} Not initialized yet.");
                return;
            }

            if (CurrentState != BridgeState.Idle)
            {
                Debug.LogWarning($"{Tag} Already in state {CurrentState}, ignoring BeginDiscovery.");
                return;
            }

            StartupProfiler.LogMilestone("ColocationBridge", "BeginDiscovery() called from UI");

            if (m_Feature == null || !m_Feature.enabled)
            {
                Debug.LogWarning($"{Tag} ColocationDiscoveryFeature not available. Starting as host without colocation.");
                BecomeHost();
                return;
            }

            Debug.Log($"{Tag} Starting colocation discovery...");
            CurrentState = BridgeState.Discovering;
            m_Resolved = false;

            StartupProfiler.LogMilestone("ColocationBridge", "TryStartDiscoveryAsync BEGIN");
            var result = await m_Feature.TryStartDiscoveryAsync();
            StartupProfiler.LogMilestone("ColocationBridge", $"TryStartDiscoveryAsync END (success={result.IsSuccess()})");

            if (!result.IsSuccess())
            {
                Debug.LogWarning($"{Tag} Failed to start discovery. Starting as host.");
                BecomeHost();
            }
        }

        /// <summary>
        /// デバッグ用：Discovery を強制停止して Host になる。
        /// </summary>
        public void ForceHost()
        {
            if (CurrentState == BridgeState.Connected || CurrentState == BridgeState.Advertising)
            {
                Debug.LogWarning($"{Tag} Already connected/advertising, ignoring ForceHost.");
                return;
            }

            Debug.Log($"{Tag} ForceHost called — stopping discovery and becoming host.");
            m_Resolved = true;
            StopDiscoveryAndBecomeHost();
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
            StartupProfiler.LogMilestone("ColocationBridge", "BecomeHost BEGIN");
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
