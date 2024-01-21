using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

using UnityObject = UnityEngine.Object;
using UnityRandom = UnityEngine.Random;

namespace JLChnToZ.AnimatorBehaviours {
    /// <summary>Base class for animator drivers.</summary>
    public abstract class AnimatorDriverBase : StateMachineBehaviour {
        [SerializeField] StateMachineTiming whenToExecute = StateMachineTiming.StateEnter, whenToCancel = StateMachineTiming.StateExit;
        [SerializeField] float minDelay, maxDelay;
        [SerializeField] bool randomDelay;
        CancellationTokenSource cts;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
            if (whenToExecute == StateMachineTiming.StateEnter)
                Run(animator, StateMachineTiming.StateEnter, stateInfo, layerIndex, stateInfo.fullPathHash);
            else if (whenToCancel == StateMachineTiming.StateEnter) cts?.Cancel();
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
            if (whenToExecute == StateMachineTiming.StateUpdate)
                Run(animator, StateMachineTiming.StateUpdate, stateInfo, layerIndex, stateInfo.fullPathHash);
            else if (whenToCancel == StateMachineTiming.StateUpdate) cts?.Cancel();
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
            if (whenToExecute == StateMachineTiming.StateExit)
                Run(animator, StateMachineTiming.StateExit, stateInfo, layerIndex, stateInfo.fullPathHash);
            else if (whenToCancel == StateMachineTiming.StateExit) cts?.Cancel();
        }

        public override void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
            if (whenToExecute == StateMachineTiming.StateMove)
                Run(animator, StateMachineTiming.StateMove, stateInfo, layerIndex, stateInfo.fullPathHash);
            else if (whenToCancel == StateMachineTiming.StateMove) cts?.Cancel();
        }

        public override void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
            if (whenToExecute == StateMachineTiming.StateIK)
                Run(animator, StateMachineTiming.StateIK, stateInfo, layerIndex, stateInfo.fullPathHash);
            else if (whenToCancel == StateMachineTiming.StateIK) cts?.Cancel();
        }

        public override void OnStateMachineEnter(Animator animator, int stateMachinePathHash) {
            if (whenToExecute == StateMachineTiming.StateMachineEnter)
                Run(animator, StateMachineTiming.StateMachineEnter, default, -1, stateMachinePathHash);
            else if (whenToCancel == StateMachineTiming.StateMachineEnter) cts?.Cancel();
        }

        public override void OnStateMachineExit(Animator animator, int stateMachinePathHash) {
            if (whenToExecute == StateMachineTiming.StateMachineExit)
                Run(animator, StateMachineTiming.StateMachineExit, default, -1, stateMachinePathHash);
            else if (whenToCancel == StateMachineTiming.StateMachineExit) cts?.Cancel();
        }

        void Run(Animator animator, StateMachineTiming timing, AnimatorStateInfo stateInfo, int layerIndex, int pathHash) =>
            RunCore(animator, timing, stateInfo, layerIndex, pathHash).Forget();

        protected abstract UniTaskVoid RunCore(
            Animator animator,
            StateMachineTiming timing,
            AnimatorStateInfo stateInfo,
            int layerIndex,
            int pathHash
        );

        protected UniTask Delay(out CancellationToken cancellationToken) {
            var delayTime = randomDelay && maxDelay > minDelay ? UnityRandom.Range(minDelay, maxDelay) : minDelay;
            if (delayTime <= 0) return UniTask.CompletedTask;
            var ts = new TimeSpan((long)(delayTime * TimeSpan.TicksPerSecond));
            if (whenToCancel == StateMachineTiming.Never) {
                cancellationToken = default;
                return UniTask.Delay(ts);
            }
            cts?.Cancel();
            cts = new CancellationTokenSource();
            cancellationToken = cts.Token;
            return UniTask.Delay(ts, cancellationToken: cts.Token);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(AnimatorDriverBase), true)]
    public class AnimatorDriverBaseEditor : Editor {
        static GUIContent delayLabel, randomDelayLabel;
        static GUIContent[] minMaxDelayLabel;
        static float[] minMaxDelay;
        SerializedProperty executeTimingProperty, cancelTimingProperty, minDelayProperty, maxDelayProperty, randomDelayProperty;

        protected virtual void OnEnable() {
            if (delayLabel == null) delayLabel = new GUIContent("Delay");
            if (randomDelayLabel == null) randomDelayLabel = new GUIContent("R", "Random Delay");
            if (minMaxDelayLabel == null) minMaxDelayLabel = new[] { new GUIContent("Min"), new GUIContent("Max") };
            if (minMaxDelay == null) minMaxDelay = new float[2];
            executeTimingProperty = serializedObject.FindProperty("whenToExecute");
            cancelTimingProperty = serializedObject.FindProperty("whenToCancel");
            minDelayProperty = serializedObject.FindProperty("minDelay");
            maxDelayProperty = serializedObject.FindProperty("maxDelay");
            randomDelayProperty = serializedObject.FindProperty("randomDelay");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            DrawBaseFields();
            serializedObject.ApplyModifiedProperties();
        }

        protected void DrawBaseFields() {
            EditorGUILayout.PropertyField(executeTimingProperty);
            using (new EditorGUILayout.HorizontalScope()) {
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    var rect = EditorGUILayout.GetControlRect(true);
                    rect = EditorGUI.PrefixLabel(rect, delayLabel);
                    if (randomDelayProperty.boolValue) {
                        minMaxDelay[0] = minDelayProperty.floatValue;
                        minMaxDelay[1] = maxDelayProperty.floatValue;
                        EditorGUI.MultiFloatField(rect, minMaxDelayLabel, minMaxDelay);
                    } else {
                        minMaxDelay[0] = EditorGUI.FloatField(rect, minDelayProperty.floatValue);
                        minMaxDelay[1] = minMaxDelay[0];
                    }
                    if (changed.changed) {
                        if (minMaxDelay[0] < 0) minMaxDelay[0] = 0;
                        if (minMaxDelay[1] < minMaxDelay[0]) minMaxDelay[1] = minMaxDelay[0];
                        minDelayProperty.floatValue = minMaxDelay[0];
                        maxDelayProperty.floatValue = minMaxDelay[1];
                    }
                }
                {
                    var buttonStyle = EditorStyles.miniButton;
                    var size = buttonStyle.CalcSize(randomDelayLabel);
                    var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, buttonStyle, GUILayout.Width(size.x));
                    using (new EditorGUI.PropertyScope(rect, GUIContent.none, randomDelayProperty))
                        randomDelayProperty.boolValue = GUI.Toggle(rect, randomDelayProperty.boolValue, randomDelayLabel, buttonStyle);
                }
            }
            if (maxDelayProperty.floatValue > 0) EditorGUILayout.PropertyField(cancelTimingProperty);
        }

        public static AnimatorController GetAnimatorController(UnityObject target) {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out var guid, out long _)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }
    }
#endif

    public enum StateMachineTiming : byte {
        Never,
        StateEnter,
        StateUpdate,
        StateMove,
        StateIK,
        StateExit,
        StateMachineEnter,
        StateMachineExit,
    }
}