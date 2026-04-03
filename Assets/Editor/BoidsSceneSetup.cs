using System;
using System.IO;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boids.EditorTools
{
    [InitializeOnLoad]
    public static class BoidsSceneSetup
    {
        private const string SessionKey = "Boids.DotsSceneSetup.Completed.v3";
        private const string RootFolder = "Assets/Boids";
        private const string MaterialsFolder = "Assets/Boids/Materials";
        private const string PrefabsFolder = "Assets/Boids/Prefabs";
        private const string MaterialPath = "Assets/Boids/Materials/BoidMaterial.mat";
        private const string PrefabPath = "Assets/Boids/Prefabs/Boid.prefab";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SubScenePath = "Assets/Scenes/BoidsSubScene.unity";

        static BoidsSceneSetup()
        {
            QueueSetup();
        }

        [MenuItem("Tools/Boids/Setup DOTS Boids Sample")]
        public static void RunFromMenu()
        {
            Setup(forceRun: true);
        }

        private static void QueueSetup()
        {
            EditorApplication.delayCall -= RunQueuedSetup;
            EditorApplication.delayCall += RunQueuedSetup;
        }

        private static void RunQueuedSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                QueueSetup();
                return;
            }

            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            Setup(forceRun: false);
        }

        private static void Setup(bool forceRun)
        {
            if (!forceRun && SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            try
            {
                EnsureFolder(RootFolder);
                EnsureFolder(MaterialsFolder);
                EnsureFolder(PrefabsFolder);

                Material material = EnsureMaterial();
                GameObject prefab = EnsurePrefab(material);

                bool changed = EnsureBoidsSubScene(prefab);
                changed |= EnsureSampleSceneReference();

                if (changed)
                {
                    AssetDatabase.SaveAssets();
                }

                SessionState.SetBool(SessionKey, true);
            }
            catch (Exception exception)
            {
                Debug.LogError("Boids DOTS scene setup failed.\n" + exception);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);

            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static Material EnsureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("A Universal Render Pipeline shader could not be found.");
            }

            material = new Material(shader)
            {
                name = "BoidMaterial",
                color = new Color(0.23f, 0.74f, 0.95f, 1f),
                enableInstancing = true
            };

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.2f);
            }

            AssetDatabase.CreateAsset(material, MaterialPath);
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }

        private static GameObject EnsurePrefab(Material material)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                EnsurePrefabAuthoringComponent();
                return prefab;
            }

            GameObject root = new GameObject("Boid");
            TryAddPrefabAuthoring(root);
            CreateVisualChild(
                root.transform,
                "Body",
                PrimitiveType.Capsule,
                material,
                Vector3.zero,
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.35f, 0.9f, 0.35f));
            CreateVisualChild(
                root.transform,
                "Tail",
                PrimitiveType.Cube,
                material,
                new Vector3(0f, 0f, -0.75f),
                Quaternion.identity,
                new Vector3(0.12f, 0.12f, 0.55f));

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static void EnsurePrefabAuthoringComponent()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);

            if (TryAddPrefabAuthoring(prefabRoot))
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static bool TryAddPrefabAuthoring(GameObject target)
        {
            MonoScript scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Boids/BoidPrefabAuthoring.cs");
            Type componentType = scriptAsset != null ? scriptAsset.GetClass() : null;
            if (componentType == null || target.GetComponent(componentType) != null)
            {
                return false;
            }

            target.AddComponent(componentType);
            return true;
        }

        private static bool EnsureComponentFromScript(GameObject target, string scriptPath)
        {
            MonoScript scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            Type componentType = scriptAsset != null ? scriptAsset.GetClass() : null;
            if (componentType == null || target.GetComponent(componentType) != null)
            {
                return false;
            }

            target.AddComponent(componentType);
            return true;
        }

        private static void CreateVisualChild(
            Transform parent,
            string objectName,
            PrimitiveType primitiveType,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = objectName;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = localScale;

            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static bool EnsureBoidsSubScene(GameObject prefab)
        {
            Scene scene = OpenOrCreateScene(SubScenePath, out bool wasAlreadyOpen);
            bool changed = false;

            BoidSimulationAuthoring authoring = FindComponentInScene<BoidSimulationAuthoring>(scene);
            if (authoring == null)
            {
                GameObject root = new GameObject("Boids Simulation");
                SceneManager.MoveGameObjectToScene(root, scene);
                authoring = root.AddComponent<BoidSimulationAuthoring>();
                changed = true;
            }

            if (authoring.BoidPrefab != prefab)
            {
                authoring.BoidPrefab = prefab;
                EditorUtility.SetDirty(authoring);
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!wasAlreadyOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            return changed;
        }

        private static bool EnsureSampleSceneReference()
        {
            Scene scene = OpenOrCreateScene(SampleScenePath, out bool wasAlreadyOpen);
            bool changed = false;

            SubScene subScene = FindComponentInScene<SubScene>(scene);
            if (subScene == null)
            {
                GameObject subSceneRoot = new GameObject("BoidsSubScene");
                SceneManager.MoveGameObjectToScene(subSceneRoot, scene);
                subScene = subSceneRoot.AddComponent<SubScene>();
                changed = true;
            }

            SceneAsset subSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath);
            if (subSceneAsset == null)
            {
                throw new InvalidOperationException("Boids sub scene could not be loaded after creation.");
            }

            if (subScene.SceneAsset != subSceneAsset)
            {
                subScene.SceneAsset = subSceneAsset;
                changed = true;
            }

            if (!subScene.AutoLoadScene)
            {
                subScene.AutoLoadScene = true;
                changed = true;
            }

            changed |= ConfigureMainCamera(scene);

            if (changed)
            {
                EditorUtility.SetDirty(subScene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!wasAlreadyOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            return changed;
        }

        private static Scene OpenOrCreateScene(string path, out bool wasAlreadyOpen)
        {
            Scene existingScene = SceneManager.GetSceneByPath(path);
            if (existingScene.isLoaded)
            {
                wasAlreadyOpen = true;
                return existingScene;
            }

            wasAlreadyOpen = false;

            if (File.Exists(path))
            {
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(newScene, path))
            {
                throw new InvalidOperationException("Could not create scene at path: " + path);
            }

            return newScene;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                T component = rootObject.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static bool ConfigureMainCamera(Scene scene)
        {
            Camera camera = FindComponentInScene<Camera>(scene);
            if (camera == null)
            {
                return false;
            }

            bool changed = false;
            Transform cameraTransform = camera.transform;
            Vector3 targetPosition = new Vector3(0f, 52f, -110f);
            Quaternion targetRotation = Quaternion.Euler(22f, 0f, 0f);

            if (cameraTransform.position != targetPosition)
            {
                cameraTransform.position = targetPosition;
                changed = true;
            }

            if (cameraTransform.rotation != targetRotation)
            {
                cameraTransform.rotation = targetRotation;
                changed = true;
            }

            if (Mathf.Abs(camera.fieldOfView - 55f) > 0.01f)
            {
                camera.fieldOfView = 55f;
                changed = true;
            }

            if (Mathf.Abs(camera.farClipPlane - 400f) > 0.01f)
            {
                camera.farClipPlane = 400f;
                changed = true;
            }

            if (EnsureComponentFromScript(camera.gameObject, "Assets/Scripts/Boids/BoidClickTargetController.cs"))
            {
                EditorUtility.SetDirty(camera.gameObject);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(camera);
            }

            return changed;
        }
    }
}
