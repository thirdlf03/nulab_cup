using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace NulabCup
{
    /// <summary>
    /// ハンドトラッキングで掴んで投げられるキューブ。
    /// XRGrabInteractable のビルトイン throwOnDetach を活用する。
    /// </summary>
    public class ThrowableCube : XRGrabInteractable
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            throwOnDetach = true;
            throwSmoothingDuration = 0.25f;
            throwVelocityScale = 1.5f;
            throwAngularVelocityScale = 1.0f;
            movementType = MovementType.VelocityTracking;
        }
    }
}
