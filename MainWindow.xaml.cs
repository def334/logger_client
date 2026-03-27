using logger_client.Tools.network;
using logger_client.ViewModels;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace logger_client {
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private ViewModelBase? _currentViewModel = null;
        public ViewModelBase? CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (_currentViewModel == value) return;
                _currentViewModel = value;
                OnPropertyChanged(nameof(CurrentViewModel));
            }
        }

        public static string Version => AppState.Version;

        private string _aiLoaded = "Checking...";
        public string AILoaded
        {
            get => _aiLoaded;
            set
            {
                if (_aiLoaded == value) return;
                _aiLoaded = value;
                OnPropertyChanged(nameof(AILoaded));
            }
        }

        private string _ragStatusText = "Checking...";
        public string RagStatusText
        {
            get => _ragStatusText;
            set
            {
                if (_ragStatusText == value) return;
                _ragStatusText = value;
                OnPropertyChanged(nameof(RagStatusText));
            }
        }

        public ICommand OpenHighlightView { get; }
        public ICommand OpenSystemView { get; }
        public ICommand OpenProgramView { get; }
        public ICommand OpenHistoryView { get; }
        public ICommand OpenHardwareSpecView { get; }

        private string _serchQuery = "";
        public string SearchQuery
        {
            get => _serchQuery;
            set
            {
                if (_serchQuery == value) return;
                _serchQuery = value;
                CurrentViewModel?.OnChangeQuery(value);
                OnPropertyChanged(nameof(SearchQuery));
            }
        }

        private int _currentIndex = -1;
        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (_currentIndex == value) return;
                _currentIndex = value;

                AppState.ActiveViewIndex = IndexToView(value);
                CurrentIndexChanged?.Invoke(this, value);

                OnPropertyChanged(nameof(CurrentIndex));
            }
        }

        public MainViewModel()
        {
            RefreshCommand = new RelayCommand(_ => _ = RefreshAsync());
            OpenHighlightView = new RelayCommand(_ => { CurrentIndex = 3; });
            OpenSystemView = new RelayCommand(_ => { CurrentIndex = 0; });
            OpenProgramView = new RelayCommand(_ => { CurrentIndex = 1; });
            OpenHistoryView = new RelayCommand(_ => { CurrentIndex = 2; });       
            OpenHardwareSpecView = new RelayCommand(_ => { CurrentIndex = 4; });

            CurrentIndexChanged += OnCurrentIndexChanged;
            AppState.AIModelLoadedChanged += OnAIModelLoadedChanged;

            _ = RefreshAsync();
        }

        private void OnAIModelLoadedChanged(object? sender, bool isLoaded)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AILoaded = isLoaded ? "AI Ready" : "AI Not Ready";
            });
        }

        private void OnCurrentIndexChanged(object? sender, int newIndex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentViewModel = ResolveCurrentViewModel();
                CurrentViewModel?.OnCurrentIndexChanged(newIndex);
            });
        }

        public ICommand RefreshCommand { get; }

        public async Task RefreshAsync()
        {
            AILoaded = AppState.IsAIModelLoaded ? "AI Ready" : "AI Not Ready";

            bool isConnected;
            try
            {
                isConnected = await NetWorkService.ConnectedToRagServer();
            }
            catch
            {
                isConnected = false;
            }

            RagStatusText = isConnected ? "Connected" : "Disconnected";

            if (CurrentIndex == -1)
                return;

            if (_currentViewModel != null)
                await _currentViewModel.Refresh();
        }

        public void Refresh()
        {
            _ = RefreshAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public static event EventHandler<int>? CurrentIndexChanged;
        private ViewModelBase? ResolveCurrentViewModel()
        {
            return IndexToView(CurrentIndex) switch
            {
                ViewIndex.LogListView => AppState.LogListViewModel,
                ViewIndex.SystemSpecStatusView => AppState.SystemSpecStatusViewModel,
                _ => null
            };
        }

        private static ViewIndex IndexToView(int value)
        {
            return value switch
            {
                0 => ViewIndex.LogListView,
                1 => ViewIndex.LogListView,
                2 => ViewIndex.LogListView,
                3 => ViewIndex.LogListView,
                4 => ViewIndex.SystemSpecStatusView,
                _ => ViewIndex.init
            };
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}