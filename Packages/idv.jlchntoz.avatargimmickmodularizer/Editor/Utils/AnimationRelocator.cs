using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityObject = UnityEngine.Object;
using static UnityEngine.Object;

namespace JLChnToZ.GimmickInstallModularizer.Editors {
    public class AnimationRelocator {
        readonly Dictionary<RuntimeAnimatorController, AnimatorOverrideController> controllerOverrides = new();
        readonly Dictionary<AnimationClip, HashSet<AnimatorController>> dependencies = new();
        readonly Dictionary<AnimationClip, AnimationClip> clonedClips = new();
        readonly HashSet<AnimationClip> clonedClipsSet = new();

        public RuntimeAnimatorController this[RuntimeAnimatorController controller] =>
            controller != null &&
            controllerOverrides.TryGetValue(controller, out var overrideController) &&
            overrideController != null ? overrideController : controller;

        public AnimationClip this[AnimationClip clip] =>
            clip != null && clonedClips.TryGetValue(clip, out var clone) ? clone : clip;

        public ICollection<AnimationClip> OriginalClips => dependencies.Keys;

        public ICollection<AnimationClip> ClonedClips => clonedClips.Values;

        public ICollection<RuntimeAnimatorController> OriginalControllers => controllerOverrides.Keys;

        public ICollection<AnimatorOverrideController> OverrideControllers => controllerOverrides.Values;

        public void AddController(RuntimeAnimatorController baseController) {
            if (baseController == null || controllerOverrides.ContainsKey(baseController)) return;
            controllerOverrides[baseController] = null;
            foreach (var clip in baseController.animationClips) {
                if (!dependencies.TryGetValue(clip, out var controllers))
                    dependencies[clip] = controllers = new HashSet<AnimatorController>();
                controllers.Add(baseController as AnimatorController);
            }
        }

        public void RewriteBindingPaths(IDictionary<string, string> paths) {
            if (paths == null || paths.Count == 0) return;
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            var objectCurves = new List<ObjectReferenceKeyframe[]>();
            foreach (var clip in dependencies.Keys) {
                var isCloned = clonedClips.TryGetValue(clip, out var clonedClip);
                if (!isCloned) clonedClip = clip;
                foreach (var binding in AnimationUtility.GetCurveBindings(clonedClip)) {
                    if (!RewriteBindingPaths(binding, out var newBinding, paths)) continue;
                    bindings.Add(binding);
                    curves.Add(null);
                    bindings.Add(newBinding);
                    curves.Add(AnimationUtility.GetEditorCurve(clonedClip, binding));
                }
                if (bindings.Count > 0) {
                    if (!isCloned) {
                        clonedClip = GetClone(clip);
                        isCloned = true;
                    }
                    AnimationUtility.SetEditorCurves(clonedClip, bindings.ToArray(), curves.ToArray());
                    bindings.Clear();
                    curves.Clear();
                }
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clonedClip)) {
                    if (!RewriteBindingPaths(binding, out var newBinding, paths)) continue;
                    bindings.Add(binding);
                    objectCurves.Add(null);
                    bindings.Add(newBinding);
                    objectCurves.Add(AnimationUtility.GetObjectReferenceCurve(clonedClip, binding));
                }
                if (bindings.Count > 0) {
                    if (!isCloned) clonedClip = GetClone(clip);
                    AnimationUtility.SetObjectReferenceCurves(clonedClip, bindings.ToArray(), objectCurves.ToArray());
                    bindings.Clear();
                    objectCurves.Clear();
                }
            }
        }

        bool RewriteBindingPaths(EditorCurveBinding inBinding, out EditorCurveBinding outBinding, IDictionary<string, string> paths) {
            outBinding = inBinding;
            if (inBinding.path != null && paths.TryGetValue(inBinding.path, out var newPath)) {
                outBinding.path = newPath;
                return true;
            }
            return false;
        }


        public AnimationClip GetClone(AnimationClip clip) {
            if (clonedClipsSet.Contains(clip)) return clip;
            if (clonedClips.TryGetValue(clip, out var clone)) return clone;
            clonedClips[clip] = clone = Instantiate(clip);
            clone.name = $"{clip.name} Modified";
            if (dependencies.TryGetValue(clip, out var depdControllers))
                foreach (var controller in depdControllers) {
                    if (controller == null) continue;
                    if (!controllerOverrides.TryGetValue(controller, out var overrideController) || overrideController == null)
                        controllerOverrides[controller] = overrideController = new AnimatorOverrideController {
                            name = $"{controller.name} Override",
                            runtimeAnimatorController = controller,
                        };
                    overrideController[clip] = clone;
                    clonedClipsSet.Add(clone);
                }
            return clone;
        }
    }

    public class AnimatorOverrideControllerBaker {
        AnimatorController controller;
        readonly HashSet<UnityObject> createdObjects = new();
        readonly string name;
        readonly Dictionary<AnimationClip, AnimationClip> clipOverrides = new();

        public AnimatorOverrideControllerBaker(AnimatorOverrideController overrideController) {
            name = overrideController.name;
            var temp = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            while (overrideController != null) {
                overrideController.GetOverrides(temp);
                foreach (var map in temp)
                    if (map.Key != null && map.Value != null &&
                        !clipOverrides.ContainsKey(map.Key))
                        clipOverrides.Add(map.Key, map.Value);
                if (overrideController.runtimeAnimatorController is AnimatorController baseController) {
                    controller = baseController;
                    break;
                }
                overrideController = overrideController.runtimeAnimatorController as AnimatorOverrideController;
            }
        }

        public AnimatorController Bake() {
            if (clipOverrides.Count == 0) return controller; // No need to bake
            var dependencies = new Dictionary<UnityObject, HashSet<UnityObject>>();
            var remap = new Dictionary<UnityObject, UnityObject>();
            var walked = new HashSet<UnityObject>();
            var pending = new Stack<UnityObject>();
            var parentStack = new Stack<UnityObject>();
            var cloneNeeded = new HashSet<UnityObject>();
            foreach (var kv in clipOverrides) remap[kv.Key] = kv.Value;
            pending.Push(controller);
            while (pending.TryPop(out var entry)) {
                using var so = new SerializedObject(entry);
                var sp = so.GetIterator();
                while (sp.Next(true)) {
                    if (sp.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = sp.objectReferenceValue;
                    if (value == null || value is GameObject || value is Component) continue;
                    HashSet<UnityObject> parents;
                    if (cloneNeeded.Contains(value) || (value is AnimationClip clip && clipOverrides.ContainsKey(clip))) {
                        parentStack.Push(entry);
                        while (parentStack.TryPop(out var parent))
                            if (dependencies.TryGetValue(parent, out parents) &&
                                cloneNeeded.Add(parent))
                                foreach (var p in parents)
                                    parentStack.Push(p);
                    }
                    if (dependencies.TryGetValue(value, out parents))
                        parents.Add(entry);
                    else
                        dependencies.Add(value, new HashSet<UnityObject>() { entry });
                    if (walked.Add(value)) pending.Push(value);
                }
            }
            var newController = Instantiate(controller);
            createdObjects.Add(newController);
            newController.name = name;
            pending.Push(newController);
            while (pending.TryPop(out var entry)) {
                using var so = new SerializedObject(entry);
                var sp = so.GetIterator();
                while (sp.Next(true)) {
                    if (sp.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = sp.objectReferenceValue;
                    if (value == null) continue;
                    if (remap.TryGetValue(value, out var newValue)) {
                        sp.objectReferenceValue = newValue;
                        continue;
                    }
                    if (cloneNeeded.Contains(value)) {
                        try {
                            newValue = Instantiate(value);
                            if (newValue == null) throw new Exception("Failed to instantiate object.");
                        } catch (Exception ex) {
                            Debug.LogError($"Failed to clone {value.name} in {entry.name}: {ex.Message}");
                            cloneNeeded.Remove(value);
                            continue;
                        }
                        newValue.name = value.name;
                        newValue.hideFlags = value.hideFlags;
                        createdObjects.Add(newValue);
                        remap[value] = newValue;
                        sp.objectReferenceValue = newValue;
                        pending.Push(newValue);
                    }
                }
                so.ApplyModifiedProperties();
            }
            controller = newController;
            return controller;
        }

        public void SaveToAsset(UnityObject asset) {
            foreach (var createdObject in createdObjects)
                if (!AssetDatabase.Contains(createdObject))
                    AssetDatabase.AddObjectToAsset(createdObject, asset);
            foreach (var clip in clipOverrides.Values)
                if (!AssetDatabase.Contains(clip))
                    AssetDatabase.AddObjectToAsset(clip, asset);
        }
    }
}