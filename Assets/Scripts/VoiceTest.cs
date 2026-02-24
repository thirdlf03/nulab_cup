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
        voiceExperience.VoiceEvents.OnPartialTranscription.AddListener(OnPartialTranscription);
        voiceExperience.VoiceEvents.OnFullTranscription.AddListener(OnFullTranscription);

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
            voiceExperience.VoiceEvents.OnPartialTranscription.RemoveListener(OnPartialTranscription);
            voiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(OnFullTranscription);
        }

        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed -= OnPressed;
            toggleAction.action.canceled -= OnReleased;
            toggleAction.action.Disable();
        }
    }

    private void OnPressed(InputAction.CallbackContext ctx)
    {
        if (isRecording)
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
    }

    private void OnResponse(WitResponseNode response)
    {
        Debug.Log($"[VoiceTest][Event] OnResponse - {response}");
    }

    // --- Transcription callbacks ---

    public void OnFullTranscription(string text)
    {
        Debug.Log($"[VoiceTest][Event] FullTranscription: {text}");
        if (transcriptionText != null)
            transcriptionText.text = text;
        UpdateStatus("Ready");
        isRecording = false;
    }

    public void OnPartialTranscription(string text)
    {
        Debug.Log($"[VoiceTest][Event] PartialTranscription: {text}");
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
