using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HotspotInteractor : MonoBehaviour
{
    public Camera playerCamera;
    public float interactionDistance = 3f;
    public LayerMask interactionLayers = ~0;
    public GameObject promptPanel;
    public Text promptText;
    public GameObject crosshair;
    private Hotspot currentHotspot;
    private void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) enabled = false;
    }
    private void Update()
    {
        currentHotspot = null;
        bool modal = UIFlowController.Instance != null && UIFlowController.Instance.HasOpenModal;
        if (crosshair != null) crosshair.SetActive(!modal);
        if (!modal)
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayers, QueryTriggerInteraction.Collide))
            {
                currentHotspot = hit.collider.GetComponentInParent<Hotspot>();
                if (currentHotspot != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    currentHotspot.Interact();
            }
        }
        if (promptPanel != null) promptPanel.SetActive(currentHotspot != null);
        if (currentHotspot != null && promptText != null) promptText.text = "E  |  " + currentHotspot.HotspotName;
    }
}
