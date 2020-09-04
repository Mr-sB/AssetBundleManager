using System.IO;
using UnityEngine;

namespace GameUtil
{
    public class AssetBundleManagerSetting : ScriptableObject
    {
        public enum LoadBundlePathMode
        {
            StreamingAssets,
            PersistentDataPath
        }

        public string BundleExtension = ".assetbundle";
        public string AssetPath;
        public string BuildBundlePath;
        public LoadBundlePathMode LoadBundleRootPath = LoadBundlePathMode.StreamingAssets;
        public string LoadBundlePath;
        public int BuildTarget = 13;//BuildTarget.Android;
        public int BuildAssetBundleOptions = 0;//BuildAssetBundleOptions.None;
        [Header("BuildAction")]
        public bool ClearBuildBundlePath = true;
        public bool ClearStreamingAssetsBundlePath = true;
        public bool CopyToStreamingAssetsBundlePath = true;
        public bool ClearLoadAssetBundlePath = true;
        public bool CopyToLoadAssetBundlePath = true;

        public bool IsValid => !string.IsNullOrWhiteSpace(AssetPath) && !string.IsNullOrWhiteSpace(BuildBundlePath);

        public static string GetLoadBundleFullPath(LoadBundlePathMode loadBundleRootPath, string loadBundlePath)
        {
            switch (loadBundleRootPath)
            {
                case LoadBundlePathMode.StreamingAssets:
                    return Path.Combine(Application.streamingAssetsPath, loadBundlePath);
                case LoadBundlePathMode.PersistentDataPath:
                    return Path.Combine(Application.persistentDataPath, loadBundlePath);
                default:
                    return string.Empty;
            }
        }
        
        public string GetLoadBundleFullPath()
        {
            return GetLoadBundleFullPath(LoadBundleRootPath, LoadBundlePath);
        }

        public bool TryGetBundleExtension(out string bundleExtension)
        {
            if (string.IsNullOrWhiteSpace(BundleExtension))
            {
                bundleExtension = null;
                return false;
            }
            bundleExtension = BundleExtension.Trim('.');
            if (string.IsNullOrWhiteSpace(bundleExtension))
            {
                bundleExtension = null;
                return false;
            }
            bundleExtension = '.' + bundleExtension.ToLower();
            return true;
        }

        public string GetManifestBundleName()
        {
            return Path.GetFileNameWithoutExtension(BuildBundlePath);
        }
    }
}