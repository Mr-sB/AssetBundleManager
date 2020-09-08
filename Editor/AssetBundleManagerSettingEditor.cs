using UnityEditor;
using UnityEngine;

namespace GameUtil
{
    [CustomEditor(typeof(AssetBundleManagerSetting))]
    public class AssetBundleManagerSettingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            Draw(serializedObject, true);
            if (GUILayout.Button("Open AssetBundleManager Window"))
                AssetBundleManagerSettingWindow.CreateWindow();
        }

        public static void Draw(SerializedObject serializedObject, bool disable)
        {
            if (!(serializedObject.targetObject is AssetBundleManagerSetting setting))
            {
                EditorGUILayout.HelpBox("TargetObject is not AssetBundleManagerSetting!", MessageType.Error);
                return;
            }
            EditorGUI.BeginDisabledGroup(disable);
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty iterator = serializedObject.GetIterator();
            for (bool enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
            {
                if (iterator.propertyPath == "m_Script")
                {
                    if(disable)
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
            EditorGUI.EndDisabledGroup();
            if (!setting.IsValid)
                EditorGUILayout.HelpBox($"{nameof(AssetBundleManagerSetting.AssetPath)} and {nameof(AssetBundleManagerSetting.BuildBundlePath)} can not be empty!", MessageType.Warning);
        }
    }
}