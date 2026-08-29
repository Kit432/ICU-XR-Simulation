using System;
using ICUSimulation.Scenarios;
using UnityEngine;

public sealed class AlarmAudioController : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float volume = 0.16f;

    private readonly MonitorAlarmPresentationModel alarmModel = new MonitorAlarmPresentationModel();
    private ScenarioController scenarioController;
    private AudioSource audioSource;
    private AudioClip generatedAlarmClip;
    private bool subscribed;

    public void Initialize(ScenarioController controller)
    {
        Detach();
        scenarioController = controller;
        EnsureAudioSource();
        Attach();
        StopAlarm();
    }

    private void OnEnable()
    {
        Attach();
    }

    private void OnDisable()
    {
        Detach();
        StopAlarm();
    }

    private void Attach()
    {
        if (subscribed || scenarioController == null)
        {
            return;
        }

        scenarioController.UiEffectRequested += HandleUiEffect;
        scenarioController.StateChanged += HandleStateChanged;
        scenarioController.ScenarioCompleted += HandleScenarioCompleted;
        subscribed = true;
    }

    private void Detach()
    {
        if (!subscribed || scenarioController == null)
        {
            return;
        }

        scenarioController.UiEffectRequested -= HandleUiEffect;
        scenarioController.StateChanged -= HandleStateChanged;
        scenarioController.ScenarioCompleted -= HandleScenarioCompleted;
        subscribed = false;
    }

    private void HandleUiEffect(ScenarioUiEffectRequest request)
    {
        if (!IsMonitorAlarmEffect(request))
        {
            return;
        }

        alarmModel.ApplyUiEffect(request);
        UpdatePlayback();
    }

    private void HandleStateChanged(ScenarioState state)
    {
        if (scenarioController.CurrentRunner == null)
        {
            alarmModel.ResetFromState(state);
            StopAlarm();
        }
    }

    private void HandleScenarioCompleted(ScenarioState state)
    {
        StopAlarm();
    }

    private void UpdatePlayback()
    {
        bool shouldPlay = alarmModel.IsAlarmActive &&
                          scenarioController != null &&
                          scenarioController.CurrentRunner != null &&
                          scenarioController.CurrentRunner.IsRunning;

        if (!shouldPlay)
        {
            StopAlarm();
            return;
        }

        EnsureAudioSource();
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
        }

        if (generatedAlarmClip == null)
        {
            generatedAlarmClip = CreateProceduralAlarmClip();
        }

        audioSource.clip = generatedAlarmClip;
        audioSource.volume = volume;
    }

    private void StopAlarm()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private static AudioClip CreateProceduralAlarmClip()
    {
        const int sampleRate = 44100;
        const float durationSeconds = 0.9f;
        const float toneSeconds = 0.18f;
        const float frequency = 880f;
        int sampleCount = Mathf.RoundToInt(sampleRate * durationSeconds);
        int toneSamples = Mathf.RoundToInt(sampleRate * toneSeconds);
        float[] samples = new float[sampleCount];

        for (int index = 0; index < toneSamples; index++)
        {
            float normalized = index / (float)toneSamples;
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * index / sampleRate) * envelope * 0.45f;
        }

        AudioClip clip = AudioClip.Create("ICU_Procedural_Alarm", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static bool IsMonitorAlarmEffect(ScenarioUiEffectRequest request)
    {
        return request != null &&
               string.Equals(request.EffectType, "ui_visual", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.Target, "hs_monitor", StringComparison.Ordinal) &&
               string.Equals(request.State, "blinking_red", StringComparison.OrdinalIgnoreCase);
    }

    private void OnDestroy()
    {
        if (generatedAlarmClip != null)
        {
            Destroy(generatedAlarmClip);
        }
    }
}
