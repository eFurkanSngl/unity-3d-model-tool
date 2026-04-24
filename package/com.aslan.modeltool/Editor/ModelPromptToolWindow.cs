using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aslan.ModelTool.Editor
{
    public sealed class ModelPromptToolWindow : EditorWindow
    {
        private enum ModelComplexity
        {
            Low,
            Medium,
            High
        }

        private const string BlenderScriptFileName = "gameagents_model_prompt_generator.py";

        private string _prompt = "stylized puzzle block";
        private string _modelName = "PuzzleBlock01";
        private string _outputFolder = "Assets/Art/Generated3D";
        private ModelComplexity _complexity = ModelComplexity.Medium;
        private int _generateCount = 1;
        private bool _createPrefab = true;
        private bool _addBoxCollider = true;
        private bool _markPrefabStatic = false;
        private ModelImporterMeshCompression _meshCompression = ModelImporterMeshCompression.Low;
        private bool _generateSecondaryUV = false;
        private bool _optimizeMesh = true;
        private bool _isReadable = false;
        private bool _focusInProject = true;
        private string _lastGeneratedAsset = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Tools/Aslan 3D Model Tool/Open Tool")]
        public static void Open()
        {
            var window = GetWindow<ModelPromptToolWindow>("3D Model Tool");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            var settings = ModelToolSettings.GetOrCreate();
            if (!string.IsNullOrWhiteSpace(settings.generatedModelsFolder))
            {
                _outputFolder = settings.generatedModelsFolder;
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(8f);

            EditorGUILayout.LabelField("Prompt To 3D Model (Blender)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bu panel prompt girdisini Blender batch script ile modele cevirir, sonra Unity icine import eder.",
                MessageType.Info);

            _modelName = EditorGUILayout.TextField("Model Name", _modelName);
            EditorGUILayout.LabelField("Prompt");
            _prompt = EditorGUILayout.TextArea(_prompt, GUILayout.MinHeight(72f));
            _complexity = (ModelComplexity)EditorGUILayout.EnumPopup("Complexity", _complexity);

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Variants", EditorStyles.boldLabel);
            _generateCount = EditorGUILayout.IntSlider("Count", _generateCount, 1, 12);

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _createPrefab = EditorGUILayout.Toggle("Create Prefab", _createPrefab);
            _addBoxCollider = EditorGUILayout.Toggle("Add BoxCollider", _addBoxCollider);
            _markPrefabStatic = EditorGUILayout.Toggle("Mark Prefab Static", _markPrefabStatic);
            _focusInProject = EditorGUILayout.Toggle("Ping Result", _focusInProject);

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Performance Import", EditorStyles.boldLabel);
            _meshCompression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup("Mesh Compression", _meshCompression);
            _generateSecondaryUV = EditorGUILayout.Toggle("Generate Lightmap UV", _generateSecondaryUV);
            _optimizeMesh = EditorGUILayout.Toggle("Optimize Mesh", _optimizeMesh);
            _isReadable = EditorGUILayout.Toggle("Read/Write Enabled", _isReadable);

            GUILayout.Space(6f);
            if (GUILayout.Button("Use Settings Default Folder"))
            {
                var settings = ModelToolSettings.GetOrCreate();
                _outputFolder = settings.generatedModelsFolder;
                GUI.FocusControl(null);
            }

            GUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Generate And Import", GUILayout.Height(34f)))
                {
                    GenerateAndImport();
                }
            }

            if (!string.IsNullOrWhiteSpace(_lastGeneratedAsset))
            {
                GUILayout.Space(10f);
                EditorGUILayout.LabelField("Last Result", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(_lastGeneratedAsset, EditorStyles.textField, GUILayout.Height(36f));

                if (GUILayout.Button("Select Last Result"))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_lastGeneratedAsset);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void GenerateAndImport()
        {
            var settings = ModelToolSettings.GetOrCreate();
            if (string.IsNullOrWhiteSpace(settings.blenderPath) || !File.Exists(settings.blenderPath))
            {
                EditorUtility.DisplayDialog("3D Model Tool", "Blender yolu bulunamadi. Tools > Aslan 3D Model Tool > Open Settings ile blenderPath duzelt.", "Tamam");
                return;
            }

            var safeModelName = SanitizeName(string.IsNullOrWhiteSpace(_modelName) ? "GeneratedModel" : _modelName);
            var relativeOutputFolder = NormalizeAssetsPath(string.IsNullOrWhiteSpace(_outputFolder) ? settings.generatedModelsFolder : _outputFolder);
            if (string.IsNullOrWhiteSpace(relativeOutputFolder) || !relativeOutputFolder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("3D Model Tool", "Output folder 'Assets/' ile baslamali.", "Tamam");
                return;
            }

            EnsureAssetFolder(relativeOutputFolder);

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                EditorUtility.DisplayDialog("3D Model Tool", "Project root bulunamadi.", "Tamam");
                return;
            }

            var absoluteOutputFolder = Path.Combine(projectRoot, relativeOutputFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteOutputFolder);

            var tempDir = Path.Combine(projectRoot, "Library", "GameAgentsTemp");
            Directory.CreateDirectory(tempDir);
            var scriptPath = Path.Combine(tempDir, BlenderScriptFileName);
            File.WriteAllText(scriptPath, BlenderGeneratorScript, Encoding.UTF8);

            var count = Mathf.Clamp(_generateCount, 1, 12);
            var producedAssets = new List<string>(count);

            for (var i = 0; i < count; i++)
            {
                var variantName = count == 1 ? safeModelName : $"{safeModelName}_{i + 1:D2}";
                var absoluteModelPath = Path.Combine(absoluteOutputFolder, variantName + ".fbx");

                var args = string.Join(" ", new[]
                {
                    "-b",
                    "-P",
                    QuoteArgument(scriptPath),
                    "--",
                    "--output",
                    QuoteArgument(absoluteModelPath),
                    "--prompt",
                    QuoteArgument(_prompt ?? string.Empty),
                    "--complexity",
                    QuoteArgument(_complexity.ToString().ToLowerInvariant())
                });

                var result = ExternalToolRunner.Run(settings.blenderPath, args, projectRoot, 900000);
                if (!result.Success)
                {
                    EditorUtility.DisplayDialog("3D Model Tool", $"Blender uretemedi (#{i + 1}):\n{result.StdErr}", "Tamam");
                    return;
                }

                var modelAssetPath = relativeOutputFolder + "/" + variantName + ".fbx";
                AssetDatabase.Refresh();
                ConfigureModelImporter(modelAssetPath);
                AssetDatabase.ImportAsset(modelAssetPath, ImportAssetOptions.ForceUpdate);

                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
                if (modelAsset == null)
                {
                    EditorUtility.DisplayDialog("3D Model Tool", "Model import edildi ama Unity asset bulunamadi: " + modelAssetPath, "Tamam");
                    return;
                }

                var resultAssetPath = modelAssetPath;
                if (_createPrefab)
                {
                    EnsureAssetFolder(settings.generatedPrefabsFolder);
                    var prefabPath = settings.generatedPrefabsFolder + "/" + variantName + ".prefab";
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                    if (instance != null)
                    {
                        try
                        {
                            instance.name = variantName;
                            if (_addBoxCollider)
                            {
                                AddOrUpdateBoxCollider(instance);
                            }

                            if (_markPrefabStatic)
                            {
                                GameObjectUtility.SetStaticEditorFlags(
                                    instance,
                                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
                            }

                            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                            resultAssetPath = prefabPath;
                        }
                        finally
                        {
                            DestroyImmediate(instance);
                        }
                    }
                }

                producedAssets.Add(resultAssetPath);
            }

            _lastGeneratedAsset = producedAssets.Count > 0 ? producedAssets[producedAssets.Count - 1] : string.Empty;
            Debug.Log($"[AslanModelTool] 3D model generation completed. Count={producedAssets.Count}");
            foreach (var asset in producedAssets)
            {
                Debug.Log("[AslanModelTool] -> " + asset);
            }

            if (_focusInProject && producedAssets.Count > 0)
            {
                if (producedAssets.Count == 1)
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(producedAssets[0]);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }
                else
                {
                    var objects = producedAssets
                        .Select(path => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path))
                        .Where(o => o != null)
                        .ToArray();
                    if (objects.Length > 0)
                    {
                        Selection.objects = objects;
                        EditorGUIUtility.PingObject(objects[0]);
                    }
                }
            }
        }

        private static void AddOrUpdateBoxCollider(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var collider = root.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = root.AddComponent<BoxCollider>();
            }

            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
        }

        private void ConfigureModelImporter(string modelAssetPath)
        {
            var importer = AssetImporter.GetAtPath(modelAssetPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            importer.globalScale = 1f;
            importer.meshCompression = _meshCompression;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.isReadable = _isReadable;
            importer.generateSecondaryUV = _generateSecondaryUV;
            importer.optimizeMeshPolygons = _optimizeMesh;
            importer.optimizeMeshVertices = _optimizeMesh;
            importer.weldVertices = true;
            importer.SaveAndReimport();
        }

        private static string SanitizeName(string raw)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var filtered = new string(raw.Where(ch => !invalid.Contains(ch)).ToArray());
            filtered = filtered.Replace(" ", "_");
            return string.IsNullOrWhiteSpace(filtered) ? "GeneratedModel" : filtered;
        }

        private static string NormalizeAssetsPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var normalized = path.Replace("\\", "/").Trim();
            while (normalized.Contains("//"))
            {
                normalized = normalized.Replace("//", "/");
            }

            return normalized;
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            var normalized = NormalizeAssetsPath(folderPath);
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new InvalidOperationException("Folder path Assets ile baslamali: " + folderPath);
            }

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private const string BlenderGeneratorScript = @"
import bpy
import hashlib
import os
import sys


def parse_args():
    argv = sys.argv
    if '--' in argv:
        argv = argv[argv.index('--') + 1:]
    else:
        argv = []

    data = {
        'output': '',
        'prompt': '',
        'complexity': 'medium'
    }

    i = 0
    while i < len(argv):
        token = argv[i]
        if token.startswith('--') and i + 1 < len(argv):
            key = token[2:]
            data[key] = argv[i + 1]
            i += 2
            continue
        i += 1

    return data


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def color_from_prompt(prompt):
    h = hashlib.md5(prompt.encode('utf-8')).hexdigest()
    r = int(h[0:2], 16) / 255.0
    g = int(h[2:4], 16) / 255.0
    b = int(h[4:6], 16) / 255.0
    return (0.2 + r * 0.65, 0.2 + g * 0.65, 0.2 + b * 0.65, 1.0)


def make_material(base_color):
    mat = bpy.data.materials.new(name='GA_Material')
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    if bsdf:
        bsdf.inputs['Base Color'].default_value = base_color
        bsdf.inputs['Roughness'].default_value = 0.45
        bsdf.inputs['Metallic'].default_value = 0.05
    return mat


def apply_material_to_objects(mat):
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            if obj.data.materials:
                obj.data.materials[0] = mat
            else:
                obj.data.materials.append(mat)


def create_puzzle_block(detail):
    size = 1.0 + detail * 0.05
    bpy.ops.mesh.primitive_cube_add(size=size, location=(0, 0, 0.4))
    base = bpy.context.active_object
    base.name = 'PuzzleBlock'

    stud_rows = 2 + detail
    spacing = size * 0.38
    start = -spacing * (stud_rows - 1) * 0.5
    stud_radius = 0.09 + detail * 0.01
    stud_depth = 0.09 + detail * 0.02
    stud_top = 0.4 + size * 0.5 + stud_depth * 0.5

    for x in range(stud_rows):
        for y in range(stud_rows):
            bpy.ops.mesh.primitive_cylinder_add(
                radius=stud_radius,
                depth=stud_depth,
                location=(start + x * spacing, start + y * spacing, stud_top)
            )


def create_wall_block(detail):
    length = 2.1 + detail * 0.35
    height = 0.9 + detail * 0.15
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, height * 0.5))
    obj = bpy.context.active_object
    obj.scale = (length, 0.45, height)
    obj.name = 'WallBlock'


def create_platform(detail):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.18))
    obj = bpy.context.active_object
    obj.scale = (1.5 + detail * 0.25, 1.5 + detail * 0.25, 0.18)
    obj.name = 'Platform'


def create_pillar(detail):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.35 + detail * 0.06, depth=2.0 + detail * 0.25, location=(0, 0, 1.0))
    obj = bpy.context.active_object
    obj.name = 'Pillar'


def create_coin(detail):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.55 + detail * 0.05, depth=0.12 + detail * 0.03, location=(0, 0, 0.2))
    obj = bpy.context.active_object
    obj.name = 'CoinToken'


def create_monkey(detail):
    bpy.ops.mesh.primitive_monkey_add(size=1.0 + detail * 0.2, location=(0, 0, 0.65))
    obj = bpy.context.active_object
    obj.name = 'MonkeyHead'

    if detail > 0:
        sub = obj.modifiers.new(name='GA_Subdivision', type='SUBSURF')
        sub.levels = 1 + detail
        sub.render_levels = 1 + detail


def apply_bevel(detail):
    width = 0.03 + detail * 0.01
    segments = 2 + detail
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH':
            continue
        bev = obj.modifiers.new(name='GA_Bevel', type='BEVEL')
        bev.width = width
        bev.segments = segments
        bev.limit_method = 'ANGLE'


def save_fbx(output_path):
    out_dir = os.path.dirname(output_path)
    if out_dir and not os.path.exists(out_dir):
        os.makedirs(out_dir, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=output_path, use_selection=False, path_mode='AUTO')


def main():
    args = parse_args()
    output = args.get('output', '')
    prompt = (args.get('prompt', '') or '').lower()
    complexity = (args.get('complexity', 'medium') or 'medium').lower()

    detail = 1
    if complexity == 'low':
        detail = 0
    elif complexity == 'high':
        detail = 2

    clear_scene()

    if 'monkey' in prompt or 'suzanne' in prompt or 'ape' in prompt:
        create_monkey(detail)
    elif 'coin' in prompt or 'token' in prompt:
        create_coin(detail)
    elif 'pillar' in prompt:
        create_pillar(detail)
    elif 'platform' in prompt or 'floor' in prompt:
        create_platform(detail)
    elif 'wall' in prompt:
        create_wall_block(detail)
    else:
        create_puzzle_block(detail)

    apply_bevel(detail)

    mat = make_material(color_from_prompt(prompt))
    apply_material_to_objects(mat)

    if not output:
        raise RuntimeError('Output path is empty.')

    save_fbx(output)


if __name__ == '__main__':
    main()
";
    }
}

