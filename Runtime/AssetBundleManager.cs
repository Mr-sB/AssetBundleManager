using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace GameUtil
{
    public static class AssetBundleManager
    {
        #region AssetKey

        //Implement IEquatable<T> interface to avoid boxing and unboxing when comparing.
        private struct AssetKey : IEquatable<AssetKey>
        {
            public readonly Type ObjectType;
            public readonly string AssetName;

            public AssetKey(Type objectType, string assetName)
            {
                ObjectType = objectType;
                AssetName = assetName;
            }

            public bool Equals(AssetKey other)
            {
                return ObjectType == other.ObjectType && AssetName == other.AssetName;
            }

            public static bool operator ==(AssetKey lhs, AssetKey rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(AssetKey lhs, AssetKey rhs)
            {
                return !(lhs == rhs);
            }

            public override bool Equals(object other)
            {
                return other is AssetKey other1 && Equals(other1);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((ObjectType != null ? ObjectType.GetHashCode() : 0) * 397) ^ (AssetName != null ? AssetName.GetHashCode() : 0);
                }
            }
        }

        #endregion

        #region FastMode

#if UNITY_EDITOR
        private static readonly bool mFastMode;
        private static readonly string mAssetPath;
        private static Dictionary<string, List<string>> mAllAssetsPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RecordAssets()
        {
            if (!mFastMode) return;
            if (mAllAssetsPath == null)
                mAllAssetsPath = new Dictionary<string, List<string>>();
            else
                mAllAssetsPath.Clear();

            var dataPath = Application.dataPath;
            foreach (var directory in Directory.GetDirectories(Path.Combine(dataPath, mAssetPath)))
            {
                List<string> fileNames = new List<string>();
                mAllAssetsPath.Add(Path.GetFileName(directory).ToLower(), fileNames);
                foreach (var filename in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    var extension = Path.GetExtension(filename);
                    if (".meta".Equals(extension) || ".DS_Store".Equals(extension)) continue;
                    fileNames.Add(filename.Substring(Application.dataPath.Length - 6));
                }
            }
        }

        private static T GetAssetFastMode<T>(string bundleName, string assetName) where T : Object
        {
            return (T) GetAssetFastMode(bundleName, assetName, typeof(T));
        }
        
        private static Object GetAssetFastMode(string bundleName, string assetName, Type assetType)
        {
            if (!mAllAssetsPath.TryGetValue(bundleName, out var paths))
            {
                Debug.LogError($"GetAssetFastMode {assetName} from {bundleName} error: Null Directory!");
                return null;
            }

            foreach (var path in paths)
            {
                //Use StringComparison.OrdinalIgnoreCase to compare asset name.
                bool isEqual = path.EndsWith(assetName, StringComparison.OrdinalIgnoreCase);
                if (!isEqual)
                {
                    int index;
                    if ((index = path.LastIndexOf('.')) != -1)
                    {
                        var pathWithoutExtension = path.Substring(0, index);
                        isEqual = pathWithoutExtension.EndsWith(assetName, StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (!isEqual) continue;

                var asset = AssetDatabase.LoadAssetAtPath(path, assetType);
                if (asset != null)
                    return asset;
            }

            Debug.LogError($"GetAssetFastMode {assetName} from {bundleName} error: Null Asset!");
            return null;
        }
#endif

        #endregion

        #region LoadedAssetBundle

        private class LoadedAssetBundle
        {
            public readonly string BundleName;
            public AssetBundle AssetBundle { private set; get; }
            private List<string> mAllDependencies;
            private bool mAutoUnload;
            private HashSet<string> mReferences;

            public LoadedAssetBundle(string bundleName, AssetBundle assetBundle, string referenceBundleName)
            {
                BundleName = bundleName;
                SetAssetBundle(assetBundle);
                mReferences = new HashSet<string>();
                mAutoUnload = true;
                AddReference(referenceBundleName);
            }

            public LoadedAssetBundle(string bundleName, AssetBundle assetBundle, HashSet<string> referencesBundleName)
            {
                BundleName = bundleName;
                SetAssetBundle(assetBundle);
                mReferences = new HashSet<string>();
                mAutoUnload = true;
                AddReference(referencesBundleName);
            }

            public void SetAssetBundle(AssetBundle assetBundle)
            {
                if (AssetBundle == assetBundle) return;
                AssetBundle = assetBundle;
                var allDependencies = Manifest.GetAllDependencies(AssetBundle.name);
                var len = allDependencies.Length;
                if (mAllDependencies == null)
                    mAllDependencies = new List<string>(len);
                else
                {
                    mAllDependencies.Clear();
                    if (mAllDependencies.Capacity < len)
                        mAllDependencies.Capacity = len;
                }

                for (int i = 0; i < len; i++)
                    mAllDependencies.Add(Path.GetFileNameWithoutExtension(allDependencies[i]));
            }

            public void AddReference(string referenceBundleName)
            {
                if (!mAutoUnload) return;
                //Explicit load the AssetBundle, can not auto unload.
                if (BundleName == referenceBundleName)
                {
                    mAutoUnload = false;
                    mReferences.Clear();
                    mReferences = null;
                    //All dependencies add reference
                    foreach (var dependency in mAllDependencies)
                    {
                        //Already loaded
                        if (mLoadedAssetBundleDict.TryGetValue(dependency, out var loadedAssetBundle))
                        {
                            //Add Reference
                            loadedAssetBundle.AddReference(BundleName);
                            continue;
                        }

                        //Loading
                        if (mLoadingAssetBundleDict.TryGetValue(dependency, out var loadingAssetBundle))
                        {
                            //Add Reference
                            loadingAssetBundle.AddReference(BundleName);
                            loadingAssetBundle.GetAssetBundle(); //Force load sync
                        }
                    }

                    return;
                }

                if (!mReferences.Contains(referenceBundleName))
                    mReferences.Add(referenceBundleName);
            }

            public void AddReference(HashSet<string> referencesBundleName)
            {
                if (!mAutoUnload) return;
                foreach (var referenceBundleName in referencesBundleName)
                {
                    AddReference(referenceBundleName);
                    if (!mAutoUnload) return;
                }
            }

            private void RemoveReference(string referenceBundleName, bool unloadAllLoadedObjects)
            {
                if (!mAutoUnload) return;
                if (mReferences.Contains(referenceBundleName))
                    mReferences.Remove(referenceBundleName);
                //References count is 0, auto unload.
                if (mReferences.Count == 0)
                    Unload(unloadAllLoadedObjects);
            }

            public void Unload(bool unloadAllLoadedObjects)
            {
                mLoadedAssetBundleDict.Remove(BundleName);
                if (AssetBundle)
                    AssetBundle.Unload(unloadAllLoadedObjects);
                for (int i = 0, count = mAllDependencies.Count; i < count; i++)
                {
                    if (!mLoadedAssetBundleDict.TryGetValue(mAllDependencies[i], out var loadedAssetBundle)) continue;
                    loadedAssetBundle.RemoveReference(BundleName, unloadAllLoadedObjects);
                }
            }
        }

        #endregion

        #region LoadingAssetBundle

        private class LoadingAssetBundle
        {
            public readonly string BundleName;
            public event Action<AssetBundle> Completed;
            private readonly HashSet<string> mReferencesBundleName;
            private readonly AssetBundleCreateRequest mAssetBundleCreateRequest;

            public LoadingAssetBundle(string bundleName, string referenceBundleName, AssetBundleCreateRequest assetBundleCreateRequest)
            {
                BundleName = bundleName;
                mReferencesBundleName = new HashSet<string> {referenceBundleName};
                mAssetBundleCreateRequest = assetBundleCreateRequest;
                mAssetBundleCreateRequest.completed += OnCompleted;
                mLoadingAssetBundleDict.Add(BundleName, this);
            }

            public AssetBundle GetAssetBundle()
            {
                return mAssetBundleCreateRequest.assetBundle;
            }

            public void AddReference(string referenceBundleName)
            {
                if (!mReferencesBundleName.Contains(referenceBundleName))
                    mReferencesBundleName.Add(referenceBundleName);
            }

            private void OnCompleted(AsyncOperation operation)
            {
                var assetBundle = mAssetBundleCreateRequest.assetBundle;
                if (!assetBundle)
                    Debug.LogError($"Load LoadAssetBundleAsync {BundleName} error: Null AssetBundle!");
                if (!mLoadedAssetBundleDict.TryGetValue(BundleName, out var loadedAssetBundle))
                {
                    loadedAssetBundle = new LoadedAssetBundle(BundleName, assetBundle, mReferencesBundleName);
                    mLoadedAssetBundleDict.Add(BundleName, loadedAssetBundle);
                }
                else
                {
                    loadedAssetBundle.SetAssetBundle(assetBundle);
                    //Add references
                    loadedAssetBundle.AddReference(mReferencesBundleName);
                }

                mLoadingAssetBundleDict.Remove(BundleName);
                Completed?.Invoke(assetBundle);
            }
        }

        #endregion

        #region LoadingAsset

        private abstract class LoadingAssetBase
        {
            public readonly string BundleName;
            public readonly AssetKey AssetKey;
            private readonly AssetBundleRequest mAssetBundleRequest;
            public event Action<Object> BaseCompleted;

            protected abstract void OnCompleted(Object asset);

            protected LoadingAssetBase(string bundleName, AssetKey assetKey, AssetBundleRequest assetBundleRequest)
            {
                BundleName = bundleName;
                AssetKey = assetKey;
                mAssetBundleRequest = assetBundleRequest;
                mAssetBundleRequest.completed += OnCompleted;
                if (!mLoadingAssetDicts.TryGetValue(BundleName, out var loadingAssetDict))
                {
                    loadingAssetDict = new Dictionary<AssetKey, LoadingAssetBase>();
                    mLoadingAssetDicts.Add(BundleName, loadingAssetDict);
                }

                loadingAssetDict.Add(AssetKey, this);
            }

            public Object GetAsset()
            {
                return mAssetBundleRequest.asset;
            }

            private void OnCompleted(AsyncOperation operation)
            {
                var asset = mAssetBundleRequest.asset;
                if (!asset)
                    Debug.LogError($"GetAssetAsync {AssetKey.AssetName} from {BundleName} error: Null Asset!");
                if (!mAssetDicts.TryGetValue(BundleName, out var assetDict))
                {
                    assetDict = new Dictionary<AssetKey, Object>();
                    mAssetDicts.Add(BundleName, assetDict);
                }

                assetDict[AssetKey] = asset;
                if (mLoadingAssetDicts.TryGetValue(BundleName, out var loadingAssetDict))
                {
                    loadingAssetDict.Remove(AssetKey);
                    if (loadingAssetDict.Count == 0)
                        mLoadingAssetDicts.Remove(BundleName);
                }
#if UNITY_EDITOR
                ReplaceShader(asset);
#endif
                BaseCompleted?.Invoke(asset);
                OnCompleted(asset);
            }
        }

        private class LoadingAsset<T> : LoadingAssetBase where T : Object
        {
            public event Action<T> Completed;

            public LoadingAsset(string bundleName, string assetName, AssetBundleRequest assetBundleRequest) : base(bundleName,
                new AssetKey(typeof(T), assetName), assetBundleRequest)
            {
            }

            protected override void OnCompleted(Object asset)
            {
                Completed?.Invoke(asset as T);
            }

            public new T GetAsset()
            {
                return base.GetAsset() as T;
            }
        }

        #endregion

        public const string ShaderBundleName = "shaders";
        public const string AssetBundleManagerSettingPath = "Assets/Resources/" + AssetBundleManagerSettingName + ".asset";
        public const string AssetBundleManagerSettingName = "AssetBundleManagerSetting";

        public static readonly bool HasBundleExtension;
        public static readonly string BundleExtension;
        public static readonly string LoadBundlePath;
        public static readonly string AssetBundleRootPath;
        public static readonly string ManifestBundleName;
        private static readonly Dictionary<string, LoadedAssetBundle> mLoadedAssetBundleDict = new Dictionary<string, LoadedAssetBundle>();
        private static readonly Dictionary<string, Dictionary<AssetKey, Object>> mAssetDicts = new Dictionary<string, Dictionary<AssetKey, Object>>();
        private static readonly Dictionary<string, LoadingAssetBundle> mLoadingAssetBundleDict = new Dictionary<string, LoadingAssetBundle>();

        private static readonly Dictionary<string, Dictionary<AssetKey, LoadingAssetBase>> mLoadingAssetDicts =
            new Dictionary<string, Dictionary<AssetKey, LoadingAssetBase>>();

        private static readonly List<LoadingAssetBase> mCacheLoadingAssetList = new List<LoadingAssetBase>();
        private static bool mIsForceSyncLoadingAssets = false;

        private static AssetBundleManifest mManifest;

        public static AssetBundleManifest Manifest
        {
            get
            {
#if UNITY_EDITOR
                if (mFastMode) return null;
#endif
                if (mManifest != null) return mManifest;
                //Load AssetBundleManifest
                var manifestBundle = AssetBundle.LoadFromFile(GetAssetBundlePath(ManifestBundleName, false));
                if (!manifestBundle)
                    Debug.LogError($"Load ManifestBundle {ManifestBundleName} error: Null AssetBundle!");
                else
                {
                    mManifest = manifestBundle.LoadAsset<AssetBundleManifest>(nameof(AssetBundleManifest));
                    if (!mManifest)
                        Debug.LogError("Load AssetBundleManifest error: Null Asset!");
                    manifestBundle.Unload(false);
                }

                return mManifest;
            }
        }

        #region Static Ctor

        static AssetBundleManager()
        {
            var setting = Resources.Load<AssetBundleManagerSetting>(AssetBundleManagerSettingName);
            if (setting)
            {
                HasBundleExtension = setting.TryGetBundleExtension(out BundleExtension);
                ManifestBundleName = setting.GetManifestBundleName();
#if UNITY_EDITOR
                mFastMode = setting.FastMode;
                mAssetPath = setting.AssetPath;
#endif
                LoadBundlePath = setting.LoadBundlePath;
                AssetBundleRootPath = setting.GetLoadBundleFullPath();
                Resources.UnloadAsset(setting);
            }
            else
            {
                HasBundleExtension = false;
                BundleExtension = null;
                ManifestBundleName = string.Empty;
                LoadBundlePath = string.Empty;
                AssetBundleRootPath = string.Empty;
#if UNITY_EDITOR
                mFastMode = false;
                mAssetPath = string.Empty;
#endif
                Debug.LogError("Null AssetBundleManagerSetting!");
            }
        }

        #endregion

        // #region Instance
        // private static AssetBundleManager instance;
        //
        // private static AssetBundleManager Instance
        // {
        //     get
        //     {
        //         if (instance == null)
        //         {
        //             //Find
        //             instance = FindObjectOfType<AssetBundleManager>();
        //             //Create
        //             if (instance == null)
        //             {
        //                 var go = new GameObject(nameof(AssetBundleManager));
        //                 instance = go.AddComponent<AssetBundleManager>();
        //                 DontDestroyOnLoad(go);
        //             }
        //         }
        //         return instance;
        //     }
        // }
        // #endregion

        #region Shader

        public static void LoadShaderAssetBundle()
        {
            LoadAssetBundle(ShaderBundleName);
        }

        public static void LoadShaderAssetBundleAsync(Action loaded)
        {
            LoadAssetBundleAsync(ShaderBundleName, _ => { loaded?.Invoke(); });
        }

        public static Shader FindShader(string shaderName)
        {
            var shader = GetAsset<Shader>(ShaderBundleName, shaderName);
            return shader ? shader : Shader.Find(shaderName);
        }

        #endregion

        #region LoadAssetBundle

        /// <summary>
        /// Load AssetBundle synchronously.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        public static AssetBundle LoadAssetBundle(string bundleName)
        {
#if UNITY_EDITOR
            if (mFastMode) return null;
#endif
            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out var loadedAssetBundle))
            {
                //Add Reference
                loadedAssetBundle.AddReference(bundleName);
                return loadedAssetBundle.AssetBundle;
            }

            LoadingAssetBundle loadingAssetBundle;
            if (Manifest)
            {
                //Load all dependencies AssetBundle
                string[] dependencies = Manifest.GetAllDependencies(GetAssetBundleName(bundleName));
                for (int i = 0, len = dependencies.Length; i < len; i++)
                {
                    var dependencyBundleName = dependencies[i];
                    if (dependencyBundleName == null)
                    {
                        Debug.LogError($"Load AssetBundle {bundleName} error: Dependency name is Null!");
                        continue;
                    }

                    var bundleNameWithoutExtension = Path.GetFileNameWithoutExtension(dependencyBundleName);
                    //Already loaded
                    if (mLoadedAssetBundleDict.TryGetValue(bundleNameWithoutExtension, out loadedAssetBundle))
                    {
                        //Add Reference
                        loadedAssetBundle.AddReference(bundleName);
                        continue;
                    }

                    //Loading
                    if (mLoadingAssetBundleDict.TryGetValue(bundleNameWithoutExtension, out loadingAssetBundle))
                    {
                        //Add Reference
                        loadingAssetBundle.AddReference(bundleName);
                        loadingAssetBundle.GetAssetBundle(); //Force load sync
                        continue;
                    }

                    var dependencyBundle = AssetBundle.LoadFromFile(GetAssetBundlePath(dependencyBundleName, false));
                    if (!dependencyBundle)
                        Debug.LogError($"Load AssetBundle {bundleName} error: Load dependency bundle {dependencyBundleName} Null AssetBundle!");
                    mLoadedAssetBundleDict.Add(bundleNameWithoutExtension, new LoadedAssetBundle(bundleNameWithoutExtension, dependencyBundle, bundleName));
                }
            }

            //Double check
            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out loadedAssetBundle))
            {
                //Add Reference
                loadedAssetBundle.AddReference(bundleName);
                return loadedAssetBundle.AssetBundle;
            }

            //Loading
            if (mLoadingAssetBundleDict.TryGetValue(bundleName, out loadingAssetBundle))
            {
                //Add Reference
                loadingAssetBundle.AddReference(bundleName);
                return loadingAssetBundle.GetAssetBundle(); //Force load sync
            }

            var assetBundle = AssetBundle.LoadFromFile(GetAssetBundlePath(bundleName));
            if (!assetBundle)
                Debug.LogError($"Load AssetBundle {bundleName} error: Null AssetBundle!");
            mLoadedAssetBundleDict.Add(bundleName, new LoadedAssetBundle(bundleName, assetBundle, bundleName));
            return assetBundle;
        }

        /// <summary>
        /// Load AssetBundle asynchronously.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        /// <param name="loaded">Callback when loaded.</param>
        public static void LoadAssetBundleAsync(string bundleName, Action<AssetBundle> loaded)
        {
#if UNITY_EDITOR
            if (mFastMode)
            {
                loaded?.Invoke(null);
                return;
            }
#endif
            //Already loaded
            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out var loadedAssetBundle))
            {
                //Add Reference
                loadedAssetBundle.AddReference(bundleName);
                loaded?.Invoke(loadedAssetBundle.AssetBundle);
                return;
            }

            //Loading
            if (mLoadingAssetBundleDict.TryGetValue(bundleName, out var loadingAssetBundle))
            {
                //Add Reference
                loadingAssetBundle.AddReference(bundleName);
                loadingAssetBundle.Completed += loaded;
                return;
            }

            LoadAssetBundleAsyncInternal(bundleName, loaded);
        }

        private static void LoadAssetBundleAsyncInternal(string bundleName, Action<AssetBundle> loaded)
        {
            int loadingCount = 1;

            void OnCompleted(AssetBundle _)
            {
                loadingCount--;
                if (loadingCount <= 0)
                    loaded?.Invoke(mLoadedAssetBundleDict[bundleName].AssetBundle);
            }

            LoadedAssetBundle loadedAssetBundle;
            LoadingAssetBundle loadingAssetBundle;
            if (Manifest)
            {
                //Load all dependencies AssetBundle
                string[] dependencies = Manifest.GetAllDependencies(GetAssetBundleName(bundleName));
                for (int i = 0, len = dependencies.Length; i < len; i++)
                {
                    var dependencyBundleName = dependencies[i];
                    if (dependencyBundleName == null)
                    {
                        Debug.LogError($"Load LoadAssetBundleAsync {bundleName} error: Dependency name is Null!");
                        continue;
                    }

                    var bundleNameWithoutExtension = Path.GetFileNameWithoutExtension(dependencyBundleName);
                    //Already loaded
                    if (mLoadedAssetBundleDict.TryGetValue(bundleNameWithoutExtension, out loadedAssetBundle))
                    {
                        //Add Reference
                        loadedAssetBundle.AddReference(bundleName);
                        continue;
                    }

                    loadingCount++;
                    //Not loading
                    if (!mLoadingAssetBundleDict.TryGetValue(bundleNameWithoutExtension, out loadingAssetBundle))
                    {
                        //Add to loading
                        loadingAssetBundle = new LoadingAssetBundle(bundleNameWithoutExtension, bundleName,
                            AssetBundle.LoadFromFileAsync(GetAssetBundlePath(dependencyBundleName, false)));
                    }
                    else
                    {
                        //Add Reference
                        loadingAssetBundle.AddReference(bundleName);
                    }

                    loadingAssetBundle.Completed += OnCompleted;
                }
            }

            //Double check
            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out loadedAssetBundle))
            {
                //Add Reference
                loadedAssetBundle.AddReference(bundleName);
                loaded?.Invoke(loadedAssetBundle.AssetBundle);
                return;
            }

            //Not loading
            if (!mLoadingAssetBundleDict.TryGetValue(bundleName, out loadingAssetBundle))
            {
                var assetBundleCreateRequest = AssetBundle.LoadFromFileAsync(GetAssetBundlePath(bundleName));
                if (assetBundleCreateRequest == null)
                {
                    Debug.LogError($"Load LoadAssetBundleAsync {bundleName} error: Null AssetBundleCreateRequest!");
                    mLoadedAssetBundleDict[bundleName] = null;
                    loaded?.Invoke(null);
                    return;
                }

                //Add to loading
                loadingAssetBundle = new LoadingAssetBundle(bundleName, bundleName, assetBundleCreateRequest);
            }
            else
            {
                //Add Reference
                loadingAssetBundle.AddReference(bundleName);
            }

            loadingAssetBundle.Completed += OnCompleted;
        }

        #endregion

        #region GetAsset

        /// <summary>
        /// Get asset from AssetBundle synchronously.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        /// <param name="assetName">Asset name.</param>
        /// <typeparam name="T">Asset type.</typeparam>
        /// <returns>Asset.</returns>
        public static T GetAsset<T>(string bundleName, string assetName) where T : Object
        {
            return (T) GetAsset(bundleName, assetName, typeof(T));
        }

        /// <summary>
        /// Get asset from AssetBundle synchronously.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        /// <param name="assetName">Asset name.</param>
        /// <param name="assetType">Asset type.</param>
        /// <returns>Asset.</returns>
        public static Object GetAsset(string bundleName, string assetName, Type assetType)
        {
            if (!typeof(Object).IsAssignableFrom(assetType))
            {
                Debug.LogError($"GetAsset {assetName} from {bundleName} error: Type {assetType} can not cast to UnityEngine.Object!");
                return null;
            }
#if UNITY_EDITOR
            if (mFastMode)
                return GetAssetFastMode(bundleName, assetName, assetType);
#endif
            AssetKey assetKey = new AssetKey(assetType, assetName);
            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict))
            {
                assetDict = new Dictionary<AssetKey, Object>();
                mAssetDicts.Add(bundleName, assetDict);
            }

            //Already loaded
            if (assetDict.TryGetValue(assetKey, out var asset)) return asset;
            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
            {
                if (loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
                    return loadingAsset.GetAsset(); //Force load sync
            }

            var assetBundle = LoadAssetBundle(bundleName);
            //Bundle is null
            if (!assetBundle)
            {
                Debug.LogError($"GetAsset {assetName} from {bundleName} error: Null AssetBundle!");
                //Add null to the dictionary. When the same resource is loaded next time, null is returned directly.
                assetDict.Add(assetKey, null);
                return null;
            }

            //Double Check
            if (assetDict.TryGetValue(assetKey, out asset)) return asset;
            asset = assetBundle.LoadAsset(assetName, assetType);
            if (!asset)
                Debug.LogError($"GetAsset {assetName} from {bundleName} error: Null Asset!");
            assetDict.Add(assetKey, asset);
#if UNITY_EDITOR
            ReplaceShader(asset);
#endif
            return asset;
        }

        /// <summary>
        /// Get asset from AssetBundle asynchronously.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        /// <param name="assetName">Asset name.</param>
        /// <param name="loaded">Callback when loaded.</param>
        /// <typeparam name="T">Asset type.</typeparam>
        public static void GetAssetAsync<T>(string bundleName, string assetName, Action<T> loaded) where T : Object
        {
#if UNITY_EDITOR
            if (mFastMode)
            {
                var obj = GetAssetFastMode<T>(bundleName, assetName);
                loaded?.Invoke(obj);
                return;
            }
#endif
            AssetKey assetKey = new AssetKey(typeof(T), assetName);
            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict))
            {
                assetDict = new Dictionary<AssetKey, Object>();
                mAssetDicts.Add(bundleName, assetDict);
            }

            //Already loaded
            if (assetDict.TryGetValue(assetKey, out var asset))
            {
                loaded?.Invoke((T) asset);
                return;
            }

            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
            {
                if (loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
                {
                    ((LoadingAsset<T>) loadingAsset).Completed += loaded;
                    return;
                }
            }

            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out var loadedAssetBundle))
            {
                //Bundle is null
                if (!loadedAssetBundle.AssetBundle)
                {
                    Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null AssetBundle!");
                    //Add null to the dictionary. When the same resource is loaded next time, null is returned directly.
                    assetDict.Add(assetKey, null);
                    loaded?.Invoke(null);
                    return;
                }

                GetAssetAsyncInternal(loadedAssetBundle.AssetBundle, bundleName, assetName, loaded);
            }
            else
            {
                var param1 = bundleName;
                var param2 = assetName;
                var param3 = loaded;
                LoadAssetBundleAsyncInternal(bundleName,
                    bundle => { GetAssetAsyncInternal(bundle, param1, param2, param3); });
            }
        }
        
        /// <summary>
        /// Get asset from AssetBundle asynchronously.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        /// <param name="assetName">Asset name.</param>
        /// <param name="assetType">Asset type.</param>
        /// <param name="loaded">Callback when loaded.</param>
        public static void GetAssetAsync(string bundleName, string assetName, Type assetType, Action<Object> loaded)
        {
            if (!typeof(Object).IsAssignableFrom(assetType))
            {
                Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Type {assetType} can not cast to UnityEngine.Object!");
                loaded?.Invoke(null);
            }
#if UNITY_EDITOR
            if (mFastMode)
            {
                var obj = GetAssetFastMode(bundleName, assetName, assetType);
                loaded?.Invoke(obj);
                return;
            }
#endif
            AssetKey assetKey = new AssetKey(assetType, assetName);
            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict))
            {
                assetDict = new Dictionary<AssetKey, Object>();
                mAssetDicts.Add(bundleName, assetDict);
            }

            //Already loaded
            if (assetDict.TryGetValue(assetKey, out var asset))
            {
                loaded?.Invoke(asset);
                return;
            }

            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
            {
                if (loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
                {
                    loadingAsset.BaseCompleted += loaded;
                    return;
                }
            }

            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out var loadedAssetBundle))
            {
                //Bundle is null
                if (!loadedAssetBundle.AssetBundle)
                {
                    Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null AssetBundle!");
                    //Add null to the dictionary. When the same resource is loaded next time, null is returned directly.
                    assetDict.Add(assetKey, null);
                    loaded?.Invoke(null);
                    return;
                }

                GetAssetAsyncInternal(loadedAssetBundle.AssetBundle, bundleName, assetName, assetType, loaded);
            }
            else
            {
                var param1 = bundleName;
                var param2 = assetName;
                var param3 = assetType;
                var param4 = loaded;
                LoadAssetBundleAsyncInternal(bundleName,
                    bundle => { GetAssetAsyncInternal(bundle, param1, param2, param3, param4); });
            }
        }

        private static void GetAssetAsyncInternal<T>(AssetBundle assetBundle, string bundleName, string assetName, Action<T> loaded) where T : Object
        {
            AssetKey assetKey = new AssetKey(typeof(T), assetName);
            Dictionary<AssetKey, Object> assetDict;
            //Bundle is null
            if (!assetBundle)
            {
                Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null AssetBundle!");
                //Add null to the dictionary. When the same resource is loaded next time, null is returned directly.
                if (!mAssetDicts.TryGetValue(bundleName, out assetDict))
                {
                    assetDict = new Dictionary<AssetKey, Object>();
                    mAssetDicts.Add(bundleName, assetDict);
                }

                assetDict[assetKey] = null;
                loaded?.Invoke(null);
                return;
            }

            if (!mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
            {
                loadingAssetDict = new Dictionary<AssetKey, LoadingAssetBase>();
                mLoadingAssetDicts.Add(bundleName, loadingAssetDict);
            }

            //Not loading
            if (!loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
            {
                var assetBundleRequest = assetBundle.LoadAssetAsync<T>(assetName);
                if (assetBundleRequest == null)
                {
                    Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null AssetBundleRequest!");
                    //Add null to the dictionary. When the same resource is loaded next time, null is returned directly.
                    if (!mAssetDicts.TryGetValue(bundleName, out assetDict))
                    {
                        assetDict = new Dictionary<AssetKey, Object>();
                        mAssetDicts.Add(bundleName, assetDict);
                    }

                    assetDict[assetKey] = null;
                    loaded?.Invoke(null);
                    return;
                }

                //Add to loading
                loadingAsset = new LoadingAsset<T>(bundleName, assetName, assetBundleRequest);
            }

            ((LoadingAsset<T>) loadingAsset).Completed += loaded;
        }
        
        private static void GetAssetAsyncInternal(AssetBundle assetBundle, string bundleName, string assetName, Type assetType, Action<Object> loaded)
        {
            AssetKey assetKey = new AssetKey(assetType, assetName);
            Dictionary<AssetKey, Object> assetDict;
            //Bundle is null
            if (!assetBundle)
            {
                Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null AssetBundle!");
                //Add null to the dictionary. When the same resource is loaded next time, null is returned directly.
                if (!mAssetDicts.TryGetValue(bundleName, out assetDict))
                {
                    assetDict = new Dictionary<AssetKey, Object>();
                    mAssetDicts.Add(bundleName, assetDict);
                }

                assetDict[assetKey] = null;
                loaded?.Invoke(null);
                return;
            }

            if (!mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
            {
                loadingAssetDict = new Dictionary<AssetKey, LoadingAssetBase>();
                mLoadingAssetDicts.Add(bundleName, loadingAssetDict);
            }

            //Not loading
            if (!loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
            {
                var assetBundleRequest = assetBundle.LoadAssetAsync(assetName, assetType);
                if (assetBundleRequest == null)
                {
                    Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null AssetBundleRequest!");
                    //Add null to the dictionary. When the same resource is loaded next time, null is returned directly.
                    if (!mAssetDicts.TryGetValue(bundleName, out assetDict))
                    {
                        assetDict = new Dictionary<AssetKey, Object>();
                        mAssetDicts.Add(bundleName, assetDict);
                    }

                    assetDict[assetKey] = null;
                    loaded?.Invoke(null);
                    return;
                }

                //Add to loading
                loadingAsset = (LoadingAssetBase) Activator.CreateInstance(
                    typeof(LoadingAsset<>).MakeGenericType(assetType), bundleName, assetName, assetBundleRequest);
            }

            loadingAsset.BaseCompleted += loaded;
        }

        #endregion

        #region Unload

        /// <summary>
        /// Unload AssetBundle.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        /// <param name="unloadAllLoadedObjects">Whether unload add loaded objects.</param>
        public static void UnloadAssetBundle(string bundleName, bool unloadAllLoadedObjects)
        {
            //Loading
            if (mLoadingAssetBundleDict.TryGetValue(bundleName, out var loadingAssetBundle))
                loadingAssetBundle.GetAssetBundle(); //Force sync

            //Clear caches first to avoid AssetBundle.Unload(false) can not unload them.
            ClearAllLoadedAssets(bundleName);
            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out var loadedAssetBundle))
                loadedAssetBundle.Unload(unloadAllLoadedObjects);
        }

        /// <summary>
        /// Unload all loaded assets.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        public static void UnloadAllLoadedAssets(string bundleName)
        {
            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
            {
                //Avoid mCacheLoadingAssetList be modified.
                if (mIsForceSyncLoadingAssets)
                {
                    Debug.LogWarning("Is unloading!");
                    return;
                }

                mIsForceSyncLoadingAssets = true;
                mCacheLoadingAssetList.Clear();
                mCacheLoadingAssetList.AddRange(loadingAssetDict.Values);
                foreach (var loadingAsset in mCacheLoadingAssetList)
                    loadingAsset.GetAsset(); //Force sync
                mCacheLoadingAssetList.Clear();
                mIsForceSyncLoadingAssets = false;
            }

            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            foreach (var asset in assetDict.Values)
                Resources.UnloadAsset(asset);
            assetDict.Clear();
            mAssetDicts.Remove(bundleName);
        }

        /// <summary>
        /// Unload loaded asset.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        /// <param name="assetName">Asset name.</param>
        /// <typeparam name="T">Asset type.</typeparam>
        public static void UnloadLoadedAsset<T>(string bundleName, string assetName) where T : Object
        {
            var assetKey = new AssetKey(typeof(T), assetName);
            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict) && loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
                loadingAsset.GetAsset(); //Force sync
            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            if (!assetDict.TryGetValue(assetKey, out var asset)) return;
            Resources.UnloadAsset(asset);
            assetDict.Remove(assetKey);
            if (assetDict.Count == 0)
                mAssetDicts.Remove(bundleName);
        }

        /// <summary>
        /// Clear all loaded assets.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        public static void ClearAllLoadedAssets(string bundleName)
        {
            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
            {
                //Avoid mCacheLoadingAssetList be modified.
                if (mIsForceSyncLoadingAssets)
                {
                    Debug.LogWarning("Is unloading!");
                    return;
                }

                mIsForceSyncLoadingAssets = true;
                mCacheLoadingAssetList.Clear();
                mCacheLoadingAssetList.AddRange(loadingAssetDict.Values);
                foreach (var loadingAsset in mCacheLoadingAssetList)
                    loadingAsset.GetAsset(); //Force sync
                mCacheLoadingAssetList.Clear();
                mIsForceSyncLoadingAssets = false;
            }

            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            assetDict.Clear();
            mAssetDicts.Remove(bundleName);
        }

        /// <summary>
        /// Clear loaded asset.
        /// </summary>
        /// <param name="bundleName">Bundle name without extension.</param>
        /// <param name="assetName">Asset name.</param>
        /// <typeparam name="T">Asset type.</typeparam>
        public static void ClearLoadedAsset<T>(string bundleName, string assetName) where T : Object
        {
            var assetKey = new AssetKey(typeof(T), assetName);
            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
                if (loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
                    loadingAsset.GetAsset(); //Force sync
            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            if (!assetDict.TryGetValue(assetKey, out var asset)) return;
            assetDict.Remove(assetKey);
            if (assetDict.Count == 0)
                mAssetDicts.Remove(bundleName);
        }

        #endregion

        #region Get Path/Name

        public static string GetAssetBundleName(string bundleName, bool autoAddExtension = true)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                Debug.LogError("Empty bundle name!");
                return bundleName;
            }

            if (autoAddExtension && HasBundleExtension && !bundleName.EndsWith(BundleExtension)) return bundleName + BundleExtension;
            return bundleName;
        }

        public static string GetAssetBundlePath(string bundleName, bool autoAddExtension = true)
        {
            return Path.Combine(AssetBundleRootPath, GetAssetBundleName(bundleName, autoAddExtension));
        }

        public static string GetAssetBundlePath(AssetBundleManagerSetting.LoadBundlePathMode loadBundlePathMode, string bundleName,
            bool autoAddExtension = true)
        {
            return Path.Combine(AssetBundleManagerSetting.GetLoadBundleFullPath(loadBundlePathMode, LoadBundlePath),
                GetAssetBundleName(bundleName, autoAddExtension));
        }

        public static string GetAssetBundlePath(string loadBundlePath, string bundleName, bool autoAddExtension = true)
        {
            return Path.Combine(Path.Combine(loadBundlePath, LoadBundlePath), GetAssetBundleName(bundleName, autoAddExtension));
        }

        #endregion

        #region ReplaceShader

#if UNITY_EDITOR
        //Materials loaded by AssetBundle in the editor environment need to refresh the shader.
        private static void ReplaceShader(Object item)
        {
            if (!item) return;
            if (item is GameObject go)
            {
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length <= 0) continue;
                    foreach (var mat in renderer.sharedMaterials)
                        ReplaceMaterialShader(mat);
                }

                foreach (var graphic in go.GetComponentsInChildren<Graphic>(true))
                    ReplaceMaterialShader(graphic.material);

                foreach (var particleSystemRenderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (particleSystemRenderer.sharedMaterials == null || particleSystemRenderer.sharedMaterials.Length <= 0) continue;
                    foreach (var mat in particleSystemRenderer.sharedMaterials)
                        ReplaceMaterialShader(mat);
                }

                foreach (var tmpText in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmpText.fontSharedMaterials == null || tmpText.fontSharedMaterials.Length <= 0) continue;
                    foreach (var mat in tmpText.fontSharedMaterials)
                        ReplaceMaterialShader(mat);
                }
            }
            else if (item is Material mat)
                ReplaceMaterialShader(mat);
        }

        public static void ReplaceSceneShader()
        {
            ReplaceMaterialShader(RenderSettings.skybox);

            foreach (var renderer in Object.FindObjectsOfType<Renderer>())
            {
                if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length <= 0) continue;
                foreach (var mat in renderer.sharedMaterials)
                    ReplaceMaterialShader(mat);
            }

            foreach (var graphic in Object.FindObjectsOfType<Graphic>())
                ReplaceMaterialShader(graphic.material);

            foreach (var particleSystemRenderer in Object.FindObjectsOfType<ParticleSystemRenderer>())
            {
                if (particleSystemRenderer.sharedMaterials == null || particleSystemRenderer.sharedMaterials.Length <= 0) continue;
                foreach (var mat in particleSystemRenderer.sharedMaterials)
                    ReplaceMaterialShader(mat);
            }

            foreach (var tmpText in Object.FindObjectsOfType<TMP_Text>())
            {
                if (tmpText.fontSharedMaterials == null || tmpText.fontSharedMaterials.Length <= 0) continue;
                foreach (var mat in tmpText.fontSharedMaterials)
                    ReplaceMaterialShader(mat);
            }
        }

        private static readonly PropertyInfo mRawRenderQueuePropertyInfo =
            typeof(Material).GetProperty("rawRenderQueue", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void ReplaceMaterialShader(Material mat)
        {
            if (!mat) return;
            if (mRawRenderQueuePropertyInfo != null)
            {
                //Get rawRenderQueue
                var value = mRawRenderQueuePropertyInfo.GetValue(mat);
                mat.shader = Shader.Find(mat.shader.name);
                //rawRenderQueue <= -1 means from shader.
                if (value is int rawRenderQueue && rawRenderQueue > -1)
                    mat.renderQueue = rawRenderQueue;
            }
            else
                mat.shader = Shader.Find(mat.shader.name);
        }
#endif

        #endregion
    }
}