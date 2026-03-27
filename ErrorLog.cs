using logger_client.model_module_cpp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace logger_client
{
    public enum ErrorLevel
    {
        Critical=1,
        Error = 2,
        Warning = 3,
        Information = 4,
        all = 15
    }

    public class ErrorLog : INotifyPropertyChanged
    {
        public ErrorLog(LogEventData value)
        {
            this.ProviderName = value.ProviderName;
            this.EventId = value.EventID;
            this.Level = (ErrorLevel)(value.Level);
            this.Timestamp = DateTime.FromFileTimeUtc((long)value.TimeCreated);
            this.RecordId = value.RecordId;
            this.dirId = value.dirCount;
            this.AIResult = null;
        }

        public ErrorLog()
        {
            this.ProviderName = "";
            this.EventId = 0;
            this.Level = (ErrorLevel)(0);
            this.Timestamp = DateTime.Now;
            this.RecordId = 0;
            this.dirId = 0;
            this._aiResult = null;
        }

        public string ProviderName { get; set; }
        public int EventId { get; set; }
        public ErrorLevel Level { get; set; }
        public DateTime Timestamp { get; set; }

        public ulong RecordId { get; set; }

        public UInt16 dirId { get; set; }
        public string? Source { get; set; }

        public string? xml { get; set; }

        private AIInference? _aiResult;
        public AIInference? AIResult
        {
            get => _aiResult;
            set { _aiResult = value; OnPropertyChanged(nameof(AIResult)); }
        }

        public void ToCopy(ErrorLog value)
        {
            this.ProviderName = value.ProviderName;
            this.EventId = value.EventId;
            this.Level = value.Level;
            this.Timestamp = value.Timestamp;
            this.RecordId = value.RecordId;
            this.dirId = value.dirId;
            this.Source = value.Source;
            this.xml = value.xml;
            this.AIResult = value.AIResult;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
