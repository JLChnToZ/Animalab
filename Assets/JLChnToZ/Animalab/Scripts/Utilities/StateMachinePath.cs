using System;
using System.Collections;
using System.Collections.Generic;

namespace JLChnToZ.Animalab {
    public readonly struct StateMachinePath : IEquatable<StateMachinePath>, IEnumerable<string> {
        readonly string[] path;

        public int Depth => path?.Length ?? 0;

        public StateMachinePath Parent => Up(1);

        public StateMachinePath(string root) : this(new[] { root ?? "" }) { }

        public StateMachinePath(params string[] path) => this.path = path;

        public StateMachinePath Up(int depth) {
            if (depth == 0) return this;
            if (path == null || path.Length < depth)
                throw new ArgumentOutOfRangeException(nameof(depth));
            int newLength = path.Length - depth;
            var newPath = new string[newLength];
            Array.Copy(path, newPath, newLength);
            return new StateMachinePath(newPath);
        }

        Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(StateMachinePath other) {
            if (path == other.path) return true;
            if (path == null || path.Length == 0)
                return other.path == null || other.path.Length == 0;
            if (other.path == null || other.path.Length == 0 ||
                path.Length != other.path.Length)
                return false;
            for (int i = 0; i < path.Length; i++)
                if (path[i] != other.path[i]) return false;
            return true;
        }

        public override bool Equals(object obj) =>
            obj is StateMachinePath other && Equals(other);

        public override int GetHashCode() {
            int hash = 0;
            if (path != null) {
                hash = path.Length;
                var comparer = StringComparer.Ordinal;
                foreach (var name in path)
                    hash ^= comparer.GetHashCode(name);
            }
            return hash;
        }

        public override string ToString() =>
            path == null || path.Length == 0 ? "" : string.Join("/", path);

        public static implicit operator StateMachinePath(string name) =>
            new StateMachinePath(name);

        public static bool operator ==(StateMachinePath left, StateMachinePath right) =>
            left.Equals(right);

        public static bool operator !=(StateMachinePath left, StateMachinePath right) =>
            !left.Equals(right);

        public static StateMachinePath operator +(StateMachinePath left, string right) {
            if (left.path == null || left.path.Length == 0)
                return new StateMachinePath(right);
            int length = left.path.Length;
            var newPath = new string[length + 1];
            Array.Copy(left.path, newPath, length);
            newPath[length] = right;
            return new StateMachinePath(newPath);
        }

        public static StateMachinePath operator +(StateMachinePath left, StateMachinePath right) {
            if (right.path == null || right.path.Length == 0) return left;
            if (left.path == null || left.path.Length == 0) return right;
            int leftLength = left.path.Length, rightLength = right.path.Length;
            var newPath = new string[leftLength + rightLength];
            Array.Copy(left.path, newPath, leftLength);
            Array.Copy(right.path, 0, newPath, leftLength, rightLength);
            return new StateMachinePath(newPath);
        }

        public struct Enumerator : IEnumerator<string> {
            readonly string[] path;
            int index;

            public string Current {
                get {
                    if (path == null) throw new InvalidOperationException();
                    return path[index];
                }
            }

            object IEnumerator.Current => Current;

            public Enumerator(StateMachinePath path) {
                this.path = path.path;
                index = -1;
            }

            void IDisposable.Dispose() { }

            public bool MoveNext() => path != null && ++index < path.Length;

            public void Reset() => index = -1;
        }
    }
}