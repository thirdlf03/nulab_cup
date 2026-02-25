using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NulabCup.Networking
{
    /// <summary>
    /// Photon Fusion 2 セッションの開始・管理を行う。
    /// Shared Mode で動作し、Colocation 環境でのP2Pマルチプレイに使用する。
    /// </summary>
    public class FusionBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        const string Tag = "[FusionBootstrap]";

        NetworkRunner m_Runner;

        public bool IsConnected => m_Runner != null && m_Runner.IsRunning;
        public string SessionName { get; private set; }
        public int PlayerCount => m_Runner != null && m_Runner.IsRunning ? m_Runner.SessionInfo.PlayerCount : 0;

        public event Action OnJoinedSession;
        public event Action<string> OnConnectionFailed;

        public async void StartSession(string sessionName)
        {
            if (m_Runner != null)
            {
                Debug.LogWarning($"{Tag} Session already exists. Shutting down first.");
                await m_Runner.Shutdown();
            }

            m_Runner = gameObject.AddComponent<NetworkRunner>();
            m_Runner.ProvideInput = false;

            Debug.Log($"{Tag} Starting Fusion session: {sessionName}");

            var result = await m_Runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = sessionName,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (result.Ok)
            {
                SessionName = sessionName;
                Debug.Log($"{Tag} Joined session: {sessionName}");
                OnJoinedSession?.Invoke();
            }
            else
            {
                var reason = result.ShutdownReason.ToString();
                Debug.LogError($"{Tag} Failed to start session: {reason}");
                OnConnectionFailed?.Invoke(reason);
            }
        }

        void OnDestroy()
        {
            if (m_Runner != null)
                m_Runner.Shutdown();
        }

        // --- INetworkRunnerCallbacks ---

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"{Tag} Player joined: {player}");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"{Tag} Player left: {player}");
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"{Tag} Shutdown: {shutdownReason}");
            SessionName = null;
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
