using UnityEngine;

public class JumpPadSensor : MonoBehaviour
{
    [Tooltip("Reference to the parent PressureSwitch script.")]
    public JumpPad parentSwitch;

    private void OnTriggerEnter(Collider other)
    {
        if (parentSwitch != null)
        {
            parentSwitch.HandleTriggerEnter(other);
        }
    }

}
