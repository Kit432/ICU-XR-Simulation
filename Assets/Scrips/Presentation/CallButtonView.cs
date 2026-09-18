using ICUSimulation.Scenarios;
using UnityEngine;

public sealed class CallButtonView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private ScenarioController scenarioController;
    private Renderer buttonCapRenderer;
    private MaterialPropertyBlock propertyBlock;
    private bool escalationComplete;
    private bool subscribed;

    public void Initialize(ScenarioController controller, Transform callButtonBase)
    {
        Detach();
        scenarioController = controller;
        EnsureButtonVisual(callButtonBase);
        propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
        Attach();
        Refresh(scenarioController != null ? scenarioController.CurrentState : null);
    }

    private void OnEnable()
    {
        Attach();
        if (scenarioController != null)
        {
            Refresh(scenarioController.CurrentState);
        }
    }

    private void OnDisable()
    {
        Detach();
        ClearRendererState();
    }

    private void Attach()
    {
        if (subscribed || scenarioController == null)
        {
            return;
        }

        scenarioController.StateChanged += HandleStateChanged;
        scenarioController.FlagChanged += HandleFlagChanged;
        subscribed = true;
    }

    private void Detach()
    {
        if (!subscribed || scenarioController == null)
        {
            return;
        }

        scenarioController.StateChanged -= HandleStateChanged;
        scenarioController.FlagChanged -= HandleFlagChanged;
        subscribed = false;
    }

    private void HandleStateChanged(ScenarioState state)
    {
        Refresh(state);
    }

    private void HandleFlagChanged(ScenarioFlagChangedEvent change)
    {
        Refresh(scenarioController.CurrentState);
    }

    private void Refresh(ScenarioState state)
    {
        escalationComplete = ScenarioPresentationSnapshot.FromState(state).EscalationComplete;
        if (buttonCapRenderer == null)
        {
            return;
        }

        if (!escalationComplete)
        {
            ClearRendererState();
            return;
        }

        propertyBlock.Clear();
        Color completedColor = new Color(0.08f, 0.9f, 0.25f, 1f);
        propertyBlock.SetColor(BaseColorId, completedColor);
        propertyBlock.SetColor(ColorId, completedColor);
        propertyBlock.SetColor(EmissionColorId, completedColor * 1.5f);
        buttonCapRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ClearRendererState()
    {
        if (buttonCapRenderer != null)
        {
            buttonCapRenderer.SetPropertyBlock(null);
        }
    }

    // The mesh and materials are stored in the editable CallButton prefab.
    private void EnsureButtonVisual(Transform callButtonBase)
    {
        buttonCapRenderer = callButtonBase?.Find("CallButtonVisual_Runtime/ButtonCap")?.GetComponent<Renderer>();
    }
}
