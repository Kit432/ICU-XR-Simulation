using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIFlowController : MonoBehaviour
{
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private StarterAssetsInputs playerInputs;

    private GameObject activePanel;
    private string activeMessage;
    private bool playerControllerWasEnabled;
    private bool modalStateActive;

    public static UIFlowController Instance { get; private set; }

    public bool HasOpenModal => activePanel != null || !string.IsNullOrEmpty(activeMessage);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one UIFlowController may be active in a scene.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<FirstPersonController>();
        }

        if (playerInputs == null && playerController != null)
        {
            playerInputs = playerController.GetComponent<StarterAssetsInputs>();
        }

        if (playerController == null)
        {
            Debug.LogWarning("UIFlowController could not find the first-person controller. Modal panels will still manage the cursor.", this);
        }
    }

    private void Update()
    {
        if (HasOpenModal &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseActivePanel();
        }
    }

    public void OpenPanel(GameObject panel)
    {
        if (panel == null)
        {
            Debug.LogWarning("UIFlowController cannot open a null panel.", this);
            return;
        }

        CloseCurrentVisual();
        activePanel = panel;
        activePanel.SetActive(true);
        EnterModalState();
    }

    public void ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        CloseCurrentVisual();
        activeMessage = message;
        EnterModalState();
    }

    public void CloseActivePanel()
    {
        CloseCurrentVisual();
        ExitModalState();
    }

    private void CloseCurrentVisual()
    {
        if (activePanel != null)
        {
            activePanel.SetActive(false);
            activePanel = null;
        }

        activeMessage = null;
    }

    private void EnterModalState()
    {
        if (modalStateActive)
        {
            return;
        }

        modalStateActive = true;

        ClearPlayerInput();

        if (playerController != null)
        {
            playerControllerWasEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ExitModalState()
    {
        if (!modalStateActive)
        {
            return;
        }

        ClearPlayerInput();

        if (playerController != null)
        {
            playerController.enabled = playerControllerWasEnabled;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        modalStateActive = false;
    }

    private void ClearPlayerInput()
    {
        if (playerInputs == null)
        {
            return;
        }

        playerInputs.MoveInput(Vector2.zero);
        playerInputs.LookInput(Vector2.zero);
        playerInputs.JumpInput(false);
        playerInputs.SprintInput(false);
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(activeMessage))
        {
            return;
        }

        const float width = 460f;
        const float height = 140f;
        Rect messageRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUI.Box(messageRect, activeMessage + "\n\nPress Esc to close");
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        CloseCurrentVisual();
        ExitModalState();
        Instance = null;
    }
}
