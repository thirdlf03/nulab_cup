using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace NulabCup
{
    /// <summary>
    /// Meta XR Interaction SDK 向けの投擲可能キューブ。
    /// 必要な Interactable コンポーネントを自動で構成する。
    /// </summary>
    [DisallowMultipleComponent]
    public class ThrowableCube : MonoBehaviour
    {
        [Header("Meta XR Interaction")]
        [SerializeField] bool m_EnableControllerGrab = true;
        [SerializeField] bool m_EnableHandGrab = true;
        [SerializeField] int m_MaxGrabPoints = 1;
        [SerializeField] bool m_ForceKinematicDisabledOnThrow = true;

        void Reset()
        {
            ConfigureMetaComponents();
        }

        void Awake()
        {
            ConfigureMetaComponents();
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
                ConfigureMetaComponents();
        }

        void ConfigureMetaComponents()
        {
            var rb = GetOrAdd<Rigidbody>();
            var grabbable = GetOrAdd<Grabbable>();

            grabbable.MaxGrabPoints = Mathf.Max(-1, m_MaxGrabPoints);
            grabbable.ForceKinematicDisabled = m_ForceKinematicDisabledOnThrow;

            if (m_EnableControllerGrab)
            {
                var grabInteractable = GetOrAdd<GrabInteractable>();
                grabInteractable.InjectRigidbody(rb);
                grabInteractable.InjectOptionalPointableElement(grabbable);
            }

            if (m_EnableHandGrab)
            {
                var handGrabInteractable = GetOrAdd<HandGrabInteractable>();
                handGrabInteractable.InjectRigidbody(rb);
                handGrabInteractable.InjectOptionalPointableElement(grabbable);
            }
        }

        T GetOrAdd<T>() where T : Component
        {
            if (TryGetComponent<T>(out var component))
                return component;

            return gameObject.AddComponent<T>();
        }
    }
}
