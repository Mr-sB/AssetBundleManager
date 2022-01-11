using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameUtil
{
    public static class AssetBundleEditorTools
    {
        public const string AssetBundleBuildRecordsName = "AssetBundleBuildRecords.json";
        
        public static AssetBundleManifest Build()
        {
            if (!TryGetValidAssetBundleManagerSetting(out var setting)) return null;
            var outputPath = setting.BuildBundlePath;
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            //Build
            var manifest = BuildPipeline.BuildAssetBundles(outputPath, GetBuildList().ToArray(),
                (BuildAssetBundleOptions) setting.BuildAssetBundleOptions,
                setting.UseActiveBuildTarget ? EditorUserBuildSettings.activeBuildTarget : (BuildTarget) setting.BuildTarget);
            if (!manifest)
            {
                Debug.LogError("AssetBundle Build fail! Build list is Null!");
                AssetDatabase.Refresh();
                return null;
            }
            //Record version
            var buildRecordsPath = Path.Combine(outputPath, AssetBundleBuildRecordsName);
            int buildVersion = 0;
            if (File.Exists(buildRecordsPath))
            {
                var lastBuildRecords = JsonUtility.FromJson<AssetBundleBuildRecords>(File.ReadAllText(buildRecordsPath));
                if (lastBuildRecords != null)
                    buildVersion = lastBuildRecords.Version;
            }
            buildVersion++;
            //Remove unused asset bundle
            var manifestBundleName = setting.GetManifestBundleName();
            var assetBundleNames = new HashSet<string>(manifest.GetAllAssetBundles());
            foreach (var filePath in Directory.GetFiles(outputPath))
            {
                var assetBundleName = Path.GetFileName(filePath);
                if (assetBundleName.EndsWith(".meta")) continue;
                if (assetBundleName.EndsWith(".manifest"))
                    assetBundleName = assetBundleName.Substring(0, assetBundleName.Length - 9);
                if (!assetBundleNames.Contains(assetBundleName) && assetBundleName != manifestBundleName)
                    File.Delete(filePath);
            }
            
            //After build copy
            if (setting.ClearStreamingAssetsBundlePath)
                ClearStreamingAssetsBundlePath();
            if (setting.CopyToStreamingAssetsBundlePath)
                CopyToStreamingAssetsBundlePath();
            if (setting.ClearLoadAssetBundlePath)
                ClearLoadAssetBundlePath();
            if (setting.CopyToLoadAssetBundlePath)
                CopyToLoadAssetBundlePath();
            
            //Record asset bundle info
            AssetBundleBuildRecords buildRecords = new AssetBundleBuildRecords
            {
                Version = buildVersion
            };
            foreach (var assetBundleName in assetBundleNames)
            {
                string hash = manifest.GetAssetBundleHash(assetBundleName).ToString();
                long size = AssetBundleUtil.GetFileLength(Path.Combine(outputPath, assetBundleName));
                buildRecords.Records.Add(new AssetBundleBuildRecord
                {
                    AssetBundleName = assetBundleName,
                    Hash = hash,
                    Size = size
                });
            }
            File.WriteAllText(buildRecordsPath, JsonUtility.ToJson(buildRecords, true));
            AssetDatabase.Refresh();
            Debug.Log("AssetBundle Build success : " + outputPath);
            return manifest;
        }

        /// <summary>
        ///All files in the Top Directory of AssetPath are set to the same asset bundle name, and named the asset bundle name to directory name lowercase.
        /// </summary>
        public static void SetAssetBundleName()
        {
            ForEachAssetBundleAssets((assetBundleName, assetPath) =>
            {
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                {
                    Debug.LogWarning("Can not load asset: " + assetPath);
                    return;
                }
                importer.assetBundleName = assetBundleName;
            });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AssetBundle SetName success");
        }
        
        public static void ClearAssetBundleName()
        {
            ForEachAssetBundleAssets((assetBundleName, assetPath) =>
            {
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                    return;
                importer.assetBundleName = null;
            });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AssetBundle ClearName success");
        }

        public static List<AssetBundleBuild> GetBuildList()
        {
            Dictionary<string, List<string>> bundleDict = new Dictionary<string, List<string>>();
            ForEachAssetBundleAssets((assetBundleName, assetPath) =>
            {
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                {
                    Debug.LogWarning("Can not load asset: "+ assetPath);
                    return;
                }
                if (!bundleDict.TryGetValue(assetBundleName, out var assetNames))
                {
                    assetNames = new List<string>();
                    bundleDict.Add(assetBundleName, assetNames);
                }
                assetNames.Add(assetPath);
            });
            return bundleDict.Select(
                pair => new AssetBundleBuild {assetBundleName = pair.Key, assetNames = pair.Value.ToArray()}).ToList();
        }

        public static void OpenLoadAssetBundlePath()
        {
            if (!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var outputPath = setting.GetLoadBundleFullPath();
            EditorUtility.RevealInFinder(outputPath);
        }

        public static void ClearStreamingAssetsBundlePath()
        {
            if (!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var outputPath = Path.Combine(Application.streamingAssetsPath, setting.LoadBundlePath);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void CopyToStreamingAssetsBundlePath()
        {
            if (!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var sourceFolder = setting.BuildBundlePath;
            var outputPath = Path.Combine(Application.streamingAssetsPath, setting.LoadBundlePath);
            CopyFolder(sourceFolder, outputPath);
        }

        public static void ClearLoadAssetBundlePath()
        {
            if (!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var outputPath = setting.GetLoadBundleFullPath();
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void CopyToLoadAssetBundlePath()
        {
            if (!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            var sourceFolder = setting.BuildBundlePath;
            var outputPath = setting.GetLoadBundleFullPath();
            CopyFolder(sourceFolder, outputPath);
        }

        public static void CopyFolder(string sourceFolder, string outputPath)
        {
            AssetBundleUtil.CopyFolder(sourceFolder, outputPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ForEachAssetBundleFolders(Action<string, string> callback)
        {
            if (!TryGetValidAssetBundleManagerSetting(out var setting)) return;
            bool hasBundleExtension = setting.TryGetBundleExtension(out var bundleExtension);
            foreach (var directory in Directory.GetDirectories(setting.AssetPath))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(directory);
                var assetBundleName = hasBundleExtension ? fileNameWithoutExtension.ToLower() + bundleExtension : fileNameWithoutExtension.ToLower();
                callback?.Invoke(assetBundleName, directory);
            }
        }
        
        /// <summary>
        /// For each AssetBundle assets with continue flag.
        /// </summary>
        /// <param name="callback">assetBundleName, assetPath, continue.</param>
        /// <returns>For each complete.</returns>
        private static bool ForEachAssetBundleAssets(string assetBundleName, string directory, Func<string, string, bool> callback)
        {
            foreach (var assetPath in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(assetPath);
                if (".meta".Equals(extension) || ".DS_Store".Equals(extension)) continue;
                if (callback != null && !callback(assetBundleName, assetPath))
                    return false;
            }
            return true;
        }
        
        private static void ForEachAssetBundleAssets(Action<string, string> callback)
        {
            ForEachAssetBundleFolders((assetBundleName, directory) =>
            {
                ForEachAssetBundleAssets(assetBundleName, directory,
                    (assetBundleName1, assetPath) =>
                    {
                        callback?.Invoke(assetBundleName1, assetPath);
                        return true;
                    });
            });
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
            var directoryPath = Path.GetDirectoryName(AssetBundleManager.AssetBundleManagerSettingPath);
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