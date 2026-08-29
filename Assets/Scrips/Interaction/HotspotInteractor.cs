using UnityEngine;
using UnityEngine.InputSystem;

public class HotspotInteractor : MonoBehaviour
{
    public Camera playerCamera;
    public float interactionDistance = 3f;
    public LayerMask interactionLayers = ~0;

    private Hotspot currentHotspot;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera == null)
        {
            Debug.LogError("HotspotInteractor requires a player camera. Interaction has been disabled.", this);
            enabled = false;
        }
    }

    void Update()
    {
        currentHotspot = null;

        if (UIFlowController.Instance != null && UIFlowController.Instance.HasOpenModal)
        {
            return;
        }

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide))
        {
            Hotspot hotspot = hit.collider.GetComponentInParent<Hotspot>();

            if (hotspot != null)
            {
                currentHotspot = hotspot;

                if (Keyboard.current != null &&
                    Keyboard.current.eKey.wasPressedThisFrame)
                {
                    hotspot.Interact();
                }
            }
        }
    }

    void OnGUI()
    {
        if (currentHotspot != null)
        {
            GUI.Box(
                new Rect(Screen.width / 2 - 180, Screen.height - 100, 360, 40),
                "Press E to interact with " + currentHotspot.HotspotName
            );
        }
    }
}
