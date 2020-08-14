using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameUtil
{
    public class AssetBundleManager : MonoBehaviour
    {
        #region AssetKey
        //实现IEquatable<T>接口，避免在比较时装箱拆箱，产生GC
        private struct AssetKey : IEquatable<AssetKey>
        {
            public readonly Type PoolType;
            public readonly string AssetName;

            public AssetKey(Type poolType, string assetName)
            {
                PoolType = poolType;
                AssetName = assetName;
            }

            public bool Equals(AssetKey other)
            {
                return PoolType == other.PoolType && AssetName == other.AssetName;
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
                    return ((PoolType != null ? PoolType.GetHashCode() : 0) * 397) ^ (AssetName != null ? AssetName.GetHashCode() : 0);
                }
            }
        }
        #endregion
        
        #region LoadingAssetBundle
        private class LoadingAssetBundle
        {
            public readonly string BundleName;
            public event Action<AssetBundle> Completed;
            private readonly AssetBundleCreateRequest mAssetBundleCreateRequest;

            public LoadingAssetBundle(string bundleName, AssetBundleCreateRequest assetBundleCreateRequest)
            {
                BundleName = bundleName;
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
                mAssetBundleDict[BundleName] = assetBundle;
                mLoadingAssetBundleDict.Remove(BundleName);
                Completed?.Invoke(assetBundle);
            }
        }
        #endregion
        
        public const string ShaderBundleName = "shaders";
        public const string AssetBundleManagerSettingPath = "Assets/Resources/" + AssetBundleManagerSettingName + ".asset";
        public const string AssetBundleManagerSettingName = "AssetBundleManagerSetting";

        public static readonly AssetBundleManifest Manifest;
        private static readonly bool mHasBundleExtension;
        private static readonly string mBundleExtension;
        private static readonly string mAssetBundleRootPath;
        private static readonly Dictionary<string, AssetBundle> mAssetBundleDict = new Dictionary<string, AssetBundle>();
        private static readonly Dictionary<string, Dictionary<AssetKey, Object>> mAssetDicts = new Dictionary<string, Dictionary<AssetKey, Object>>();
        private static readonly Dictionary<string, LoadingAssetBundle> mLoadingAssetBundleDict = new Dictionary<string, LoadingAssetBundle>();

        #region Static Ctor
        static AssetBundleManager()
        {
            var setting = Resources.Load<AssetBundleManagerSetting>(AssetBundleManagerSettingName);
            string manifestBundleName; 
            if (setting)
            {
                mHasBundleExtension = setting.TryGetBundleExtension(out mBundleExtension);
                manifestBundleName = Path.GetFileNameWithoutExtension(setting.BuildBundlePath);
                mAssetBundleRootPath = setting.GetLoadBundleFullPath();
                Resources.UnloadAsset(setting);
            }
            else
            {
                mHasBundleExtension = false;
                mBundleExtension = null;
                manifestBundleName = string.Empty;
                mAssetBundleRootPath = string.Empty;
                Debug.LogError("Null AssetBundleManagerSetting!");
            }
            
            //Load AssetBundleManifest
            var manifestBundle = AssetBundle.LoadFromFile(GetAssetBundlePath(manifestBundleName, false));
            if (!manifestBundle)
                Debug.LogError($"Load ManifestBundle {manifestBundleName} error: Null AssetBundle!");
            else
            {
                Manifest = manifestBundle.LoadAsset<AssetBundleManifest>(nameof(AssetBundleManifest));
                if(!Manifest)
                    Debug.LogError($"Load AssetBundleManifest error: Null Asset!");
                manifestBundle.Unload(false);
            }
        }
        #endregion
        
        #region Instance
        private static AssetBundleManager instance;
        
        private static AssetBundleManager Instance
        {
            get
            {
                if (instance == null)
                {
                    //Find
                    instance = FindObjectOfType<AssetBundleManager>();
                    //Create
                    if (instance == null)
                    {
                        var go = new GameObject(nameof(AssetBundleManager));
                        instance = go.AddComponent<AssetBundleManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }
        #endregion
        
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
            if(mAssetBundleDict.TryGetValue(bundleName, out var assetBundle)) return assetBundle;
            LoadingAssetBundle loadingAssetBundle;
            if (Manifest)
            {
                //Load all dependencies AssetBundle
                string[] dependencies = Manifest.GetAllDependencies(GetAssetBundleName(bundleName));
                for (int i = 0; i < dependencies.Length; i++)
                {
                    var dependencyBundleName = dependencies[i];
                    if (dependencyBundleName == null)
                    {
                        Debug.LogError($"Load AssetBundle {bundleName} error: Dependency name is Null!");
                        continue;
                    }

                    var bundleNameWithoutExtension = Path.GetFileNameWithoutExtension(dependencyBundleName);
                    //Already loaded
                    if (mAssetBundleDict.ContainsKey(bundleNameWithoutExtension)) continue;
                    //Loading
                    if (mLoadingAssetBundleDict.TryGetValue(bundleNameWithoutExtension, out loadingAssetBundle))
                    {
                        loadingAssetBundle.GetAssetBundle();//Force load sync
                        continue;
                    }
                    var dependencyBundle = AssetBundle.LoadFromFile(GetAssetBundlePath(dependencyBundleName, false));
                    if (!dependencyBundle)
                        Debug.LogError($"Load AssetBundle {bundleName} error: Load dependency bundle {dependencyBundleName} Null AssetBundle!");
                    mAssetBundleDict.Add(bundleNameWithoutExtension, dependencyBundle);
                }
            }

            //Double check
            if(mAssetBundleDict.TryGetValue(bundleName, out assetBundle)) return assetBundle;
            if (mLoadingAssetBundleDict.TryGetValue(bundleName, out loadingAssetBundle))
                return loadingAssetBundle.GetAssetBundle();//Force load sync
            assetBundle = AssetBundle.LoadFromFile(GetAssetBundlePath(bundleName));
            if(!assetBundle)
                Debug.LogError($"Load AssetBundle {bundleName} error: Null AssetBundle!");
            mAssetBundleDict.Add(bundleName, assetBundle);
            return assetBundle;
        }

        /// <summary>
        /// 异步加载AssetBundle
        /// </summary>
        /// <param name="bundleName">无后缀的BundleName</param>
        /// <param name="onLoaded">回调函数</param>
        public static void LoadAssetBundleAsync(string bundleName, Action<AssetBundle> onLoaded)
        {
            //Already loaded
            if (mAssetBundleDict.TryGetValue(bundleName, out var assetBundle))
            {
                onLoaded?.Invoke(assetBundle);
                return;
            }
            //Loading
            if (mLoadingAssetBundleDict.TryGetValue(bundleName, out var loadingAssetBundle))
            {
                loadingAssetBundle.Completed += onLoaded;
                return;
            }
            Instance.StartCoroutine(LoadAssetBundleAsyncInternal(bundleName, onLoaded));
        }

        private static IEnumerator LoadAssetBundleAsyncInternal(string bundleName, Action<AssetBundle> onLoaded)
        {
            int loadingCount = 0;
            LoadingAssetBundle loadingAssetBundle;
            if (Manifest)
            {
                //Load all dependencies AssetBundle
                string[] dependencies = Manifest.GetAllDependencies(GetAssetBundleName(bundleName));
                for (int i = 0; i < dependencies.Length; i++)
                {
                    var dependencyBundleName = dependencies[i];
                    if (dependencyBundleName == null)
                    {
                        Debug.LogError($"Load LoadAssetBundleAsync {bundleName} error: Dependency name is Null!");
                        continue;
                    }

                    var bundleNameWithoutExtension = Path.GetFileNameWithoutExtension(dependencyBundleName);
                    //Already loaded
                    if (mAssetBundleDict.ContainsKey(bundleNameWithoutExtension)) continue;
                    loadingCount++;
                    //Not loading
                    if (!mLoadingAssetBundleDict.TryGetValue(bundleNameWithoutExtension, out loadingAssetBundle))
                    {
                        //Add to loading
                        loadingAssetBundle = new LoadingAssetBundle(bundleNameWithoutExtension,
                            AssetBundle.LoadFromFileAsync(GetAssetBundlePath(dependencyBundleName, false)));
                    }
                    loadingAssetBundle.Completed += _ => loadingCount--;
                }
            }
            //Not loading
            if (!mLoadingAssetBundleDict.TryGetValue(bundleName, out loadingAssetBundle))
            {
                var assetBundleCreateRequest = AssetBundle.LoadFromFileAsync(GetAssetBundlePath(bundleName));
                if (assetBundleCreateRequest == null)
                {
                    Debug.LogError($"Load LoadAssetBundleAsync {bundleName} error: Null AssetBundleCreateRequest!");
                    mAssetBundleDict[bundleName] = null;
                    onLoaded?.Invoke(null);
                    yield break;
                }
                //Add to loading
                loadingAssetBundle = new LoadingAssetBundle(bundleName, assetBundleCreateRequest);
            }
            loadingCount++;
            loadingAssetBundle.Completed += _ => loadingCount--;
            //Wait for loading finished.
            while (loadingCount > 0) yield return null;
            onLoaded?.Invoke(mAssetBundleDict[bundleName]);
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
            AssetKey assetKey = new AssetKey(typeof(T), assetName);
            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict))
            {
                assetDict = new Dictionary<AssetKey, Object>();
                mAssetDicts.Add(bundleName, assetDict);
            }
            if (assetDict.TryGetValue(assetKey, out var asset)) return (T) asset;

            var assetBundle = LoadAssetBundle(bundleName);
            //Bundle为null，返回null对象
            if (!assetBundle)
            {
                Debug.LogError($"GetAsset {assetName} from {bundleName} error: Null AssetBundle!");
                //添加null对象，下次再加载同样的资源直接返回null
                assetDict.Add(assetKey, null);
                return null;
            }
            
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
            AssetKey assetKey = new AssetKey(typeof(T), assetName);
            if (!mAssetDicts.TryGetValue(bundleName, out var assetDict))
            {
                assetDict = new Dictionary<AssetKey, Object>();
                mAssetDicts.Add(bundleName, assetDict);
            }
            if (assetDict.TryGetValue(assetKey, out var asset))
            {
                onLoaded?.Invoke((T) asset);
                return;
            }

            if (mAssetBundleDict.TryGetValue(bundleName, out var assetBundle))
            {
                //Bundle为null，返回null对象
                if (!assetBundle)
                {
                    Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null AssetBundle!");
                    //添加null对象，下次再加载同样的资源直接返回null
                    assetDict.Add(assetKey, null);
                    onLoaded?.Invoke(null);
                    return;
                }
                Instance.StartCoroutine(GetAssetAsyncInternal(assetBundle, bundleName, assetName, onLoaded));
            }
            else
            {
                var param1 = bundleName;
                var param2 = assetName;
                var param3 = onLoaded;
                Instance.StartCoroutine(LoadAssetBundleAsyncInternal(bundleName,
                    bundle => { Instance.StartCoroutine(GetAssetAsyncInternal(bundle, param1, param2, param3)); }));
            }
        }

        private static IEnumerator GetAssetAsyncInternal<T>(AssetBundle assetBundle, string bundleName, string assetName, Action<T> onLoaded) where T : Object
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
                yield break;
            }
            
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
                yield break;
            }
            yield return assetBundleRequest;
            if (!assetBundleRequest.isDone)
                Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: AssetBundleRequest is not done!");

            var asset = assetBundleRequest.asset;
            if(!asset)
                Debug.LogError($"GetAssetAsync {assetName} from {bundleName} error: Null Asset!");
            if (!mAssetDicts.TryGetValue(bundleName, out assetDict))
            {
                assetDict = new Dictionary<AssetKey, Object>();
                mAssetDicts.Add(bundleName, assetDict);
            }
            assetDict[assetKey] = asset;
#if UNITY_EDITOR
            ReplaceShader(asset);
#endif
            onLoaded?.Invoke((T) asset);
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
            if (mAssetBundleDict.TryGetValue(bundleName, out var assetBundle))
            {
                mAssetBundleDict.Remove(bundleName);
                if(assetBundle)
                    assetBundle.Unload(unloadAllLoadedObjects);
            }

            if (unloadAllLoadedObjects)
                UnloadAllLoadedAssets(bundleName);

            Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 卸载AssetBundle所有已加载的资源
        /// </summary>
        /// <param name="bundleName">无后缀的BundleName</param>
        public static void UnloadAllLoadedAssets(string bundleName)
        {
            if(!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            foreach (var asset in assetDict.Values)
                Resources.UnloadAsset(asset);
            assetDict.Clear();
            mAssetDicts.Remove(bundleName);
            Resources.UnloadUnusedAssets();
        }

        public static void UnloadLoadedAssets<T>(string bundleName, string assetName) where T : Object
        {
            if(!mAssetDicts.TryGetValue(bundleName, out var assetDict)) return;
            var assetKey = new AssetKey(typeof(T), assetName);
            if(!assetDict.TryGetValue(assetKey, out var asset)) return;
            Resources.UnloadAsset(asset);
            assetDict.Remove(assetKey);
            if(assetDict.Count == 0)
                mAssetDicts.Remove(bundleName);
            Resources.UnloadUnusedAssets();
        }
        #endregion
        
        private static string GetAssetBundleName(string bundleName, bool autoAddExtension = true)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                Debug.LogError("Empty bundle name!");
                return bundleName;
            }
            if (autoAddExtension && mHasBundleExtension && !bundleName.EndsWith(mBundleExtension)) return bundleName + mBundleExtension;
            return bundleName;
        }

        private static string GetAssetBundlePath(string bundleName, bool autoAddExtension = true)
        {
            return Path.Combine(mAssetBundleRootPath, GetAssetBundleName(bundleName, autoAddExtension));
        }

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

            foreach (var renderer in FindObjectsOfType<Renderer>())
            {
                if(renderer.sharedMaterials == null || renderer.sharedMaterials.Length <= 0) continue;
                foreach (var mat in renderer.sharedMaterials)
                    ReplaceMaterialShader(mat);
            }

            foreach (var graphic in FindObjectsOfType<Graphic>())
                ReplaceMaterialShader(graphic.material);
            
            foreach (var particleSystemRenderer in FindObjectsOfType<ParticleSystemRenderer>())
            {
                if(particleSystemRenderer.sharedMaterials == null || particleSystemRenderer.sharedMaterials.Length <= 0) continue;
                foreach (var mat in particleSystemRenderer.sharedMaterials)
                    ReplaceMaterialShader(mat);
            }
            
            foreach (var tmpText in FindObjectsOfType<TMP_Text>())
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