
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[RequireComponent(typeof(UnityEngine.UI.Toggle))]
public class RelayToggleChangeTo : UdonSharpBehaviour
{
    public UdonBehaviour behaviour;
    public string onMethodName;
    public string offMethodName;

    public UnityEngine.UI.Toggle toggle;
    private void Reset()
    {
        toggle = GetComponent<UnityEngine.UI.Toggle>();
    }

    public override void Interact()
    {
        behaviour.SendCustomEvent(toggle.isOn ? onMethodName : offMethodName);
    }
}
