
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VoyageVoyage;
using VRC.SDK3.Components;

namespace VoyageVoyage
{
    public class GLBLoaderURLInput : UdonSharpBehaviour
    {
        public GLBLoader loader;
        public VRCUrlInputField inputField;

        public override void Interact()
        {
            VRCUrl url = inputField.GetUrl();
            if (url != null && url.ToString() != "")
            {
                loader.SetURL(url);
            }
            
        }
    }
}

