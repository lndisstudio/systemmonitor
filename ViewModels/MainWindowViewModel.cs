using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using SystemMonitor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SystemMonitor.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ISystemMonitorService _monitorService;
        private bool _isMonitoring;
        private int _refreshIntervalMs = 2000; // Varsayılan 2 saniye
        private readonly List<string> _logEntries = new();

        // CPU Properties
        [ObservableProperty]
        private string _cpuName = "Bilgi alınıyor...";
        
        [ObservableProperty]
        private double _cpuUsage;
        
        [ObservableProperty]
        private string _cpuTemperatureDisplay = "N/A";
        
        [ObservableProperty]
        private string _cpuHealth = "Unknown";
        
        [ObservableProperty]
        private int _cpuCores;
        
        [ObservableProperty]
        private double _cpuFrequency;

        // Memory Properties
        [ObservableProperty]
        private string _memoryUsage = "0 GB / 0 GB";
        
        [ObservableProperty]
        private double _memoryUsagePercent;
        
        [ObservableProperty]
        private double _memorySpeed;
        
        [ObservableProperty]
        private string _memoryType = "DDR4";
        
        [ObservableProperty]
        private string _memoryHealth = "Unknown";

        // Disk Properties
        [ObservableProperty]
        private string _diskName = "C:";
        
        [ObservableProperty]
        private string _diskUsage = "0 GB / 0 GB";
        
        [ObservableProperty]
        private double _diskUsagePercent;
        
        [ObservableProperty]
        private string _diskTemperatureDisplay = "N/A";
        
        [ObservableProperty]
        private string _diskHealth = "Unknown";
        
        [ObservableProperty]
        private string _diskModel = "Unknown";

        // GPU Properties
        [ObservableProperty]
        private string _gpuName = "Bilgi alınıyor...";
        
        [ObservableProperty]
        private double _gpuUsage;
        
        [ObservableProperty]
        private string _gpuTemperatureDisplay = "N/A";
        
        [ObservableProperty]
        private string _gpuMemoryUsage = "0 GB / 0 GB";
        
        [ObservableProperty]
        private double _gpuMemoryUsagePercent;
        
        [ObservableProperty]
        private double _gpuFanSpeed;
        
        [ObservableProperty]
        private double _gpuPowerUsage;
        
        [ObservableProperty]
        private string _gpuHealth = "Unknown";

        // Motherboard Properties
        [ObservableProperty]
        private string _motherboardModel = "Bilgi alınıyor...";
        
        [ObservableProperty]
        private string _motherboardManufacturer = "Unknown";
        
        [ObservableProperty]
        private string _biosVersion = "Unknown";
        
        [ObservableProperty]
        private string _motherboardTemperatureDisplay = "N/A";
        
        [ObservableProperty]
        private string _motherboardHealth = "Unknown";

        // PSU Properties
        [ObservableProperty]
        private string _psuName = "Power Supply";
        
        [ObservableProperty]
        private string _psuModel = "Unknown";
        
        [ObservableProperty]
        private int _psuWattage;
        
        [ObservableProperty]
        private double _psuLoadPercent;
        
        [ObservableProperty]
        private double _psuCurrentLoadWatts;
        
        [ObservableProperty]
        private string _psuTemperatureDisplay = "N/A";
        
        [ObservableProperty]
        private double _psuEfficiency;
        
        [ObservableProperty]
        private string _psuVoltage3VDisplay = "N/A";
        
        [ObservableProperty]
        private string _psuVoltage5VDisplay = "N/A";
        
        [ObservableProperty]
        private string _psuVoltage12VDisplay = "N/A";
        
        [ObservableProperty]
        private string _psuHealth = "Unknown";

        // Status & Log
        [ObservableProperty]
        private string _statusMessage = "Hazır";
        
        [ObservableProperty]
        private string _logText = "";

        public MainWindowViewModel()
        {
            _monitorService = SystemMonitorServiceFactory.Create();
            _isMonitoring = false;
            
            // Uygulama açılır açılmaz otomatik başlat
            Task.Run(async () => await StartMonitoring());
        }

        [RelayCommand]
        private async Task StartMonitoring()
        {
            if (_isMonitoring) return;
            _isMonitoring = true;
            StatusMessage = "İzleniyor...";
            AddLog("Sistem izleme başlatıldı");
            
            while (_isMonitoring)
            {
                try
                {
                    await UpdateSystemInfo();
                    await Task.Delay(_refreshIntervalMs);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Hata: {ex.Message}";
                    AddLog($"HATA: {ex.Message}");
                    _isMonitoring = false;
                    break;
                }
            }
        }

        [RelayCommand]
        private void StopMonitoring()
        {
            _isMonitoring = false;
            StatusMessage = "Durduruldu";
            AddLog("Sistem izleme durduruldu");
        }

        [RelayCommand]
        private void SetRefreshInterval(string seconds)
        {
            if (int.TryParse(seconds, out var sec))
            {
                _refreshIntervalMs = sec * 1000;
                StatusMessage = $"Yenileme: {sec}s";
                AddLog($"Yenileme süresi {sec} saniye olarak ayarlandı");
            }
        }

        [RelayCommand]
        private async Task UpdateSystemInfoCommand()
        {
            await UpdateSystemInfo();
            StatusMessage = "Yenilendi";
            AddLog("Sistem bilgileri manuel yenilendi");
        }

        [RelayCommand]
        private void ShowLog()
        {
            var logWindow = new Views.LogWindow(this);
            logWindow.Show();
        }

        [RelayCommand]
        private void ClearLog()
        {
            _logEntries.Clear();
            LogText = "";
            AddLog("Log temizlendi");
        }

        [RelayCommand]
        private void Exit()
        {
            _isMonitoring = false;
            Environment.Exit(0);
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logEntries.Add($"[{timestamp}] {message}");
            if (_logEntries.Count > 100) _logEntries.RemoveAt(0); // Max 100 log
            LogText = string.Join("\n", _logEntries);
        }

        private string FormatTemperature(double temp)
        {
            if (temp <= 0) return "N/A";
            return $"{temp:F0}°C";
        }

        private void ShowTemperatureWarning(string component, double temp)
        {
            try
            {
                // Windows toast notification using PowerShell
                var script = $"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime] | Out-Null; " +
                    $"$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02); " +
                    $"$textNodes = $template.GetElementsByTagName('text'); " +
                    $"$textNodes.Item(0).AppendChild($template.CreateTextNode('System Monitor - {component} ALARM')) | Out-Null; " +
                    $"$textNodes.Item(1).AppendChild($template.CreateTextNode('{component} sıcaklığı {temp:F0}°C ile kritik seviyede!')) | Out-Null; " +
                    $"$toast = [Windows.UI.Notifications.ToastNotification]::new($template); " +
                    $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('SystemMonitor').Show($toast)";
                
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-WindowStyle Hidden -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch { /* Bildirim hatası */ }
        }

        private string FormatVoltage(double voltage)
        {
            if (voltage <= 0) return "N/A";
            return $"{voltage:F2}V";
        }

        private async Task UpdateSystemInfo()
        {
            // CPU
            try
            {
                var cpuInfo = await _monitorService.GetCpuInfoAsync();
                CpuName = cpuInfo.Name;
                CpuUsage = Math.Round(cpuInfo.UsagePercent, 1);
                CpuTemperatureDisplay = FormatTemperature(cpuInfo.TemperatureCelsius);
                CpuHealth = cpuInfo.HealthStatus;
                CpuCores = cpuInfo.CoreCount;
                CpuFrequency = Math.Round(cpuInfo.CurrentFrequencyMHz / 1000.0, 2); // GHz
                
                if (cpuInfo.TemperatureCelsius < 0)
                    AddLog("CPU sıcaklık sensörü bulunamadı");
                
                // Kritik sıcaklık uyarısı
                if (cpuInfo.TemperatureCelsius > 90)
                {
                    AddLog($"KRITIK: CPU sıcaklığı {cpuInfo.TemperatureCelsius:F0}°C - Tehlikli!");
                    ShowTemperatureWarning("CPU", cpuInfo.TemperatureCelsius);
                }
                else if (cpuInfo.TemperatureCelsius > 80)
                {
                    AddLog($"UYARI: CPU sıcaklığı {cpuInfo.TemperatureCelsius:F0}°C - Yüksek");
                }
            }
            catch (Exception ex)
            {
                AddLog($"CPU okuma hatası: {ex.Message}");
            }

            // Memory
            try
            {
                var memInfo = await _monitorService.GetMemoryInfoAsync();
                MemoryUsage = $"{FormatBytes(memInfo.UsedBytes)} / {FormatBytes(memInfo.TotalBytes)}";
                MemoryUsagePercent = Math.Round(memInfo.UsagePercent, 1);
                MemorySpeed = memInfo.SpeedMHz;
                MemoryType = memInfo.Type;
                MemoryHealth = memInfo.HealthStatus;
            }
            catch (Exception ex)
            {
                AddLog($"RAM okuma hatası: {ex.Message}");
            }

            // Disk
            try
            {
                var diskInfo = await _monitorService.GetDiskInfoAsync();
                DiskName = diskInfo.Name;
                DiskUsage = $"{FormatBytes(diskInfo.UsedBytes)} / {FormatBytes(diskInfo.TotalBytes)}";
                DiskUsagePercent = Math.Round(diskInfo.UsagePercent, 1);
                DiskTemperatureDisplay = FormatTemperature(diskInfo.TemperatureCelsius);
                DiskHealth = diskInfo.HealthStatus;
                DiskModel = diskInfo.Model;
                
                if (diskInfo.TemperatureCelsius < 0)
                    AddLog("Disk sıcaklık sensörü bulunamadı");
            }
            catch (Exception ex)
            {
                AddLog($"Disk okuma hatası: {ex.Message}");
            }

            // GPU
            try
            {
                var gpuInfo = await _monitorService.GetGpuInfoAsync();
                GpuName = gpuInfo.Name;
                GpuUsage = Math.Round(gpuInfo.UsagePercent, 1);
                GpuTemperatureDisplay = FormatTemperature(gpuInfo.TemperatureCelsius);
                GpuMemoryUsage = $"{FormatBytes(gpuInfo.UsedMemoryBytes)} / {FormatBytes(gpuInfo.TotalMemoryBytes)}";
                GpuMemoryUsagePercent = Math.Round(gpuInfo.MemoryUsagePercent, 1);
                GpuFanSpeed = Math.Round(gpuInfo.FanSpeedPercent, 1);
                GpuPowerUsage = Math.Round(gpuInfo.PowerUsageWatts, 1);
                GpuHealth = gpuInfo.HealthStatus;
                
                if (gpuInfo.TemperatureCelsius < 0)
                    AddLog("GPU sıcaklık sensörü bulunamadı");
                
                // GPU sıcaklık uyarısı
                if (gpuInfo.TemperatureCelsius > 85)
                {
                    AddLog($"KRITIK: GPU sıcaklığı {gpuInfo.TemperatureCelsius:F0}°C - Tehlikli!");
                    ShowTemperatureWarning("GPU", gpuInfo.TemperatureCelsius);
                }
                else if (gpuInfo.TemperatureCelsius > 75)
                {
                    AddLog($"UYARI: GPU sıcaklığı {gpuInfo.TemperatureCelsius:F0}°C - Yüksek");
                }
            }
            catch (Exception ex)
            {
                AddLog($"GPU okuma hatası: {ex.Message}");
            }

            // Motherboard
            try
            {
                var mbInfo = await _monitorService.GetMotherboardInfoAsync();
                MotherboardModel = $"{mbInfo.Manufacturer} {mbInfo.Model}";
                MotherboardManufacturer = mbInfo.Manufacturer;
                BiosVersion = mbInfo.BiosVersion;
                MotherboardTemperatureDisplay = FormatTemperature(mbInfo.TemperatureCelsius);
                MotherboardHealth = mbInfo.HealthStatus;
                
                if (mbInfo.TemperatureCelsius < 0)
                    AddLog("Anakart sıcaklık sensörü bulunamadı");
                if (string.IsNullOrEmpty(mbInfo.Model) || mbInfo.Model == "Unknown")
                    AddLog("Anakart modeli okunamadı - WMI erişimi kontrol ediliyor");
            }
            catch (Exception ex)
            {
                AddLog($"Anakart okuma hatası: {ex.Message}");
            }

            // PSU
            try
            {
                var psuInfo = await _monitorService.GetPsuInfoAsync();
                PsuName = psuInfo.Name;
                PsuModel = psuInfo.Model;
                PsuWattage = psuInfo.Wattage;
                PsuLoadPercent = Math.Round(psuInfo.LoadPercent, 1);
                PsuCurrentLoadWatts = Math.Round(psuInfo.CurrentLoadWatts, 1);
                PsuTemperatureDisplay = FormatTemperature(psuInfo.TemperatureCelsius);
                PsuEfficiency = psuInfo.EfficiencyPercent;
                PsuVoltage3VDisplay = FormatVoltage(psuInfo.Voltage3V);
                PsuVoltage5VDisplay = FormatVoltage(psuInfo.Voltage5V);
                PsuVoltage12VDisplay = FormatVoltage(psuInfo.Voltage12V);
                PsuHealth = psuInfo.HealthStatus;
                
                if (psuInfo.Voltage3V <= 0 && psuInfo.Voltage5V <= 0 && psuInfo.Voltage12V <= 0)
                    AddLog("PSU voltaj sensörleri bulunamadı - WMI Voltage Probe erişimi yok");
            }
            catch (Exception ex)
            {
                AddLog($"PSU okuma hatası: {ex.Message}");
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
