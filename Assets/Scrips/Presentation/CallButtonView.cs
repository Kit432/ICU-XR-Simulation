using ICUSimulation.Scenarios;
using UnityEngine;

public sealed class CallButtonView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private ScenarioController scenarioController;
    private Renderer buttonCapRenderer;
    private Material buttonCapMaterial;
    private Material buttonRimMaterial;
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

    private void EnsureButtonVisual(Transform callButtonBase)
    {
        if (callButtonBase == null)
        {
            return;
        }

        Transform existing = callButtonBase.Find("CallButtonVisual_Runtime");
        if (existing != null)
        {
            Transform existingCap = existing.Find("ButtonCap");
            buttonCapRenderer = existingCap != null ? existingCap.GetComponent<Renderer>() : null;
            return;
        }

        GameObject visualRoot = new GameObject("CallButtonVisual_Runtime");
        visualRoot.transform.SetParent(callButtonBase, false);

        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "ButtonRim";
        rim.transform.SetParent(visualRoot.transform, false);
        rim.transform.localPosition = new Vector3(0f, 0f, -0.62f);
        rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rim.transform.localScale = new Vector3(0.72f, 0.16f, 0.72f);
        buttonRimMaterial = CreateRuntimeMaterial(
            "CallButtonRim_Runtime",
            new Color(0.08f, 0.10f, 0.12f, 1f),
            Color.black);
        rim.GetComponent<Renderer>().sharedMaterial = buttonRimMaterial;
        Destroy(rim.GetComponent<Collider>());

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "ButtonCap";
        cap.transform.SetParent(visualRoot.transform, false);
        cap.transform.localPosition = new Vector3(0f, 0f, -0.82f);
        cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cap.transform.localScale = new Vector3(0.54f, 0.20f, 0.54f);
        buttonCapMaterial = CreateRuntimeMaterial(
            "CallButtonCap_Runtime",
            new Color(0.82f, 0.045f, 0.035f, 1f),
            new Color(0.18f, 0.005f, 0.003f, 1f));
        buttonCapRenderer = cap.GetComponent<Renderer>();
        buttonCapRenderer.sharedMaterial = buttonCapMaterial;
    }

    private static Material CreateRuntimeMaterial(string materialName, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                        Shader.Find("Standard") ??
                        Shader.Find("Sprites/Default");
        Material material = new Material(shader)
        {
            name = materialName,
            color = baseColor
        };

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, baseColor);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, baseColor);
        }

        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, emissionColor);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.45f);
        }

        return material;
    }

    private void OnGUI()
    {
        if (!escalationComplete)
        {
            return;
        }

        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.12f, 0.72f, 0.32f, 0.95f);
        GUI.Box(new Rect(Screen.width - 290f, 54f, 274f, 48f), "CALL STATUS: Physician notified");
        GUI.backgroundColor = previous;
    }

    private void OnDestroy()
    {
        if (buttonCapMaterial != null)
        {
            Destroy(buttonCapMaterial);
        }

        if (buttonRimMaterial != null)
        {
            Destroy(buttonRimMaterial);
        }
    }
}
