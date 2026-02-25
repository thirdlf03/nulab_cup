#if FUSION2
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace NulabCup.Whiteboard
{
    /// <summary>
    /// Handles whiteboard drawing via XR Hands (index fingertip touch) and controllers (trigger).
    /// </summary>
    public class WhiteboardDrawing : MonoBehaviour
    {
        private enum InputSource
        {
            Hand,
            Controller
        }

        [Header("Drawing Settings")]
        [SerializeField] private Color m_PenColor = Color.blue;
        [SerializeField] private int m_BrushRadius = 6;

        [Header("Hand Tracking")]
        [SerializeField] private float m_FingertipTouchRadius = 0.015f;
        [SerializeField] private float m_FingertipTouchDepth = 0.03f;

        [Header("Controller Input")]
        [SerializeField] private InputActionReference m_LeftTriggerAction;
        [SerializeField] private InputActionReference m_RightTriggerAction;
        [SerializeField] private Transform m_LeftControllerRayOrigin;
        [SerializeField] private Transform m_RightControllerRayOrigin;

        [Header("Raycast")]
        [SerializeField] private LayerMask m_WhiteboardLayer;

        // Hand subsystem
        private XRHandSubsystem m_HandSubsystem;
        private readonly List<XRHandSubsystem> m_HandSubsystems = new();

        // Whiteboard reference
        private WhiteboardManager m_WhiteboardManager;
        private bool m_IsReady;

        // Per-hand drawing state
        private bool m_LeftDrawing;
        private bool m_RightDrawing;
        private Vector2 m_LeftLastUV;
        private Vector2 m_RightLastUV;

        // Per-hand touch state tracking (for edge detection)
        private bool m_LeftWasTouching;
        private bool m_RightWasTouching;

        private void Start()
        {
            StartCoroutine(WaitForManager());
        }

        private IEnumerator WaitForManager()
        {
            yield return new WaitUntil(() => WhiteboardManager.Instance != null);
            m_WhiteboardManager = WhiteboardManager.Instance;
            m_IsReady = true;
        }

        private void Update()
        {
            if (!m_IsReady || m_WhiteboardManager == null || InteractionStateManager.Instance == null)
            {
                return;
            }

            TryGetHandSubsystem();

            // Process left hand/controller
            ProcessSide(
                isLeft: true,
                ref m_LeftDrawing,
                ref m_LeftLastUV,
                ref m_LeftWasTouching
            );

            // Process right hand/controller
            ProcessSide(
                isLeft: false,
                ref m_RightDrawing,
                ref m_RightLastUV,
                ref m_RightWasTouching
            );
        }

        private void TryGetHandSubsystem()
        {
            if (m_HandSubsystem != null && m_HandSubsystem.running)
            {
                return;
            }

            m_HandSubsystems.Clear();
            SubsystemManager.GetSubsystems(m_HandSubsystems);
            if (m_HandSubsystems.Count > 0)
            {
                m_HandSubsystem = m_HandSubsystems[0];
            }
        }

        private void ProcessSide(
            bool isLeft,
            ref bool isDrawing,
            ref Vector2 lastUV,
            ref bool wasTouching)
        {
            var source = GetActiveSource(isLeft);

            if (source == InputSource.Hand)
            {
                if (!TryGetFingerTouchPoint(isLeft, out var hitPoint))
                {
                    wasTouching = false;
                    StopDrawingIfNeeded(ref isDrawing);
                    return;
                }

                var inputDown = !wasTouching;
                const bool inputHeld = true;
                const bool inputUp = false;
                wasTouching = true;
                HandleDrawing(hitPoint, inputDown, inputHeld, inputUp, ref isDrawing, ref lastUV);
                return;
            }

            wasTouching = false;
            var ray = GetControllerRay(isLeft);
            if (!ray.HasValue || !Physics.Raycast(ray.Value, out var hit, Mathf.Infinity, m_WhiteboardLayer))
            {
                StopDrawingIfNeeded(ref isDrawing);
                return;
            }

            GetControllerInputState(isLeft, out var controllerDown, out var controllerHeld, out var controllerUp);
            HandleDrawing(hit.point, controllerDown, controllerHeld, controllerUp, ref isDrawing, ref lastUV);
        }

        private InputSource GetActiveSource(bool isLeft)
        {
            if (m_HandSubsystem == null || !m_HandSubsystem.running)
            {
                return InputSource.Controller;
            }

            var hand = isLeft ? m_HandSubsystem.leftHand : m_HandSubsystem.rightHand;
            return hand.isTracked ? InputSource.Hand : InputSource.Controller;
        }

        private bool TryGetFingerTouchPoint(bool isLeft, out Vector3 hitPoint)
        {
            hitPoint = default;

            if (!TryGetIndexPoses(isLeft, out var tipPose, out var proximalPose))
            {
                return false;
            }

            var direction = (tipPose.position - proximalPose.position).normalized;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            var backOffset = direction * (m_FingertipTouchDepth * 0.5f);
            var rayOrigin = tipPose.position - backOffset;

            if (Physics.SphereCast(
                    rayOrigin,
                    m_FingertipTouchRadius,
                    direction,
                    out var hit,
                    m_FingertipTouchDepth,
                    m_WhiteboardLayer,
                    QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                return true;
            }

            return false;
        }

        private bool TryGetIndexPoses(bool isLeft, out Pose tipPose, out Pose proximalPose)
        {
            tipPose = default;
            proximalPose = default;

            if (m_HandSubsystem == null)
            {
                return false;
            }

            var hand = isLeft ? m_HandSubsystem.leftHand : m_HandSubsystem.rightHand;
            if (!hand.isTracked)
            {
                return false;
            }

            if (!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out tipPose))
            {
                return false;
            }

            if (!hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out proximalPose))
            {
                return false;
            }

            return true;
        }

        private Ray? GetControllerRay(bool isLeft)
        {
            var rayOrigin = isLeft ? m_LeftControllerRayOrigin : m_RightControllerRayOrigin;
            if (rayOrigin == null)
            {
                return null;
            }

            return new Ray(rayOrigin.position, rayOrigin.forward);
        }

        private void GetControllerInputState(bool isLeft, out bool inputDown, out bool inputHeld, out bool inputUp)
        {
            var action = isLeft ? m_LeftTriggerAction : m_RightTriggerAction;
            if (action == null || action.action == null)
            {
                inputDown = false;
                inputHeld = false;
                inputUp = false;
                return;
            }

            inputDown = action.action.WasPressedThisFrame();
            inputHeld = action.action.IsPressed();
            inputUp = action.action.WasReleasedThisFrame();
        }

        private void StopDrawingIfNeeded(ref bool isDrawing)
        {
            if (!isDrawing)
            {
                return;
            }

            isDrawing = false;
            InteractionStateManager.Instance.ResetMode(InteractionMode.DrawingPointer);
        }

        private void HandleDrawing(
            Vector3 hitPoint,
            bool inputDown,
            bool inputHeld,
            bool inputUp,
            ref bool isDrawing,
            ref Vector2 lastUV)
        {
            if (inputDown && InteractionStateManager.Instance.CanDrawWithPointer())
            {
                InteractionStateManager.Instance.SetMode(InteractionMode.DrawingPointer);
                isDrawing = true;
                lastUV = m_WhiteboardManager.WorldToUV(hitPoint);
                m_WhiteboardManager.RPC_DrawLine(lastUV, lastUV, m_PenColor, m_BrushRadius);
            }
            else if (inputHeld && isDrawing)
            {
                var currentUV = m_WhiteboardManager.WorldToUV(hitPoint);
                if (Vector2.Distance(lastUV, currentUV) > 0.001f)
                {
                    m_WhiteboardManager.RPC_DrawLine(lastUV, currentUV, m_PenColor, m_BrushRadius);
                    lastUV = currentUV;
                }
            }
            else if (inputUp && isDrawing)
            {
                isDrawing = false;
                InteractionStateManager.Instance.ResetMode(InteractionMode.DrawingPointer);
            }
        }
    }
}
#endif
