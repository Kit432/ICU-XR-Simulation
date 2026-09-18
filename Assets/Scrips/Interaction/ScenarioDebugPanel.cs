using UnityEngine;

/// <summary>Legacy scene compatibility. Release scenes use the authored TrainingInterface prefab.</summary>
public sealed class ScenarioDebugPanel : MonoBehaviour
{
    private void Awake() { enabled = false; }
}
