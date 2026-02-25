using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Meta.XR.MRUtilityKit;

namespace NulabCup
{
    /// <summary>
    /// 右手のサムズアップ（👍）ジェスチャーを検出してキューブをスポーンする。
    /// 親指が上向き＋他の指が握られている状態で判定する。
    /// </summary>
    public class CubeSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] GameObject m_CubePrefab;
        [SerializeField] float m_TargetHeightOffset = 0.3f;
        [SerializeField] float m_RainHeight = 2.0f;
        [SerializeField] float m_RainRadius = 0.25f;
        [SerializeField] int m_MaxCubes = 10;
        [SerializeField] float m_Cooldown = 2.0f;

        [Header("Thumbs Up Detection")]
        [SerializeField] float m_CurlThreshold = 0.05f;
        [SerializeField] float m_ThumbUpDot = 0.7f;

        [Header("MRUK Spawn")]
        [SerializeField] bool m_UseMrukFloorHeight = true;
        [SerializeField, Min(0.5f)] float m_FloorRayStartHeight = 2.5f;
        [SerializeField, Min(0.5f)] float m_FloorRayDistance = 6.0f;
        [SerializeField] bool m_DebugFloorRay = false;

        XRHandSubsystem m_HandSubsystem;
        readonly List<GameObject> m_SpawnedCubes = new();
        readonly List<XRHandSubsystem> m_HandSubsystems = new();
        float m_LastSpawnTime;
        bool m_WasThumbsUp;

        static readonly LabelFilter s_FloorFilter = new(
            MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.GLOBAL_MESH,
            MRUKAnchor.ComponentType.All);

        void Update()
        {
            if (m_HandSubsystem == null || !m_HandSubsystem.running)
            {
                TryGetHandSubsystem();
                return;
            }

            var rightHand = m_HandSubsystem.rightHand;
            if (!rightHand.isTracked)
                return;

            bool isThumbsUp = IsThumbsUp(rightHand);

            if (isThumbsUp && !m_WasThumbsUp && Time.time - m_LastSpawnTime >= m_Cooldown)
            {
                SpawnCube(rightHand);
                m_LastSpawnTime = Time.time;
            }

            m_WasThumbsUp = isThumbsUp;
        }

        void TryGetHandSubsystem()
        {
            m_HandSubsystems.Clear();
            SubsystemManager.GetSubsystems(m_HandSubsystems);
            if (m_HandSubsystems.Count > 0)
                m_HandSubsystem = m_HandSubsystems[0];
        }

        bool IsThumbsUp(XRHand hand)
        {
            // 親指の先端が上を向いているか
            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbTipPose))
                return false;
            if (!hand.GetJoint(XRHandJointID.ThumbProximal).TryGetPose(out var thumbProxPose))
                return false;

            var thumbDir = (thumbTipPose.position - thumbProxPose.position).normalized;
            if (Vector3.Dot(thumbDir, Vector3.up) < m_ThumbUpDot)
                return false;

            // 他の4本指が握られているか（先端と付け根が近い）
            if (!IsFingerCurled(hand, XRHandJointID.IndexTip, XRHandJointID.IndexProximal))
                return false;
            if (!IsFingerCurled(hand, XRHandJointID.MiddleTip, XRHandJointID.MiddleProximal))
                return false;
            if (!IsFingerCurled(hand, XRHandJointID.RingTip, XRHandJointID.RingProximal))
                return false;
            if (!IsFingerCurled(hand, XRHandJointID.LittleTip, XRHandJointID.LittleProximal))
                return false;

            return true;
        }

        bool IsFingerCurled(XRHand hand, XRHandJointID tipId, XRHandJointID proximalId)
        {
            if (!hand.GetJoint(tipId).TryGetPose(out var tipPose))
                return false;
            if (!hand.GetJoint(proximalId).TryGetPose(out var proxPose))
                return false;

            return Vector3.Distance(tipPose.position, proxPose.position) < m_CurlThreshold;
        }

        void SpawnCube(XRHand hand)
        {
            if (m_CubePrefab == null)
                return;

            if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose))
                return;

            // 外部で破棄されたキューブをリストから除去
            m_SpawnedCubes.RemoveAll(c => c == null);

            var targetPos = palmPose.position + Vector3.up * m_TargetHeightOffset;
            var horizontalOffset = Random.insideUnitCircle * m_RainRadius;
            var spawnXZ = targetPos + new Vector3(horizontalOffset.x, 0f, horizontalOffset.y);

            var baseHeight = targetPos.y;
            if (m_UseMrukFloorHeight && TryGetFloorHeight(spawnXZ, out var floorY))
            {
                baseHeight = floorY + m_TargetHeightOffset;
            }

            var spawnPos = new Vector3(spawnXZ.x, baseHeight + m_RainHeight, spawnXZ.z);
            var cube = Instantiate(m_CubePrefab, spawnPos, Quaternion.identity);

            if (cube.TryGetComponent<Rigidbody>(out var rb))
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
#pragma warning disable CS0618
                rb.velocity = Vector3.zero;
#pragma warning restore CS0618
#endif
                rb.angularVelocity = Vector3.zero;
            }

            m_SpawnedCubes.Add(cube);

            while (m_SpawnedCubes.Count > m_MaxCubes)
            {
                var oldest = m_SpawnedCubes[0];
                m_SpawnedCubes.RemoveAt(0);
                if (oldest != null)
                    Destroy(oldest);
            }
        }

        bool TryGetFloorHeight(Vector3 worldPosition, out float floorY)
        {
            floorY = 0f;

            var mruk = MRUK.Instance;
            if (mruk == null || !mruk.IsInitialized)
                return false;

            var room = mruk.GetCurrentRoom();
            if (room == null && mruk.Rooms.Count > 0)
                room = mruk.Rooms[0];
            if (room == null)
                return false;

            var rayOrigin = worldPosition + Vector3.up * m_FloorRayStartHeight;
            var ray = new Ray(rayOrigin, Vector3.down);

            if (!room.Raycast(ray, m_FloorRayDistance, s_FloorFilter, out var hit, out var _))
            {
                if (m_DebugFloorRay)
                    Debug.DrawRay(rayOrigin, Vector3.down * m_FloorRayDistance, Color.red, 0.15f);
                return false;
            }

            if (m_DebugFloorRay)
                Debug.DrawLine(rayOrigin, hit.point, Color.cyan, 0.15f);

            floorY = hit.point.y;
            return true;
        }
    }
}
