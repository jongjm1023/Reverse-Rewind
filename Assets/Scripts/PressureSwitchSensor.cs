using UnityEngine;

public class PressureSwitchSensor : MonoBehaviour
{
    [Tooltip("Reference to the parent PressureSwitch script.")]
    public PressureSwitch parentSwitch;

    private void OnTriggerEnter(Collider other)
    {
        if (parentSwitch != null)
        {
            parentSwitch.HandleTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentSwitch != null)
        {
            parentSwitch.HandleTriggerExit(other);
        }
    }
}
