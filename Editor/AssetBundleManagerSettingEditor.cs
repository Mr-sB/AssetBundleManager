using UnityEditor;

namespace GameUtil
{
    [CustomEditor(typeof(AssetBundleManagerSetting))]
    public class AssetBundleManagerSettingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty iterator = serializedObject.GetIterator();
            for (bool enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
            {
                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(iterator, true);
                }
                else if (iterator.propertyPath == nameof(AssetBundleManagerSetting.BuildTarget))
                {
                    BuildTarget buildTarget = (BuildTarget) EditorGUILayout.EnumPopup(iterator.displayName, (BuildTarget) iterator.intValue);
                    iterator.intValue = (int) buildTarget;
                }
                else if (iterator.propertyPath == nameof(AssetBundleManagerSetting.BuildAssetBundleOptions))
                {
                    BuildAssetBundleOptions buildAssetBundleOptions = (BuildAssetBundleOptions) EditorGUILayout.EnumFlagsField(iterator.displayName, (BuildAssetBundleOptions) iterator.intValue);
                    iterator.intValue = (int) buildAssetBundleOptions;
                }
                else
                    EditorGUILayout.PropertyField(iterator, true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}