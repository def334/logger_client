using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace logger_client.Tools.network
{
    internal static class NetWorkService
    {

        public static readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromMinutes(1)
        };

        public static readonly SemaphoreSlim _downloadLock = new(1, 1);

        // RAG 서버 기본 URL: 필요하면 환경변수 또는 설정으로 바꿔서 사용
        private static readonly string _ragServerBaseUrl = "https://ddalkkag.com";

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(
                        UnicodeRanges.BasicLatin,
                        UnicodeRanges.HangulCompatibilityJamo,
                        UnicodeRanges.HangulJamo,
                        UnicodeRanges.HangulSyllables)
        };

        public static async Task<bool> ConnectedToRagServer()
        {
            var requestUri = new Uri(new Uri(_ragServerBaseUrl.TrimEnd('/')), "/api/health");
            return await _httpClient.GetAsync(requestUri).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    var resp = task.Result.StatusCode == System.Net.HttpStatusCode.OK;
                    return resp;
                }
                return false;
            });
        }

        // RAG 서버에서 참조(요약/본문)를 가져오는 유틸리티 타입
        public record RagReference(string? Url, string? Title, string? Summary, string? Content);

        // 서버에 POST { "query": "<text>" } 요청하고, 서버가 반환한 참 references 목록을 RagReference 리스트로 반환
        public static async Task<RagReference> GetRagReferencesFromServerAsync(string query, CancellationToken ct = default, int timeoutSeconds = 20)
        {
            var results = new RagReference(null, null, null, null);
            if (string.IsNullOrWhiteSpace(query)) return results;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            var token = cts.Token;

            try
            {
                Uri requestUri = new Uri(new Uri(_ragServerBaseUrl.TrimEnd('/')), "/api/solution"); // 예: POST { query }
                string payload = JsonSerializer.Serialize(new { query });
                using StringContent content = new StringContent(payload, Encoding.UTF8, "application/json");

                using HttpResponseMessage resp = await _httpClient.PostAsync(requestUri, content, token).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

                string respText = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(respText)) return results;

                // 유연한 파싱: 문자열 배열 또는 객체 배열을 지원
                try
                {
                    using var doc = JsonDocument.Parse(respText);
                    var root = doc.RootElement;
                    foreach (var el in root.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String)
                        {
                            results = new RagReference(null, null, el.GetString(), null);
                        }
                        else if (el.ValueKind == JsonValueKind.Object)
                        {
                            string? url = "RAG_SERVER";
                            string? title = "empty";
                            string? summary = el.GetProperty("solution").GetString();
                            string? contentText = "empty";
                            results = new RagReference(url, title, summary, contentText);
                        }
                    }
                }
                catch (JsonException)
                {
                    // 응답이 JSON이 아니면 전체 텍스트를 하나의 항목으로 반환
                    results = new RagReference(null, null, respText, null);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // 호출 실패 시 빈 리스트 반환(상위에서 폴백 처리)
                System.Diagnostics.Debug.WriteLine($"GetRagReferencesFromServerAsync 실패: {ex.Message}");
            }

            return results;
        }

        public static async Task<string> GetCurrentNetWorkStatus()
        {
            await Task.Yield();

            var adapters = new List<object>();

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                {
                    continue;
                }

                var name = nic.Name ?? string.Empty;
                var disc = nic.Description ?? string.Empty;

                if (!name.Contains("realtek", StringComparison.OrdinalIgnoreCase) &&
                    !disc.Contains("realtek", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("intel", StringComparison.OrdinalIgnoreCase) &&
                    !disc.Contains("intel", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var ipProps = nic.GetIPProperties();

                var ipList = new List<string>();

                if (ipProps.UnicastAddresses.Count == 0)
                {
                    continue;
                }

                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipList.Add($"{unicast.Address}/{unicast.PrefixLength}");
                    }
                }

                var gatewayList = new List<string>();
                foreach (var gateway in ipProps.GatewayAddresses)
                {
                    if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        gatewayList.Add(gateway.Address.ToString());
                    }
                }

                var dnsList = new List<string>();
                foreach (var dns in ipProps.DnsAddresses)
                {
                    if (dns.AddressFamily == AddressFamily.InterNetwork)
                    {
                        dnsList.Add(dns.ToString());
                    }
                }

                var powerStatus = await GetAdapterPowerStatusAsync(name).ConfigureAwait(false);

                adapters.Add(new
                {
                    Name = name,
                    Description = nic.Description,
                    Type = nic.NetworkInterfaceType.ToString(),
                    Status = nic.OperationalStatus.ToString(),
                    SpeedMbps = nic.Speed / 1_000_000,
                    IpAddresses = ipList,
                    Gateways = gatewayList,
                    DnsServers = dnsList,
                    Power = powerStatus
                });
            }

            var payload = new {
                Adapters = adapters
            };

            return JsonSerializer.Serialize(payload, _jsonOptions);
        }

        private static async Task<JsonObject?> GetAdapterPowerStatusAsync(string adapterName, CancellationToken ct = default)
        {
            string script = $@"
                $pm = Get-NetAdapterPowerManagement -Name '{adapterName.Replace("'", "''")}' -ErrorAction SilentlyContinue
                $adv = Get-NetAdapterAdvancedProperty -Name '{adapterName.Replace("'", "''")}' -ErrorAction SilentlyContinue |
                    Where-Object {{
                        $_.DisplayName -match 'Energy|Green|EEE|절전|전원'
                    }} |
                    Select-Object DisplayName, DisplayValue, RegistryKeyword

                [pscustomobject]@{{
                    Adapter = '{adapterName.Replace("'", "''")}'
                    PowerManagement = $pm
                    EnergySettings = $adv
                }} | ConvertTo-Json -Depth 5 -Compress
                ";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            string output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(output)) return null;

            try
            {
                return JsonNode.Parse(output) as JsonObject;
            }
            catch
            {
                return null;
            }
        }
    }
}

