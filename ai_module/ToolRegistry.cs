using logger_client.Tools.network;
using logger_client.Tools.os;
using System;
using System.Collections.Generic;
using System.Linq;

namespace logger_client.ai_module
{
    internal static class ToolRegistry
    {
        private static readonly IReadOnlyList<ToolDefinition> Tools =
        [
            new ToolDefinition
            {
                Name = "get_network_status",
                Description = "현재 네트워크 어댑터/IP/DNS/게이트웨이 상태를 조회한다.",
                ExecuteAsync = ct => NetWorkService.GetCurrentNetWorkStatus()
            },

            // 통합 조회
            new ToolDefinition
            {
                Name = "get_system_status",
                Description = "시스템 상태(부팅 모드, 보안 부팅, TPM, Windows 버전, 하드웨어, VBS)를 통합 조회한다.",
                ExecuteAsync = ct => OSInfomationService.GetCurrentOSStatus()
            },

            // 분리 조회
            new ToolDefinition
            {
                Name = "get_boot_type",
                Description = "BIOS/UEFI 부팅 형식을 조회한다.",
                ExecuteAsync = ct => OSInfomationService.GetBootTypeStatus()
            },
            new ToolDefinition
            {
                Name = "get_secure_boot_status",
                Description = "보안 부팅 활성화 여부를 조회한다.",
                ExecuteAsync = ct => OSInfomationService.GetSecureBootStatus()
            },
            new ToolDefinition
            {
                Name = "get_tpm_status",
                Description = "TPM 활성화/준비/활성 상태를 조회한다.",
                ExecuteAsync = ct => OSInfomationService.GetTpmStatus()
            },
            new ToolDefinition
            {
                Name = "get_windows_version",
                Description = "Windows 버전/빌드 정보를 조회한다.",
                ExecuteAsync = ct => OSInfomationService.GetWindowsVersionStatus()
            },
            new ToolDefinition
            {
                Name = "get_hardware_status",
                Description = "CPU/메모리/메인보드/디스크 하드웨어 정보를 조회한다.",
                ExecuteAsync = ct => OSInfomationService.GetHardwareStatus()
            },
            new ToolDefinition
            {
                Name = "get_vbs_status",
                Description = "VBS/Credential Guard 상태를 조회한다.",
                ExecuteAsync = ct => OSInfomationService.GetVbsStatus()
            },
        ];

        public static bool TryGetTool(string? name, out ToolDefinition? tool)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                tool = null;
                return false;
            }

            tool = Tools.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            return tool != null;
        }

        public static string BuildToolListForPrompt()
        {
            return string.Join(
                "\n",
                Tools.Select(t => $@"- ""{t.Name}"": {t.Description}"));
        }
    }
}
