using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Based on Robin Hood hashtable implementation at
// http://www.sebastiansylvan.com/post/robin-hood-hashing-should-be-your-default-hash-table-implementation/
namespace OlliSaarikivi.Sre
{
    class MultiValueIntDictionary<T>
    {
        const int EmptyKey = default(int);
        const int LoadFactorPercent = 75;

        internal int Size { get; private set; }
        internal int Capacity { get; private set; }

        T[] values;
        int[] keys;
        int resizeThreshold;
        int mask;

        int DesiredPos(int key) => key & mask;
        int Distance(int pos1, int pos2) => (pos2 + Capacity - pos1) & mask;
        int ProbeDistance(int pos) => Distance(DesiredPos(keys[pos]), pos);

        internal MultiValueIntDictionary(int initialCapacity = 256)
        {
            Size = 0;
            Capacity = initialCapacity;
            Alloc();
        }

        void Alloc()
        {
            values = new T[Capacity];
            keys = new int[Capacity];

            resizeThreshold = (Capacity * LoadFactorPercent) / 100;
            mask = Capacity - 1;
        }

        void Grow()
        {
            var oldValues = values;
            var oldKeys = keys;
            int oldCapacity = Capacity;
            Capacity *= 2;
            Alloc();

            // Copy over old entries
            for (int i = 0; i < oldCapacity; ++i)
            {
                var value = oldValues[i];
                var key = oldKeys[i];
                if (key != EmptyKey)
                    AddHelper(key, value);
            }
        }

        void Construct(int pos, int key, T value)
        {
            values[pos] = value;
            keys[pos] = key;
        }

        void Delete(int pos)
        {
            values[pos] = default(T);
            keys[pos] = EmptyKey;
        }

        void AddHelper(int key, T value)
        {
            int pos = DesiredPos(key);
            int dist = 0;
            for (;;)
            {
                if (keys[pos] == EmptyKey)
                {
                    Construct(pos, key, value);
                    return;
                }

                // If the existing elem has probed less than us, then swap places with existing
                // elem, and keep going to find another slot for that elem.
                int existingElemProbeDist = ProbeDistance(pos);
                if (existingElemProbeDist < dist)
                {
                    var tmpKey = keys[pos];
                    keys[pos] = key;
                    key = tmpKey;

                    var tmpValue = values[pos];
                    values[pos] = value;
                    value = tmpValue;

                    dist = existingElemProbeDist;
                }

                pos = (pos + 1) & mask;
                ++dist;
            }
        }

        internal void Add(int key, T value)
        {
            if (++Size >= resizeThreshold)
            {
                Grow();
            }
            AddHelper(key, value);
        }

        struct ValuesEnumerator : IEnumerator<T>
        {
            readonly MultiValueIntDictionary<T> dict;
            readonly int key;
            int pos;
            int dist;

            internal ValuesEnumerator(MultiValueIntDictionary<T> dict, int key)
            {
                this.dict = dict;
                this.key = key;
                pos = dict.DesiredPos(key);
                dist = 0;
            }

            public T Current { get { return dict.values[pos]; } }

            object IEnumerator.Current { get { return Current; } }

            public void Dispose() { }

            public bool MoveNext()
            {
                for (;;)
                {
                    if (dict.keys[pos] == EmptyKey)
                        return false;
                    if (dist > dict.ProbeDistance(pos))
                        return false;
                    if (dict.keys[pos] == key)
                        return true;

                    pos = (pos + 1) & dict.mask;
                    ++dist;
                }
            }

            public void Reset()
            {
                pos = dict.DesiredPos(key);
                dist = 0;
            }
        }

        struct ValuesEnumerable : IEnumerable<T>
        {
            MultiValueIntDictionary<T> dict;
            int key;

            internal ValuesEnumerable(MultiValueIntDictionary<T> dict, int key)
            {
                this.dict = dict;
                this.key = key;
            }

            public IEnumerator<T> GetEnumerator() => new ValuesEnumerator(dict, key);
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        internal IEnumerable<T> this[int key]
        {
            get
            {
                return new ValuesEnumerable(this, key);
            }
        }

        internal int Remove(int key)
        {
            var pos = DesiredPos(key);
            var shiftPos = pos;
            int dist = 0;
            int posDesiredPos;
            int numRemoved = 0;
            for (;;)
            {
                // Empty slot? Nothing left to find or shift
                if (keys[pos] == EmptyKey)
                    goto END;

                // Shorter probe distance? Nothing to find, might have to shift
                posDesiredPos = DesiredPos(keys[pos]);
                if (dist > Distance(posDesiredPos, pos))
                    break;

                if (keys[pos] == key)
                {
                    Delete(pos);
                    ++numRemoved;
                }
                else
                {
                    if (shiftPos != pos)
                    {
                        keys[shiftPos] = keys[pos];
                        values[shiftPos] = values[pos];
                        Delete(pos);
                    }
                    shiftPos = (shiftPos + 1) & mask;
                }

                pos = (pos + 1) & mask;
                ++dist;
            }
            for (;;)
            {
                if (Distance(posDesiredPos, pos) < Distance(shiftPos, pos))
                    shiftPos = posDesiredPos;
                if (shiftPos != pos)
                {
                    keys[shiftPos] = keys[pos];
                    values[shiftPos] = values[pos];
                    Delete(pos);

                    shiftPos = (shiftPos + 1) & mask;
                    pos = (pos + 1) & mask;
                }
                else
                {
                    break;
                }
            }
        END:
            Size -= numRemoved;
            return numRemoved;
        }
    }
}