
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class HierarchyManager : UdonSharpBehaviour
{
    public BoxCollider ownCollider;

    public void SceneLoaded()
    {
        SetOwnColliderState(true);
        SetChildrenCollidersState(false);
    }

    public void SetChildrenCollidersState(bool state)
    {
        BoxCollider[] colliders = GetComponentsInChildren<BoxCollider>();
        int nColliders = colliders.Length;
        for (int c = 0; c < nColliders; c++)
        {
            BoxCollider collider = colliders[c];
            if (collider != ownCollider)
            {
                collider.enabled = state;
            }   
        }
    }

    public void ChildrenColliderStateOn() => SetChildrenCollidersState(true);
    public void ChildrenColliderStateOff() => SetChildrenCollidersState(false);

    public void SetOwnColliderState(bool state)
    {
        ownCollider.enabled = state;
    }

    public void OwnColliderStateOn() => SetOwnColliderState(true);
    public void OwnColliderStateOff() => SetOwnColliderState(false);
}
