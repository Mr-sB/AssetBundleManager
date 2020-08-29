using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameUtil
{
    public static class AssetBundleEditorTools
    {
        [MenuItem("Tools/AssetBundleTool/CreateAssetBundleManagerSetting")]
        public static void CreateAssetBundleManagerSetting()
        {
            GetAssetBundleManagerSetting();
        }
        
        [MenuItem("Tools/AssetBundleTool/SetAssetBundleName")]
        public static void SetAssetBundleName()
        {
            if(!TryGetAssetBundleManagerSetting(out var setting)) return;
            var dataPath = Application.dataPath;
            bool hasBundleExtension = setting.TryGetBundleExtension(out var bundleExtension);
            foreach (var directory in Directory.GetDirectories(Path.Combine(dataPath, setting.AssetPath)))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(directory);
                // int index = int.TryParse(fileNameWithoutExtension.Replace("Scene", ""), out var value)
                //     ? value
                //     : -1;
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
                    // {
                    //     if (index < 0)
                    //     {
                    //         Debug.LogError("GameScene Name Error: " + filename);
                    //         continue;
                    //     }
                    //
                    //     importer.assetBundleName = "gamescene" + index + ".assetbundle";
                    // }
                    // else
                        importer.assetBundleName = assetBundleName;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AssetBundle SetName success");
        }

        [MenuItem("Tools/AssetBundleTool/BuildAndroid")]
        public static void BuildAndroid()
        {
            Build(BuildTarget.Android);
        }

        [MenuItem("Tools/AssetBundleTool/BuildIOS")]
        public static void BuildIOS()
        {
            Build(BuildTarget.iOS);
        }
        
        [MenuItem("Tools/AssetBundleTool/CopyToStreamingAssetsBundlePath")]
        public static void CopyToStreamingAssetsBundlePath()
        {
            if(!TryGetAssetBundleManagerSetting(out var setting)) return;
            var sourceFolder = Path.Combine(Application.dataPath, setting.BuildBundlePath);
            var outputPath = Path.Combine(Application.streamingAssetsPath, setting.LoadBundlePath);
            ClearAndCopy(sourceFolder, outputPath);
        }
        
        [MenuItem("Tools/AssetBundleTool/ClearStreamingAssetsBundlePath")]
        public static void Clear()
        {
            if(!TryGetAssetBundleManagerSetting(out var setting)) return;
            var outputPath = Path.Combine(Application.streamingAssetsPath, setting.LoadBundlePath);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/AssetBundleTool/CopyToLoadAssetBundlePath")]
        public static void CopyToLoadAssetBundlePath()
        {
            if(!TryGetAssetBundleManagerSetting(out var setting)) return;
            var sourceFolder = Path.Combine(Application.dataPath, setting.BuildBundlePath);
            var outputPath = setting.GetLoadBundleFullPath();
            ClearAndCopy(sourceFolder, outputPath);
        }
        
        [MenuItem("Tools/AssetBundleTool/ClearLoadAssetBundlePath")]
        public static void ClearLoadAssetBundlePath()
        {
            if(!TryGetAssetBundleManagerSetting(out var setting)) return;
            var outputPath = setting.GetLoadBundleFullPath();
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ClearAndCopy(string sourceFolder, string outputPath)
        {
            if (!Directory.Exists(sourceFolder))
                return;
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            AssetBundleUtil.CopyFolder(sourceFolder, outputPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Build(BuildTarget target)
        {
            if(!TryGetAssetBundleManagerSetting(out var setting)) return;
            SetAssetBundleName();
            var outputPath = Path.Combine(Application.dataPath, setting.BuildBundlePath);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            var manifest = BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.None, target);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.Refresh();
            Debug.Log("AssetBundle Build success : " + outputPath);
        }

        private static bool TryGetAssetBundleManagerSetting(out AssetBundleManagerSetting setting)
        {
            setting = GetAssetBundleManagerSetting();
            if (setting.IsValid) return true;
            Debug.LogError("Please set the correct path in AssetBundleManagerSetting! Click me to select AssetBundleManagerSetting.", setting);
            setting = null;
            return false;
        }
        
        private static AssetBundleManagerSetting GetAssetBundleManagerSetting()
        {
            var setting = AssetDatabase.LoadAssetAtPath<AssetBundleManagerSetting>(AssetBundleManager.AssetBundleManagerSettingPath);
            if (setting) return setting;
            setting = ScriptableObject.CreateInstance<AssetBundleManagerSetting>();
            var dataPath = Application.dataPath;
            var directoryPath = Path.GetDirectoryName(Path.Combine(dataPath.Substring(0, dataPath.Length - 7), AssetBundleManager.AssetBundleManagerSettingPath));
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            AssetDatabase.CreateAsset(setting, AssetBundleManager.AssetBundleManagerSettingPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return setting;
        }
    }
}