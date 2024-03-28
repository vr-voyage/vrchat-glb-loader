
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class UIPanel : UdonSharpBehaviour
{
    public GameObject[] hiddenUntilLoaded;

    public Toggle[] toggles;
    int nToggles;
    bool[] states;



    void SaveTogglesStates()
    {
        for (int t = 0; t < nToggles; t++)
        {
            states[t] = toggles[t].isOn;
        }
    }

    void ResetToggles()
    {
        for (int t = 0; t < nToggles; t++)
        {
            toggles[t].isOn = states[t];
        }
    }

    void ResetUIElements()
    {
        ResetToggles();
    }

    void SetHiddenUntilLoaded(bool hidden)
    {
        int nHiddenUntilLoaded = hiddenUntilLoaded.Length;
        for (int h = 0; h < nHiddenUntilLoaded; h++)
        {
            GameObject toHide = hiddenUntilLoaded[h];
            if (toHide == null) return;
            toHide.SetActive(!hidden);
        }
    }

    public void SceneCleared()
    {
        SetHiddenUntilLoaded(true);
    }

    public void SceneLoaded()
    {
        ResetUIElements();
        SetHiddenUntilLoaded(false);
    }

    private void Start()
    {
        nToggles = toggles.Length;
        states = new bool[toggles.Length];
        SaveTogglesStates();
        enabled = false;
        SceneCleared();
    }
}
