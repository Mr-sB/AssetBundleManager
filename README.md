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

# Setup
* Click "Tools/AssetBundleManager" menu item to open AssetBundleManager setting window.
* Click "Setup AssetBundleManager" button to create a setting asset, then you can edit Build and Load options.

# Usage
* Use `AssetBundleManager` class to load/unload asset bundles and get/clear assets.
* `AssetBundleUtil` provides Download asset bundles and CopyFile/Folder methods.