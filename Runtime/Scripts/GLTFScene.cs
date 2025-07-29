
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class GLTFScene : UdonSharpBehaviour
{
    public string sceneName;
    public GameObject[] nodes;

    public void Show()
    {
        if (nodes == null) { return; }
        foreach (GameObject go in nodes)
        {
            if (go == null) { continue; }
            go.SetActive(true);
        }
    }

    public void Hide()
    {
        if (nodes == null) { return; }
        foreach (GameObject go in nodes)
        {
            if (go == null) { continue; }
            go.SetActive(false);
        }
    }
}
