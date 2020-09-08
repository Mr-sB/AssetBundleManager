using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameUtil
{
    public static class AssetBundleEditorTools
    {
        public static void Build()
        {
            if(!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            if(setting.SetAssetBundleName)
                SetAssetBundleName();
            var outputPath = Path.Combine(Application.dataPath, setting.BuildBundlePath);
            if (setting.ClearBuildBundlePath && Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            //Build
            var manifest = BuildPipeline.BuildAssetBundles(outputPath, (BuildAssetBundleOptions)setting.BuildAssetBundleOptions, (BuildTarget)setting.BuildTarget);
            EditorUtility.SetDirty(manifest);
            if (setting.ClearStreamingAssetsBundlePath)
                ClearStreamingAssetsBundlePath();
            if(setting.CopyToStreamingAssetsBundlePath)
                CopyToStreamingAssetsBundlePath();
            if(setting.ClearLoadAssetBundlePath)
                ClearLoadAssetBundlePath();
            if(setting.CopyToLoadAssetBundlePath)
                CopyToLoadAssetBundlePath();
            AssetDatabase.Refresh();
            Debug.Log("AssetBundle Build success : " + outputPath);
        }
        
        /// <summary>
        ///All files in the Top Directory of AssetPath are set to the same asset bundle name, and named the asset bundle name to directory name lowercase.
        /// </summary>
        public static void SetAssetBundleName()
        {
            if(!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var dataPath = Application.dataPath;
            bool hasBundleExtension = setting.TryGetBundleExtension(out var bundleExtension);
            foreach (var directory in Directory.GetDirectories(Path.Combine(dataPath, setting.AssetPath)))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(directory);
                var assetBundleName = hasBundleExtension ? fileNameWithoutExtension.ToLower() + bundleExtension : fileNameWithoutExtension.ToLower();
                foreach (var filename in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    var extension = Path.GetExtension(filename);
                    if (".meta".Equals(extension) || ".DS_Store".Equals(extension)) continue;
                    AssetImporter importer = AssetImporter.GetAtPath(filename.Replace(dataPath, "Assets"));
                    if (importer == null)
                    {
                        Debug.LogError(filename);
                        continue;
                    }
                    // if (".unity".Equals(extension))
                    importer.assetBundleName = assetBundleName;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AssetBundle SetName success");
        }
        
        public static void ClearStreamingAssetsBundlePath()
        {
            if(!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var outputPath = Path.Combine(Application.streamingAssetsPath, setting.LoadBundlePath);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        public static void CopyToStreamingAssetsBundlePath()
        {
            if(!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var sourceFolder = Path.Combine(Application.dataPath, setting.BuildBundlePath);
            var outputPath = Path.Combine(Application.streamingAssetsPath, setting.LoadBundlePath);
            CopyFolder(sourceFolder, outputPath);
        }
        
        public static void ClearLoadAssetBundlePath()
        {
            if(!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var outputPath = setting.GetLoadBundleFullPath();
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        public static void CopyToLoadAssetBundlePath()
        {
            if(!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var sourceFolder = Path.Combine(Application.dataPath, setting.BuildBundlePath);
            var outputPath = setting.GetLoadBundleFullPath();
            CopyFolder(sourceFolder, outputPath);
        }

        public static void CopyFolder(string sourceFolder, string outputPath)
        {
            AssetBundleUtil.CopyFolder(sourceFolder, outputPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool TryGetValidAssetBundleManagerSetting(out AssetBundleManagerSetting setting)
        {
            setting = GetOrCreateAssetBundleManagerSetting();
            if (setting.IsValid) return true;
            Debug.LogError("Please set the correct path in AssetBundleManagerSetting! Click me to select AssetBundleManagerSetting.", setting);
            setting = null;
            return false;
        }
        
        public static AssetBundleManagerSetting GetOrCreateAssetBundleManagerSetting()
        {
            var setting = GetAssetBundleManagerSetting();
            if (setting) return setting;
            setting = ScriptableObject.CreateInstance<AssetBundleManagerSetting>();
            //Save
            var dataPath = Application.dataPath;
            var directoryPath = Path.GetDirectoryName(Path.Combine(dataPath.Substring(0, dataPath.Length - 7), AssetBundleManager.AssetBundleManagerSettingPath));
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            AssetDatabase.CreateAsset(setting, AssetBundleManager.AssetBundleManagerSettingPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return setting;
        }
        
        public static AssetBundleManagerSetting GetAssetBundleManagerSetting()
        {
            return AssetDatabase.LoadAssetAtPath<AssetBundleManagerSetting>(AssetBundleManager
                .AssetBundleManagerSettingPath);
        }
    }
}