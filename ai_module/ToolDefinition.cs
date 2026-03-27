using System;
using System.Threading;
using System.Threading.Tasks;

namespace logger_client.ai_module
{
    internal sealed class ToolDefinition
    {
        public required string Name { get; init; }
        public required string Description { get; init; }

        public required Func<CancellationToken, Task<string>> ExecuteAsync { get; init; }
    }
}