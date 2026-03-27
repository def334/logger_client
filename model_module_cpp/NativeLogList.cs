using System.Collections;
using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace logger_client.model_module_cpp
{
    public unsafe class NativeLogList : IList<ErrorLog>, INotifyCollectionChanged
    {
        private readonly LogEventData* _basePtr; // C# 비관리 시작 주소
        private int _count;

        private ErrorLevel _level_Bitmask = (ErrorLevel.Critical | ErrorLevel.Warning | ErrorLevel.Error | ErrorLevel.Information);
        private string _cutsom_String = "";

        public NativeLogList(nint ptr, int count)
        {
            unsafe
            {
                _basePtr = (LogEventData*)ptr;
                _count = count;
            }
        }

        public ErrorLog this[int index]
        {
            get => new ErrorLog(_basePtr[index]);
            set => throw new NotSupportedException("Read-only pointer access.");
        }

        public int Count => _count;
        public bool IsReadOnly => true;

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public void Add(ErrorLog item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(ErrorLog item)
        {
            throw new NotImplementedException();
        }

        public string? SearchByLogs(string ProviderName, ErrorLevel level = ErrorLevel.Critical)
        {
            unsafe
            {
                for(int i = 0; i< _count; i++)
                {
                    LogEventData value;
                    IntPtr currentPtr = IntPtr.Add((IntPtr)_basePtr, i * Marshal.SizeOf(typeof(LogEventData)));
                    value = Marshal.PtrToStructure<LogEventData>(currentPtr);
                    if (value.ProviderName == ProviderName || value.Level == (int)level)
                    {
                        return DataManager.DetailedLog(value.ProviderName, value.RecordId, i);
                    }
                }
            }
            return null;
        }

        public void CopyTo(ErrorLog[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<ErrorLog> GetEnumerator()
        {

            int structSize = Marshal.SizeOf(typeof(LogEventData));

            for (int i = 0; i < _count; i++)
            {
                LogEventData value;
                unsafe
                {
                    IntPtr currentPtr = IntPtr.Add((IntPtr)_basePtr, i * structSize);

                    value = Marshal.PtrToStructure<LogEventData>(currentPtr);

                    if ((value.Level & (int)_level_Bitmask) == 0)
                    {
                        continue;
                    }
                }

                yield return new ErrorLog(value);
            }
        }

        public int IndexOf(ErrorLog item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, ErrorLog item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(ErrorLog item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
