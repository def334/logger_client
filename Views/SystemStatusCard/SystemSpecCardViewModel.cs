using logger_client.Tools.network;
using logger_client.Tools.os;
using logger_client.ViewModels;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace logger_client.Views.SystemStatusCard
{
    public partial class SystemSpecCardViewModel : ViewModelBase
    {
        // 하드웨어 설명
        private string _gpu = "Unknown";
        private string _cpu = "Unknown";
        private string _ram = "Unknown";
        private string _disk = "Unknown";
        private string _mainboard = "Unknown";

        // 시스템 상태 설명
        private string _bootType = "Unknown";
        private string _secureBoot = "Unknown";
        private string _windowsVersion = "Unknown";
        private string _osBuild = "Unknown";
        private string _tpmStatus = "false";
        private string _lastUpdatedText = "Last Updated: -";

        // 네트워크 상태 설명
        private string _ipV4 = "Unknown";
        private string _macAddress = "Unknown";
        private string _dnsServers = "Unknown";
        private string _eeiStatus = "Unknown";
        private string _wolStatus = "Unknown";

        public string Gpu
        {
            get => _gpu;
            set
            {
                if (_gpu == value) return;
                _gpu = value;
                OnPropertyChanged();
            }
        }

        public string Cpu
        {
            get => _cpu;
            set
            {
                if (_cpu == value) return;
                _cpu = value;
                OnPropertyChanged();
            }
        }

        public string Ram
        {
            get => _ram;
            set
            {
                if (_ram == value) return;
                _ram = value;
                OnPropertyChanged();
            }
        }

        public string Disk
        {
            get => _disk;
            set
            {
                if (_disk == value) return;
                _disk = value;
                OnPropertyChanged();
            }
        }

        public string Mainboard
        {
            get => _mainboard;
            set
            {
                if (_mainboard == value) return;
                _mainboard = value;
                OnPropertyChanged();
            }
        }

        public string BootType
        {
            get => _bootType;
            set
            {
                if (_bootType == value) return;
                _bootType = value;
                OnPropertyChanged();
            }
        }

        public string SecureBoot
        {
            get => _secureBoot;
            set
            {
                if (_secureBoot == value) return;
                _secureBoot = value;
                OnPropertyChanged();
            }
        }

        public string WindowsVersion
        {
            get => _windowsVersion;
            set
            {
                if (_windowsVersion == value) return;
                _windowsVersion = value;
                OnPropertyChanged();
            }
        }

        public string OsBuild
        {
            get => _osBuild;
            set
            {
                if (_osBuild == value) return;
                _osBuild = value;
                OnPropertyChanged();
            }
        }

        public string TPMStatus
        {
            get => _tpmStatus;
            set
            {
                if (_tpmStatus == value) return;
                _tpmStatus = value;
                OnPropertyChanged();
            }
        }

        public string IPv4Address
        {
            get => _ipV4;
            set
            {
                if (_ipV4 == value) return;
                _ipV4 = value;
                OnPropertyChanged();
            }
        }

        public string DNSAddress
        {
            get => _dnsServers;
            set
            {
                if (_dnsServers == value) return;
                _dnsServers = value;
                OnPropertyChanged();
            }
        }

        public string MACAddress
        {
            get => _macAddress;
            set
            {
                if (_macAddress == value) return;
                _macAddress = value;
                OnPropertyChanged();
            }
        }

        public string EEIStatus
        {
            get => _eeiStatus;
            set
            {
                if (_eeiStatus == value) return;
                _eeiStatus = value;
                OnPropertyChanged();
            }
        }

        public string WOLStatus
        {
            get => _wolStatus;
            set
            {
                if (_wolStatus == value) return;
                _wolStatus = value;
                OnPropertyChanged();
            }
        }

        public string LastUpdatedText
        {
            get => _lastUpdatedText;
            set
            {
                if (_lastUpdatedText == value) return;
                _lastUpdatedText = value;
                OnPropertyChanged();
            }
        }

        public override Task Refresh()
        {
            _ = RefreshAsync();
            return Task.CompletedTask;  
        }

        public override void OnChangeQuery(string query)
        {
            return;
        }

        public SystemSpecCardViewModel()
        {
            _ = RefreshAsync();
        }
        private async Task RefreshAsync()
        {
            try
            {
                string json = await OSInfomationService.GetCurrentOSStatus().ConfigureAwait(true);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                TPMStatus = ReadString(root, "TpmEnabled", "false");
                BootType = ReadString(root, "BootType", "Unknown");
                SecureBoot = ReadBoolAsText(root, "SecureBootEnabled");

                JsonElement? windowsContainer = ReadObjectOrJsonString(root, "Windows");
                if (windowsContainer.HasValue)
                {
                    JsonElement windowsRoot = windowsContainer.Value;
                    if (windowsRoot.TryGetProperty("Windows", out JsonElement w))
                        windowsRoot = w;

                    string displayVersion = ReadString(windowsRoot, "DisplayVersion", "Unknown");
                    string osVersion = ReadString(windowsRoot, "OsVersion", "Unknown");
                    string currentBuild = ReadString(windowsRoot, "CurrentBuild", "Unknown");
                    string ubr = ReadString(windowsRoot, "Ubr", "Unknown");

                    WindowsVersion = $"Windows {osVersion} ({displayVersion})";
                    OsBuild = $"{currentBuild}.{ubr}";
                }

                JsonElement? hardwareContainer = ReadObjectOrJsonString(root, "Hardware");
                if (hardwareContainer.HasValue)
                {
                    JsonElement hardwareRoot = hardwareContainer.Value;
                    if (hardwareRoot.TryGetProperty("Hardware", out JsonElement h))
                        hardwareRoot = h;

                    if (hardwareRoot.TryGetProperty("CPU", out JsonElement cpu))
                        Cpu = ReadString(cpu, "Name", "Unknown");

                    if (hardwareRoot.TryGetProperty("Memory", out JsonElement memory))
                        Ram = FormatMemory(memory);

                    if (hardwareRoot.TryGetProperty("GPU", out JsonElement gpu))
                        Gpu = ExtractGpuName(gpu);

                    if (hardwareRoot.TryGetProperty("Disks", out JsonElement disks))
                        Disk = FormatDisks(disks);

                    if (hardwareRoot.TryGetProperty("Mainboard", out JsonElement boards))
                        Mainboard = FormatMainboards(boards);
                }

                string networkJson = await NetWorkService.GetCurrentNetWorkStatus().ConfigureAwait(true);
                ApplyNetworkStatus(networkJson);

                LastUpdatedText = $"Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
            catch
            {
                LastUpdatedText = $"Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
        }

        private void ApplyNetworkStatus(string networkJson)
        {
            if (string.IsNullOrWhiteSpace(networkJson))
                return;

            using JsonDocument networkDoc = JsonDocument.Parse(networkJson);
            JsonElement root = networkDoc.RootElement;

            if (!root.TryGetProperty("Adapters", out JsonElement adapters) || adapters.ValueKind != JsonValueKind.Array)
                return;

            JsonElement selected = default;
            bool found = false;

            foreach (JsonElement adapter in adapters.EnumerateArray())
            {
                if (!found)
                {
                    selected = adapter;
                    found = true;
                }

                if (ReadString(adapter, "Status", "").Equals("Up", StringComparison.OrdinalIgnoreCase))
                {
                    selected = adapter;
                    break;
                }
            }

            if (!found)
                return;

            IPv4Address = JoinStringArray(selected, "IpAddresses");
            DNSAddress = JoinStringArray(selected, "DnsServers");
            MACAddress = ReadString(selected, "MacAddress", "Unknown");

            if (selected.TryGetProperty("Power", out JsonElement power) && power.ValueKind == JsonValueKind.Object)
            {
                EEIStatus = ReadEnergySettings(power);
                WOLStatus = ReadWakeOnLan(power);
            }
            else
            {
                EEIStatus = "Unknown";
                WOLStatus = "Unknown";
            }
        }

        private static string JoinStringArray(JsonElement parent, string property)
        {
            if (!parent.TryGetProperty(property, out JsonElement arr) || arr.ValueKind != JsonValueKind.Array)
                return "Unknown";

            List<string> values = [];
            foreach (JsonElement item in arr.EnumerateArray())
            {
                string text = item.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    values.Add(text);
            }

            return values.Count == 0 ? "Unknown" : string.Join(", ", values);
        }

        private static string ReadEnergySettings(JsonElement power)
        {
            if (!power.TryGetProperty("EnergySettings", out JsonElement settings))
                return "Unknown";

            List<string> items = [];

            if (settings.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement row in settings.EnumerateArray())
                {
                    string name = ReadString(row, "DisplayName", "Unkwon");
                    string value = ReadString(row, "DisplayValue", "비활성");
                    if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(value))
                        items.Add($"{name}={value}".Trim('='));
                }
            }
            else if (settings.ValueKind == JsonValueKind.Object)
            {
                string name = ReadString(settings, "DisplayName", "Unkwon");
                string value = ReadString(settings, "DisplayValue", "비활성");
                if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(value))
                    items.Add($"{name}={value}".Trim('='));
            }

            return items.Count == 0 ? "Unknown" : string.Join("\n", items);
        }

        private static string ReadWakeOnLan(JsonElement power)
        {
            if (!power.TryGetProperty("PowerManagement", out JsonElement pm) || pm.ValueKind != JsonValueKind.Object)
                return "Unknown";

            List<string> items = [];
            foreach (JsonProperty p in pm.EnumerateObject())
            {
                string key = p.Name;
                if (!key.Contains("Wake", StringComparison.OrdinalIgnoreCase) &&
                    !key.Contains("Magic", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = p.Value.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    items.Add($"{key}={value}");
            }

            return items.Count == 0 ? "UnEnabled" : string.Join("\n", items);
        }

        private static string FormatDisks(JsonElement disksElement)
        {
            if (disksElement.ValueKind != JsonValueKind.Array || disksElement.GetArrayLength() == 0)
                return "Unknown";

            List<string> lines = new();

            foreach (JsonElement disk in disksElement.EnumerateArray())
            {
                string model = ReadString(disk, "Model", "Unknown");
                string sizeRaw = ReadString(disk, "Size", "0");
                string sizeText = ToSizeText(sizeRaw);

                lines.Add($"{model} ({sizeText})");
            }

            return lines.Count == 0 ? "Unknown" : string.Join("\n", lines);
        }

        private static string FormatMainboards(JsonElement boardsElement)
        {
            if (boardsElement.ValueKind == JsonValueKind.Object)
            {
                string manufacturer = ReadString(boardsElement, "Manufacturer", "Unknown");
                string product = ReadString(boardsElement, "Product", "Unknown");
                string version = ReadString(boardsElement, "Version", "Unknown");
                return $"{manufacturer} {product} ({version})";
            }

            if (boardsElement.ValueKind == JsonValueKind.Array)
            {
                List<string> lines = new();
                foreach (JsonElement board in boardsElement.EnumerateArray())
                {
                    string manufacturer = ReadString(board, "Manufacturer", "Unknown");
                    string product = ReadString(board, "Product", "Unknown");
                    string version = ReadString(board, "Version", "Unknown");
                    lines.Add($"{manufacturer} {product} ({version})");
                }
                return lines.Count == 0 ? "Unknown" : string.Join("\n", lines);
            }

            return "Unknown";
        }

        private static string ToSizeText(string bytesText)
        {
            if (!ulong.TryParse(bytesText, out ulong bytes) || bytes == 0)
                return "Unknown";

            double gb = bytes / 1024d / 1024d / 1024d;
            return $"{gb:F1} GB";
        }

        private static string FormatMemory(JsonElement memoryElement)
        {
            string totalGb = ReadString(memoryElement, "TotalGB", "Unknown");
            var lines = new List<string>();

            if (totalGb != "Unknown")
            {
                lines.Add($"{totalGb} GB");
            }

            if (memoryElement.TryGetProperty("Modules", out JsonElement modules))
            {
                if (modules.ValueKind == JsonValueKind.Array)
                {
                    int index = 1;
                    foreach (JsonElement module in modules.EnumerateArray())
                    {
                        lines.Add(FormatMemoryModule(module, index));
                        index++;
                    }
                }
                else if (modules.ValueKind == JsonValueKind.Object)
                {
                    lines.Add(FormatMemoryModule(modules, 1));
                }
            }

            if (lines.Count == 0)
            {
                return "Unknown";
            }

            return string.Join("\n", lines);
        }

        private static string FormatMemoryModule(JsonElement module, int index)
        {
            string locator = ReadString(module, "DeviceLocator", ReadString(module, "BankLabel", $"Slot {index}"));
            string manufacturer = ReadString(module, "Manufacturer", "Unknown");
            string partNumber = ReadString(module, "PartNumber", "Unknown");

            string speedRaw = ReadString(module, "ConfiguredClockSpeed", "Unknown");
            if (speedRaw == "Unknown")
            {
                speedRaw = ReadString(module, "Speed", "Unknown");
            }

            string speedText = speedRaw == "Unknown" ? "Unknown" : $"{speedRaw} MHz";
            string capacityText = ToSizeText(ReadString(module, "Capacity", "0"));

            return $"{locator}: {capacityText}, {manufacturer} {partNumber}, {speedText}";
        }

        private static string ReadString(JsonElement element, string property, string defaultValue)
        {
            if (!element.TryGetProperty(property, out JsonElement value))
                return defaultValue;

            string? text = value.ToString();
            return string.IsNullOrWhiteSpace(text) ? defaultValue : text;
        }

        private static string ReadBoolAsText(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out JsonElement value))
                return "Unknown";

            return value.ValueKind switch
            {
                JsonValueKind.True => "Enabled",
                JsonValueKind.False => "Disabled",
                _ => "Unknown"
            };
        }

        private static JsonElement? ReadObjectOrJsonString(JsonElement root, string property)
        {
            if (!root.TryGetProperty(property, out JsonElement value))
                return null;

            if (value.ValueKind == JsonValueKind.Object)
                return value.Clone();

            if (value.ValueKind == JsonValueKind.String)
            {
                string? raw = value.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    return null;

                using JsonDocument nested = JsonDocument.Parse(raw);
                return nested.RootElement.Clone();
            }

            return null;
        }

        private static string ExtractGpuName(JsonElement gpuElement)
        {
            if (gpuElement.ValueKind == JsonValueKind.Array && gpuElement.GetArrayLength() > 0)
            {
                JsonElement first = gpuElement[0];
                return ReadString(first, "Name", "Unknown");
            }

            if (gpuElement.ValueKind == JsonValueKind.Object)
                return ReadString(gpuElement, "Name", "Unknown");

            return "Unknown";
        }
    }
}