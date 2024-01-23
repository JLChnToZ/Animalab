using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.Animalab {
    public class Unparser {
        readonly StringBuilder sb;
        readonly Stack<(StateMachinePath, AnimatorStateMachine)> processingQueue = new Stack<(StateMachinePath, AnimatorStateMachine)>();
        readonly Dictionary<UnityObject, StateMachinePath> pathLookup = new Dictionary<UnityObject, StateMachinePath>();
        readonly Stack<(Motion, int, BlendTreeType, ChildMotion)> pendingMotionStack = new Stack<(Motion, int, BlendTreeType, ChildMotion)>();
        readonly Stack<ChildMotion> motionStack = new Stack<ChildMotion>();
        readonly List<List<AnimatorTransitionBase>> transitionGroups = new List<List<AnimatorTransitionBase>>();
        readonly Queue<List<AnimatorTransitionBase>> transitionPool = new Queue<List<AnimatorTransitionBase>>();
        string mainAssetPath;
        AnimatorControllerLayer[] layers;

        public static string Unparse(AnimatorController anim) {
            var sb = new StringBuilder();
            Unparse(anim, sb);
            return sb.ToString();
        }

        public static void Unparse(AnimatorController anim, StringBuilder sb) {
            new Unparser(sb).Write(anim);
        }

        public static string Unparse(StateMachineBehaviour smb) {
            var sb = new StringBuilder();
            new Unparser(sb).Write(smb, 0);
            return sb.ToString();
        }

        public static void Unparse(StringBuilder sb, StateMachineBehaviour smb) =>
            new Unparser(sb).Write(smb, 0);

        private Unparser(StringBuilder sb) => this.sb = sb;

        void Write(AnimatorController anim) {
            mainAssetPath = AssetDatabase.GetAssetPath(anim);
            layers = anim.layers;
            foreach (var param in anim.parameters) Write(param);
            foreach (var layer in layers) Write(layer);
        }

        void Write(AnimatorControllerParameter parameter) {
            sb.Append($"{Format(parameter.type)} {Format(parameter.name)} = ");
            switch (parameter.type) {
                case AnimatorControllerParameterType.Bool:
                    sb.Append(parameter.defaultBool ? "true" : "false");
                    break;
                case AnimatorControllerParameterType.Float:
                    sb.Append(parameter.defaultFloat);
                    break;
                case AnimatorControllerParameterType.Int:
                    sb.Append(parameter.defaultInt);
                    break;
            }
            sb.AppendLine(";");
        }

        void Write(AnimatorControllerLayer layer) {
            sb.AppendLine().AppendLine($"layer {Format(layer.name)} {{");
            if (layer.defaultWeight != 1) sb.AppendLine($"  weight {layer.defaultWeight};");
            if (layer.blendingMode != AnimatorLayerBlendingMode.Override) sb.AppendLine($"  {Format(layer.blendingMode)};");
            if (layer.iKPass) sb.AppendLine($"  ikPass;");
            if (layer.avatarMask != null) sb.AppendLine($"  mask {Format(GetShortAssetPath(mainAssetPath, layer.avatarMask), true)};");
            if (layer.syncedLayerIndex != -1) sb.AppendLine($"  sync {Format(layers[layer.syncedLayerIndex].name)};");
            if (layer.syncedLayerAffectsTiming) sb.AppendLine($"  syncTiming;");
            var rootStateMachine = layer.stateMachine;
            pathLookup.Clear();
            processingQueue.Clear();
            processingQueue.Push((rootStateMachine.name, rootStateMachine));
            while (processingQueue.Count > 0) {
                var (name, stateMachine) = processingQueue.Pop();
                pathLookup[stateMachine] = name;
                foreach (var child in stateMachine.states) {
                    var state = child.state;
                    var childPath = name + state.name;
                    pathLookup[state] = childPath;
                }
                foreach (var child in stateMachine.stateMachines) {
                    var subStateMachine = child.stateMachine;
                    processingQueue.Push((name + subStateMachine.name, subStateMachine));
                }
            }
            Write(rootStateMachine, 1);
            sb.AppendLine($"}}");
        }

        void Write(AnimatorStateMachine stateMachine, int indentLevel) {
            var indentStr = new string(' ', indentLevel * 2);
            var defaultState = stateMachine.defaultState;
            if (defaultState != null) {
                sb.Append(indentStr);
                sb.AppendLine($"default {Format(defaultState.name)};");
            }
            var entryTrans = stateMachine.entryTransitions;
            if (entryTrans.Length > 0)
                Write(entryTrans, stateMachine, indentLevel, false);
            var anyStateTrans = stateMachine.anyStateTransitions;
            if (anyStateTrans.Length > 0)
                Write(anyStateTrans, stateMachine, indentLevel, true);
            foreach (var entry in stateMachine.states) {
                var state = entry.state;
                sb.AppendLine();
                sb.Append(indentStr);
                sb.AppendLine($"state {Format(state.name)} {{");
                Write(state, indentLevel + 1);
                foreach (var smb in state.behaviours) {
                    sb.AppendLine();
                    Write(smb, indentLevel + 1);
                }
                Write(state.transitions, state, indentLevel + 1, false);
                sb.Append(indentStr);
                sb.AppendLine($"}}");
            }
            foreach (var entry in stateMachine.stateMachines) {
                var subStateMachine = entry.stateMachine;
                sb.AppendLine();
                sb.Append(indentStr);
                sb.AppendLine($"stateMachine {Format(subStateMachine.name)} {{");
                Write(subStateMachine, indentLevel + 1);
                foreach (var smb in subStateMachine.behaviours) {
                    sb.AppendLine();
                    Write(smb, indentLevel + 1);
                }
                sb.Append(indentStr);
                sb.AppendLine($"}}");
            }
        }

        void Write(AnimatorState state, int indentLevel) {
            var indentStr = new string(' ', indentLevel * 2);
            if (state.timeParameterActive)
                sb.Append(indentStr).AppendLine($"time {Format(state.timeParameter)};");
            if (state.speedParameterActive)
                sb.Append(indentStr).AppendLine($"speed {Format(state.speedParameter)};");
            if (state.cycleOffsetParameterActive)
                sb.Append(indentStr).AppendLine($"cycleOffset {Format(state.cycleOffsetParameter)};");
            if (state.mirrorParameterActive)
                sb.Append(indentStr).AppendLine($"mirror {Format(state.mirrorParameter)};");
            if (state.iKOnFeet)
                sb.Append(indentStr).AppendLine("ikOnFeet;");
            if (state.writeDefaultValues)
                sb.Append(indentStr).AppendLine("writeDefaults;");
            if (!string.IsNullOrEmpty(state.tag))
                sb.Append(indentStr).AppendLine($"tag {Format(state.tag, true)};");
            pendingMotionStack.Clear();
            motionStack.Clear();
            var rootMotion = new ChildMotion {
                motion = state.motion,
                cycleOffset = state.cycleOffset,
                mirror = state.mirror,
                timeScale = state.speed,
            };
            pendingMotionStack.Push((state.motion, 0, (BlendTreeType)(-1), rootMotion));
            motionStack.Push(rootMotion);
            int lastDepth = 0;
            while (pendingMotionStack.Count > 0) {
                var (motion, depth, blendTreeType, childMotion) = pendingMotionStack.Pop();
                while (depth < lastDepth) {
                    lastDepth--;
                    indentStr = new string(' ', (lastDepth + indentLevel) * 2);
                    sb.Append($"{indentStr}}}");
                    Write(motionStack.Pop());
                }
                if (lastDepth != depth) {
                    lastDepth = depth;
                    indentStr = new string(' ', (lastDepth + indentLevel) * 2);
                }
                sb.Append(indentStr);
                switch (blendTreeType) {
                    case BlendTreeType.Simple1D:
                        sb.Append($"{childMotion.threshold}: ");
                        break;
                    case BlendTreeType.SimpleDirectional2D:
                    case BlendTreeType.FreeformDirectional2D:
                    case BlendTreeType.FreeformCartesian2D:
                        sb.Append($"({childMotion.position.x}, {childMotion.position.y}): ");
                        break;
                    case BlendTreeType.Direct:
                        sb.Append($"{Format(childMotion.directBlendParameter)}: ");
                        break;
                }
                if (motion is BlendTree blendTree) {
                    var blendType = blendTree.blendType;
                    sb.Append($"blendtree {Format(blendTree.name, true)} {Format(blendType)}");
                    switch (blendType) {
                        case BlendTreeType.Simple1D:
                            sb.Append($"({Format(blendTree.blendParameter)})");
                            break;
                        case BlendTreeType.SimpleDirectional2D:
                        case BlendTreeType.FreeformDirectional2D:
                        case BlendTreeType.FreeformCartesian2D:
                            sb.Append($"({Format(blendTree.blendParameter)}, {Format(blendTree.blendParameterY)})");
                            break;
                    }
                    if (!blendTree.useAutomaticThresholds) sb.Append($" threshold({blendTree.minThreshold}, {blendTree.maxThreshold})");
                    sb.AppendLine(" {");
                    var children = blendTree.children;
                    for (int i = children.Length - 1; i >= 0; i--) {
                        var child = children[i];
                        pendingMotionStack.Push((child.motion, depth + 1, blendType, child));
                        motionStack.Push(child);
                    }
                    continue;
                }
                if (motion == null)
                    sb.Append("empty");
                else {
                    if (motion is AnimationClip) sb.Append("clip ");
                    else sb.Append("motion ");
                    var assetPath = GetShortAssetPath(mainAssetPath, motion);
                    if (!string.IsNullOrEmpty(assetPath))
                        sb.Append(Format(assetPath, true));
                    else
                        sb.Append(Format(motion.name, true));
                }
                Write(childMotion);
            }
            while (lastDepth > 0) {
                lastDepth--;
                sb.Append($"{new string(' ', (lastDepth + indentLevel) * 2)}}}");
                Write(motionStack.Pop());
            }
        }

        void Write(ChildMotion childMotion) {
            if (childMotion.timeScale != 1)
                sb.Append($" * {childMotion.timeScale}");
            if (childMotion.cycleOffset != 0)
                sb.Append($" {childMotion.cycleOffset:+ 0.###;- 0.###;}");
            if (childMotion.mirror)
                sb.Append(" mirror");
            sb.AppendLine(";");
        }

        void Write(AnimatorTransitionBase[] transitions, UnityObject source, int indention, bool isAny) {
            if (transitions == null || transitions.Length == 0) return;
            var indentStr = new string(' ', indention * 2 - 1);
            {
                transitionGroups.Clear();
                List<AnimatorTransitionBase> group;
                if (transitionPool.Count > 0) {
                    group = transitionPool.Dequeue();
                    group.Clear();
                } else
                    group = new List<AnimatorTransitionBase>();
                group.Add(transitions[0]);
                transitionGroups.Add(group);
                for (int i = 1; i < transitions.Length; i++) {
                    var lastTransition = transitions[i - 1];
                    var transition = transitions[i];
                    if (lastTransition.mute == transition.mute &&
                        lastTransition.solo == transition.solo &&
                        GetState(lastTransition) == GetState(transition) &&
                        GetExitTime(lastTransition) == GetExitTime(transition) &&
                        (lastTransition.conditions.Length > 0) == (transition.conditions.Length > 0) && (
                        !(lastTransition is AnimatorStateTransition lastASF && transition is AnimatorStateTransition asf) || (
                            lastASF.hasFixedDuration == asf.hasFixedDuration &&
                            lastASF.duration == asf.duration &&
                            lastASF.offset == asf.offset &&
                            lastASF.interruptionSource == asf.interruptionSource &&
                            GetOrderedInterruption(lastASF) == GetOrderedInterruption(asf) &&
                            lastASF.canTransitionToSelf == asf.canTransitionToSelf
                        )
                    )) {
                        group.Add(transition);
                        continue;
                    }
                    if (transitionPool.Count > 0) {
                        group = transitionPool.Dequeue();
                        group.Clear();
                    } else
                        group = new List<AnimatorTransitionBase>();
                    group.Add(transition);
                    transitionGroups.Add(group);
                }
            }
            foreach (var group in transitionGroups) {
                bool isFirst = true;
                foreach (var transition in group) {
                    var conditions = transition.conditions;
                    if (conditions.Length == 0) continue;
                    if (isFirst) {
                        sb.Append(indentStr);
                        if (transition.mute) sb.Append(" muted");
                        if (transition.solo) sb.Append(" solo");
                        if (transition is AnimatorStateTransition trans) {
                            if (isAny) {
                                sb.Append(" any");
                                if (!trans.canTransitionToSelf) sb.Append(" noSelf");
                            }
                            if (trans.hasExitTime) sb.Append($" wait({trans.exitTime})");
                        }
                        sb.Append(" if(");
                        isFirst = false;
                    } else {
                        sb.AppendLine(" ||");
                        sb.Append(indentStr);
                        sb.Append("    ");
                    }
                    bool notFirstCondition = false;
                    foreach (var condition in conditions) {
                        if (notFirstCondition) sb.Append(" && ");
                        else notFirstCondition = true;
                        if (condition.mode == AnimatorConditionMode.IfNot) sb.Append('!');
                        sb.Append(Format(condition.parameter));
                        switch (condition.mode) {
                            case AnimatorConditionMode.Equals: sb.Append($" == {condition.threshold}"); break;
                            case AnimatorConditionMode.NotEqual: sb.Append($" != {condition.threshold}"); break;
                            case AnimatorConditionMode.Greater: sb.Append($" > {condition.threshold}"); break;
                            case AnimatorConditionMode.Less: sb.Append($" < {condition.threshold}"); break;
                        }
                    }
                }
                if (isFirst) {
                    sb.Append(indentStr);
                    var transition = group[0];
                    if (transition.mute) sb.Append(" muted");
                    if (transition.solo) sb.Append(" solo");
                    if (transition is AnimatorStateTransition trans) {
                        if (isAny) {
                            sb.Append(" any");
                            if (!trans.canTransitionToSelf) sb.Append(" noSelf");
                        }
                        if (trans.hasExitTime) sb.Append($" wait({trans.exitTime})");
                    }
                } else {
                    sb.AppendLine(")");
                    sb.Append(indentStr);
                    sb.Append("  ");
                }
                {
                    var transition = group[0];
                    if (transition is AnimatorStateTransition trans) {
                        if (trans.duration > 0) sb.Append($" fade({trans.duration}{(trans.hasFixedDuration ? "s" : "")})");
                        if (trans.offset != 0) sb.Append($" {trans.offset:+ 0.###;- 0.###;}");
                        if (trans.interruptionSource != TransitionInterruptionSource.None) {
                            sb.Append($" {Format(trans.interruptionSource)}");
                            if (trans.orderedInterruption) sb.Append(" ordered");
                        }
                    }
                    var state = GetState(transition);
                    if (state == null)
                        sb.Append(" end");
                    else {
                        sb.Append(" goto ");
                        if (!pathLookup.TryGetValue(source, out var sourcePath)) {
                            Debug.LogWarning($"Cannot find path for {source.name}");
                        }
                        if (!pathLookup.TryGetValue(state, out var statePath)) {
                            pathLookup[state] = statePath = sourcePath + state.name;
                            Debug.LogWarning($"Cannot find path for {state.name}, created path {statePath}");
                        }
                        if (statePath.Parent == sourcePath)
                            sb.Append(Format(state.name));
                        else {
                            bool isFirstPart = statePath.Depth > 1;
                            foreach (var part in statePath) {
                                if (isFirstPart) isFirstPart = false;
                                else sb.Append('/');
                                sb.Append(Format(part));
                            }
                        }
                    }
                    sb.AppendLine(";");
                }
                transitionPool.Enqueue(group);
            }
        }

        void Write(StateMachineBehaviour smb, int indentLevel) {
            sb.Append(new string(' ', indentLevel * 2));
            var smbType = smb.GetType();
            var ns = smbType.Namespace;
            if (!string.IsNullOrEmpty(ns)) sb.Append(ns).Append('.');
            sb.Append(smbType.Name).AppendLine(" {");
            var depthData = new Stack<(int propertyNameLength, bool isArray)>();
            using (var so = new SerializedObject(smb)) {
                var sp = so.GetIterator();
                sp.NextVisible(true); // skip m_Script
                while (sp.NextVisible(true)) {
                    while (sp.depth < depthData.Count) {
                        sb.Append(new string(' ', (indentLevel + depthData.Count) * 2));
                        var (_, isArray) = depthData.Pop();
                        if (isArray)
                            sb.AppendLine("];");
                        else
                            sb.AppendLine("};");
                    }
                    var path = sp.propertyPath;
                    var depth = sp.depth;
                    var (lastLevelPropNameLength, lastLevelIsArray) = depthData.Count > 0 ? depthData.Peek() : (-1, false);
                    if (lastLevelIsArray && sp.propertyType == SerializedPropertyType.ArraySize) continue;
                    sb.Append(new string(' ', (indentLevel + depth + 1) * 2));
                    if (!lastLevelIsArray) sb.Append(path.Substring(lastLevelPropNameLength + 1));
                    switch (sp.propertyType) {
                        case SerializedPropertyType.Boolean:
                            sb.Append(sp.boolValue ? " = true" : " = false");
                            break;
                        case SerializedPropertyType.Float:
                            sb.Append($" = {sp.doubleValue}");
                            break;
                        case SerializedPropertyType.Integer:
                            sb.Append($" = {sp.longValue}");
                            break;
                        case SerializedPropertyType.String:
                            sb.Append($" = {Format(sp.stringValue, true)}");
                            break;
                        case SerializedPropertyType.Enum:
                            sb.Append($" = {Format(sp.enumNames[sp.enumValueIndex])}");
                            break;
                        case SerializedPropertyType.LayerMask:
                            sb.Append(" = ");
                            bool isFirst = true;
                            for (int i = 0; i < 32; i++)
                                if ((sp.intValue & (1 << i)) != 0) {
                                    if (isFirst) isFirst = false;
                                    else sb.Append(", ");
                                    var layerName = LayerMask.LayerToName(i);
                                    if (string.IsNullOrEmpty(layerName))
                                        sb.Append(i);
                                    else
                                        sb.Append(Format(layerName));
                                }
                            break;
                        case SerializedPropertyType.ObjectReference:
                            if (sp.objectReferenceValue == null) {
                                sb.Append(" = null");
                                break;
                            }
                            var assetPath = GetShortAssetPath(AssetDatabase.GetAssetPath(smb), sp.objectReferenceValue);
                            if (!string.IsNullOrEmpty(assetPath))
                                sb.Append($" = {Format(assetPath, true)}");
                            else
                                sb.Append($" = {Format(sp.objectReferenceValue.name, true)}");
                            break;
                    }
                    if (sp.hasVisibleChildren) {
                        var isArray = sp.isArray;
                        depthData.Push((path.Length, isArray));
                        if (!lastLevelIsArray)
                            sb.Append(" = ");
                        if (isArray)
                            sb.AppendLine("[");
                        else
                            sb.AppendLine("{");
                    } else
                        sb.AppendLine(";");
                }
            }
            while (depthData.Count > 0) {
                sb.Append(new string(' ', (indentLevel + depthData.Count) * 2));
                var (_, isArray) = depthData.Pop();
                if (isArray)
                    sb.AppendLine("];");
                else
                    sb.AppendLine("};");
            }
            sb.Append(new string(' ', indentLevel * 2)).AppendLine("};");
        }

        static string GetShortAssetPath(string mainAssetPath, UnityObject asset) {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath)) return "";
            if (!string.IsNullOrEmpty(mainAssetPath) && !string.IsNullOrEmpty(assetPath)) {
                var mainAssetPathUri = new Uri($"file:///{mainAssetPath}");
                var assetPathUri = new Uri($"file:///{assetPath}");
                var newUri = Uri.UnescapeDataString(mainAssetPathUri.MakeRelativeUri(assetPathUri).ToString());
                if (!newUri.StartsWith(".")) newUri = $"./{newUri}";
                if (assetPath.Length > newUri.Length)
                    return AssetDatabase.IsMainAsset(asset) ? newUri : $"{newUri}#{asset.name}";
            }
            return AssetDatabase.IsMainAsset(asset) ? assetPath : $"{assetPath}#{asset.name}";
        }

        static UnityObject GetState(AnimatorTransitionBase transition) {
            if (transition.isExit)
                return null;
            if (transition.destinationState != null)
                return transition.destinationState;
            if (transition.destinationStateMachine != null)
                return transition.destinationStateMachine;
            return null;
        }

        static float GetExitTime(AnimatorTransitionBase baseTrans) =>
            baseTrans is AnimatorStateTransition transition && transition.hasExitTime ? transition.exitTime : -1;

        static bool GetOrderedInterruption(AnimatorStateTransition transition) =>
            transition.interruptionSource != TransitionInterruptionSource.None && transition.orderedInterruption;
        
        static string Format(string input, bool forced = false) {
            if (input == null) input = "";
            StringBuilder sb = null;
            char delimiter = '"';
            if (forced || (input.Length > 1 && (
                char.IsDigit(input[0]) ||
                char.IsWhiteSpace(input[0]) ||
                char.IsWhiteSpace(input[input.Length - 1])
            ))) {
                if (input.Length > 0 && input.IndexOf('"') >= 0 && input.IndexOf('\'') < 0)
                    delimiter = '\'';
                sb = new StringBuilder(input.Length + 2).Append(delimiter);
            }
            for (int i = 0; i < input.Length; i++) {
                var c = input[i];
                if (sb != null) {
                    switch (c) {
                        case '"':
                            if (delimiter == '"') goto case '\\';
                            break;
                        case '\'':
                            if (delimiter == '\'') goto case '\\';
                            break;
                        case '\0': c = '0'; goto case '\\';
                        case '\a': c = 'a'; goto case '\\';
                        case '\b': c = 'b'; goto case '\\';
                        case '\x1b': c = 'e'; goto case '\\';
                        case '\f': c = 'f'; goto case '\\';
                        case '\n': c = 'n'; goto case '\\';
                        case '\r': c = 'r'; goto case '\\';
                        case '\t': c = 't'; goto case '\\';
                        case '\v': c = 'v'; goto case '\\';
                        case '\\': sb.Append('\\'); break;
                    }
                    sb.Append(c);
                } else switch (c) {
                    case '"':
                        if (i < input.Length - 1 && input.IndexOf('\'', i + 1) < 0)
                            delimiter = '\'';
                        goto case '\'';
                    case '\0': c = '0'; goto case '\\';
                    case '\a': c = 'a'; goto case '\\';
                    case '\b': c = 'b'; goto case '\\';
                    case '\x1b': c = 'e'; goto case '\\';
                    case '\f': c = 'f'; goto case '\\';
                    case '\n': c = 'n'; goto case '\\';
                    case '\r': c = 'r'; goto case '\\';
                    case '\t': c = 't'; goto case '\\';
                    case '\v': c = 'v'; goto case '\\';
                    case '\'':
                    case '\\':
                        sb = new StringBuilder(input.Length + 2)
                            .Append(delimiter)
                            .Append(input, 0, i)
                            .Append('\\')
                            .Append(c);
                        break;
                    default:
                        if (Array.IndexOf(AnimalabParserBase.symbols, c) >= 0 || (c < 128 && c != '_' && !char.IsLetterOrDigit(c))) {
                            if (i < input.Length - 1 && input.IndexOf('"', i + 1) >= 0 && input.IndexOf('\'', i + 1) < 0)
                                delimiter = '\'';
                            sb = new StringBuilder(input.Length + 2)
                                .Append(delimiter)
                                .Append(input, 0, i + 1);
                        }
                        break;
                }
            }
            return sb != null ? sb.Append(delimiter).ToString() : input;
        }

        static string Format(Enum value) {
            var s = value.ToString();
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }
    }
}