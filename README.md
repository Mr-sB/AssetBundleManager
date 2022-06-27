# AssetBundleManager
AssetBundleManager can help you manage AssetBundles, such as synchronous loading, asynchronous loading, unloading and other features. And provide some convenient editor tools to build AssetBundles.

# Feature
* Load AssetBundles and assets by sync and async.
* Auto load all dependencies AssetBundles.
* Cache AssetBundles and assets to reduce repeat load time.
* Will not load duplicate AssetBundles.
* Easily unload AssetBundles and assets.
* Allow load or unload the same AssetBundle or Asset at the same time by sync and async.
* Fix materials loaded from `AssetBundle` in Editor mode will loss shader references.
* Provide editor tools to help you build AssetBundles easily.
* Provide FastMode loading option in editor mode, which load assets by AssetDatabase.

# Setup
* Click "Tools/AssetBundleManager" menu item to open AssetBundleManager setting window.
* Click "Setup AssetBundleManager" button to create a setting asset, then you can edit Build and Load options.

![image](https://github.com/Mr-sB/AssetBundleManager/raw/master/Screenshots/SettingWindow.png)

# Usage
* Use `AssetBundleManager` class to load/unload asset bundles and get/clear assets.
* `AssetBundleUtil` provides Download asset bundles and CopyFile/Folder methods.

# Note
Automatically manage the dependencies of AssetBundles. When an AssetBundle is unloaded(Call AssetBundleManager.UnloadAssetBundle(bundleName, unloadAllLoadedObjects) method.), 
it will traverse all the dependent AssetBundles: If the dependent AssetBundle is not loaded by LoadAssetBundle method explicitly, 
but loaded by another AssetBundle dependencies, the reference count will be reduced. When the reference count is 0, 
the dependent AssetBundle will be unloaded automatically.(Call AssetBundle.Unload(unloadAllLoadedObjects) method.)