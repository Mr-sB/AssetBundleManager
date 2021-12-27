using System;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.Callbacks;
using UnityEngine;

namespace GameUtil
{
    [CustomEditor(typeof(AssetBundleManagerSetting))]
    public class AssetBundleManagerSettingEditor : UnityEditor.Editor
    {
        private AnimBool mShowBuildTarget = new AnimBool();

        private void OnEnable()
        {
            mShowBuildTarget.value = !serializedObject.FindProperty(nameof(AssetBundleManagerSetting.UseActiveBuildTarget)).boolValue;
            mShowBuildTarget.valueChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            mShowBuildTarget.valueChanged.RemoveListener(Repaint);
        }

        public override void OnInspectorGUI()
        {
            Draw(serializedObject, mShowBuildTarget, true);
            if (GUILayout.Button("Open AssetBundleManager Window"))
                AssetBundleManagerWindow.CreateWindow();
        }

        //Callback attribute for opening an asset in Unity (e.g the callback is fired when double clicking an asset in the Project Browser.)
        [OnOpenAsset]
        public static bool OpenAsset(int instanceID, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceID) is AssetBundleManagerSetting)) return false;
            AssetBundleManagerWindow.CreateWindow();
            return true;
        }

        public static void Draw(SerializedObject serializedObject, AnimBool showBuildTarget, bool disable)
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
                switch (iterator.propertyPath)
                {
                    case "m_Script":
                        if (disable)
                            EditorGUILayout.PropertyField(iterator, true);
                        break;
                    case nameof(AssetBundleManagerSetting.UseActiveBuildTarget):
                        EditorGUILayout.PropertyField(iterator, true);
                        showBuildTarget.target = !iterator.boolValue;
                        break;
                    case nameof(AssetBundleManagerSetting.BuildTarget):
                        BuildTarget buildTarget = (BuildTarget) iterator.intValue;
                        if (EditorGUILayout.BeginFadeGroup(showBuildTarget.faded))
                            buildTarget = (BuildTarget) EditorGUILayout.EnumPopup(iterator.displayName, (BuildTarget) iterator.intValue);
                        EditorGUILayout.EndFadeGroup();
                        iterator.intValue = (int) buildTarget;
                        break;
                    case nameof(AssetBundleManagerSetting.BuildAssetBundleOptions):
                        BuildAssetBundleOptions buildAssetBundleOptions =
                            (BuildAssetBundleOptions) EditorGUILayout.EnumFlagsField(iterator.displayName, (BuildAssetBundleOptions) iterator.intValue);
                        iterator.intValue = (int) buildAssetBundleOptions;
                        break;
                    case nameof(AssetBundleManagerSetting.FastMode):
                        EditorGUILayout.PropertyField(iterator, true);
                        if (iterator.boolValue)
                            EditorGUILayout.HelpBox("In fast mode, the assets are loading by AssetDatabase,\nand load AssetBundle will return null!",
                                MessageType.Info);
                        break;
                    case nameof(AssetBundleManagerSetting.AssetPath):
                    case nameof(AssetBundleManagerSetting.BuildBundlePath):
                        if (disable)
                            EditorGUILayout.PropertyField(iterator, true);
                        else
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PropertyField(iterator, true);
                            if (GUILayout.Button("Browse", GUILayout.Width(60)))
                            {
                                iterator.stringValue = EditorUtility.OpenFolderPanel(iterator.propertyPath, iterator.stringValue, "").TrimStart(Environment.CurrentDirectory.ToCharArray());
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        break;
                    case nameof(AssetBundleManagerSetting.LoadBundlePath):
                        if (disable)
                            EditorGUILayout.PropertyField(iterator, true);
                        else
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PropertyField(iterator, true);
                            if (GUILayout.Button("Open", GUILayout.Width(60)))
                            {
                                EditorUtility.RevealInFinder(setting.GetLoadBundleFullPath());
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        break;
                    default:
                        EditorGUILayout.PropertyField(iterator, true);
                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorGUI.EndDisabledGroup();
            if (!setting.IsValid)
                EditorGUILayout.HelpBox(
                    $"{nameof(AssetBundleManagerSetting.AssetPath)} and {nameof(AssetBundleManagerSetting.BuildBundlePath)} can not be empty!",
                    MessageType.Warning);
        }
    }
}