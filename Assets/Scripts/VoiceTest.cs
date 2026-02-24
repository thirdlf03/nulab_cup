using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Meta.WitAi;
using Meta.WitAi.Events;
using Meta.WitAi.Json;
using Oculus.Voice;

public class VoiceTest : MonoBehaviour
{
    [Header("Voice")]
    [SerializeField] private AppVoiceExperience voiceExperience;
    [SerializeField] private InputActionReference toggleAction;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI transcriptionText;

    [Header("Audio")]
    [SerializeField] private AudioClip startClip;
    [SerializeField] private AudioClip stopClip;

    private AudioSource audioSource;
    private bool isRecording;
    private bool isProcessing;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void OnEnable()
    {
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
        // OnPartialTranscription and OnFullTranscription are wired via persistent UnityEvents in the scene

        Debug.Log($"[VoiceTest] OnEnable - Active: {voiceExperience.Active}, MicActive: {voiceExperience.MicActive}");

        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += OnPressed;
            toggleAction.action.canceled += OnReleased;
            Debug.Log($"[VoiceTest] Input action bound: {toggleAction.action.name}");
        }
        else
        {
            Debug.LogWarning("[VoiceTest] toggleAction is null or has no action!");
        }

        UpdateStatus("Ready");
    }

    private void OnDisable()
    {
        if (voiceExperience != null)
        {
            voiceExperience.VoiceEvents.OnStartListening.RemoveListener(OnStartListening);
            voiceExperience.VoiceEvents.OnStoppedListening.RemoveListener(OnStoppedListening);
            voiceExperience.VoiceEvents.OnError.RemoveListener(OnError);
            voiceExperience.VoiceEvents.OnResponse.RemoveListener(OnResponse);
            // OnPartialTranscription and OnFullTranscription are managed via persistent UnityEvents in the scene
        }

        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed -= OnPressed;
            toggleAction.action.canceled -= OnReleased;
            toggleAction.action.Disable();
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
        isRecording = false;
        isProcessing = false;
    }

    private void OnResponse(WitResponseNode response)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[VoiceTest][Event] OnResponse received");
#endif
    }

    // --- Transcription callbacks ---

    public void OnFullTranscription(string text)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[VoiceTest][Event] FullTranscription received");
#endif
        if (transcriptionText != null)
            transcriptionText.text = text;
        UpdateStatus("Ready");
        isRecording = false;
        isProcessing = false;
    }

    public void OnPartialTranscription(string text)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[VoiceTest][Event] PartialTranscription received");
#endif
        if (transcriptionText != null)
            transcriptionText.text = text;
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
}
