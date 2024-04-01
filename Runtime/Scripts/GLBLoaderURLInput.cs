
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VoyageVoyage;
using VRC.SDK3.Components;

namespace VoyageVoyage
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class GLBLoaderURLInput : UdonSharpBehaviour
    {
        public UdonSharpBehaviour[] behaviours;

        public VRCUrlInputField inputField;

        VRCUrl lastUrl;

        [UdonSynced]
        [HideInInspector]
        public VRCUrl syncedUrl;

        public void Notify()
        {
            int nBehaviours = behaviours.Length;
            for (int b = 0; b < nBehaviours; b++)
            {
                var behaviour = behaviours[b];
                if (behaviour == null) continue;
                behaviour.SetProgramVariable("userURL", syncedUrl);
                behaviour.SendCustomEvent("UserURLUpdated");
            }
        }

        bool ValidURL(VRCUrl url)
        {
            return ((url != lastUrl) & (url != null)) && url.ToString() != "";
        }

        public override void Interact()
        {
            VRCUrl url = inputField.GetUrl();
            if (!ValidURL(url)) return;

            syncedUrl = url;
            lastUrl = url;
            Notify();

            if (WeAreTheOwner())
            {
                WeGotOwnership();
            }
            else
            {
                GetOwnership();
            }
        }

        public override void OnDeserialization()
        {
            if (!ValidURL(syncedUrl)) return;
            Notify();
        }

        public bool WeAreTheOwner()
        {
            return Networking.LocalPlayer == Networking.GetOwner(gameObject);
        }

        public void GetOwnership()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player != Networking.LocalPlayer) return;
            WeGotOwnership();
        }

        void WeGotOwnership()
        {
            RequestSerialization();
        }


    }
}

