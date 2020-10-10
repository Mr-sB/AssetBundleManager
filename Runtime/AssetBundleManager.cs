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
        //实现IEquatable<T>接口，避免在比较时装箱拆箱，产生GC
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
        [RuntimeInitializeOnLoadMethod]
        private static void RecordAssets()
        {
            if(!mFastMode) return;
            if(mAllAssetsPath == null)
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
            if (!mAllAssetsPath.TryGetValue(bundleName, out var paths))
            {
                Debug.LogError($"GetAssetFastMode {assetName} from {bundleName} error: Null Directory!");
                return null;
            }
            foreach (var path in paths)
            {
                bool isEqual = path.EndsWith(assetName);
                if (!isEqual)
                {
                    int index;
                    if ((index = path.LastIndexOf('.')) != -1)
                    {
                        var pathWithoutExtension = path.Substring(0, index);
                        isEqual = pathWithoutExtension.EndsWith(assetName);
                    }
                }
                if (!isEqual) continue;
                
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
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
            public string[] DirectDependencies { private set; get; }
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

            public void SetAssetBundle(AssetBundle assetBundle)
            {
                if(AssetBundle == assetBundle) return;
                AssetBundle = assetBundle;
                var directDependencies = Manifest.GetDirectDependencies(AssetBundle.name);
                var len = directDependencies.Length;
                if(DirectDependencies == null || DirectDependencies.Length != len)
                    DirectDependencies = new string[len];
                for (int i = 0; i < len; i++)
                    DirectDependencies[i] = Path.GetFileNameWithoutExtension(directDependencies[i]);
            }

            public void AddReference(string referenceBundleName)
            {
                if(!mAutoUnload) return;
                //Explicit load the AssetBundle, can not auto unload.
                if (BundleName == referenceBundleName)
                {
                    mAutoUnload = false;
                    return;
                }
                if (!mReferences.Contains(referenceBundleName))
                    mReferences.Add(referenceBundleName);
            }

            /// <param name="unloadAllLoadedObjects">是否卸载所有已加载的Objects</param>
            public void Unload(bool unloadAllLoadedObjects)
            {
                mLoadedAssetBundleDict.Remove(BundleName);
                if(AssetBundle)
                    AssetBundle.Unload(unloadAllLoadedObjects);
                for (int i = 0, len = DirectDependencies.Length; i < len; i++)
                {
                    if(!mLoadedAssetBundleDict.TryGetValue(DirectDependencies[i], out var loadedAssetBundle)) continue;
                    loadedAssetBundle.RemoveReference(BundleName, unloadAllLoadedObjects);
                }
            }

            private void RemoveReference(string referenceBundleName, bool unloadAllLoadedObjects)
            {
                if(!mAutoUnload) return;
                if (mReferences.Contains(referenceBundleName))
                    mReferences.Remove(referenceBundleName);
                //References count is 0, auto unload.
                if (mReferences.Count == 0)
                    Unload(unloadAllLoadedObjects);
            }
        }
        #endregion
        
        #region LoadingAssetBundle
        private class LoadingAssetBundle
        {
            public readonly string BundleName;
            public readonly string ReferenceBundleName;
            public event Action<AssetBundle> Completed;
            private readonly AssetBundleCreateRequest mAssetBundleCreateRequest;

            public LoadingAssetBundle(string bundleName, string referenceBundleName, AssetBundleCreateRequest assetBundleCreateRequest)
            {
                BundleName = bundleName;
                ReferenceBundleName = referenceBundleName;
                mAssetBundleCreateRequest = assetBundleCreateRequest;
                mAssetBundleCreateRequest.completed += OnCompleted;
                mLoadingAssetBundleDict.Add(BundleName, this);
            }

            public AssetBundle GetAssetBundle()
            {
                return mAssetBundleCreateRequest.assetBundle;
            }

            private void OnCompleted(AsyncOperation operation)
            {
                var assetBundle = mAssetBundleCreateRequest.assetBundle;
                if (!assetBundle)
                    Debug.LogError($"Load LoadAssetBundleAsync {BundleName} error: Null AssetBundle!");
                if (!mLoadedAssetBundleDict.TryGetValue(BundleName, out var loadedAssetBundle))
                {
                    loadedAssetBundle = new LoadedAssetBundle(BundleName, assetBundle, ReferenceBundleName);
                    mLoadedAssetBundleDict.Add(BundleName, loadedAssetBundle);
                }
                else
                {
                    loadedAssetBundle.SetAssetBundle(assetBundle);
                    //Add references
                    loadedAssetBundle.AddReference(ReferenceBundleName);
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
                OnCompleted(asset);
            }
        }
        
        private class LoadingAsset<T> : LoadingAssetBase where T : Object
        {
            public event Action<T> Completed;

            public LoadingAsset(string bundleName, string assetName, AssetBundleRequest assetBundleRequest) : base(bundleName, new AssetKey(typeof(T), assetName), assetBundleRequest)
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
        private static readonly Dictionary<string, Dictionary<AssetKey, LoadingAssetBase>> mLoadingAssetDicts = new Dictionary<string, Dictionary<AssetKey, LoadingAssetBase>>();
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
                    if(!mManifest)
                        Debug.LogError($"Load AssetBundleManifest error: Null Asset!");
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

        public static void LoadShaderAssetBundleAsync(Action onLoaded)
        {
            LoadAssetBundleAsync(ShaderBundleName, _ => { onLoaded?.Invoke(); });
        }

        public static Shader FindShader(string shaderName)
        {
            var shader = GetAsset<Shader>(ShaderBundleName, shaderName);
            return shader ? shader : Shader.Find(shaderName);
        }
        #endregion

        #region LoadAssetBundle
        /// <summary>
        /// 同步加载AssetBundle
        /// </summary>
        /// <param name="bundleName">无后缀的BundleName</param>
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
                    if (mLoadedAssetBundleDict.ContainsKey(bundleNameWithoutExtension)) continue;
                    //Loading
                    if (mLoadingAssetBundleDict.TryGetValue(bundleNameWithoutExtension, out loadingAssetBundle))
                    {
                        loadingAssetBundle.GetAssetBundle();//Force load sync
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
                return loadingAssetBundle.GetAssetBundle();//Force load sync
            var assetBundle = AssetBundle.LoadFromFile(GetAssetBundlePath(bundleName));
            if(!assetBundle)
                Debug.LogError($"Load AssetBundle {bundleName} error: Null AssetBundle!");
            mLoadedAssetBundleDict.Add(bundleName, new LoadedAssetBundle(bundleName, assetBundle, bundleName));
            return assetBundle;
        }

        /// <summary>
        /// 异步加载AssetBundle
        /// </summary>
        /// <param name="bundleName">无后缀的BundleName</param>
        /// <param name="onLoaded">回调函数</param>
        public static void LoadAssetBundleAsync(string bundleName, Action<AssetBundle> onLoaded)
        {
#if UNITY_EDITOR
            if (mFastMode)
            {
                onLoaded?.Invoke(null);
                return;
            }
#endif
            //Already loaded
            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out var loadedAssetBundle))
            {
                //Add Reference
                loadedAssetBundle.AddReference(bundleName);
                onLoaded?.Invoke(loadedAssetBundle.AssetBundle);
                return;
            }
            //Loading
            if (mLoadingAssetBundleDict.TryGetValue(bundleName, out var loadingAssetBundle))
            {
                loadingAssetBundle.Completed += onLoaded;
                return;
            }
            LoadAssetBundleAsyncInternal(bundleName, onLoaded);
        }

        private static void LoadAssetBundleAsyncInternal(string bundleName, Action<AssetBundle> onLoaded)
        {
            int loadingCount = 1;
            void OnCompleted(AssetBundle _)
            {
                loadingCount--;
                if (loadingCount <= 0)
                    onLoaded?.Invoke(mLoadedAssetBundleDict[bundleName].AssetBundle);
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
                        Debug.LogError($"Load LoadAssetBundleAsync {bundleName} error: Dependency name is Null!");
                        continue;
                    }

                    var bundleNameWithoutExtension = Path.GetFileNameWithoutExtension(dependencyBundleName);
                    //Already loaded
                    if (mLoadedAssetBundleDict.ContainsKey(bundleNameWithoutExtension)) continue;
                    loadingCount++;
                    //Not loading
                    if (!mLoadingAssetBundleDict.TryGetValue(bundleNameWithoutExtension, out loadingAssetBundle))
                    {
                        //Add to loading
                        loadingAssetBundle = new LoadingAssetBundle(bundleNameWithoutExtension, bundleName,
                            AssetBundle.LoadFromFileAsync(GetAssetBundlePath(dependencyBundleName, false)));
                    }
                    loadingAssetBundle.Completed += OnCompleted;
                }
            }
            
            //Double check
            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out var loadedAssetBundle))
            {
                //Add Reference
                loadedAssetBundle.AddReference(bundleName);
                onLoaded?.Invoke(loadedAssetBundle.AssetBundle);
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
                    onLoaded?.Invoke(null);
                    return;
                }
                //Add to loading
                loadingAssetBundle = new LoadingAssetBundle(bundleName, bundleName, assetBundleCreateRequest);
            }
            loadingAssetBundle.Completed += OnCompleted;
        }
        #endregion

        #region GetAsset
        /// <summary>
        /// 同步获取场景资源
        /// </summary>
        /// <param name="bundleName">无后缀的BundleName</param>
        /// <param name="assetName">资源名称</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns>加载的资源</returns>
        public static T GetAsset<T>(string bundleName, string assetName) where T : Object
        {
#if UNITY_EDITOR
            if (mFastMode)
                return GetAssetFastMode<T>(bundleName, assetName);
#endif
            AssetKey assetKey = new AssetKey(typeof(T), assetName);
            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict))
            {
                assetDict = new Dictionary<AssetKey, Object>();
                mAssetDicts.Add(bundleName, assetDict);
            }
            //Already loaded
            if (assetDict.TryGetValue(assetKey, out var asset)) return (T) asset;
            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
            {
                if (loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
                    return (T) loadingAsset.GetAsset();//Force load sync
            }

            var assetBundle = LoadAssetBundle(bundleName);
            //Bundle为null，返回null对象
            if (!assetBundle)
            {
                Debug.LogError($"GetAsset {assetName} from {bundleName} error: Null AssetBundle!");
                //添加null对象，下次再加载同样的资源直接返回null
                assetDict.Add(assetKey, null);
                return null;
            }
            
            //Double Check
            if (assetDict.TryGetValue(assetKey, out asset)) return (T) asset;
            asset = assetBundle.LoadAsset<T>(assetName);
            if(!asset)
                Debug.LogError($"GetAsset {assetName} from {bundleName} error: Null Asset!");
            assetDict.Add(assetKey, asset);
#if UNITY_EDITOR
            ReplaceShader(asset);
#endif
            return (T) asset;
        }

        public static void GetAssetAsync<T>(string bundleName, string assetName, Action<T> onLoaded) where T : Object
        {
#if UNITY_EDITOR
            if (mFastMode)
            {
                var obj = GetAssetFastMode<T>(bundleName, assetName);
                onLoaded?.Invoke(obj);
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
                onLoaded?.Invoke((T) asset);
                return;
            }
            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
            {
                if (loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
                {
                    ((LoadingAsset<T>)loadingAsset).Completed += onLoaded;
                    return;
                }
            }
            
            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out var loadedAssetBundle))
            {
                //Bundle为null，返回null对象
                if (!loadedAssetBundle.AssetBundle)
                {
                    Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null AssetBundle!");
                    //添加null对象，下次再加载同样的资源直接返回null
                    assetDict.Add(assetKey, null);
                    onLoaded?.Invoke(null);
                    return;
                }
                GetAssetAsyncInternal(loadedAssetBundle.AssetBundle, bundleName, assetName, onLoaded);
            }
            else
            {
                var param1 = bundleName;
                var param2 = assetName;
                var param3 = onLoaded;
                LoadAssetBundleAsyncInternal(bundleName,
                    bundle => { GetAssetAsyncInternal(bundle, param1, param2, param3); });
            }
        }

        private static void GetAssetAsyncInternal<T>(AssetBundle assetBundle, string bundleName, string assetName, Action<T> onLoaded) where T : Object
        {
            AssetKey assetKey = new AssetKey(typeof(T), assetName);
            Dictionary<AssetKey, Object> assetDict;
            //Bundle为null，返回null对象
            if (!assetBundle)
            {
                Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null AssetBundle!");
                //添加null对象，下次再加载同样的资源直接返回null
                if (!mAssetDicts.TryGetValue(bundleName, out assetDict))
                {
                    assetDict = new Dictionary<AssetKey, Object>();
                    mAssetDicts.Add(bundleName, assetDict);
                }
                assetDict[assetKey] = null;
                onLoaded?.Invoke(null);
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
                    //添加null对象，下次再加载同样的资源直接返回null
                    if (!mAssetDicts.TryGetValue(bundleName, out assetDict))
                    {
                        assetDict = new Dictionary<AssetKey, Object>();
                        mAssetDicts.Add(bundleName, assetDict);
                    }
                    assetDict[assetKey] = null;
                    onLoaded?.Invoke(null);
                    return;
                }
                //Add to loading
                loadingAsset = new LoadingAsset<T>(bundleName, assetName, assetBundleRequest);
            }
            ((LoadingAsset<T>) loadingAsset).Completed += onLoaded;
        }
        #endregion

        #region Unload
        /// <summary>
        /// 卸载AssetBundle
        /// </summary>
        /// <param name="bundleName">无后缀的BundleName</param>
        /// <param name="unloadAllLoadedObjects">是否卸载所有已加载的Objects</param>
        public static void UnloadAssetBundle(string bundleName, bool unloadAllLoadedObjects)
        {
            //Loading
            if(mLoadingAssetBundleDict.TryGetValue(bundleName, out var loadingAssetBundle))
                loadingAssetBundle.GetAssetBundle();//Force sync
            
            //先Clear cache，避免AssetBundle.Unload(false)无法卸载cache的资源
            ClearAllLoadedAssets(bundleName);
            if (mLoadedAssetBundleDict.TryGetValue(bundleName, out var loadedAssetBundle))
                loadedAssetBundle.Unload(unloadAllLoadedObjects);
        }

        /// <summary>
        /// 卸载AssetBundle所有已加载的资源
        /// </summary>
        /// <param name="bundleName">无后缀的BundleName</param>
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
            if(!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            foreach (var asset in assetDict.Values)
                Resources.UnloadAsset(asset);
            assetDict.Clear();
            mAssetDicts.Remove(bundleName);
        }

        /// <summary>
        /// 卸载AssetBundle指定已加载的资源
        /// </summary>
        /// <param name="bundleName"></param>
        /// <param name="assetName"></param>
        /// <typeparam name="T"></typeparam>
        public static void UnloadLoadedAsset<T>(string bundleName, string assetName) where T : Object
        {
            var assetKey = new AssetKey(typeof(T), assetName);
            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
                if(loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
                    loadingAsset.GetAsset();//Force sync
            if(!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            if(!assetDict.TryGetValue(assetKey, out var asset)) return;
            Resources.UnloadAsset(asset);
            assetDict.Remove(assetKey);
            if(assetDict.Count == 0)
                mAssetDicts.Remove(bundleName);
        }

        /// <summary>
        /// 清除AssetBundle所有Cache的资源
        /// </summary>
        /// <param name="bundleName">无后缀的BundleName</param>
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
            if(!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            assetDict.Clear();
            mAssetDicts.Remove(bundleName);
        }
        
        /// <summary>
        /// 清除AssetBundle指定Cache的资源
        /// </summary>
        /// <param name="bundleName"></param>
        /// <param name="assetName"></param>
        /// <typeparam name="T"></typeparam>
        public static void ClearLoadedAsset<T>(string bundleName, string assetName) where T : Object
        {
            var assetKey = new AssetKey(typeof(T), assetName);
            //Loading
            if (mLoadingAssetDicts.TryGetValue(bundleName, out var loadingAssetDict))
                if(loadingAssetDict.TryGetValue(assetKey, out var loadingAsset))
                    loadingAsset.GetAsset();//Force sync
            if(!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            if(!assetDict.TryGetValue(assetKey, out var asset)) return;
            assetDict.Remove(assetKey);
            if(assetDict.Count == 0)
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
        
        public static string GetAssetBundlePath(AssetBundleManagerSetting.LoadBundlePathMode loadBundlePathMode, string bundleName, bool autoAddExtension = true)
        {
            return Path.Combine(AssetBundleManagerSetting.GetLoadBundleFullPath(loadBundlePathMode, LoadBundlePath), GetAssetBundleName(bundleName, autoAddExtension));
        }
        
        public static string GetAssetBundlePath(string loadBundlePath, string bundleName, bool autoAddExtension = true)
        {
            return Path.Combine(Path.Combine(loadBundlePath, LoadBundlePath), GetAssetBundleName(bundleName, autoAddExtension));
        }
        #endregion

        #region ReplaceShader
#if UNITY_EDITOR
        //Editor下通过AssetBundle加载出来的材质需要刷新一下shader
        private static void ReplaceShader(Object item)
        {
            if(!item) return;
            if (item is GameObject go)
            {
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer.sharedMaterials == null || renderer.sharedMaterials.Length <= 0) continue;
                    foreach (var mat in renderer.sharedMaterials)
                        ReplaceMaterialShader(mat);
                }
                
                foreach (var graphic in go.GetComponentsInChildren<Graphic>(true))
                    ReplaceMaterialShader(graphic.material);
                
                foreach (var particleSystemRenderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if(particleSystemRenderer.sharedMaterials == null || particleSystemRenderer.sharedMaterials.Length <= 0) continue;
                    foreach (var mat in particleSystemRenderer.sharedMaterials)
                        ReplaceMaterialShader(mat);
                }
                
                foreach (var tmpText in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    if(tmpText.fontSharedMaterials == null || tmpText.fontSharedMaterials.Length <= 0) continue;
                    foreach (var mat in tmpText.fontSharedMaterials)
                        ReplaceMaterialShader(mat);
                }
            }
            else if(item is Material mat)
                ReplaceMaterialShader(mat);
        }

        public static void ReplaceSceneShader()
        {
            ReplaceMaterialShader(RenderSettings.skybox);

            foreach (var renderer in Object.FindObjectsOfType<Renderer>())
            {
                if(renderer.sharedMaterials == null || renderer.sharedMaterials.Length <= 0) continue;
                foreach (var mat in renderer.sharedMaterials)
                    ReplaceMaterialShader(mat);
            }

            foreach (var graphic in Object.FindObjectsOfType<Graphic>())
                ReplaceMaterialShader(graphic.material);
            
            foreach (var particleSystemRenderer in Object.FindObjectsOfType<ParticleSystemRenderer>())
            {
                if(particleSystemRenderer.sharedMaterials == null || particleSystemRenderer.sharedMaterials.Length <= 0) continue;
                foreach (var mat in particleSystemRenderer.sharedMaterials)
                    ReplaceMaterialShader(mat);
            }
            
            foreach (var tmpText in Object.FindObjectsOfType<TMP_Text>())
            {
                if(tmpText.fontSharedMaterials == null || tmpText.fontSharedMaterials.Length <= 0) continue;
                foreach (var mat in tmpText.fontSharedMaterials)
                    ReplaceMaterialShader(mat);
            }
        }

        private static readonly PropertyInfo mRawRenderQueuePropertyInfo =
            typeof(Material).GetProperty("rawRenderQueue", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void ReplaceMaterialShader(Material mat)
        {
            if(!mat) return;
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