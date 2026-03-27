using LLama;
using LLama.Common;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static logger_client.Tools.network.NetWorkService;

namespace logger_client.ai_module
{
    public class LocalGemmaService
    {
        private static readonly string ModelFileName = "gemma-3-4b-it-q8_0.gguf";
        private static readonly string ModelRepo = "TouchNight/gemma-3-4b-it-Q8_0-GGUF";
        private static readonly string ModelPath =
            Path.Combine(Environment.CurrentDirectory, ModelFileName);

        private LLamaWeights? _model;
        private ModelParams? _parameters;
        private StatelessExecutor? _singleExecutor;

        private static readonly Lazy<LocalGemmaService> _instance = new(() => new LocalGemmaService());

        public static LocalGemmaService Instance => _instance.Value;

        private readonly SemaphoreSlim _aiLock = new(1, 1);

        private bool _isLoaded = false;
        public bool IsLoaded
        {
            get => _isLoaded;
            private set
            {
                if (_isLoaded == value) return;
                _isLoaded = value;
                IsLoadedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? IsLoadedChanged;

        public LocalGemmaService()
        {
            var ragUrl = Environment.GetEnvironmentVariable("RAG_SERVER_URL");
        }

        public async Task InitializeAsync()
        {
            if (IsLoaded) return;

            await Task.Run(() =>
            {
                try
                {
                    _parameters = new ModelParams(ModelPath)
                    {
                        ContextSize = 4096,
                        Threads = Environment.ProcessorCount / 2,
                        BatchThreads = Environment.ProcessorCount / 2,
                        GpuLayerCount = 99,
                        UseMemorymap = true,
                    };

                    _model = LLamaWeights.LoadFromFile(_parameters);
                    _singleExecutor = new StatelessExecutor(_model, _parameters);

                    IsLoaded = true;
                    System.Diagnostics.Debug.WriteLine("Gemma 모델 로드 완료");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"모델 로드 실패: {ex.Message}");
                    IsLoaded = false;
                }

                return Task.CompletedTask;
            });
        }

        public bool HasModelFile()
        {
            return File.Exists(ModelPath) && new FileInfo(ModelPath).Length > 0;
        }

        public async Task DownloadModelAsync(HttpClient httpClient, CancellationToken ct, IProgress<double>? progress = null)
        {
            if (HasModelFile())
            {
                progress?.Report(100);
                return;
            }

            await _downloadLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (HasModelFile())
                {
                    progress?.Report(100);
                    return;
                }

                string downloadUrl = GetModelDownloadUrl();
                string tempPath = ModelPath + ".download";
                File.Delete(tempPath);

                using var req = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("User-Agent");

                using HttpResponseMessage resp = await httpClient.SendAsync(
                    req,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct).ConfigureAwait(false);

                resp.EnsureSuccessStatusCode();

                long total = resp.Content.Headers.ContentLength ?? -1;
                long readTotal = 0;
                byte[] buffer = new byte[1024 * 128];

                await using Stream src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using FileStream dst = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

                while (true)
                {

                    int read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                    if (read == 0) break;

                    await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    readTotal += read;

                    if (total > 0)
                    {
                        double percent = (double)readTotal / total * 100.0;
                        progress?.Report(percent);
                    }
                }

                await dst.FlushAsync();
                dst.Dispose();
                src.Dispose();

                if (File.Exists(ModelPath))
                    File.Delete(ModelPath);

                File.Move(tempPath, ModelPath);

                progress?.Report(100);
            }
            finally
            {
                _downloadLock.Release();
            }
        }

        private static string GetModelDownloadUrl()
        {
            return $"https://huggingface.co/{ModelRepo}/resolve/main/{ModelFileName}?download=true";
        }


        public async Task<string> ChatAsync(string userMessage, string Search, CancellationToken ct, IProgress<string>? progress = null, bool JsonMode = false)
        {
            if (!IsLoaded || _singleExecutor == null) return "모델이 로드되지 않았습니다.";
            if (string.IsNullOrWhiteSpace(userMessage)) return "";

            await _aiLock.WaitAsync(ct);
            try
            {
                Task<RagReference> RagData = GetRagReferencesFromServerAsync(Search, ct);
                string ragResult = await RagData.ConfigureAwait(false) is RagReference reference
                    ? $"[RAG Reference]\nTitle: {reference.Title}\nURL: {reference.Url}\nSummary: {reference.Summary}\nContent: {reference.Content}"
                    : "";

                string prompt = CreateGemmaChatPrompt(userMessage, ragResult, JsonMode);

                var inferenceParams = new InferenceParams()
                {
                    MaxTokens = 2048,
                    AntiPrompts = new List<string> { "<start_of_turn>user" }
                };

                return await InferSingleCoreAsync(prompt, inferenceParams, ct, progress).ConfigureAwait(false);
            }
            finally
            {
                _aiLock.Release();
            }
        }

        public async Task<string> AgentChatAsync(
            string userMessage,
            IReadOnlyList<(string Role, string Content)>? history,
            CancellationToken ct,
            IProgress<string>? progress = null)
        {
            if (!IsLoaded || _singleExecutor == null) return "모델이 로드되지 않았습니다.";
            if (string.IsNullOrWhiteSpace(userMessage)) return "";

            string toolPrompt = CreateGemmaToolCallPrompt(userMessage, history);

            var toolParams = new InferenceParams()
            {
                MaxTokens = 256,
                AntiPrompts = new List<string> { "<start_of_turn>user" }
            };

            await _aiLock.WaitAsync(ct);
            try
            {
                string toolRaw = await InferSingleCoreAsync(toolPrompt, toolParams, ct, progress: null).ConfigureAwait(false);
                string toolJson = ExtractFirstJsonObject(toolRaw);

                ToolCallDto toolCall;
                try
                {
                    toolCall = JsonSerializer.Deserialize<ToolCallDto>(
                        toolJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ToolCallDto();
                }
                catch
                {
                    toolCall = new ToolCallDto();
                }

                var selectedTools = new List<string>();
                if (toolCall.Tools != null && toolCall.Tools.Count > 0)
                    selectedTools.AddRange(toolCall.Tools);

                selectedTools = selectedTools
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .Where(t => !string.Equals(t, "none", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var toolResults = new List<(string ToolName, string ResultJson)>();
                foreach (string toolName in selectedTools)
                {
                    if (ToolRegistry.TryGetTool(toolName, out var tool) && tool != null)
                    {
                        string result = await tool.ExecuteAsync(ct).ConfigureAwait(false);
                        toolResults.Add((toolName, result));
                    }
                }

                // 최종 답변도 Stateless로 통일
                string finalPrompt = CreateGemmaToolResultAnswerPrompt(userMessage, history, toolResults);

                var finalParams = new InferenceParams()
                {
                    MaxTokens = 2048,
                    AntiPrompts = new List<string> { "<start_of_turn>user" }
                };

                return await InferSingleCoreAsync(finalPrompt, finalParams, ct, progress).ConfigureAwait(false);
            }
            finally
            {
                _aiLock.Release();
            }
        }

        private async Task<string> InferSingleCoreAsync(string prompt, InferenceParams inferenceParams, CancellationToken ct, IProgress<string>? progress)
        {
            if (_singleExecutor == null) return "모델이 로드되지 않았습니다.";

            var outSb = new StringBuilder();
            await foreach (var token in _singleExecutor.InferAsync(prompt, inferenceParams, ct))
            {
                outSb.Append(token);
                progress?.Report(token);
            }

            return outSb.ToString();
        }

        private static string CreateGemmaChatPrompt(string userMessage, string RagData, bool JsonMode)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<start_of_turn>user");
            sb.AppendLine("당신은 Windows 시스템 전문가이며, 친절하고 정확한 어시스턴트입니다. 답변은 한국어로 합니다.");

            if (JsonMode)
            {
                sb.AppendLine("반드시 유효한 JSON 객체 1개만 출력하세요.");
                sb.AppendLine("JSON 이외의 텍스트(설명, 마크다운, 코드블록, 백틱)를 절대 출력하지 마세요.");
                sb.AppendLine("출력 스키마를 정확히 따르세요:");
                sb.AppendLine(@"{");
                sb.AppendLine(@"  ""Confidence"": 0~100, ");
                sb.AppendLine(@"  ""Description"": ""string"",  ");
                sb.AppendLine(@"  ""Solutions"": ""string"",   ");
                sb.AppendLine(@"}");
                sb.AppendLine("규칙:");
                sb.AppendLine(@"- 키 이름을 변경하지 마세요.");
                sb.AppendLine(@"- 문자열 값은 반드시 큰따옴표를 사용하세요.");
                sb.AppendLine(@"- 줄바꿈이 필요하면 문자열 내부에서 \n 로 이스케이프하세요.");
                sb.AppendLine(@"- 정보가 부족하면 추측하지 말고 ""Description"" 또는 ""Solutions""에 ""정보 부족""을 명시하세요.");
            }

            if (!string.IsNullOrWhiteSpace(RagData))
            {
                sb.AppendLine("[RAG Reference]");
                sb.AppendLine(RagData);
            }

            sb.AppendLine("<end_of_turn>");

            sb.AppendLine("<start_of_turn>user");
            sb.AppendLine(userMessage);
            sb.AppendLine("<end_of_turn>");
            sb.AppendLine("<start_of_turn>model");

            return sb.ToString();
        }

        private static string CreateGemmaToolCallPrompt(string userMessage, IReadOnlyList<(string Role, string Content)>? history)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<start_of_turn>user");
            sb.AppendLine("너는 사용자의 요청을 해결하기 위해 도구 호출이 필요한지 결정하는 라우터다.");
            sb.AppendLine("반드시 JSON 객체 1개만 출력하고, JSON 이외의 텍스트는 절대 출력하지 마라.");
            sb.AppendLine("tool 목록은 아래와 같다:");
            sb.AppendLine(ToolRegistry.BuildToolListForPrompt());
            sb.AppendLine(@"- ""none"": 도구가 필요 없을 때");
            sb.AppendLine(@"출력 형식: { ""Tools"": [""tool1"", ""tool2""] }");
            sb.AppendLine(@"도구가 필요 없으면: { ""Tools"": [""none""] }");
            sb.AppendLine("<end_of_turn>");

            if (history != null)
            {
                foreach (var (role, content) in history)
                {
                    if (string.IsNullOrWhiteSpace(content)) continue;
                    var normalizedRole = string.Equals(role, "model", StringComparison.OrdinalIgnoreCase) ? "model" : "user";

                    sb.AppendLine($"<start_of_turn>{normalizedRole}");
                    sb.AppendLine(content);
                    sb.AppendLine("<end_of_turn>");
                }
            }

            sb.AppendLine("<start_of_turn>user");
            sb.AppendLine(userMessage);
            sb.AppendLine("<end_of_turn>");
            sb.AppendLine("<start_of_turn>model");

            return sb.ToString();
        }

        private static string CreateGemmaToolResultAnswerPrompt(
            string userMessage,
            IReadOnlyList<(string Role, string Content)>? history,
            IReadOnlyList<(string ToolName, string ResultJson)> toolResults)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<start_of_turn>user");
            sb.AppendLine("너는 Windows 문제를 진단하고 해결책을 안내하는 전문가다. 답변은 한국어로 한다.");
            sb.AppendLine("아래 [도구 결과들]은 실행된 도구들의 원본 JSON이다.");
            sb.AppendLine("각 결과를 종합해 원인을 추정하고, 사용자가 따라할 수 있는 순서대로 조치 단계를 제시해라.");
            sb.AppendLine("문제 원인이 조치가 필요 없음으로 보이면 \"조치가 필요 없음\"이라고 명시해라.");
            sb.AppendLine("도구 결과 JSON을 그대로 길게 인용하지 말고, 핵심만 요약해라.");
            sb.AppendLine("<end_of_turn>");

            if (history != null)
            {
                foreach (var (role, content) in history)
                {
                    if (string.IsNullOrWhiteSpace(content)) continue;
                    var normalizedRole = string.Equals(role, "model", StringComparison.OrdinalIgnoreCase) ? "model" : "user";

                    sb.AppendLine($"<start_of_turn>{normalizedRole}");
                    sb.AppendLine(content);
                    sb.AppendLine("<end_of_turn>");
                }
            }

            sb.AppendLine("<start_of_turn>user");
            sb.AppendLine($"[사용자 입력]\n{userMessage}");
            sb.AppendLine();
            sb.AppendLine("[실행된 도구 결과들]");

            if (toolResults.Count == 0)
            {
                sb.AppendLine("- none");
            }
            else
            {
                foreach (var (toolName, resultJson) in toolResults)
                {
                    sb.AppendLine($"- Tool: {toolName}");
                    sb.AppendLine(resultJson);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("<end_of_turn>");
            sb.AppendLine("<start_of_turn>model");

            return sb.ToString();
        }

        private static string ExtractFirstJsonObject(string text)
        {
            int start = text.IndexOf('{');
            if (start < 0) return @"{ ""Tools"": [""none""] }";

            int depth = 0;
            bool inString = false;

            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\')) inString = !inString;
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}') depth--;

                if (depth == 0)
                    return text.Substring(start, i - start + 1).Trim();
            }

            return @"{ ""Tools"": [""none""] }";
        }
    }
}