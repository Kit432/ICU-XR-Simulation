using UnityEngine;

public class Hotspot : MonoBehaviour
{
    public string hotspotName = "Monitor";

    public GameObject interactionPanel;

    public void Interact()
    {
        Debug.Log("Interacted with: " + hotspotName);

        if (interactionPanel != null)
        {
            interactionPanel.SetActive(true);
        }
    }
}