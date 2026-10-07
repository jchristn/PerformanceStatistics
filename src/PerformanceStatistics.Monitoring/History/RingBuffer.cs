using System;
using System.Collections.Generic;

namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// Fixed-capacity buffer that overwrites its oldest item. Not thread-safe.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public sealed class RingBuffer<T>
    {
        #region Public-Members

        /// <summary>
        /// Capacity.
        /// </summary>
        public int Capacity
        {
            get { return _Items.Length; }
        }

        /// <summary>
        /// Items currently held.
        /// </summary>
        public int Count { get; private set; } = 0;

        #endregion

        #region Private-Members

        private readonly T[] _Items;
        private int _Next = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="capacity">Capacity (at least 1).</param>
        public RingBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _Items = new T[capacity];
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add an item, overwriting the oldest when full.
        /// </summary>
        /// <param name="item">Item.</param>
        public void Add(T item)
        {
            _Items[_Next] = item;
            _Next = (_Next + 1) % _Items.Length;
            if (Count < _Items.Length) Count++;
        }

        /// <summary>
        /// Most recent item, or default.
        /// </summary>
        /// <returns>Item.</returns>
        public T Last()
        {
            if (Count == 0) return default(T);
            return _Items[(_Next - 1 + _Items.Length) % _Items.Length];
        }

        /// <summary>
        /// Items from oldest to newest.
        /// </summary>
        /// <returns>Items.</returns>
        public List<T> ToList()
        {
            List<T> ret = new List<T>(Count);
            int start = (_Next - Count + _Items.Length) % _Items.Length;
            for (int i = 0; i < Count; i++) ret.Add(_Items[(start + i) % _Items.Length]);
            return ret;
        }

        #endregion
    }
}
