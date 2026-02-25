using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Meta.WitAi;
using Meta.WitAi.Events;
using Meta.WitAi.Json;
using Oculus.Voice;
using NulabCup.Debugging;

public class VoiceTest : MonoBehaviour
{
    private enum VoiceInputMode
    {
        LeftControllerYButton = 0,
        ActionReference = 1
    }

    [Header("Voice")]
    [SerializeField] private AppVoiceExperience voiceExperience;
    [SerializeField] private VoiceInputMode inputMode = VoiceInputMode.LeftControllerYButton;
    [SerializeField] private InputActionReference toggleAction;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI transcriptionText;

    [Header("Audio")]
    [SerializeField] private AudioClip startClip;
    [SerializeField] private AudioClip stopClip;

    private const float ProcessingTimeout = 10f;

    private AudioSource audioSource;
    private bool isRecording;
    private bool isProcessing;
    private Coroutine processingTimeoutCoroutine;
    private InputAction activeInputAction;
    private InputAction runtimeYButtonAction;

    private void Awake()
    {
        StartupProfiler.LogMilestone("VoiceTest", "Awake() BEGIN");
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        StartupProfiler.LogMilestone("VoiceTest", "Awake() END");
    }

    private void OnEnable()
    {
        StartupProfiler.LogMilestone("VoiceTest", "OnEnable() BEGIN");

        if (voiceExperience == null)
        {
            Debug.LogError("[VoiceTest] voiceExperience is NULL! Assign it in the Inspector.");
            return;
        }

        // Voice SDK event listeners for debugging
        voiceExperience.VoiceEvents.OnStartListening.AddListener(OnStartListening);
        voiceExperience.VoiceEvents.OnStoppedListening.AddListener(OnStoppedListening);
        voiceExperience.VoiceEvents.OnError.AddListener(OnError);
        voiceExperience.VoiceEvents.OnResponse.AddListener(OnResponse);
        voiceExperience.VoiceEvents.OnPartialTranscription.AddListener(OnPartialTranscription);
        voiceExperience.VoiceEvents.OnFullTranscription.AddListener(OnFullTranscription);

        Debug.Log($"[VoiceTest] OnEnable - Active: {voiceExperience.Active}, MicActive: {voiceExperience.MicActive}");

        activeInputAction = ResolveInputAction();
        if (activeInputAction != null)
        {
            activeInputAction.Enable();
            activeInputAction.performed += OnPressed;
            activeInputAction.canceled += OnReleased;
            Debug.Log($"[VoiceTest] Input action bound: {activeInputAction.name} (mode: {inputMode})");
        }
        else
        {
            Debug.LogWarning("[VoiceTest] No valid input action found for recording.");
        }

        UpdateStatus("Ready");
        StartupProfiler.LogMilestone("VoiceTest", "OnEnable() END");
    }

    private void OnDisable()
    {
        if (voiceExperience != null)
        {
            voiceExperience.VoiceEvents.OnStartListening.RemoveListener(OnStartListening);
            voiceExperience.VoiceEvents.OnStoppedListening.RemoveListener(OnStoppedListening);
            voiceExperience.VoiceEvents.OnError.RemoveListener(OnError);
            voiceExperience.VoiceEvents.OnResponse.RemoveListener(OnResponse);
            voiceExperience.VoiceEvents.OnPartialTranscription.RemoveListener(OnPartialTranscription);
            voiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(OnFullTranscription);
        }

        if (activeInputAction != null)
        {
            activeInputAction.performed -= OnPressed;
            activeInputAction.canceled -= OnReleased;

            if (activeInputAction == runtimeYButtonAction)
            {
                activeInputAction.Disable();
            }

            activeInputAction = null;
        }

        isRecording = false;
        isProcessing = false;
    }

    private void OnPressed(InputAction.CallbackContext ctx)
    {
        if (isRecording || isProcessing)
            return;

        Debug.Log($"[VoiceTest] Button pressed. Active: {voiceExperience.Active}, MicActive: {voiceExperience.MicActive}");

        voiceExperience.ActivateImmediately();

        isRecording = true;
        UpdateStatus("<color=red>● Recording...</color>");
        PlayClip(startClip);
        Debug.Log($"[VoiceTest] ActivateImmediately() called. Active: {voiceExperience.Active}, MicActive: {voiceExperience.MicActive}");
    }

    private void OnReleased(InputAction.CallbackContext ctx)
    {
        if (!isRecording)
            return;

        Debug.Log($"[VoiceTest] Button released. Active: {voiceExperience.Active}, MicActive: {voiceExperience.MicActive}");
        voiceExperience.Deactivate();
        isRecording = false;
        isProcessing = true;
        processingTimeoutCoroutine = StartCoroutine(ProcessingTimeoutRoutine());
        UpdateStatus("Processing...");
        PlayClip(stopClip);
        Debug.Log("[VoiceTest] Deactivate() called, waiting for transcription...");
    }

    // --- Voice SDK event callbacks ---

    private void OnStartListening()
    {
        Debug.Log("[VoiceTest][Event] OnStartListening - Mic is now active");
    }

    private void OnStoppedListening()
    {
        Debug.Log("[VoiceTest][Event] OnStoppedListening - Mic stopped");
    }

    private void OnError(string error, string message)
    {
        Debug.LogError($"[VoiceTest][Event] OnError - error: {error}, message: {message}");
        UpdateStatus($"<color=red>Error: {error}</color>");
        ResetProcessingState();
    }

    private void OnResponse(WitResponseNode response)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[VoiceTest][Event] OnResponse received");
#endif
        ResetProcessingState();
    }

    // --- Transcription callbacks ---

    public void OnFullTranscription(string text)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[VoiceTest][Event] FullTranscription received");
#endif
        if (transcriptionText != null)
            transcriptionText.text = text;
        ResetProcessingState();
    }

    public void OnPartialTranscription(string text)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[VoiceTest][Event] PartialTranscription received");
#endif
        if (transcriptionText != null)
            transcriptionText.text = text;
    }

    private void ResetProcessingState()
    {
        isRecording = false;
        isProcessing = false;
        if (processingTimeoutCoroutine != null)
        {
            StopCoroutine(processingTimeoutCoroutine);
            processingTimeoutCoroutine = null;
        }
        UpdateStatus("Ready");
    }

    private IEnumerator ProcessingTimeoutRoutine()
    {
        yield return new WaitForSeconds(ProcessingTimeout);
        if (isProcessing)
        {
            Debug.LogWarning("[VoiceTest] Processing timed out, resetting state");
            ResetProcessingState();
        }
    }

    private void UpdateStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    private InputAction ResolveInputAction()
    {
        if (inputMode == VoiceInputMode.ActionReference)
        {
            if (toggleAction != null && toggleAction.action != null)
            {
                return toggleAction.action;
            }

            Debug.LogWarning("[VoiceTest] inputMode is ActionReference but toggleAction is not set. Falling back to Left Y button.");
        }

        if (runtimeYButtonAction == null)
        {
            // Quest left Y is typically exposed as LeftHand secondaryButton.
            runtimeYButtonAction = new InputAction("VoiceRecordLeftY", InputActionType.Button);
            runtimeYButtonAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
        }

        return runtimeYButtonAction;
    }

    private void OnDestroy()
    {
        if (runtimeYButtonAction != null)
        {
            runtimeYButtonAction.Dispose();
            runtimeYButtonAction = null;
        }
    }
}
