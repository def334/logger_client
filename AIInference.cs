using logger_client.ai_module;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Specialized;

namespace logger_client
{
    public enum Severity { High, Medium, Low }

    public partial class AIInference(ErrorLog log) : INotifyPropertyChanged
    {
        public string ProviderName { get; set; } = log.ProviderName;
        public int EventID { get; set; } = log.EventId;
        public string Message { get; set; } = log.Source;

        private int _confidence = 0;
        public int Confidence { get => _confidence; set { _confidence = value; OnPropertyChanged(); } }

        private ObservableCollection<string> _description = new();
        public ObservableCollection<string> Description
        {
            get => _description;
            set
            {
                if (_description == value) return;
                if (_description != null) _description.CollectionChanged -= Collections_CollectionChanged;
                _description = value ?? new ObservableCollection<string>();
                _description.CollectionChanged += Collections_CollectionChanged;
                OnPropertyChanged(nameof(Description));
            }
        }

        private ObservableCollection<string> _solutions = new();

        public ObservableCollection<string> Solutions
        {
            get => _solutions;
            set
            {
                if (_solutions == value) return;
                if (_solutions != null) _solutions.CollectionChanged -= Collections_CollectionChanged;
                _solutions = value ?? new ObservableCollection<string>();
                _solutions.CollectionChanged += Collections_CollectionChanged;
                OnPropertyChanged(nameof(Solutions));
            }
        }

        private void Collections_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 컬렉션 항목이 바뀌면 해당 텍스트 속성에 대한 알림 발생
            if (ReferenceEquals(sender, _description))
                OnPropertyChanged(nameof(Description));
            else if (ReferenceEquals(sender, _solutions))
                OnPropertyChanged(nameof(Solutions));
        }

        public async Task AnalyzeWithServiceAsync(LocalGemmaService service, CancellationToken ct)
        {
            try
            {
                string logDetails = $"Provider: {ProviderName}, EventID: {EventID}\n Message: {Message} \n";
                string Search = $"{EventID} {ProviderName}";
                string rawJson = await service.ChatAsync(logDetails, Search, ct, null, true);
                string cleanJson = ExtractJson(rawJson);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var dto = JsonSerializer.Deserialize<AIAnalysisResultDto>(cleanJson, options);
                if(dto != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        this.Confidence = dto.Confidence;

                        this.Description = new ObservableCollection<string>(dto.Description);
                        this.Solutions = new ObservableCollection<string>(dto.Solutions);

                    });
                }
            }

            catch (Exception ex) 
            { 
                App.Current.Dispatcher.Invoke(() =>
                {
                    this.Confidence = 0;
                    this.Description = new ObservableCollection<string> { $"Error: {ex.Message}" };
                    this.Solutions = new ObservableCollection<string>();
                });
            }

        }
        private string ExtractJson(string text)
        {
            int start = text.IndexOf('{');
            if (start < 0) return text;
            int depth = 0;
            bool inString = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\')) inString = !inString;
                if (inString) continue;
                if (c == '{') depth++;
                else if (c == '}') depth--;
                if (depth == 0) return text.Substring(start, i - start + 1).Trim();
            }
            return text;

        }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SingleOrArrayConverter<T> : JsonConverter<List<T>>
    {
        public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String || reader.TokenType == JsonTokenType.Number)
            {
                var value = JsonSerializer.Deserialize<T>(ref reader, options);
                return value != null ? new List<T> { value } : new List<T>();
            }

            return JsonSerializer.Deserialize<List<T>>(ref reader, options) ?? new List<T>();
        }

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}

