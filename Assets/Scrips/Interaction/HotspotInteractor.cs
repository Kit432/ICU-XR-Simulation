using UnityEngine;
using UnityEngine.InputSystem;

public class HotspotInteractor : MonoBehaviour
{
    public Camera playerCamera;
    public float interactionDistance = 3f;

    private Hotspot currentHotspot;

    void Update()
    {
        currentHotspot = null;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            Hotspot hotspot = hit.collider.GetComponent<Hotspot>();

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
                new Rect(Screen.width / 2 - 100, Screen.height - 100, 200, 40),
                "Press E to interact"
            );
        }
    }
}