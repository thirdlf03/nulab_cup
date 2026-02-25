#if FUSION2
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace NulabCup.Whiteboard
{
    /// <summary>
    /// Handles whiteboard drawing via XR Hands (pinch gesture) and controllers (trigger).
    /// Combines pointer raycasting and drawing state management in one component.
    /// Replaces MRMotifs' PointerDrawingMotif + PointerHandlerMotif with XR Hands / Input System.
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
        [SerializeField] private float m_PinchThreshold = 0.02f;

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

        // Per-hand pinch state tracking (for edge detection)
        private bool m_LeftWasPinching;
        private bool m_RightWasPinching;

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
                ref m_LeftWasPinching
            );

            // Process right hand/controller
            ProcessSide(
                isLeft: false,
                ref m_RightDrawing,
                ref m_RightLastUV,
                ref m_RightWasPinching
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
            ref bool wasPinching)
        {
            var source = GetActiveSource(isLeft);
            var ray = GetPointerRay(isLeft, source);

            if (!ray.HasValue)
            {
                if (isDrawing)
                {
                    isDrawing = false;
                    InteractionStateManager.Instance.ResetMode(InteractionMode.DrawingPointer);
                }
                return;
            }

            // Raycast to whiteboard
            if (!Physics.Raycast(ray.Value, out var hit, Mathf.Infinity, m_WhiteboardLayer))
            {
                if (isDrawing)
                {
                    isDrawing = false;
                    InteractionStateManager.Instance.ResetMode(InteractionMode.DrawingPointer);
                }
                return;
            }

            // Get input state
            GetInputState(isLeft, source, ref wasPinching,
                out var inputDown, out var inputHeld, out var inputUp);

            // Drawing state machine
            HandleDrawing(hit.point, inputDown, inputHeld, inputUp, ref isDrawing, ref lastUV);
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

        private Ray? GetPointerRay(bool isLeft, InputSource source)
        {
            if (source == InputSource.Hand)
            {
                return GetHandPointerRay(isLeft);
            }

            return GetControllerRay(isLeft);
        }

        private Ray? GetHandPointerRay(bool isLeft)
        {
            if (m_HandSubsystem == null)
            {
                return null;
            }

            var hand = isLeft ? m_HandSubsystem.leftHand : m_HandSubsystem.rightHand;
            if (!hand.isTracked)
            {
                return null;
            }

            if (!hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out var proxPose))
            {
                return null;
            }
            if (!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var tipPose))
            {
                return null;
            }

            var origin = tipPose.position;
            var direction = (tipPose.position - proxPose.position).normalized;
            return new Ray(origin, direction);
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

        private void GetInputState(
            bool isLeft,
            InputSource source,
            ref bool wasPinching,
            out bool inputDown,
            out bool inputHeld,
            out bool inputUp)
        {
            if (source == InputSource.Hand)
            {
                var isPinching = IsPinching(isLeft);
                inputDown = isPinching && !wasPinching;
                inputHeld = isPinching;
                inputUp = !isPinching && wasPinching;
                wasPinching = isPinching;
            }
            else
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
        }

        private bool IsPinching(bool isLeft)
        {
            if (m_HandSubsystem == null)
            {
                return false;
            }

            var hand = isLeft ? m_HandSubsystem.leftHand : m_HandSubsystem.rightHand;
            if (!hand.isTracked)
            {
                return false;
            }

            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbPose))
            {
                return false;
            }
            if (!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexPose))
            {
                return false;
            }

            return Vector3.Distance(thumbPose.position, indexPose.position) < m_PinchThreshold;
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
