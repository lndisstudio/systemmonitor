using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SystemMonitor.Services
{
    public class WindowsSystemMonitorService : ISystemMonitorService
    {
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        
        public WindowsSystemMonitorService()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                _cpuCounter.NextValue(); // İlk okuma genellikle 0 döndürür,hazırla
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Performance counters başlatılamadı: {ex.Message}");
            }
        }

        public async Task<CpuInfo> GetCpuInfoAsync()
        {
            return await Task.Run(() =>
            {
                var cpuInfo = new CpuInfo();
                try
                {
                    // CPU kullanımı
                    cpuInfo.UsagePercent = _cpuCounter?.NextValue() ?? 0;
                    
                    // CPU bilgileri - WMI üzerinden
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        cpuInfo.Name = obj["Name"]?.ToString() ?? "Unknown";
                        cpuInfo.CoreCount = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                        cpuInfo.ThreadCount = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                        cpuInfo.MaxFrequencyMHz = Convert.ToDouble(obj["MaxClockSpeed"] ?? 0);
                        break;
                    }
                    
                    // Mevcut frekans (yaklaşık)
                    cpuInfo.CurrentFrequencyMHz = cpuInfo.MaxFrequencyMHz;
                    
                    // CPU sıcaklığı - Validate and sanitize
                    var temp = GetCpuTemperature();
                    // Sanity check: CPU temp should be between 0 and 120°C
                    cpuInfo.TemperatureCelsius = (temp > 0 && temp < 120) ? temp : -1;

                    // Sağlık durumu - Dynamic based on temp and usage
                    if (cpuInfo.TemperatureCelsius <= 0)
                        cpuInfo.HealthStatus = "N/A"; // No sensor
                    else if (cpuInfo.TemperatureCelsius > 90)
                        cpuInfo.HealthStatus = "Critical";
                    else if (cpuInfo.TemperatureCelsius > 80)
                        cpuInfo.HealthStatus = "Warning";
                    else if (cpuInfo.UsagePercent > 90)
                        cpuInfo.HealthStatus = "Warning";
                    else
                        cpuInfo.HealthStatus = "Good";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CPU bilgisi alınamadı: {ex.Message}");
                }
                return cpuInfo;
            });
        }

        private double GetCpuTemperature()
        {
            try
            {
                // Sıcaklık sensörü dene
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_TemperatureProbe");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var temp = Convert.ToDouble(obj["CurrentReading"] ?? 0);
                    return temp / 10.0; // Kelvin'den Celsius'a
                }
            }
            catch
            {
                // Sıcaklık yoksa -1 döndür
            }
            return -1;
        }

        public async Task<MemoryInfo> GetMemoryInfoAsync()
        {
            return await Task.Run(() =>
            {
                var memInfo = new MemoryInfo();
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var totalKB = Convert.ToInt64(obj["TotalVisibleMemorySize"] ?? 0);
                        var freeKB = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0);
                        
                        memInfo.TotalBytes = totalKB * 1024;
                        memInfo.AvailableBytes = freeKB * 1024;
                        memInfo.UsedBytes = memInfo.TotalBytes - memInfo.AvailableBytes;
                        memInfo.UsagePercent = (double)memInfo.UsedBytes / memInfo.TotalBytes * 100;
                        break;
                    }
                    
                    // RAM tipi ve hızı - BIOS'tan (DDR5 destekli)
                    try
                    {
                        using var ramSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                        var sticks = new List<string>();
                        foreach (ManagementObject obj in ramSearcher.Get())
                        {
                            var speed = Convert.ToInt32(obj["Speed"] ?? 0);
                            var memType = Convert.ToInt32(obj["SMBIOSMemoryType"] ?? 0);
                            sticks.Add($"{memType}@{speed}");
                        }
                        
                        if (sticks.Count > 0)
                        {
                            memInfo.SlotCount = sticks.Count;
                            var parts = sticks[0].Split('@');
                            if (parts.Length == 2 && int.TryParse(parts[1], out int speed))
                            {
                                memInfo.SpeedMHz = speed;
                            }
                            // Parse SMBIOS memory type: DDR5 = 34, DDR4 = 26, DDR3 = 24
                            if (parts.Length == 2 && int.TryParse(parts[0], out int typeNum))
                            {
                                memInfo.Type = typeNum switch
                                {
                                    34 => "DDR5",
                                    26 => "DDR4",
                                    24 => "DDR3",
                                    21 => "DDR2",
                                    20 => "DDR",
                                    _ => "DDR4"
                                };
                            }
                        }
                    }
                    catch { }
                    
                    if (string.IsNullOrEmpty(memInfo.Type))
                        memInfo.Type = memInfo.SpeedMHz >= 4800 ? "DDR5" : "DDR4";
                    
                    memInfo.HealthStatus = memInfo.UsagePercent > 90 ? "Warning" : "Good";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"RAM bilgisi alınamadı: {ex.Message}");
                }
                return memInfo;
            });
        }

        public async Task<DiskInfo> GetDiskInfoAsync()
        {
            return await Task.Run(() =>
            {
                var diskInfo = new DiskInfo();
                try
                {
                    // Sistem diski
                    var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
                    if (drive.IsReady)
                    {
                        diskInfo.Name = drive.Name;
                        diskInfo.TotalBytes = drive.TotalSize;
                        diskInfo.FreeBytes = drive.AvailableFreeSpace;
                        diskInfo.UsedBytes = diskInfo.TotalBytes - diskInfo.FreeBytes;
                        diskInfo.UsagePercent = (double)diskInfo.UsedBytes / diskInfo.TotalBytes * 100;
                        diskInfo.FileSystem = drive.DriveFormat;
                    }
                    
                    // Disk modeli
                    try
                    {
                        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            diskInfo.Model = obj["Model"]?.ToString() ?? "";
                            break;
                        }
                    }
                    catch { }
                    
                    diskInfo.TemperatureCelsius = -1; // Genelde yok
                    diskInfo.HealthStatus = diskInfo.UsagePercent > 90 ? "Warning" : "Good";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Disk bilgisi alınamadı: {ex.Message}");
                }
                return diskInfo;
            });
        }

        public async Task<GpuInfo> GetGpuInfoAsync()
        {
            return await Task.Run(() =>
            {
                var gpuInfo = new GpuInfo();
                try
                {
                    // NVIDIA GPU kontrolü
                    var nvidiaProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        Arguments = "--query-gpu=name,utilization.gpu,temperature.gpu,memory.used,memory.total,fan.speed,power.draw --format=csv,noheader,nounits",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    
                    if (nvidiaProcess != null)
                    {
                        var output = nvidiaProcess.StandardOutput.ReadToEnd();
                        nvidiaProcess.WaitForExit(3000);
                        
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            var parts = output.Trim().Split(',');
                            if (parts.Length >= 6)
                            {
                                gpuInfo.Name = parts[0].Trim();
                                gpuInfo.UsagePercent = double.TryParse(parts[1].Trim(), out var u) ? u : 0;
                                gpuInfo.TemperatureCelsius = double.TryParse(parts[2].Trim(), out var t) ? t : 0;
                                gpuInfo.UsedMemoryBytes = (long)(double.TryParse(parts[3].Trim(), out var mu) ? mu * 1024 * 1024 : 0);
                                gpuInfo.TotalMemoryBytes = (long)(double.TryParse(parts[4].Trim(), out var mt) ? mt * 1024 * 1024 : 0);
                                gpuInfo.FanSpeedPercent = double.TryParse(parts[5].Trim().Replace("%",""), out var f) ? f : 0;
                                if (parts.Length >= 7)
                                {
                                    var pw = double.TryParse(parts[6].Trim().Replace("W",""), out var p) ? p : 0;
                                    // Validate: GPU power should be under 500W, otherwise it's in mW
                                    gpuInfo.PowerUsageWatts = pw > 500 ? pw / 1000.0 : pw;
                                }
                                
                                gpuInfo.MemoryUsagePercent = gpuInfo.TotalMemoryBytes > 0 ?
                                    (double)gpuInfo.UsedMemoryBytes / gpuInfo.TotalMemoryBytes * 100 : 0;
                                
                                // Dynamic health status based on multiple factors
                                if (gpuInfo.TemperatureCelsius > 90 || gpuInfo.PowerUsageWatts > 350)
                                    gpuInfo.HealthStatus = "Critical";
                                else if (gpuInfo.TemperatureCelsius > 80 || gpuInfo.PowerUsageWatts > 250)
                                    gpuInfo.HealthStatus = "Warning";
                                else if (gpuInfo.TemperatureCelsius <= 0 || gpuInfo.TemperatureCelsius < 0)
                                    gpuInfo.HealthStatus = "Unknown";
                                else
                                    gpuInfo.HealthStatus = "Good";
                                return gpuInfo;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"GPU bilgisi alınamadı: {ex.Message}");
                }
                
                // Generic GPU bilgisi - WMI üzerinden
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        gpuInfo.Name = obj["Name"]?.ToString() ?? "Unknown";
                        break;
                    }
                }
                catch { }
                
                gpuInfo.HealthStatus = "Unknown";
                return gpuInfo;
            });
        }

        public async Task<MotherboardInfo> GetMotherboardInfoAsync()
        {
            return await Task.Run(() =>
            {
                var mbInfo = new MotherboardInfo();
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        mbInfo.Manufacturer = obj["Manufacturer"]?.ToString() ?? "";
                        mbInfo.Model = obj["Product"]?.ToString() ?? "";
                        mbInfo.BiosVersion = obj["Version"]?.ToString() ?? "";
                        break;
                    }
                    
                    // BIOS sıcaklığı
                    try
                    {
                        using var tempSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_TemperatureProbe");
                        foreach (ManagementObject obj in tempSearcher.Get())
                        {
                            mbInfo.TemperatureCelsius = Convert.ToDouble(obj["CurrentReading"] ?? 0) / 10.0;
                            break;
                        }
                    }
                    catch { }
                    
                    mbInfo.HealthStatus = mbInfo.TemperatureCelsius > 70 ? "Warning" : "Good";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Anakart bilgisi alınamadı: {ex.Message}");
                }
                return mbInfo;
            });
        }

        public async Task<PsuInfo> GetPsuInfoAsync()
        {
            return await Task.Run(async () =>
            {
                var psuInfo = new PsuInfo();
                try
                {
                    // PSU yükünü sistem bileşenlerinden hesapla
                    double totalPower = 0;
                    
                    // CPU TDP (Tahmini)
                    try
                    {
                        using var cpuSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                        foreach (ManagementObject obj in cpuSearcher.Get())
                        {
                            var tdp = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                            // CPU TDP'sini frekansından tahmin et (yaklaşık)
                            totalPower += tdp / 1000.0 * 65; // %65 verimlilik varsayımı
                            break;
                        }
                    }
                    catch { totalPower += 65; } // Varsayılan CPU TDP
                    
                    // GPU güç tüketimi
                    try
                    {
                        var gpuInfo = await GetGpuInfoAsync();
                        totalPower += gpuInfo.PowerUsageWatts;
                    }
                    catch { totalPower += 150; } // Varsayılan GPU
                    
                    // RAM (yaklaşık 3-5W per DIMM)
                    try
                    {
                        using var ramSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                        int dimmCount = 0;
                        foreach (ManagementObject obj in ramSearcher.Get())
                        {
                            dimmCount++;
                        }
                        totalPower += dimmCount * 5;
                    }
                    catch { totalPower += 10; }
                    
                    // Disk ve diğer bileşenler (yaklaşık 30W)
                    totalPower += 30;
                    
                    // PSU bilgisi - gerçek veri yoksa N/A göster
                    psuInfo.Name = "Power Supply";
                    psuInfo.Model = "N/A";
                    psuInfo.Wattage = 0;
                    psuInfo.CurrentLoadWatts = 0;
                    psuInfo.LoadPercent = 0;
                    psuInfo.TemperatureCelsius = -1;
                    psuInfo.EfficiencyPercent = 0;
                    psuInfo.Voltage3V = 0;
                    psuInfo.Voltage5V = 0;
                    psuInfo.Voltage12V = 0;
                    
                    // Voltaj değerleri - WMI Voltage Probe'den okumaya çalış
                    try
                    {
                        using var voltageSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_VoltageProbe");
                        var voltages = new List<double>();
                        foreach (ManagementObject obj in voltageSearcher.Get())
                        {
                            var reading = Convert.ToDouble(obj["CurrentReading"] ?? 0);
                            if (reading > 1000 && reading < 15000) // Geçerli voltaj aralığı
                                voltages.Add(reading / 1000.0);
                        }
                        
                        // Voltajları ata (varsa)
                        if (voltages.Count > 0) psuInfo.Voltage12V = voltages[0];
                        if (voltages.Count > 1) psuInfo.Voltage5V = voltages[1];
                        if (voltages.Count > 2) psuInfo.Voltage3V = voltages[2];
                    }
                    catch { }
                    
                    // Sağlık durumu
                    psuInfo.HealthStatus = psuInfo.LoadPercent > 90 ? "Warning" :
                                         psuInfo.LoadPercent > 100 ? "Critical" : "Good";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PSU bilgisi alınamadı: {ex.Message}");
                    psuInfo.Name = "Power Supply";
                    psuInfo.Wattage = 550;
                    psuInfo.HealthStatus = "Unknown";
                }
                return psuInfo;
            });
        }
    }
}