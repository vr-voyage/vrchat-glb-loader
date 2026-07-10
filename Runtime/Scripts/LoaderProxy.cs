
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace VoyageVoyage
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LoaderProxy : UdonSharpBehaviour
    {
        public VRCUrl userURL;
        public VRCUrlInputField inputField;
        public Text urlText;

        [HideInInspector]
        [SerializeField]
        [UdonSynced]
        VRCUrl syncedURL;
        GLBLoader glbLoader = null;
        HierarchyManager glbHierarchyManager = null;
        public GameObject loaderPrefab;
        public UIPanel panel;
        public UdonSharpBehaviour assetInfoPanel;
        public Transform glbLoaderParent;


        public void DestroyLoader()
        {
            if (glbLoader == null) return;
            GameObject glbLoaderObject = glbLoader.gameObject;
            Destroy(glbLoaderObject);
        }

        public void InstantiateNewLoader()
        {
            Debug.Log("<color=orange>New loader !</color>");
            GameObject newGLBLoaderObject = Instantiate(loaderPrefab, glbLoaderParent);
            GLBLoader newLoader = newGLBLoaderObject.GetComponent<GLBLoader>();
            glbLoader = newLoader;
            assetInfoPanel.SetProgramVariable("loader", glbLoader);

            glbHierarchyManager = glbLoader.gameObject.GetComponentInChildren<HierarchyManager>(true);

            newLoader.AddReceiver(panel);
            newLoader.AddReceiver(glbHierarchyManager);
            newLoader.AddReceiver(assetInfoPanel);
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            if (glbLoader == null) return;
            byte[] data = result.ResultBytes;
            Debug.Log("<color=orange>Loading !</color>");
            glbLoader.Load(data);

            if (inputField != null) inputField.interactable = true;
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            Debug.LogError($"Could not download {syncedURL} - Code {result.ErrorCode} : {result.Error}");
            if (inputField != null) inputField.interactable = true;
        }

        void StartDownload(VRCUrl url)
        {
            if (inputField != null)
            {
                inputField.interactable = false;
                inputField.SetUrl(url);
            }
            VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
        }

        public void UserURLUpdated()
        {
            Debug.Log("<color=cyan>USER URL UPDATED !</color>");

            DestroyLoader();
            InstantiateNewLoader();
            StartDownload(userURL);
        }

    }
}

