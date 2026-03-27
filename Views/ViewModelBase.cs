using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace logger_client.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {

        public static bool IsAiModelLoaded => AppState.IsAIModelLoaded;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ViewModelBase()
        {
            
        }
        public virtual Task Refresh() { return Task.CompletedTask; }
        public virtual void Initialize() { }
        public virtual void Dispose() { }

        public virtual void OnSelected() { }
        public virtual void OnDeselected() { }
        public virtual void OnCurrentIndexChanged(int newIndex) { }

        public abstract void OnChangeQuery(string query);

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}