using System;
using System.Collections;
using System.Collections.Generic;

namespace JLChnToZ.MathUtilities {
    /// <summary>
    /// Simple stack implementation with ability to quickly pull or push multiple elements at once.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>
    /// This stack is created as struct to avoid unnecessary heap allocations.
    /// Undely array is created upon first push or setting the capacity.
    /// </remarks>
    public struct FastStack<T> : ICollection<T>, IReadOnlyCollection<T> {
        const int defaultCapacity = 4;
        int pointer;
        T[] stack;

        /// <summary>
        /// Gets the number of elements contained in the stack.
        /// </summary>
        public readonly int Count => pointer;

        /// <summary>
        /// Gets or sets the capacity of the stack.
        /// </summary>
        /// <remarks>
        /// Setting the capacity to a value less than the current number of elements will have no effect.
        /// The capacity is always rounded up to the nearest power of two.
        /// </remarks>
        public int Capacity {
            readonly get => stack?.Length ?? 0;
            set {
                if (value < pointer) return;
                value = value < defaultCapacity ? defaultCapacity : NextPowerOfTwo(value);
                if (stack == null)
                    stack = new T[value];
                else if (value != stack.Length)
                    Array.Resize(ref stack, value);
            }
        }

        readonly bool ICollection<T>.IsReadOnly => false;

        static int NextPowerOfTwo(int value) {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        /// <summary>
        /// Returns the element at the top of the stack without removing it.
        /// </summary>
        /// <returns>The element at the top of the stack.</returns>
        /// <remarks>
        /// This method will silently return default value if the stack is empty.
        /// </remarks>
        public readonly T Peek() =>
            pointer > 0 && stack != null ? stack[pointer - 1] : default;

        /// <summary>
        /// Returns a range of elements at the top of the stack without removing them.
        /// </summary>
        /// <param name="count">The number of elements to peek.</param>
        /// <returns>The range of elements at the top of the stack.</returns>
        /// <remarks>
        /// For efficiency, the returned span is a direct view of the internal array and
        /// thus it does not obey the stack's LIFO order. The caller should be aware of this.
        /// If count is less than or equal to zero, or greater than the number of elements in the stack,
        /// this method will silently return an empty span.
        /// </remarks>
        public readonly ReadOnlySpan<T> Peek(int count) {
            if (count > pointer) count = pointer;
            if (count <= 0 || stack == null) return ReadOnlySpan<T>.Empty;
            return stack.AsSpan(pointer - count, count);
        }
        
        /// <summary>
        /// Returns the element at the top of the stack without removing it.
        /// </summary>
        /// <param name="value">The element at the top of the stack.</param>
        /// <returns>True if the stack is not empty, otherwise false.</returns>
        public readonly bool TryPeek(out T value) {
            if (pointer > 0 && stack != null) {
                value = stack[pointer - 1];
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Inserts an object at the top of the stack.
        /// </summary>
        /// <param name="value">The object to push onto the stack.</param>
        public void Push(T value) {
            if (stack == null)
                stack = new T[Math.Max(defaultCapacity, pointer << 1)];
            else if (pointer >= stack.Length)
                Array.Resize(ref stack, stack.Length << 1);
            stack[pointer++] = value;
        }

        /// <summary>
        /// Inserts a range of elements at the top of the stack.
        /// </summary>
        /// <param name="values">The range of elements to push onto the stack.</param>
        public void Push(ReadOnlySpan<T> values) {
            if (values.IsEmpty) return;
            int newSize = pointer + values.Length;
            Capacity = newSize;
            values.CopyTo(stack.AsSpan(pointer));
            pointer = newSize;
        }

        /// <summary>
        /// Removes and returns the object at the top of the stack.
        /// </summary>
        /// <returns>The object removed from the top of the stack.</returns>
        public T Pop() =>
            pointer > 0 && stack != null ? stack[--pointer] : default;
        
        /// <summary>
        /// Removes and returns the object at the top of the stack.
        /// </summary>
        /// <param name="value">The object removed from the top of the stack.</param>
        /// <returns>True if the stack is not empty, otherwise false.</returns>
        public bool TryPop(out T value) {
            if (pointer > 0 && stack != null) {
                value = stack[--pointer];
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Removes and returns a range of elements from the top of the stack.
        /// </summary>
        /// <param name="count">The number of elements to remove.</param>
        /// <returns>The range of elements removed from the top of the stack.</returns>
        /// <remarks>
        /// For efficiency, the returned span is a direct view of the internal array and
        /// thus it does not obey the stack's LIFO order. The caller should be aware of this.
        /// If count is less than or equal to zero, or greater than the number of elements in the stack,
        /// this method will silently return an empty span.
        /// </remarks>
        public ReadOnlySpan<T> Pop(int count) {
            if (count <= 0 || pointer < count || stack == null) return ReadOnlySpan<T>.Empty;
            pointer -= count;
            return stack.AsSpan(pointer, count);
        }

        /// <summary>
        /// Sets the pointer to zero, effectively clearing the stack.
        /// </summary>
        /// <remarks>
        /// For efficiency on reusing the stack, this method does not clear the internal array.
        /// If clearing the memory is necessary, use <see cref="TrimExcess"/> method.
        /// </remarks>
        public void Clear() => pointer = 0;

        /// <summary>
        /// Sets the capacity to the actual number of elements in the stack.
        /// This will alter the internal array to fit the exact number of elements,
        /// or nullify it if the stack is empty.
        /// </summary>
        public void TrimExcess() {
            if (pointer <= 0) {
                stack = null;
                return;
            }
            if (pointer < stack.Length) Array.Resize(ref stack, pointer);
        }

        /// <summary>
        /// Enumerates the elements of a <see cref="FastStack{T}"/>.
        /// </summary>
        public readonly Enumerator GetEnumerator() => new Enumerator(this);

        void ICollection<T>.Add(T item) => Push(item);

        readonly bool ICollection<T>.Contains(T item) =>
            stack != null && Array.IndexOf(stack, item, 0, pointer) >= 0;

        readonly void ICollection<T>.CopyTo(T[] array, int arrayIndex) {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || arrayIndex + pointer > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (stack == null || pointer == 0) return;
            Array.Copy(stack, 0, array, arrayIndex, pointer);
            Array.Reverse(array, arrayIndex, pointer);
        }

        readonly bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<T> {
            readonly FastStack<T> stack;
            int index;

            public readonly T Current {
                get {
                    if (index < 0 || index >= stack.pointer)
                        throw new InvalidOperationException();
                    return stack.stack[index];
                }
            }

            readonly object IEnumerator.Current => Current;

            public Enumerator(FastStack<T> stack) {
                this.stack = stack;
                index = -1;
            }

            public bool MoveNext() => ++index < stack.pointer;

            public void Reset() => index = -1;

            public readonly void Dispose() { }
        }
    }
}