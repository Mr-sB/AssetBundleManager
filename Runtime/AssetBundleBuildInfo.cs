using System;
using System.Collections.Generic;

namespace GameUtil
{
    [Serializable]
    public class AssetBundleBuildInfo
    {
        public int Version;
        public List<AssetBundleBuildRecord> Records = new List<AssetBundleBuildRecord>();
    }
    
    [Serializable]
    public class AssetBundleBuildRecord
    {
        public string AssetBundleName;
        public string Hash;
        public long Size;
    }
}