using UnityEditor;
using UnityEngine;

namespace GameUtil
{
    public class AssetBundleManagerSettingWindow : EditorWindow
    {
        private static AssetBundleManagerSettingWindow mWindow;
        private GUIStyle mSetupButtonStyle;
        private bool mIsProSkin;
        private AssetBundleManagerSetting mSetting;
        private SerializedObject mSerializedObject;
        private Vector2 mPosition;
        
        [MenuItem("Tools/AssetBundleManager")]
        public static void CreateWindow()
        {
            if (!mWindow)
            {
                mWindow = GetWindow<AssetBundleManagerSettingWindow>(false, "AssetBundleManager");
                mWindow.minSize = new Vector2(300f, 100f);
            }
            mWindow.Show();
            mWindow.Focus();
        }

        private void OnEnable()
        {
            mSetting = AssetBundleEditorTools.GetAssetBundleManagerSetting();
            if(mSetting)
                mSerializedObject = new SerializedObject(mSetting);
        }

        private void OnDestroy()
        {
            DisposeSerializedObject();
        }

        private void OnGUI()
        {
            mPosition = GUILayout.BeginScrollView(mPosition);
            if (!mSetting)
            {
                DisposeSerializedObject();
                //init or skin changed
                if (mSetupButtonStyle == null || mIsProSkin != EditorGUIUtility.isProSkin)
                {
                    mIsProSkin = EditorGUIUtility.isProSkin;
                    mSetupButtonStyle = new GUIStyle(GUI.skin.button) {fontSize = 20};
                }
                EditorGUILayout.HelpBox("Click the button below to setup AssetBundleManager.", MessageType.Info);
                if (GUILayout.Button("Setup AssetBundleManager", mSetupButtonStyle, GUILayout.Height(50)))
                {
                    mSetting = AssetBundleEditorTools.GetOrCreateAssetBundleManagerSetting();
                    mSerializedObject = new SerializedObject(mSetting);
                }
            }
            else
            {
                AssetBundleManagerSettingEditor.Draw(mSerializedObject, false);
                if (GUILayout.Button("Build"))
                    AssetBundleEditorTools.Build();
                if (GUILayout.Button("Set AssetBundle Name"))
                    AssetBundleEditorTools.SetAssetBundleName();
                if (GUILayout.Button("Clear StreamingAssets Bundle Path"))
                    AssetBundleEditorTools.ClearStreamingAssetsBundlePath();
                if (GUILayout.Button("Copy To StreamingAssets Bundle Path"))
                    AssetBundleEditorTools.CopyToStreamingAssetsBundlePath();
                if (GUILayout.Button("Clear Load AssetBundle Path"))
                    AssetBundleEditorTools.ClearLoadAssetBundlePath();
                if (GUILayout.Button("Copy To Load AssetBundle Path"))
                    AssetBundleEditorTools.CopyToLoadAssetBundlePath();
            }
            GUILayout.EndScrollView();
        }

        private void DisposeSerializedObject()
        {
            if(mSerializedObject == null) return;
            if(mSerializedObject.targetObject)
                mSerializedObject.ApplyModifiedProperties();
            mSerializedObject.Dispose();
            mSerializedObject = null;
        }
    }
}