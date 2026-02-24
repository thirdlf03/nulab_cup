using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

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
        [SerializeField] float m_SpawnDistance = 0.3f;
        [SerializeField] int m_MaxCubes = 10;
        [SerializeField] float m_Cooldown = 2.0f;

        [Header("Thumbs Up Detection")]
        [SerializeField] float m_CurlThreshold = 0.05f;
        [SerializeField] float m_ThumbUpDot = 0.7f;

        XRHandSubsystem m_HandSubsystem;
        readonly List<GameObject> m_SpawnedCubes = new();
        readonly List<XRHandSubsystem> m_HandSubsystems = new();
        float m_LastSpawnTime;
        bool m_WasThumbsUp;

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

            var spawnPos = palmPose.position + Vector3.up * m_SpawnDistance;
            var cube = Instantiate(m_CubePrefab, spawnPos, Quaternion.identity);
            m_SpawnedCubes.Add(cube);

            while (m_SpawnedCubes.Count > m_MaxCubes)
            {
                var oldest = m_SpawnedCubes[0];
                m_SpawnedCubes.RemoveAt(0);
                if (oldest != null)
                    Destroy(oldest);
            }
        }
    }
}
