using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace logger_client.Tools.os
{
    internal static class OSInfomationService
    {
        private const string SecureBootRegistryPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\State";
        private const string SecureBootRegistryValue = "UEFISecureBootEnabled";
        private const string WindowsCurrentVersionPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        [DllImport("kernel32.dll")]
        private static extern bool GetFirmwareType(out FirmwareType firmwareType);

        private enum FirmwareType : uint
        {
            Unknown = 0,
            Bios = 1,
            Uefi = 2,
            Max = 3
        }

        // 통합 조회
        public static async Task<string> GetCurrentOSStatus()
        {
            string bootType = GetBootType();
            bool? secureBootEnabled = GetSecureBootEnabled();
            object tpmInfo = await GetTpmStatus().ConfigureAwait(false);
            object windowsInfo = await GetWindowsVersionStatus().ConfigureAwait(false);
            object hardwareInfo = await GetHardwareStatus().ConfigureAwait(false);
            object vbsInfo = await GetVbsStatus().ConfigureAwait(false);

            var payload = new
            {
                Timestamp = DateTimeOffset.UtcNow,
                BootType = bootType,
                SecureBootEnabled = secureBootEnabled,
                Tpm = tpmInfo,
                Windows = windowsInfo,
                Hardware = hardwareInfo,
                VBSInfo = vbsInfo,
            };

            return JsonSerializer.Serialize(payload, JsonSerializerOptions.Strict);
        }

        // 분리 조회
        public static Task<string> GetBootTypeStatus()
            => Task.FromResult(JsonSerializer.Serialize(new
            {
                Timestamp = DateTimeOffset.UtcNow,
                BootType = GetBootType()
            }, JsonSerializerOptions.Strict));

        public static Task<string> GetSecureBootStatus()
            => Task.FromResult(JsonSerializer.Serialize(new
            {
                Timestamp = DateTimeOffset.UtcNow,
                SecureBootEnabled = GetSecureBootEnabled()
            }, JsonSerializerOptions.Strict));

        public static Task<string> GetWindowsVersionStatus()
            => Task.FromResult(JsonSerializer.Serialize(new
            {
                Timestamp = DateTimeOffset.UtcNow,
                Windows = GetWindowsVersionInfo()
            }, JsonSerializerOptions.Strict));

        public static async Task<string> GetTpmStatus()
            => JsonSerializer.Serialize(new
            {
                Timestamp = DateTimeOffset.UtcNow,
                Tpm = await GetTpmInfoAsync().ConfigureAwait(false)
            }, JsonSerializerOptions.Strict);

        public static async Task<string> GetHardwareStatus()
            => JsonSerializer.Serialize(new
            {
                Timestamp = DateTimeOffset.UtcNow,
                Hardware = await GetHardwareInfoAsync().ConfigureAwait(false)
            }, JsonSerializerOptions.Strict);

        public static Task<string> GetVbsStatus()
            => Task.FromResult(JsonSerializer.Serialize(new
            {
                Timestamp = DateTimeOffset.UtcNow,
                VBSInfo = GetVBSInformation()
            }, JsonSerializerOptions.Strict));

        private static string GetBootType()
        {
            try
            {
                if (!GetFirmwareType(out FirmwareType firmwareType))
                    return "Unknown";

                return firmwareType switch
                {
                    FirmwareType.Bios => "BIOS",
                    FirmwareType.Uefi => "UEFI",
                    _ => "Unknown"
                };
            }
            catch
            {
                return "Unknown";
            }
        }

        private static bool? GetSecureBootEnabled()
        {
            try
            {
                object? value = Registry.GetValue(SecureBootRegistryPath, SecureBootRegistryValue, null);
                if (value is int intValue)
                    return intValue == 1;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static object GetWindowsVersionInfo()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(WindowsCurrentVersionPath);
                if (key == null)
                {
                    return new
                    {
                        DisplayVersion = "Unknown",
                        CurrentBuild = "Unknown",
                        Ubr = "Unknown",
                        OsBuild = Environment.OSVersion.Version.Build.ToString(),
                        OsVersion = Environment.OSVersion.Version.Build >= 22000 ? 11 : 10
                    };
                }

                string displayVersion = key.GetValue("DisplayVersion")?.ToString()
                                        ?? key.GetValue("ReleaseId")?.ToString()
                                        ?? "Unknown";
                string currentBuild = key.GetValue("CurrentBuildNumber")?.ToString() ?? "Unknown";
                string ubr = key.GetValue("UBR")?.ToString() ?? "Unknown";

                return new
                {
                    DisplayVersion = displayVersion,
                    CurrentBuild = currentBuild,
                    Ubr = ubr,
                    OsBuild = Environment.OSVersion.Version.Build.ToString(),
                    OsVersion = Environment.OSVersion.Version.Build >= 22000 ? 11 : 10
                };
            }
            catch
            {
                return new
                {
                    DisplayVersion = "Unknown",
                    CurrentBuild = "Unknown",
                    Ubr = "Unknown",
                    OsBuild = Environment.OSVersion.Version.Build.ToString(),
                    OsVersion = Environment.OSVersion.Version.Build >= 22000 ? 11 : 10
                };
            }
        }

        private static object GetVBSInformation()
        {
            try
            {
                string script = @"
                    $vbs = Get-CimInstance -Class Win32_DeviceGuard -Namespace root\Microsoft\Windows\DeviceGuard
                    $credentialGuard = Get-CimInstance -Class Win32_DeviceGuardCredentialGuard -Namespace root\Microsoft\Windows\DeviceGuard
                    [pscustomobject]@{
                        VirtualizationBasedSecurityRunning = $vbs.VirtualizationBasedSecurityRunning
                        HypervisorEnforcedCodeIntegrityPolicyEnabled = $vbs.HypervisorEnforcedCodeIntegrityPolicyEnabled
                        SecureBootRequired = $vbs.SecureBootRequired
                        DMAProtectionRequired = $vbs.DMAProtectionRequired
                        UEFIFirmwareUpdateProtectionRequired = $vbs.UEFIFirmwareUpdateProtectionRequired
                        HypervisorEnforcedCodeIntegrityPolicyAvailable = $vbs.HypervisorEnforcedCodeIntegrityPolicyAvailable
                        VirtualizationBasedSecurityAvailable = $vbs.VirtualizationBasedSecurityAvailable
                        CredentialGuardRunning = $credentialGuard.CredentialGuardRunning
                        CredentialGuardAvailable = $credentialGuard.CredentialGuardAvailable
                    } | ConvertTo-Json -Compress
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

                using var process = new Process { StartInfo = psi };
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    return new
                    {
                        Error = string.IsNullOrWhiteSpace(error) ? "Get VBS info failed" : error.Trim()
                    };
                }

                output = output.Replace("null", "false", StringComparison.OrdinalIgnoreCase);
                return JsonSerializer.Deserialize<object>(output) ?? new { Error = "VBS info parse failed" };
            }
            catch (Exception ex)
            {
                return new { Error = ex.Message };
            }
        }

        private static async Task<object> GetHardwareInfoAsync()
        {
            try
            {
                string script = @"
                    $cs = Get-CimInstance Win32_ComputerSystem
                    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
                    $gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1
                    $boards = Get-CimInstance Win32_BaseBoard | Select-Object Manufacturer, Product, SerialNumber, Version
                    $memModules = Get-CimInstance Win32_PhysicalMemory | Select-Object Manufacturer, PartNumber, Speed, ConfiguredClockSpeed, Capacity, BankLabel, DeviceLocator
                    $totalMemBytes = ($memModules | Measure-Object -Property Capacity -Sum).Sum
                    $disks = Get-CimInstance Win32_DiskDrive | Select-Object Model, Manufacturer, Size, MediaType, SerialNumber

                    [pscustomobject]@{
                      ComputerSystem = [pscustomobject]@{
                        Manufacturer = $cs.Manufacturer
                        Model        = $cs.Model
                        SystemType   = $cs.SystemType
                      }
                      CPU = [pscustomobject]@{
                        Name                  = $cpu.Name
                        Manufacturer          = $cpu.Manufacturer
                        NumberOfCores         = $cpu.NumberOfCores
                        NumberOfLogicalProcessors = $cpu.NumberOfLogicalProcessors
                        MaxClockSpeedMHz      = $cpu.MaxClockSpeed
                      }
                      Memory = [pscustomobject]@{
                        TotalBytes  = $totalMemBytes
                        TotalGB     = [math]::Round($totalMemBytes / 1GB, 2)
                        ModuleCount = @($memModules).Count
                        Modules     = $memModules
                      }

                      Mainboard = $boards

                      Disks = $disks

                      GPU = [pscustomobject]@{
                        Name         = $gpu.Name
                        Manufacturer = $gpu.Manufacturer
                        VideoMemoryBytes = $gpu.AdapterRAM
                        VideoMemoryGB = [math]::Round($gpu.AdapterRAM / 1GB, 2)
                      }
                    } | ConvertTo-Json -Depth 6 -Compress
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

                using var process = new Process { StartInfo = psi };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                string error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

                await process.WaitForExitAsync().ConfigureAwait(false);

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    return new
                    {
                        Error = string.IsNullOrWhiteSpace(error) ? "Get hardware info failed" : error.Trim()
                    };
                }

                return JsonSerializer.Deserialize<object>(output) ?? new { Error = "Hardware parse failed" };
            }
            catch (Exception ex)
            {
                return new { Error = ex.Message };
            }
        }

        private static async Task<object> GetTpmInfoAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-Tpm | ConvertTo-Json -Compress\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                string error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

                await process.WaitForExitAsync().ConfigureAwait(false);

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    return new
                    {
                        Available = false,
                        Error = string.IsNullOrWhiteSpace(error) ? "Get-Tpm failed" : error.Trim()
                    };
                }

                using JsonDocument doc = JsonDocument.Parse(output);
                JsonElement root = doc.RootElement;

                bool? tpmPresent = root.TryGetProperty("TpmPresent", out JsonElement presentEl) &&
                                   (presentEl.ValueKind == JsonValueKind.True || presentEl.ValueKind == JsonValueKind.False)
                    ? presentEl.GetBoolean()
                    : null;

                bool? tpmReady = root.TryGetProperty("TpmReady", out JsonElement readyEl) &&
                                 (readyEl.ValueKind == JsonValueKind.True || readyEl.ValueKind == JsonValueKind.False)
                    ? readyEl.GetBoolean()
                    : null;

                bool? tpmEnabled = root.TryGetProperty("TpmEnabled", out JsonElement enabledEl) &&
                                   (enabledEl.ValueKind == JsonValueKind.True || enabledEl.ValueKind == JsonValueKind.False)
                    ? enabledEl.GetBoolean()
                    : null;

                bool? tpmActivated = root.TryGetProperty("TpmActivated", out JsonElement activatedEl) &&
                                     (activatedEl.ValueKind == JsonValueKind.True || activatedEl.ValueKind == JsonValueKind.False)
                    ? activatedEl.GetBoolean()
                    : null;

                string manufacturer = root.TryGetProperty("ManufacturerIdTxt", out JsonElement mEl) ? mEl.ToString() ?? "Unknown" : "Unknown";

                return new
                {
                    Available = true,
                    TpmPresent = tpmPresent,
                    TpmReady = tpmReady,
                    TpmEnabled = tpmEnabled,
                    TpmActivated = tpmActivated,
                    Manufacturer = manufacturer
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Available = false,
                    Error = ex.Message
                };
            }
        }
    }
}
