using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIFlowController : MonoBehaviour
{
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private StarterAssetsInputs playerInputs;
    public GameObject messagePanel;
    public Text messageText;
    private GameObject activePanel;
    private bool playerControllerWasEnabled;
    private bool cursorWasLocked;
    private bool lookInputWasEnabled;
    private bool modalStateActive;
    public static UIFlowController Instance { get; private set; }
    public bool HasOpenModal => activePanel != null && activePanel.activeSelf;
    public GameObject ActivePanel => activePanel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        if (playerController == null) playerController = FindAnyObjectByType<FirstPersonController>();
        if (playerInputs == null && playerController != null) playerInputs = playerController.GetComponent<StarterAssetsInputs>();
    }
    private void Update()
    {
        if (HasOpenModal && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseActivePanel();
    }
    public void OpenPanel(GameObject panel)
    {
        if (panel == null) return;
        CloseCurrentVisual(); activePanel = panel; activePanel.SetActive(true);
        activePanel.transform.SetAsLastSibling();
        EnterModalState();
    }
    public void ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || messagePanel == null) return;
        messageText.text = message;
        OpenPanel(messagePanel);
    }
    public void CloseActivePanel() { CloseCurrentVisual(); ExitModalState(); }
    private void CloseCurrentVisual()
    {
        GameObject closing = activePanel;
        activePanel = null;
        if (closing != null) closing.SetActive(false);
    }
    private void EnterModalState()
    {
        if (modalStateActive) return;
        modalStateActive = true;
        ClearPlayerInput();
        if (playerInputs != null)
        {
            cursorWasLocked = playerInputs.cursorLocked;
            lookInputWasEnabled = playerInputs.cursorInputForLook;
            playerInputs.cursorLocked = false;
            playerInputs.cursorInputForLook = false;
        }
        if (playerController != null) { playerControllerWasEnabled = playerController.enabled; playerController.enabled = false; }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    private void ExitModalState()
    {
        if (!modalStateActive) return;
        ClearPlayerInput();
        if (playerInputs != null)
        {
            playerInputs.cursorLocked = cursorWasLocked;
            playerInputs.cursorInputForLook = lookInputWasEnabled;
        }
        if (playerController != null) playerController.enabled = playerControllerWasEnabled;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        modalStateActive = false;
    }
    private void ClearPlayerInput()
    {
        if (playerInputs == null) return;
        playerInputs.MoveInput(Vector2.zero); playerInputs.LookInput(Vector2.zero);
        playerInputs.JumpInput(false); playerInputs.SprintInput(false);
    }
    private void OnDestroy()
    {
        if (Instance != this) return;
        CloseCurrentVisual(); ExitModalState(); Instance = null;
    }
}
