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
        /// <param name="uri">uri</param>
        /// <param name="timeout">LessEqual 0 means infinity</param>
        /// <param name="onCompleted">completed callback</param>
        public static void DownloadData(string uri, int timeout = 0, Action<AsyncOperation> onCompleted = null)
        {
            UnityWebRequest webRequest = UnityWebRequest.Get(uri);
            if(timeout > 0)
                webRequest.timeout = timeout;
            var webRequestAsyncOperation = webRequest.SendWebRequest();
            webRequestAsyncOperation.completed += onCompleted;
        }

        public static void DownloadDataFromLocalPath(string localPath, Action<AsyncOperation> onCompleted)
        {
#if UNITY_EDITOR || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN
            if (!localPath.StartsWith("file://"))
                localPath = "file://" + localPath;
#endif
            DownloadData(localPath, 0, onCompleted);
        }

        public static byte[] GetDataFromUnityWebRequestAsyncOperation(AsyncOperation asyncOperation)
        {
            if (!(asyncOperation is UnityWebRequestAsyncOperation webRequestAsyncOperation))
            {
                Debug.LogError("AsyncOperation is not UnityWebRequestAsyncOperation!");
                return null;
            }
            var webRequest = webRequestAsyncOperation.webRequest;
            if (webRequest == null)
            {
                Debug.LogError("UnityWebRequest is null");
                return null;
            }
            if (webRequest.isNetworkError || webRequest.isHttpError)
            {
                Debug.LogWarning(webRequest.error);
                return null;
            }
            return webRequest.downloadHandler.data;
        }
        
        /// <summary>
        /// 复制文件夹及文件
        /// </summary>
        /// <param name="sourceFolder">原文件路径</param>
        /// <param name="destFolder">目标文件路径</param>
        public static void CopyFolder(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(sourceFolder))
            {
                Debug.LogError($"SourceFolder:{sourceFolder} does not exist!");
                return;
            }
            try
            {
                //如果目标路径不存在,则创建目标路径
                if (!Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);
                }
                //得到原文件根目录下的所有文件
                string[] files = Directory.GetFiles(sourceFolder);
                foreach (string file in files)
                {
                    //不复制meta文件和manifest文件
                    if (file.EndsWith(".meta") || file.EndsWith(".manifest")) continue;
                    string name = Path.GetFileName(file);
                    string dest = Path.Combine(destFolder, name);
                    File.Copy(file, dest, true);//复制文件
                }
                //得到原文件根目录下的所有文件夹
                string[] folders = Directory.GetDirectories(sourceFolder);
                foreach (string folder in folders)
                {
                    string name = Path.GetFileName(folder);
                    string dest = Path.Combine(destFolder, name);
                    CopyFolder(folder, dest);//构建目标路径,递归复制文件
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

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
                    Debug.LogError($"Source file does not exists, destFile: {sourceFile}");
                    return;
                }
                string directoryName = Path.GetDirectoryName(destFile);
                if (string.IsNullOrEmpty(directoryName))
                {
                    Debug.LogError($"Destination directory name IsNullOrEmpty, destFile: {destFile}");
                    return;
                }
                //如果目标路径不存在,则创建目标路径
                if (!Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName);
                File.Copy(sourceFile, destFile, true);//复制文件
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}