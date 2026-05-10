using System;
using System.Collections;
using System.Collections.Generic;

namespace ProtoTable
{
    public sealed class FlatBufferArray<T>:IList<T>
    {
        private Func<int, T> _fun;
        private int _count;

        public FlatBufferArray(Func<int, T> fun, int count)
        {
            _fun = fun;
            _count = count;
        }

        public T this[int index]
        {
            get { return _fun(index); }
            set { throw new NotImplementedException(); }
        }

        public int Length
        {
            get { return _count; }
        }
        public int Count
        {
            get { return _count; }
        }

//        int ICollection<T>.Count => throw new NotImplementedException();

        bool ICollection<T>.IsReadOnly
        {
            get { return true; }
        }

//        T IList<T>.this[int index] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool Contains(T other)
        {
            for (int i = 0; i < _count; ++i)
            {
                T item = _fun(i);
                if (item.Equals(other))
                {
                    return true;
                }
            }

            return false;
        }

        int IList<T>.IndexOf(T item)
        {
            for (int i = 0; i < _count; ++i)
            {
                if (item.Equals(_fun(i)))
                {
                    return i;
                }
            }

            return -1;
        }

        void IList<T>.Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        void ICollection<T>.Add(T item)
        {
            throw new NotImplementedException();
        }

        void ICollection<T>.Clear()
        {
            throw new NotImplementedException();
        }
  
        // bool ICollection<T>.Contains(T other)
        // { 
        //     for (int i = 0; i < _count; ++i)
        //     {
        //         T item = _fun(i);
        //         if (item.Equals(other))
        //         {
        //             return true;
        //         }
        //     }

        //     return false;
        // }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            for (int i = 0; i < _count; ++i)
            {
                if (arrayIndex + i >= array.Length)
                {
                    break;
                }
                array[arrayIndex + i] = _fun(i);
            }
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotImplementedException();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public int FindIndex(Predicate<T> match)
        {
            for (int i = 0; i < _count; ++i)
            {
                if (match(_fun(i)))
                {
                    return i;
                }
            }

            return -1;
        }


        public struct Enumerator : IEnumerator<T>, System.Collections.IEnumerator
        {
            private T _current;
            private int _index;
            private FlatBufferArray<T> _fbarray;
            public Enumerator(FlatBufferArray<T> fbarray)
            {
                _current = default(T);
                _index = 0;
                _fbarray = fbarray;

            }

            public T Current
            {
                get
                {
                    return _current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return _current;
                }
            }

            T IEnumerator<T>.Current
            {
                get
                {
                    return _current;
                }
            }

            void IDisposable.Dispose()
            {
                
            }

            bool IEnumerator.MoveNext()
            {
                if (_index < _fbarray.Count)
                {
                    _current = _fbarray[_index];
                    _index++;
                    return true;
                }

                _current = default(T);
                _index = 0;
                
                return false;
            }

            void IEnumerator.Reset()
            {
                _current = default(T);
                _index = 0;
            }
        }


      public void ForEach( Action<T> action) {

 
            if( action == null) {
                throw new ArgumentNullException("action");
            }
          
 
            for(int i = 0 ; i < Count; i++) {
                action(_fun(i));
            }
        }

        //        public static implicit operator List<CrypticInt32>(fbarray<T> source)
        //        {
        //            List<CrypticInt32> list = new List<CrypticInt32>(source.Count);
        //            if (source != null)
        //            {
        //                for (int i = 0; i < source.Count; ++i)
        //                {
        //                    T item = source[i];
        //                    if (typeof(T) == typeof(int))
        //                    {
        //                        list.Add((CrypticInt32)(int)(object)item);
        //                    }
        //                }
        //            }
        //
        //            return list;
        //        }
        //
        //        public static implicit operator List<int>(fbarray<T> source)
        //        {
        //            List<int> list = new List<int>(source.Count);
        //            if (source != null)
        //            {
        //                for (int i = 0; i < source.Count; ++i)
        //                {
        //                    T item = source[i];
        //                    if (typeof(T) == typeof(int))
        //                    {
        //                        list.Add((int)(object)item);
        //                    }
        //                }
        //            }
        //
        //            return list;
        //        }

    }

    public static class FlatBufferArrayHelper
    {

//        public static List<CrypticInt32> ToArray(this fbarray<int> source)
//        {
//
//            List<CrypticInt32> list = new List<CrypticInt32>(source.Count);
//            if (source != null)
//            {
//                for (int i = 0; i < source.Count; ++i)
//                {
//                    int item = source[i];
//                    list.Add((CrypticInt32)item);
//                }
//            }
//
//            return list;
//        }
//
//        public static List<int> ToArray(this fbarray<int> source)
//        {
//
//            List<CrypticInt32> list = new List<CrypticInt32>(source.Count);
//            if (source != null)
//            {
//                for (int i = 0; i < source.Count; ++i)
//                {
//                    int item = source[i];
//                    list.Add((CrypticInt32)item);
//                }
//            }
//
//            return list;
//        }
    }
}