using logger_client.ai_module;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace logger_client
{
    public partial class AIITabInterface : UserControl, INotifyPropertyChanged
    {
        private CancellationTokenSource? aiCt;

        public static readonly DependencyProperty CurrentAnalysisProperty =
           DependencyProperty.Register(
               nameof(CurrentAnalysis),
               typeof(AIInference),
               typeof(AIITabInterface),
               new PropertyMetadata(null, OnCurrentAnalysisChanged));

        public static readonly DependencyProperty IsAnalyzingProperty =
            DependencyProperty.Register(
                nameof(IsAnalyzing),
                typeof(bool),
                typeof(AIITabInterface),
                new PropertyMetadata(false));

        public bool IsAnalyzing
        {
            get => (bool)GetValue(IsAnalyzingProperty);
            set => SetValue(IsAnalyzingProperty, value);
        }

        public AIInference? CurrentAnalysis
        {
            get => (AIInference?)GetValue(CurrentAnalysisProperty);
            set => SetValue(CurrentAnalysisProperty, value);
        }

        public AIITabInterface()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                if (CurrentAnalysis != null)
                    DataContext = CurrentAnalysis;
            };
        }

        public async Task AnalyzeCurrentLogAsync(ErrorLog log)
        {
            if (log == null) return;

            IsAnalyzing = true;
            OnPropertyChanged(nameof(IsAnalyzing));

            aiCt?.Cancel();
            aiCt = new CancellationTokenSource();
            var token = aiCt.Token;

            try
            {
                var inference = new AIInference(log);

                await inference.AnalyzeWithServiceAsync(LocalGemmaService.Instance, token);

                if (!token.IsCancellationRequested)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        log.AIResult = inference;
                        CurrentAnalysis = inference;
                        DataContext = CurrentAnalysis;
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
            }
            finally
            {
                IsAnalyzing = false;
                OnPropertyChanged(nameof(IsAnalyzing));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static void OnCurrentAnalysisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AIITabInterface control)
            {
                control.OnPropertyChanged(nameof(CurrentAnalysis));

                if (e.NewValue is AIInference vm)
                {
                    control.DataContext = vm;
                }
            }
        }
    }
}