using UnityEngine;

namespace NulabCup.Whiteboard
{
    public enum InteractionMode
    {
        None,
        DrawingPointer
    }

    public class InteractionStateManager : MonoBehaviour
    {
        public static InteractionStateManager Instance { get; private set; }

        public InteractionMode CurrentMode { get; set; } = InteractionMode.None;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool CanDrawWithPointer() => CurrentMode == InteractionMode.None;

        public void SetMode(InteractionMode mode)
        {
            CurrentMode = mode;
        }

        public void ResetMode(InteractionMode mode)
        {
            if (CurrentMode == mode)
            {
                CurrentMode = InteractionMode.None;
            }
        }
    }
}
