using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace logger_client
{
    public class BatchedObservableCollection<T> : ObservableCollection<T>
    {
        // 컬렉션을 한번에 교체하고 단일 Reset 알림만 발생시킴
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            // Items는 protected이므로 직접 조작해 내부에서 개별 알림을 피함
            Items.Clear();
            foreach (var it in items) Items.Add(it);

            // 단일 Reset 이벤트와 관련된 프로퍼티 알림 발생
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        }

        // AddRange 구현 (필요 시 사용)
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var it in items) Items.Add(it);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        }
    }
}
