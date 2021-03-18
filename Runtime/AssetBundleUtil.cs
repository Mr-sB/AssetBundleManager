using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace GameUtil
{
    public static class AssetBundleUtil
    {
        /// <summary>
        /// Download data from uri.
        /// </summary>
        /// <param name="uri">uri.</param>
        /// <param name="timeout">Timeout. Less equal 0 means infinity.</param>
        /// <param name="onCompleted">Callback when completed.</param>
        public static void DownloadData(Uri uri, int timeout = 0, Action<AsyncOperation> onCompleted = null)
        {
            UnityWebRequest webRequest = UnityWebRequest.Get(uri);
            if (timeout > 0)
                webRequest.timeout = timeout;
            var webRequestAsyncOperation = webRequest.SendWebRequest();
            webRequestAsyncOperation.completed += onCompleted;
        }

        public static byte[] GetDataFromUnityWebRequestAsyncOperation(AsyncOperation asyncOperation)
        {
            if (!TryGetUnityWebRequestFromAsyncOperation(asyncOperation, out var webRequest)) return null;
            var downloadHandler = webRequest.downloadHandler;
            if (downloadHandler == null)
            {
                Debug.LogError("DownloadHandler is null!");
                return null;
            }

            if (webRequest.isNetworkError || webRequest.isHttpError)
            {
                Debug.LogWarning(webRequest.error);
                return null;
            }

            return downloadHandler.data;
        }

        public static void DisposeUnityWebRequestByAsyncOperation(AsyncOperation asyncOperation)
        {
            if (TryGetUnityWebRequestFromAsyncOperation(asyncOperation, out var webRequest))
                webRequest.Dispose();
        }

        public static bool TryGetUnityWebRequestFromAsyncOperation(AsyncOperation asyncOperation, out UnityWebRequest webRequest)
        {
            webRequest = null;
            if (!(asyncOperation is UnityWebRequestAsyncOperation webRequestAsyncOperation))
            {
                Debug.LogError("AsyncOperation is not UnityWebRequestAsyncOperation!");
                return false;
            }

            webRequest = webRequestAsyncOperation.webRequest;
            if (webRequest == null)
            {
                Debug.LogError("UnityWebRequest is null!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copy all folders and all files deeply.
        /// </summary>
        /// <param name="sourceFolder">Source folder path.</param>
        /// <param name="destFolder">Destination folder path.</param>
        public static void CopyFolder(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(sourceFolder))
            {
                Debug.LogError($"SourceFolder: {sourceFolder} does not exist!");
                return;
            }

            try
            {
                if (!Directory.Exists(destFolder))
                    Directory.CreateDirectory(destFolder);

                //Get all files in top directory.
                string[] files = Directory.GetFiles(sourceFolder);
                foreach (string file in files)
                {
                    //Do not copy meta files and manifest files.
                    if (file.EndsWith(".meta") || file.EndsWith(".manifest")) continue;
                    string name = Path.GetFileName(file);
                    string dest = Path.Combine(destFolder, name);
                    File.Copy(file, dest, true); //Copy the file.
                }

                //Get all directories in top directory.
                string[] folders = Directory.GetDirectories(sourceFolder);
                foreach (string folder in folders)
                {
                    string name = Path.GetFileName(folder);
                    //Building the destination path.
                    string dest = Path.Combine(destFolder, name);
                    //Recursive copying.
                    CopyFolder(folder, dest);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        /// <summary>
        /// Copy file.
        /// </summary>
        /// <param name="sourceFile">Source file path.</param>
        /// <param name="destFile">Destination file path.</param>
        public static void CopyFile(string sourceFile, string destFile)
        {
            if (string.IsNullOrEmpty(sourceFile) || string.IsNullOrEmpty(destFile))
            {
                Debug.LogError($"File name IsNullOrEmpty, sourceFile: {sourceFile}, destFile: {destFile}.");
                return;
            }

            try
            {
                if (!File.Exists(sourceFile))
                {
                    Debug.LogError($"Source file does not exists, sourceFile: {sourceFile}");
                    return;
                }

                string directoryName = Path.GetDirectoryName(destFile);
                if (string.IsNullOrEmpty(directoryName))
                {
                    Debug.LogError($"Destination directory name IsNullOrEmpty, destFile: {destFile}");
                    return;
                }

                if (!Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName);
                File.Copy(sourceFile, destFile, true); //Copy the file.
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}