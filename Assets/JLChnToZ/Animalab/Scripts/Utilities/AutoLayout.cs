using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;

using UnityObject = UnityEngine.Object;
using UnityRandom = UnityEngine.Random;

namespace JLChnToZ.Animalab {
    // Quick and dirty force-directed graph layout algorithm arranges nodes in a animator state machine.
    public class AutoLayout {
        const float unitDistance = 12F;
        const float minDistance = 20F;
        const float minimalDelta = 0.01F;
        readonly AnimatorStateMachine stateMachine;
        readonly State[] nodes;
        readonly Dictionary<State, StatePosition> nodeStates = new Dictionary<State, StatePosition>();
        readonly HashSet<Transition> transitions = new HashSet<Transition>();
        public float attractiveForceStrength = 0.1f;
        public float repulsiveForceStrength = 0.5f;
        public float dampingFactor = 0.75F;

        public AutoLayout(AnimatorStateMachine stateMachine) {
            this.stateMachine = stateMachine;
            nodeStates.Add(StateType.Entry, new StatePosition(stateMachine.entryPosition));
            transitions.Add(new Transition(StateType.Entry, stateMachine.defaultState));
            foreach (var trans in stateMachine.entryTransitions)
                transitions.Add(new Transition(StateType.Entry, trans));
            nodeStates.Add(StateType.Exit, new StatePosition(stateMachine.exitPosition));
            nodeStates.Add(StateType.Any, new StatePosition(stateMachine.anyStatePosition));
            foreach (var trans in stateMachine.anyStateTransitions)
                transitions.Add(new Transition(StateType.Any, trans));
            foreach (var node in stateMachine.states) {
                nodeStates.Add(node.state, new StatePosition(node.position));
                foreach (var trans in node.state.transitions)
                    transitions.Add(new Transition(node.state, trans));
            }
            foreach (var node in stateMachine.stateMachines) {
                nodeStates.Add(node.stateMachine, new StatePosition(node.position));
                foreach (var trans in node.stateMachine.entryTransitions)
                    transitions.Add(new Transition(node.stateMachine, trans));
            }
            nodes = new State[nodeStates.Count];
            nodeStates.Keys.CopyTo(nodes, 0);
        }

        public void Iterate(int count) {
            for (int i = 0; i < count; i++)
                if (Iterate()) break;
        }

        public bool Iterate() {
            // Reset force
            var minPos = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maxPos = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var node in nodes) {
                if (!nodeStates.TryGetValue(node, out var nodeState))
                    continue;
                nodeState.force = Vector2.zero;
                minPos.x = Mathf.Min(minPos.x, nodeState.position.x);
                minPos.y = Mathf.Min(minPos.y, nodeState.position.y);
                maxPos.x = Mathf.Max(maxPos.x, nodeState.position.x);
                maxPos.y = Mathf.Max(maxPos.y, nodeState.position.y);
                nodeStates[node] = nodeState;
            }
            var maxForce = (maxPos - minPos).magnitude * 0.5F;

            // Calculate attractive force
            foreach (var trans in transitions) {
                if (!nodeStates.TryGetValue(trans.src, out var srcState) ||
                    !nodeStates.TryGetValue(trans.dest, out var destState))
                    continue;
                var delta = destState.position - srcState.position;
                float dist = delta.magnitude;
                if (dist < minDistance) continue;
                var force = delta.normalized * dist * attractiveForceStrength;
                srcState.force += force;
                destState.force -= force;
                nodeStates[trans.src] = srcState;
                nodeStates[trans.dest] = destState;
            }

            // Calculate repulsive force
            foreach (var node in nodes) {
                if (!nodeStates.TryGetValue(node, out var nodeState)) continue;
                foreach (var otherNode in nodes)
                    if (!node.Equals(otherNode) &&
                        nodeStates.TryGetValue(otherNode, out var otherState))
                        nodeState.force -= CalculateRepulseForce(
                            otherState.position - nodeState.position
                        );
                foreach (var trans in transitions)
                    if (!trans.src.Equals(node) && !trans.dest.Equals(node) &&
                        nodeStates.TryGetValue(trans.src, out var srcState) &&
                        nodeStates.TryGetValue(trans.dest, out var destState))
                        nodeState.force -= CalculateRepulseForce(
                            (srcState.position + destState.position) * 0.5F - nodeState.position
                        );
                nodeStates[node] = nodeState;
            }

            // Apply force
            bool stable = true;
            foreach (var node in nodes) {
                if (!nodeStates.TryGetValue(node, out var nodeState)) continue;
                var magnitude = nodeState.force.magnitude;
                if (magnitude > minimalDelta) stable = false;
                if (magnitude > maxForce) nodeState.force = nodeState.force.normalized * maxForce;
                nodeState.position += nodeState.force;
                nodeState.force *= dampingFactor;
                nodeStates[node] = nodeState;
            }
            return stable;
        }

        Vector2 CalculateRepulseForce(Vector2 delta) {
            float dist;
            while ((dist = delta.magnitude) < 0.0001F) {
                delta += new Vector2(
                    UnityRandom.Range(-minimalDelta, minimalDelta),
                    UnityRandom.Range(-minimalDelta, minimalDelta)
                );
            }
            var strengh = repulsiveForceStrength / dist;
            if (dist < minDistance) strengh = Mathf.Max(strengh, minDistance - dist);
            return delta.normalized * strengh;
        }

        public void Apply() {
            bool hasChanged = false;
            var states = stateMachine.states;
            StatePosition nodeState;
            for (int i = 0; i < states.Length; i++) {
                var node = states[i];
                if (!nodeStates.TryGetValue(node.state, out nodeState)) continue;
                var pos = nodeState.SteppedPosition;
                if (node.position == pos) continue;
                hasChanged = true;
                node.position = pos;
                states[i] = node;
            }
            if (hasChanged) stateMachine.states = states;
            hasChanged = false;
            var stateMachines = stateMachine.stateMachines;
            for (int i = 0; i < stateMachines.Length; i++) {
                var node = stateMachines[i];
                if (!nodeStates.TryGetValue(node.stateMachine, out nodeState)) continue;
                var pos = nodeState.SteppedPosition;
                if (node.position == pos) continue;
                hasChanged = true;
                node.position = pos;
                stateMachines[i] = node;
            }
            if (hasChanged) stateMachine.stateMachines = stateMachines;
            if (nodeStates.TryGetValue(StateType.Entry, out nodeState))
                stateMachine.entryPosition = nodeState.SteppedPosition;
            if (nodeStates.TryGetValue(StateType.Exit, out nodeState))
                stateMachine.exitPosition = nodeState.SteppedPosition;
            if (nodeStates.TryGetValue(StateType.Any, out nodeState))
                stateMachine.anyStatePosition = nodeState.SteppedPosition;
        }

        struct StatePosition {
            public Vector2 position;
            public Vector2 force;

            public Vector3 SteppedPosition {
                get {
                    var pos = new Vector3(position.x, position.y, 0F) * unitDistance;
                    pos.x = Mathf.Round(pos.x);
                    pos.y = Mathf.Round(pos.y);
                    return pos;
                }
            }

            public StatePosition(Vector3 startPosition) {
                position = (Vector2)startPosition / unitDistance;
                force = Vector2.zero;
            }
        }

        readonly struct State : IEquatable<State> {
            public readonly StateType type;
            public readonly UnityObject state;

            public State(StateType type) {
                state = null;
                this.type = type;
            }

            public State(UnityObject state) {
                type = StateType.Normal;
                this.state = state;
            }

            public bool Equals(State other) =>
                state == other.state && type == other.type;

            public override bool Equals(object obj) =>
                obj is State other && Equals(other);

            public override int GetHashCode() {
                int hash = (int)type;
                if (state != null)
                    hash ^= state.GetHashCode();
                return hash;
            }

            public override string ToString() {
                switch (type) {
                    case StateType.Normal: return state.name;
                    case StateType.Entry: return "(Entry)";
                    case StateType.Exit: return "(Exit)";
                    case StateType.Any: return "(Any)";
                    default: return "(Unknown)";
                }
            }

            public static implicit operator State(StateType type) => new State(type);

            public static implicit operator State(AnimatorState state) => new State(state);

            public static implicit operator State(AnimatorStateMachine sm) => new State(sm);

            public static implicit operator State(AnimatorTransitionBase trans) {
                if (trans.isExit) return new State(StateType.Exit);
                UnityObject result = trans.destinationState;
                if (result != null) return new State(result);
                result = trans.destinationStateMachine;
                if (result != null) return new State(result);
                return default;
            }
        }

        readonly struct Transition : IEquatable<Transition> {
            public readonly State src;
            public readonly State dest;

            public Transition(State src, State dest) {
                this.src = src;
                this.dest = dest;
            }

            public bool Equals(Transition other) =>
                (src.Equals(other.src) && dest.Equals(other.dest)) ||
                (src.Equals(other.dest) && dest.Equals(other.src));

            public override bool Equals(object obj) =>
                obj is Transition other && Equals(other);

            public override int GetHashCode() =>
                src.GetHashCode() ^ dest.GetHashCode();
        }

        enum StateType : byte {
            Unknown,
            Normal,
            Entry,
            Exit,
            Any,
        }
    }
}