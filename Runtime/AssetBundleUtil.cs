using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GameUtil
{
    public static class AssetBundleUtil
    {
        /// <summary>
        /// Download data from uri.
        /// </summary>
        /// <param name="uri">The target URI to which form data will be transmitted.</param>
        /// <param name="timeout">Timeout. Less equal 0 means infinity.</param>
        /// <param name="completed">Callback when completed.</param>
        public static UnityWebRequest DownloadData(Uri uri, int timeout = 0, Action<AsyncOperation> completed = null)
        {
            UnityWebRequest webRequest = UnityWebRequest.Get(uri);
            if (timeout > 0)
                webRequest.timeout = timeout;
            var webRequestAsyncOperation = webRequest.SendWebRequest();
            webRequestAsyncOperation.completed += completed;
            return webRequest;
        }
        
        /// <summary>
        /// Download file from uri.
        /// </summary>
        /// <param name="uri">The target URI to which form data will be transmitted.</param>
        /// <param name="path">Path to file to be written.</param>
        /// <param name="append">When true, appends data to the given file instead of overwriting.</param>
        /// <param name="timeout">Timeout. Less equal 0 means infinity.</param>
        /// <param name="completed">Callback when completed.</param>
        /// <returns></returns>
        public static UnityWebRequest DownloadFile(Uri uri, string path, bool append, int timeout = 0, Action<AsyncOperation> completed = null)
        {
            UnityWebRequest webRequest = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbGET, new DownloadHandlerFile(path, append), null);
            if (timeout > 0)
                webRequest.timeout = timeout;
            var webRequestAsyncOperation = webRequest.SendWebRequest();
            webRequestAsyncOperation.completed += completed;
            return webRequest;
        }

        public static bool TryGetDataFromUnityWebRequestAsyncOperation(AsyncOperation asyncOperation, out byte[] data)
        {
            data = null;
            if (!TryGetUnityWebRequestFromAsyncOperation(asyncOperation, out var webRequest)) return false;
            var downloadHandler = webRequest.downloadHandler;
            if (downloadHandler == null)
            {
                Debug.LogError("DownloadHandler is null!");
                return false;
            }
            data = downloadHandler.data;
#if UNITY_2020_1_OR_NEWER
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(webRequest.error);
                    return false;
            }
#else
            if (webRequest.isNetworkError || webRequest.isHttpError)
            {
                Debug.LogError(webRequest.error);
                return false;
            }
#endif
            return true;
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

        public static string GetFileHash(string filePath, MD5 md5 = null, StringBuilder sb = null)
        {
            byte[] hashCode;
            try
            {
                FileStream fileStream = new FileStream(filePath, FileMode.Open);
                if (md5 == null)
                    md5 = new MD5CryptoServiceProvider();
                hashCode = md5.ComputeHash(fileStream);
                fileStream.Close();
            }
            catch (Exception e)
            {
                Debug.LogError("ComputeHash failed! filePath: " + filePath);
                Debug.LogError(e);
                return null;
            }
            if (sb == null)
                sb = new StringBuilder();
            sb.Clear();
            foreach (var b in hashCode)
                sb.Append(b.ToString("x2"));
            string hash = sb.ToString();
            sb.Clear();
            return hash;
        }
    }
}