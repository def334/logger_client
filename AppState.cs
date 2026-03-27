using logger_client.ai_module;
using logger_client.model_module_cpp;
using logger_client.Views.LogListCard;
using logger_client.Views.SystemStatusCard;
using System;

namespace logger_client
{
    enum ViewIndex
    {
        init = -1,
        LogListView = 0,
        SystemSpecStatusView = 1,
    }

    internal static class AppState
    {
        public static OrderedDictionary<(int, UInt64), ErrorLog> LogCache = new OrderedDictionary<(int, UInt64), ErrorLog>();
        public static OrderedDictionary<(int, UInt64), ErrorLog> HighLightList = new OrderedDictionary<(int, UInt64), ErrorLog>();

        public static LogListViewCardViewModel LogListViewModel = new();
        public static SystemSpecCardViewModel SystemSpecStatusViewModel = new();

        private static ViewIndex _activeViewIndex = ViewIndex.init;

        private static readonly string _version = "v1.1.0-Preview";
        public static string Version => _version;


        public static bool IsAIModelLoaded => LocalGemmaService.Instance.IsLoaded;

        static AppState()
        {
            LocalGemmaService.Instance.IsLoadedChanged += (_, _) =>
            {
                AIModelLoadedChanged?.Invoke(null, IsAIModelLoaded);
            };
        }

        public static ViewIndex ActiveViewIndex
        {
            get => _activeViewIndex;
            set
            {
                if (_activeViewIndex == value) return;
                _activeViewIndex = value;
            }
        }

        public static event EventHandler<bool>? AIModelLoadedChanged;
    }
}
