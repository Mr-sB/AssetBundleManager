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

        public bool IsValid => !string.IsNullOrWhiteSpace(AssetPath) && !string.IsNullOrWhiteSpace(BuildBundlePath);

        public string GetLoadBundleFullPath()
        {
            switch (LoadBundleRootPath)
            {
                case LoadBundlePathMode.StreamingAssets:
                    return Path.Combine(Application.streamingAssetsPath, LoadBundlePath);
                case LoadBundlePathMode.PersistentDataPath:
                    return Path.Combine(Application.persistentDataPath, LoadBundlePath);
                default:
                    return string.Empty;
            }
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
    }
}