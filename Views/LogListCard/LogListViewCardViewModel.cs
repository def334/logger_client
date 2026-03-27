using logger_client.ai_module;
using logger_client.model_module_cpp;
using logger_client.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using logger_client.Tools.network;
using System.Windows.Documents;

namespace logger_client.Views.LogListCard
{
    public class LogListViewCardViewModel : ViewModelBase, IDisposable
    {

        private CancellationTokenSource? _highlightCts;
        private CancellationTokenSource? _downloadCts;
        private bool _highlightWarmupStarted;

        private List<NativeLogList> _allLogs = [];
        public BatchedObservableCollection<ErrorLog> FilteredLogs { get; } = [];
        public AIITabInterface AIInterface { get; set; } = new();
        public ICommand? AnalyzeSelectedLogCommand { get; }
        public ICommand? DownloadModelCommand { get; }

        private readonly string[] ViewName =
{
            "System Logs",
            "Application Logs",
            "History Logs",
            "Log Highlights",
        };

        private int _currentIndex;
        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (_currentIndex == value) return;
                _currentIndex = value;
                OnPropertyChanged();
            }
        }

        private bool _isHighlightLoading;
        public bool IsHighlightLoading
        {
            get => _isHighlightLoading;
            private set
            {
                if (_isHighlightLoading == value) return;
                _isHighlightLoading = value;
                OnPropertyChanged();
            }
        }

        private string currentView = "";
        public string CurrentView
        {
            get => currentView;
            set
            {
                if (currentView != value)
                {
                    currentView = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _showModelDownloadButton;
        public bool ShowModelDownloadButton
        {
            get => _showModelDownloadButton;
            set
            {
                if (_showModelDownloadButton == value) return;
                _showModelDownloadButton = value;
                OnPropertyChanged();
            }
        }
        public bool ShowAnalyzeButton => !ShowModelDownloadButton;

        private bool _isModelDownloading;
        public bool IsModelDownloading
        {
            get => _isModelDownloading;
            set
            {
                if (_isModelDownloading == value) return;
                _isModelDownloading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _modelDownloadStatus = "모델 다운로드";
        public string ModelDownloadStatus
        {
            get => _modelDownloadStatus;
            set
            {
                if (_modelDownloadStatus == value) return;
                _modelDownloadStatus = value;
                OnPropertyChanged();
            }
        }

        private ErrorLog? _selectedLog;
        public ErrorLog? SelectedLog
        {
            get => _selectedLog;
            set
            {
                if (_selectedLog == value || value == null) return;
                _selectedLog = value;

                ErrorLog? temp = AppState.LogCache.ContainsKey((CurrentIndex, _selectedLog.RecordId))
                    ? AppState.LogCache[(CurrentIndex, _selectedLog.RecordId)]
                    : null;

                if (temp == null)
                {
                    if (_selectedLog.Source == null || _selectedLog.xml == null)
                    {
                        var list = DataManager.DetailedLog(_selectedLog, CurrentIndex);
                        _selectedLog.Source = list[0] ?? "";
                        _selectedLog.xml = list[1] ?? "";
                    }

                    AIInterface.Visibility = Visibility.Hidden;
                }
                else
                {
                    AIInterface.Visibility = Visibility.Visible;
                    _selectedLog = temp;
                }

                OnPropertyChanged();
            }
        }

        private string? _levelFilter;
        public string LevelFilter
        {
            get => _levelFilter ?? "전체";
            set
            {
                _levelFilter = value;
                FilterLogs(CurrentIndex);
                OnPropertyChanged(nameof(FilteredLogs));
            }
        }

        public LogListViewCardViewModel()
        {
            if(!DataManager.InitDataManager())
            {
                MessageBox.Show("로그 데이터를 불러오는 데 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }

            _allLogs.Clear();
            for (int i = 0; i < DataManager.LogFileNames.Length; i++)
            {
                int count = DataManager.GetErrorLogs((int)ErrorLevel.all, DataManager.LogFileNames[i]);
                _allLogs.Add(new NativeLogList(DataManager.PtrPart[i], count));
            }

            ShowModelDownloadButton = !LocalGemmaService.Instance.HasModelFile();

            AnalyzeSelectedLogCommand = new RelayCommand(async _ => await ActivateAIModuleAsync());
            DownloadModelCommand = new RelayCommand(async _ => await DownloadModelAsync());

            currentView = ViewName[CurrentIndex];
            FilterLogs(CurrentIndex);

            _ = EnsureHighlightWarmupAsync();

            return;
        }

        private async Task EnsureHighlightWarmupAsync()
        {
            if (_highlightWarmupStarted) return;
            _highlightWarmupStarted = true;

            await StartHighLightSearchAsync();
        }

        private async Task StartHighLightSearchAsync()
        {
            _highlightCts?.Cancel();
            _highlightCts?.Dispose();
            _highlightCts = new CancellationTokenSource();

            _highlightWarmupStarted = true;

            try
            {
                await SetHighLightLogsAsync(_highlightCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
               _highlightWarmupStarted = false;
               IsHighlightLoading = false;
            }
        }

        public override void OnCurrentIndexChanged(int newIndex)
        {
            if (newIndex >= ViewName.Length)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (newIndex == 3 && _highlightWarmupStarted)
                {
                   IsHighlightLoading = true;
                }
                else
                {
                   IsHighlightLoading = false;
                }
                CurrentIndex = newIndex;
                CurrentView = ViewName[newIndex] ?? "";
                FilterLogs(newIndex);
            });
        }

        private async Task ActivateAIModuleAsync()
        {
            try
            {
                if (!LocalGemmaService.Instance.HasModelFile())
                {
                    ShowModelDownloadButton = true;
                    ModelDownloadStatus = "모델 파일이 없습니다. 다운로드를 진행해 주세요.";
                    return;
                }

                ShowModelDownloadButton = false;
                await LocalGemmaService.Instance.InitializeAsync();

                if (LocalGemmaService.Instance.IsLoaded == true)
                {
                    OnPropertyChanged(nameof(SelectedLog));
                    if (SelectedLog != null)
                    {
                        AIInterface.CurrentAnalysis = SelectedLog.AIResult;
                        _ = AIInterface.AnalyzeCurrentLogAsync(SelectedLog);

                        ErrorLog temp = new();
                        temp.ToCopy(SelectedLog);

                        if (AppState.LogCache.ContainsKey((CurrentIndex, SelectedLog.RecordId)) == false)
                            AppState.LogCache.Add((CurrentIndex, SelectedLog.RecordId), temp);
                        else
                            AppState.LogCache[(CurrentIndex, SelectedLog.RecordId)] = temp;

                        AIInterface.Visibility = Visibility.Visible;
                    }

                }
                else
                {
                    throw new Exception("AI 모델 로드 실패");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AI 모델 로드 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public override void OnChangeQuery(string query)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                FilterLogs(CurrentIndex, query);
            });
        }

        public override async Task Refresh()
        {
            await Task.Run(() =>
            {
                RefreshLogs();
            });
        }

        public void RefreshLogs()
        {
            _allLogs.Clear();
            AppState.LogCache.Clear();

            for (int i = 0; i < DataManager.LogFileNames.Length; i++)
            {
                int count = DataManager.GetErrorLogs((int)ErrorLevel.all, DataManager.LogFileNames[i]);
                _allLogs.Add(new NativeLogList(DataManager.PtrPart[i], count));
            }

            FilterLogs(CurrentIndex);
            _ = EnsureHighlightWarmupAsync();
        }

        private async Task SetHighLightLogsAsync(CancellationToken ct)
        {
            if (_allLogs == null || _allLogs.Count < 2)
                return;

            var merged = await Task.Run(() =>
            {
                var temp = new List<(int ChannelIndex, ErrorLog Log)>();

                foreach (var log in _allLogs[0].Where(l =>
                             (l.ProviderName?.Contains("BugCheck", StringComparison.OrdinalIgnoreCase) ?? false) ||
                             l.EventId == 1001))
                {
                    ct.ThrowIfCancellationRequested();

                    if (log.Source == null || log.xml == null)
                    {
                        var detail = DataManager.DetailedLog(log, 0);
                        if (detail != null && detail.Count > 0)
                        {
                            log.Source = detail[0] ?? "";
                            log.xml = detail.Count > 1 ? detail[1] ?? "" : "";
                        }
                    }

                    temp.Add((0, log));
                }

                foreach (var log in _allLogs[1].Where(l =>
                             l.ProviderName?.Contains("Application Error", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    ct.ThrowIfCancellationRequested();

                    if (log.Source == null || log.xml == null)
                    {
                        var detail = DataManager.DetailedLog(log, 1);
                        if (detail != null && detail.Count > 0)
                        {
                            log.Source = detail[0] ?? "";
                            log.xml = detail.Count > 1 ? detail[1] ?? "" : "";

                            int startidx = detail[0].IndexOf("프로그램 이름: ", StringComparison.OrdinalIgnoreCase);
                            if (startidx != -1)
                            {
                                int endidx = detail[0].IndexOf(',', startidx);
                                if (endidx != -1)
                                {
                                    log.ProviderName = detail[0].Substring(
                                        startidx + "프로그램 이름: ".Length,
                                        endidx - startidx - "프로그램 이름: ".Length).Trim();
                                }
                            }
                        }
                    }

                    temp.Add((1, log));
                }

                return temp.OrderByDescending(x => x.Log.Timestamp).ToList();
            }, ct).ConfigureAwait(false);

            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                AppState.HighLightList.Clear();

                foreach (var item in merged)
                {
                    var key = (item.ChannelIndex, item.Log.RecordId);
                    if (!AppState.HighLightList.ContainsKey(key))
                        AppState.HighLightList.Add(key, item.Log);
                }

                if (CurrentIndex == 3)
                    FilterLogs(CurrentIndex);
            });
        }
        private async Task DownloadModelAsync()
        {
            if (IsModelDownloading)
            {
                _downloadCts?.CancelAsync();
                _downloadCts?.Dispose();
                _downloadCts = null;
                ModelDownloadStatus = "모델 다운로드 취소됨";

                return;
            }

            IsModelDownloading = true;
            ShowModelDownloadButton = true;
            ModelDownloadStatus = "모델 다운로드 시작...";

            try
            {
                _downloadCts = new CancellationTokenSource();

                var progress = new Progress<double>(p =>
                {
                    ModelDownloadStatus = $"모델 다운로드 중... {p:0.0}%";
                });

                await LocalGemmaService.Instance.DownloadModelAsync(NetWorkService._httpClient, _downloadCts.Token, progress);
                ModelDownloadStatus = "모델 다운로드 완료";
                ShowModelDownloadButton = false;

                OnPropertyChanged(nameof(ShowAnalyzeButton));
            }
            catch (Exception ex)
            {
                ModelDownloadStatus = $"다운로드 실패: {ex.Message}";
                ShowModelDownloadButton = true;
            }
            finally
            {
                if(_downloadCts != null)
                {
                    _downloadCts.Dispose();
                    _downloadCts = null;
                }
                IsModelDownloading = false;
            }
        }

        private void FilterLogs(int index, string input = "")
        {
            if (_allLogs == null) return;

            string query = input;
            int levelFilter = ConvertLevelToMask(LevelFilter);
            object selectedLog;

            switch (index)
            {
                case 0:
                    selectedLog = _allLogs[0];
                    break;
                case 1:
                    selectedLog = _allLogs[1];
                    break;
                case 2:
                    selectedLog = AppState.LogCache.Values.ToList();
                    break;
                case 3:
                    selectedLog = AppState.HighLightList.Values.ToList();
                    break;
                default:
                    return;
            }

            List<ErrorLog> filtered = MatchLogQuery((IEnumerable<ErrorLog>?)selectedLog, query, levelFilter)?
                .OrderByDescending(log => log.Timestamp)
                .ToList() ?? [];

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (filtered.Count > 0)
                    SelectedLog = filtered[0];

                FilteredLogs.ReplaceAll(filtered);
                OnPropertyChanged();
            });
        }

        private List<ErrorLog>? MatchLogQuery(IEnumerable<ErrorLog>? logs, string query, int level = (int)ErrorLevel.all)
        {
            if (logs == null) return null;

            List<ErrorLog> filtered = [.. logs.Where(log =>
            {
                bool matchesSearch = string.IsNullOrEmpty(query) ||
                    (log.ProviderName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    log.EventId.ToString().Contains(query);

                bool matchesLevel = level == (int)ErrorLevel.all ||
                                    (int)log.Level == level;

                return matchesSearch && matchesLevel;
            })];

            return filtered;
        }

        private int ConvertLevelToMask(string level)
        {
            if (string.IsNullOrEmpty(level)) return (int)ErrorLevel.all;
            if (level.Contains("위급")) return (int)ErrorLevel.Critical;
            if (level.Contains("에러")) return (int)ErrorLevel.Error;
            if (level.Contains("경고")) return (int)ErrorLevel.Warning;
            if (level.Contains("정보")) return (int)ErrorLevel.Information;

            return (int)ErrorLevel.all;
        }

        public override void Dispose()
        {
            _highlightCts?.Cancel();
            _highlightCts?.Dispose();
            _highlightCts = null;

            _downloadCts?.Cancel();
            _downloadCts?.Dispose();
            _downloadCts = null;

            IsHighlightLoading = false;
        }
    }
}
