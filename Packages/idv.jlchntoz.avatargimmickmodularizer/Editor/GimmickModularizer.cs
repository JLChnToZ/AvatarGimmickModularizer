using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.Core;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.GimmickInstallModularizer.Editors {
    public class GimmickModularizerWindow : EditorWindow {
        GameObject baseAvatar, workspaceAvatar;
        GameObject maRootObject;
        string titleLanguage;
        readonly Dictionary<Transform, Transform> transformMapping = new Dictionary<Transform, Transform>();
        Vector2 scrollPos;

        [MenuItem("Tools/JLChnToZ/Gimmick Modularizer")]
        public static void ShowWindow() => GetWindow<GimmickModularizerWindow>("_");

        static Transform GetTransformByName(Transform transform, string name) {
            foreach (Transform child in transform)
                if (child.name == name) return child;
            return null;
        }

        static bool IsAnyChildOf(Transform transform, Transform parent) {
            if (parent == null) return false;
            while (transform != null) {
                if (transform == parent) return true;
                transform = transform.parent;
            }
            return false;
        }

        void OnGUI() {
            using var scrollView = new EditorGUILayout.ScrollViewScope(scrollPos);
            scrollPos = scrollView.scrollPosition;
            EditorGUILayout.Space();

            I18NEditor.DrawLocaleField();
            var i18n = I18N.Instance;
            var title = i18n.GetContent("GimmickModularizer.Name");
            if (titleLanguage != i18n.CurrentLanguage) {
                titleLanguage = i18n.CurrentLanguage;
                titleContent = new GUIContent(title);
            }
            EditorGUILayout.Space();

            GUILayout.Label(title, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label(i18n.GetContent("GimmickModularizer.Description"), EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            GUILayout.Label(i18n.GetContent("GimmickModularizer.Step1"), EditorStyles.wordWrappedLabel);
            baseAvatar = EditorGUILayout.ObjectField(baseAvatar, typeof(GameObject), true) as GameObject;
            using (new EditorGUI.DisabledScope(baseAvatar == null || workspaceAvatar != null))
                if (GUILayout.Button(i18n.GetContent("GimmickModularizer.Clone")))
                    CreateClone();
            EditorGUILayout.Space();

            GUILayout.Label(i18n.GetContent("GimmickModularizer.Step2"), EditorStyles.wordWrappedLabel);
                workspaceAvatar = EditorGUILayout.ObjectField(workspaceAvatar, typeof(GameObject), true) as GameObject;
            if (workspaceAvatar == baseAvatar) workspaceAvatar = null;
            EditorGUILayout.Space();

            GUILayout.Label(i18n.GetContent("GimmickModularizer.Step3"), EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(baseAvatar == null || workspaceAvatar == null))
                if (GUILayout.Button(i18n.GetContent("GimmickModularizer.Modularize")))
                    Modularize();
            EditorGUILayout.Space();

            GUILayout.Label(i18n.GetContent("GimmickModularizer.Step4"), EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            GUILayout.Label(i18n.GetContent("GimmickModularizer.Note"), EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
        }

        void CreateClone() {
            if (baseAvatar == null || workspaceAvatar != null) return;
            var names = new List<string>(AssetDatabase.GetSubFolders("Assets/Generated"));
            foreach (var other in baseAvatar.scene.GetRootGameObjects()) names.Add(other.name);
            var newName = ObjectNames.GetUniqueName(names.ToArray(), $"{baseAvatar.name} Dummy");
            workspaceAvatar = Instantiate(baseAvatar);
            workspaceAvatar.name = newName;
            workspaceAvatar.transform.position = baseAvatar.transform.position + Vector3.right;
            if (workspaceAvatar.TryGetComponent(out VRCAvatarDescriptor wsDesc)) {
                InstantiateLayers(wsDesc.baseAnimationLayers);
                InstantiateLayers(wsDesc.specialAnimationLayers);
                wsDesc.expressionsMenu = Instantiate(wsDesc.expressionsMenu);
                SaveAsset(wsDesc.expressionsMenu, "Temp Menu");
                wsDesc.expressionParameters = Instantiate(wsDesc.expressionParameters);
                SaveAsset(wsDesc.expressionParameters, "Temp Parameters");
            }
            Undo.RegisterCreatedObjectUndo(workspaceAvatar, "Create Workspace Avatar Clone");
            Selection.activeGameObject = workspaceAvatar;
            EditorGUIUtility.PingObject(workspaceAvatar);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        void InstantiateLayers(VRCAvatarDescriptor.CustomAnimLayer[] layers) {
            if (layers == null) return;
            for (int i = 0; i < layers.Length; i++) {
                ref var layer = ref layers[i];
                if (layer.animatorController == null) continue;
                var controller = Instantiate(layer.animatorController);
                layer.animatorController = controller;
                SaveAsset(controller, $"Temp {layer.type}");
            }
        }

        void Modularize() {
            if (baseAvatar == null || workspaceAvatar == null) return;
            if (workspaceAvatar == baseAvatar) {
                Debug.LogWarning("Cannot modularize the same avatar as workspace and base.");
                return;
            }
            maRootObject = new GameObject($"Merged Root ({workspaceAvatar.name})");
            maRootObject.transform.SetParent(baseAvatar.transform, false);
            ModularizeHierarchy();
            ModularizeDescriptor();
            Undo.RegisterCreatedObjectUndo(maRootObject, $"Create Modularized Root for {workspaceAvatar.name}");
            Undo.SetCurrentGroupName($"Modularize {workspaceAvatar.name} into {baseAvatar.name}");
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Selection.activeGameObject = maRootObject;
            EditorGUIUtility.PingObject(maRootObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        void ModularizeHierarchy() {
            var wsRoot = workspaceAvatar.transform;
            var bsRoot = baseAvatar.transform;
            transformMapping.Clear();

            // 1st pass: copy all transforms from workspace to base
            var walker = new Queue<(Transform ws, Transform bs)>();
            walker.Enqueue((wsRoot, bsRoot));
            var maRoot = maRootObject.transform;
            while (walker.TryDequeue(out var pair)) {
                var (wsParent, bsParent) = pair;
                transformMapping[wsParent] = bsParent;
                foreach (Transform wsChild in wsParent) {
                    var bsChild = GetTransformByName(bsParent, wsChild.name);
                    if (bsChild == null) {
                        var bsChildObject = Instantiate(wsChild.gameObject, maRoot);
                        bsChildObject.name = wsChild.name;
                        bsChild = bsChildObject.transform;
                        wsChild.GetPositionAndRotation(out var position, out var rotation);
                        position = wsRoot.InverseTransformPoint(position);
                        rotation = Quaternion.Inverse(wsRoot.rotation) * rotation;
                        bsChild.SetLocalPositionAndRotation(position, rotation);
                        if (!bsChild.TryGetComponent(out ModularAvatarBoneProxy _)) {
                            var boneProxy = bsChildObject.AddComponent<ModularAvatarBoneProxy>();
                            boneProxy.target = bsParent;
                            boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
                        }
                        Undo.RegisterCreatedObjectUndo(bsChildObject, $"Copy {wsChild.name} to {bsParent.name}");
                    }
                    walker.Enqueue((wsChild, bsChild));
                }
            }

            var bsComponents = new List<Component>();
            var wsComponents = new List<Component>();
            var proxyMapping = new Dictionary<Transform, Transform>();

            // 2nd pass: fix all copied references in components
            foreach (var pair in transformMapping) {
                var (wsParent, bsParent) = pair;
                bsParent.GetComponents(bsComponents);
                wsParent.GetComponents(wsComponents);
                foreach (var wsComponent in wsComponents) {
                    if (wsComponent is Transform || wsComponent is VRC_AvatarDescriptor || wsComponent is PipelineManager) continue;
                    var componentType = wsComponent.GetType();
                    Component bsComponent = null;
                    for (int i = 0; i < bsComponents.Count; i++) {
                        bsComponent = bsComponents[i];
                        if (componentType == bsComponent.GetType()) {
                            bsComponents.RemoveAt(i);
                            break;
                        }
                        bsComponent = null;
                    }
                    if (bsComponent == null) {
                        bsComponent = bsParent.gameObject.AddComponent(componentType);
                        EditorUtility.CopySerialized(wsComponent, bsComponent);
                        Undo.RegisterCreatedObjectUndo(bsComponent, $"Copy {componentType.Name} from {wsParent.name} to {bsParent.name}");
                    }
                    using var so = new SerializedObject(bsComponent);
                    var prop = so.GetIterator();
                    while (prop.Next(true)) {
                        if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var value = prop.objectReferenceValue;
                        if (value == null) continue;
                        if (value is GameObject valueGO) {
                            if (!transformMapping.TryGetValue(valueGO.transform, out var bsTransform)) continue;
                            prop.objectReferenceValue = bsTransform.gameObject;
                            continue;
                        }
                        if (value is Transform valueT) {
                            if (!transformMapping.TryGetValue(valueT, out var bsTransform)) continue;
                            if (IsAnyChildOf(bsTransform, maRoot))
                                valueT = bsTransform;
                            else if (!proxyMapping.TryGetValue(bsTransform, out valueT)) {
                                var proxyObject = new GameObject($"{bsTransform.name} Proxy");
                                valueT = proxyObject.transform;
                                valueT.SetParent(maRoot, false);
                                bsTransform.GetPositionAndRotation(out var position, out var rotation);
                                valueT.SetPositionAndRotation(position, rotation);
                                proxyMapping[bsTransform] = valueT;
                                var boneProxy = proxyObject.AddComponent<ModularAvatarBoneProxy>();
                                boneProxy.target = bsTransform;
                                boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
                                Undo.RegisterCreatedObjectUndo(proxyObject, $"Create Proxy for {bsTransform.name}");
                            }
                            prop.objectReferenceValue = valueT;
                            continue;
                        }
                        if (value is Component valueC) {
                            if (!transformMapping.TryGetValue(valueC.transform, out var bsTransform) ||
                                !bsTransform.TryGetComponent(value.GetType(), out valueC))
                                continue;
                            prop.objectReferenceValue = valueC;
                            continue;
                        }
                    }
                    so.ApplyModifiedProperties();
                }
            }
        }

        void ModularizeDescriptor() {
            if (!workspaceAvatar.TryGetComponent(out VRCAvatarDescriptor wsDesc) ||
                !baseAvatar.TryGetComponent(out VRCAvatarDescriptor bsDesc))
                return;

            // Layers
            var bsLayers = FetchLayers(bsDesc);
            var wsLayers = FetchLayers(wsDesc);
            var srcLayers = new Dictionary<VRCAvatarDescriptor.AnimLayerType, AnimatorController>();
            var animationRelocator = new AnimationRelocator();
            foreach (var layer in wsLayers) {
                bsLayers.TryGetValue(layer.Key, out var bsController);
                var diffController = GetDiffAnimatorController(layer.Value as AnimatorController, bsController as AnimatorController);
                if (diffController == null) continue;
                srcLayers[layer.Key] = diffController;
                animationRelocator.AddController(diffController);
            }
            var stack = new Stack<string>();
            var wsRoot = workspaceAvatar.transform;
            var maRoot = maRootObject.transform;
            var pathMapping = new Dictionary<string, string>();
            foreach (var pair in transformMapping) {
                if (!IsAnyChildOf(pair.Value, maRoot)) continue;
                for (var t = pair.Key; t != null && t != wsRoot; t = t.parent)
                    stack.Push(t.name);
                var srcPath = string.Join('/', stack);
                stack.Clear();
                for (var t = pair.Value; t != null && t != maRoot; t = t.parent)
                    stack.Push(t.name);
                var destPath = string.Join('/', stack);
                stack.Clear();
                pathMapping[srcPath] = destPath;
            }
            animationRelocator.RewriteBindingPaths(pathMapping);
            foreach (var layer in srcLayers) {
                var converted = animationRelocator[layer.Value];
                if (converted is AnimatorOverrideController overrideController) {
                    var baker = new AnimatorOverrideControllerBaker(overrideController);
                    converted = baker.Bake();
                    SaveAsset(converted, $"{layer.Key} Extracted");
                    baker.SaveToAsset(converted);
                } else
                    SaveAsset(converted, $"{layer.Key} Extracted");
                var mergeAnimator = Undo.AddComponent<ModularAvatarMergeAnimator>(maRootObject);
                mergeAnimator.animator = converted;
                mergeAnimator.layerType = layer.Key;
                mergeAnimator.matchAvatarWriteDefaults = true;
                mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
            }

            // Parameters
            var parameters = GetDiffParameters(wsDesc.expressionParameters, bsDesc.expressionParameters);
            if (parameters != null && parameters.Length > 0) {
                var mergeParameters = Undo.AddComponent<ModularAvatarParameters>(maRootObject);
                foreach (var p in parameters)
                    mergeParameters.parameters.Add(new ParameterConfig {
                        nameOrPrefix = p.name,
                        syncType = p.valueType switch {
                            VRCExpressionParameters.ValueType.Int => ParameterSyncType.Int,
                            VRCExpressionParameters.ValueType.Float => ParameterSyncType.Float,
                            VRCExpressionParameters.ValueType.Bool => ParameterSyncType.Bool,
                            _ => ParameterSyncType.NotSynced,
                        },
                        hasExplicitDefaultValue = !Mathf.Approximately(p.defaultValue, 0F),
                        defaultValue = p.defaultValue,
                        localOnly = !p.networkSynced,
                        saved = p.saved,
                    });
            }

            // Menus
            var diffMenu = GetDiffMenu(wsDesc.expressionsMenu, bsDesc.expressionsMenu);
            if (diffMenu != null && diffMenu.Length > 0) {
                var menuContainer = new GameObject("Expressions Menu", typeof(ModularAvatarMenuInstaller), typeof(ModularAvatarMenuGroup));
                menuContainer.transform.SetParent(maRootObject.transform, false);
                Undo.RegisterCreatedObjectUndo(menuContainer, "Create Expressions Menu Container");
                foreach (var control in diffMenu) {
                    var controlObject = new GameObject(control.name);
                    controlObject.transform.SetParent(menuContainer.transform, false);
                    var menuItem = controlObject.AddComponent<ModularAvatarMenuItem>();
                    menuItem.Control = control;
                    menuItem.MenuSource = SubmenuSource.MenuAsset;
                    Undo.RegisterCreatedObjectUndo(controlObject, $"Create Menu Control {control.name}");
                }
            }
        }

        Dictionary<VRCAvatarDescriptor.AnimLayerType, RuntimeAnimatorController> FetchLayers(VRCAvatarDescriptor desc) {
            var layers = new Dictionary<VRCAvatarDescriptor.AnimLayerType, RuntimeAnimatorController>();
            if (desc.baseAnimationLayers != null)
                foreach (var layer in desc.baseAnimationLayers)
                    layers[layer.type] = layer.animatorController;
            if (desc.specialAnimationLayers != null)
                foreach (var layer in desc.specialAnimationLayers)
                    layers[layer.type] = layer.animatorController;
            return layers;
        }

        void SaveAsset(UnityObject asset, string name) =>
            SafeSaveAsset(asset, $"Assets/Generated/{workspaceAvatar.name}/{name}");

        AnimatorController GetDiffAnimatorController(AnimatorController wsController, AnimatorController bsController) {
            if (wsController == null) return null;
            var layers = new List<AnimatorControllerLayer>(wsController.layers);
            if (bsController != null)
                foreach (var bsLayer in bsController.layers)
                    for (int i = 0; i < layers.Count; i++) {
                        var wsLayer = layers[i];
                        if (wsLayer.name == bsLayer.name) {
                            layers.RemoveAt(i);
                            break;
                        }
                    }
            if (layers.Count == 0) return null;
            var usedParamNames = new HashSet<string>();
            foreach (var layer in layers)
                GatherParameters(layer.stateMachine, usedParamNames);
            var usedParams = new List<AnimatorControllerParameter>();
            foreach (var p in wsController.parameters)
                if (usedParamNames.Contains(p.name))
                    usedParams.Add(p);
            return new AnimatorController {
                name = $"{wsController.name} Diff",
                parameters = usedParams.ToArray(),
                layers = layers.ToArray(),
            };
        }

        void GatherParameters(string name, HashSet<string> parameters) {
            if (!string.IsNullOrEmpty(name)) parameters.Add(name);
        }

        void GatherParameters(AnimatorStateMachine stateMachine, HashSet<string> parameters) {
            GatherParameters(stateMachine.anyStateTransitions, parameters);
            GatherParameters(stateMachine.entryTransitions, parameters);
            foreach (var subState in stateMachine.states) {
                var state = subState.state;
                GatherParameters(state.motion, parameters);
                GatherParameters(state.transitions, parameters);
                if (state.timeParameterActive)
                    GatherParameters(state.timeParameter, parameters);
                if (state.speedParameterActive)
                    GatherParameters(state.speedParameter, parameters);
                if (state.cycleOffsetParameterActive)
                    GatherParameters(state.cycleOffsetParameter, parameters);
                if (state.mirrorParameterActive)
                    GatherParameters(state.mirrorParameter, parameters);
            }
            foreach (var subStateMachine in stateMachine.stateMachines)
                GatherParameters(subStateMachine.stateMachine, parameters);
        }

        void GatherParameters(AnimatorTransitionBase[] transitions, HashSet<string> parameters) {
            foreach (var transition in transitions)
                GatherParameters(transition.conditions, parameters);
        }

        void GatherParameters(AnimatorCondition[] conditions, HashSet<string> parameters) {
            foreach (var condition in conditions)
                GatherParameters(condition.parameter, parameters);
        }

        void GatherParameters(Motion motion, HashSet<string> parameters) {
            if (motion is BlendTree blendTree) {
                var blendType = blendTree.blendType;
                foreach (var child in blendTree.children) {
                    if (blendType == BlendTreeType.Direct)
                        GatherParameters(child.directBlendParameter, parameters);
                    GatherParameters(child.motion, parameters);
                }
                switch (blendType) {
                    case BlendTreeType.SimpleDirectional2D:
                    case BlendTreeType.FreeformDirectional2D:
                    case BlendTreeType.FreeformCartesian2D:
                        GatherParameters(blendTree.blendParameterY, parameters);
                        goto case BlendTreeType.Simple1D;
                    case BlendTreeType.Simple1D:
                        GatherParameters(blendTree.blendParameter, parameters);
                        break;
                }
            }
        }

        static void SafeSaveAsset(UnityObject asset, string pathName) {
            var pathSplit = pathName.Split('/');
            string parentPath = pathSplit[0];
            for (int i = 1, end = pathSplit.Length - 1; i < end; i++) {
                var path = string.Join('/', pathSplit, 0, i + 1);
                if (!AssetDatabase.IsValidFolder(path))
                    AssetDatabase.CreateFolder(parentPath, pathSplit[i]);
                parentPath = path;
            }
            AssetDatabase.CreateAsset(asset, $"{pathName}.asset");
        }

        VRCExpressionParameters.Parameter[] GetDiffParameters(VRCExpressionParameters wsParams, VRCExpressionParameters bsParams) {
            if (wsParams == null) return Array.Empty<VRCExpressionParameters.Parameter>();
            if (bsParams == null) return wsParams.parameters;
            var parameterSet = new HashSet<VRCExpressionParameters.Parameter>(wsParams.parameters, SimpleVRCParameterComparer.Instance);
            parameterSet.ExceptWith(bsParams.parameters);
            var parameterArray = new VRCExpressionParameters.Parameter[parameterSet.Count];
            parameterSet.CopyTo(parameterArray);
            return parameterArray;
        }

        VRCExpressionsMenu.Control[] GetDiffMenu(VRCExpressionsMenu wsMenu, VRCExpressionsMenu bsMenu) {
            if (wsMenu == null) return Array.Empty<VRCExpressionsMenu.Control>();
            if (bsMenu == null) return wsMenu.controls.ToArray();
            var menuSet = new HashSet<VRCExpressionsMenu.Control>(wsMenu.controls, SimpleVRCMenuComparer.Instance);
            menuSet.ExceptWith(bsMenu.controls);
            var menuArray = new VRCExpressionsMenu.Control[menuSet.Count];
            menuSet.CopyTo(menuArray);
            return menuArray;
        }

        sealed class SimpleAnimParameterComparer : IEqualityComparer<AnimatorControllerParameter> {
            public static readonly SimpleAnimParameterComparer Instance = new();

            public bool Equals(AnimatorControllerParameter x, AnimatorControllerParameter y) => x.name == y.name;

            public int GetHashCode(AnimatorControllerParameter obj) => obj.name.GetHashCode();
        }

        sealed class SimpleVRCParameterComparer : IEqualityComparer<VRCExpressionParameters.Parameter> {
            public static readonly SimpleVRCParameterComparer Instance = new();

            public bool Equals(VRCExpressionParameters.Parameter x, VRCExpressionParameters.Parameter y) => x.name == y.name;

            public int GetHashCode(VRCExpressionParameters.Parameter obj) => obj.name.GetHashCode();
        }

        sealed class SimpleVRCMenuComparer : IEqualityComparer<VRCExpressionsMenu.Control> {
            public static readonly SimpleVRCMenuComparer Instance = new();

            public bool Equals(VRCExpressionsMenu.Control x, VRCExpressionsMenu.Control y) {
                if (x == y) return true;
                if (x == null || y == null) return false;
                return x.name == y.name &&
                    x.type == y.type &&
                    x.parameter.name == y.parameter.name &&
                    x.subMenu == y.subMenu &&
                    ParameterEquals(x.subParameters, y.subParameters) &&
                    x.icon == y.icon &&
                    x.value == y.value;
            }

            bool ParameterEquals(VRCExpressionsMenu.Control.Parameter[] x, VRCExpressionsMenu.Control.Parameter[] y) {
                if (x == y) return true;
                if (x == null || y == null || x.Length != y.Length) return false;
                for (int i = 0; i < x.Length; i++)
                    if (x[i].name != y[i].name)
                        return false;
                return true;
            }

            public int GetHashCode(VRCExpressionsMenu.Control obj) => obj.name.GetHashCode();
        }
    }
}