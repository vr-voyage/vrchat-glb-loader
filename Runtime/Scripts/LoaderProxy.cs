
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace VoyageVoyage
{
    public class LoaderProxy : UdonSharpBehaviour
    {
        public VRCUrl userURL;
        public GLBLoader glbLoader;
        public GameObject loaderPrefab;
        public HierarchyManager glbHierarchyManager;
        public UIPanel panel;
        public UdonSharpBehaviour assetInfoPanel;
        Transform glbLoaderParent;

        private void OnEnable()
        {
            if (glbLoader == null)
            {
                Debug.LogError("[LoaderProxy] GLB Loader not set !");
                enabled = false;
                return;
            }
            glbLoaderParent = glbLoader.transform.parent;
            enabled = false;
        }

        public bool ScriptIsAlive()
        {
            if (glbLoader == null) return false;
            float currentTime = Time.time;
            return glbLoader.IsAlive(currentTime) == currentTime;
        }

        public void DestroyLoader()
        {
            GameObject glbLoaderObject = glbLoader.gameObject;
            Destroy(glbLoaderObject);
        }

        public void InstantiateNewLoader()
        {
            GameObject newGLBLoaderObject = Instantiate(loaderPrefab, glbLoaderParent);
            GLBLoader newLoader = newGLBLoaderObject.GetComponent<GLBLoader>();
            glbLoader = newLoader;
            assetInfoPanel.SetProgramVariable("loader", glbLoader);
            glbHierarchyManager = glbLoader.GetComponentInChildren<HierarchyManager>();

            newLoader.stateReceivers = new UdonSharpBehaviour[] { panel, glbHierarchyManager, assetInfoPanel };
        }

        public void OwnColliderStateOn()
        {
            glbHierarchyManager.OwnColliderStateOn();
        }

        public void OwnColliderStateOff()
        {
            glbHierarchyManager.OwnColliderStateOff();
        }

        public void ChildrenColliderStateOn()
        {
            glbHierarchyManager.ChildrenColliderStateOn();
        }

        public void ChildrenColliderStateOff()
        {
            glbHierarchyManager.ChildrenColliderStateOff();
        }

        void StartDownload()
        {
            glbLoader.userURL = userURL;
            glbLoader.UserURLUpdated();
        }

        public void UserURLUpdated()
        {
            if (glbLoader == null || !ScriptIsAlive())
            {
                DestroyLoader();
                InstantiateNewLoader();
            }

            StartDownload();
        }
    }
}

