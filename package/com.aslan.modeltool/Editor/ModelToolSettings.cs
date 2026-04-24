using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aslan.ModelTool.Editor
{
    public sealed class ModelToolSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/AslanModelTool/ModelToolSettings.asset";

        [Header("External Tools")]
        public string blenderPath = @"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe";

        [Header("Project Folders")]
        public string generatedModelsFolder = "Assets/Art/Generated3D";
        public string generatedPrefabsFolder = "Assets/Prefabs/Generated";

        public static ModelToolSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ModelToolSettings>(AssetPath);
            if (settings != null)
            {
                return settings;
            }

            var dir = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            settings = CreateInstance<ModelToolSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        [MenuItem("Tools/Aslan 3D Model Tool/Open Settings")]
        public static void OpenSettingsAsset()
        {
            var settings = GetOrCreate();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
