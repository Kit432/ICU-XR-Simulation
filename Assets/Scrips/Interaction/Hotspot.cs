using System;
using UnityEngine;

public class Hotspot : MonoBehaviour
{
    [SerializeField] private string hotspotId;

    public string hotspotName = "Monitor";

    public GameObject interactionPanel;

    [SerializeField, TextArea] private string interactionMessage;

    public static event Action<Hotspot> HotspotInteracted;

    public string HotspotId => hotspotId;
    public string HotspotName => hotspotName;

    public void Interact()
    {
        Debug.Log("Interacted with: " + hotspotName);

        if (interactionPanel != null)
        {
            if (UIFlowController.Instance != null)
            {
                UIFlowController.Instance.OpenPanel(interactionPanel);
            }
            else
            {
                Debug.LogWarning("No UIFlowController was found. Opening the panel without modal input handling.");
                interactionPanel.SetActive(true);
            }
        }
        else if (!string.IsNullOrWhiteSpace(interactionMessage))
        {
            if (UIFlowController.Instance != null)
            {
                UIFlowController.Instance.ShowMessage(interactionMessage);
            }
            else
            {
                Debug.Log(interactionMessage);
            }
        }

        HotspotInteracted?.Invoke(this);
    }
}
