using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SystemMonitor.Services
{
    public class LinuxSystemMonitorService : ISystemMonitorService
    {
        public async Task<CpuInfo> GetCpuInfoAsync()
        {
            return await Task.Run(() =>
            {
                var cpuInfo = new CpuInfo();
                try
                {
                    // CPU kullanımı - /proc/stat
                    var statLines = File.ReadAllLines("/proc/stat");
                    var cpuLine = statLines.FirstOrDefault(l => l.StartsWith("cpu "));
                    if (cpuLine != null)
                    {
                        var parts = cpuLine.Split(' ');
                        var values = parts.Where(p => !string.IsNullOrWhiteSpace(p)).Skip(1).Select(long.Parse).ToArray();
                        
                        // Önceki okuma ile fark hesapla (basit versiyon)
                        if (values.Length >= 4)
                        {
                            var idle = values[3];
                            var total = values.Sum();
                            // İlk okuma olduğu için varsayılan değer
                            cpuInfo.UsagePercent = 10.0; // Başlangıç değeri
                        }
                    }
                    
                    // CPU detayları - /proc/cpuinfo
                    var cpuInfoLines = File.ReadAllLines("/proc/cpuinfo");
                    foreach (var line in cpuInfoLines)
                    {
                        if (line.StartsWith("model name"))
                            cpuInfo.Name = line.Split(':')[1].Trim();
                        else if (line.StartsWith("cpu cores"))
                            cpuInfo.CoreCount = int.Parse(line.Split(':')[1].Trim());
                        else if (line.StartsWith("siblings"))
                            cpuInfo.ThreadCount = int.Parse(line.Split(':')[1].Trim());
                        else if (line.StartsWith("cpu MHz"))
                            cpuInfo.CurrentFrequencyMHz = double.Parse(line.Split(':')[1].Trim());
                    }
                    
                    // Maksimum frekans - /sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq
                    try
                    {
                        var maxFreq = File.ReadAllText("/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq");
                        cpuInfo.MaxFrequencyMHz = double.Parse(maxFreq.Trim()) / 1000.0;
                    }
                    catch { }
                    
                    // CPU sıcaklığı - /sys/class/thermal
                    cpuInfo.TemperatureCelsius = GetCpuTemperature();
                    
                    cpuInfo.HealthStatus = cpuInfo.TemperatureCelsius > 85 ? "Warning" : 
                                     cpuInfo.TemperatureCelsius > 100 ? "Critical" : "Good";
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
                // Thermal zone'ları dene
                var thermalZones = Directory.GetFiles("/sys/class/thermal", "thermal_zone*/temp");
                foreach (var zone in thermalZones)
                {
                    try
                    {
                        var tempStr = File.ReadAllText(zone).Trim();
                        var temp = double.Parse(tempStr) / 1000.0;
                        if (temp > 20 && temp < 120) // Mantıklı sıcaklık aralığı
                            return temp;
                    }
                    catch { }
                }
            }
            catch { }
            return -1;
        }

        public async Task<MemoryInfo> GetMemoryInfoAsync()
        {
            return await Task.Run(() =>
            {
                var memInfo = new MemoryInfo();
                try
                {
                    // RAM bilgileri - /proc/meminfo
                    var memInfoLines = File.ReadAllLines("/proc/meminfo");
                    var memDict = new Dictionary<string, long>();
                    
                    foreach (var line in memInfoLines)
                    {
                        var parts = line.Split(':');
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var valueStr = parts[1].Trim().Split(' ')[0];
                            if (long.TryParse(valueStr, out var value))
                                memDict[key] = value * 1024; // KB to bytes
                        }
                    }
                    
                    memInfo.TotalBytes = memDict.GetValueOrDefault("MemTotal", 0);
                    memInfo.AvailableBytes = memDict.GetValueOrDefault("MemAvailable", 
                        memDict.GetValueOrDefault("MemFree", 0) + memDict.GetValueOrDefault("Buffers", 0) + 
                        memDict.GetValueOrDefault("Cached", 0));
                    memInfo.UsedBytes = memInfo.TotalBytes - memInfo.AvailableBytes;
                    memInfo.UsagePercent = memInfo.TotalBytes > 0 ? 
                        (double)memInfo.UsedBytes / memInfo.TotalBytes * 100 : 0;
                    
                    // RAM tipi ve hızı - dmidecode
                    try
                    {
                        var proc = Process.Start(new ProcessStartInfo
                        {
                            FileName = "dmidecode",
                            Arguments = "-t memory",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        });
                        
                        if (proc != null)
                        {
                            var output = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit(3000);
                            
                            // Çıktıyı parse et
                            var lines = output.Split('\n');
                            int slotCount = 0;
                            foreach (var line in lines)
                            {
                                if (line.Contains("Size:") && !line.Contains("No Module Installed"))
                                    slotCount++;
                                else if (line.Contains("Type:"))
                                    memInfo.Type = line.Split(':')[1].Trim();
                                else if (line.Contains("Speed:"))
                                {
                                    var speedStr = line.Split(':')[1].Trim().Split(' ')[0];
                                    if (double.TryParse(speedStr, out var speed))
                                        memInfo.SpeedMHz = speed;
                                }
                            }
                            memInfo.SlotCount = slotCount;
                        }
                    }
                    catch { }
                    
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
                    // Disk kullanımı - df komutu
                    var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "df",
                        Arguments = "-h /",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(3000);
                        
                        var lines = output.Split('\n');
                        if (lines.Length > 1)
                        {
                            var parts = lines[1].Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 5)
                            {
                                diskInfo.Name = parts[5]; // Mount point
                                diskInfo.FileSystem = parts[0]; // File system
                                
                                // Boyutları parse et
                                var totalStr = parts[1].TrimEnd('G', 'M', 'K');
                                var usedStr = parts[2].TrimEnd('G', 'M', 'K');
                                var availStr = parts[3].TrimEnd('G', 'M', 'K');
                                
                                if (double.TryParse(totalStr, out var total))
                                    diskInfo.TotalBytes = (long)(total * 1024 * 1024 * 1024);
                                if (double.TryParse(usedStr, out var used))
                                    diskInfo.UsedBytes = (long)(used * 1024 * 1024 * 1024);
                                if (double.TryParse(availStr, out var avail))
                                    diskInfo.FreeBytes = (long)(avail * 1024 * 1024 * 1024);
                                
                                diskInfo.UsagePercent = double.TryParse(parts[4].TrimEnd('%'), out var usage) ? usage : 0;
                            }
                        }
                    }
                    
                    // Disk modeli - lsblk veya hdparm
                    try
                    {
                        var lsblkProc = Process.Start(new ProcessStartInfo
                        {
                            FileName = "lsblk",
                            Arguments = "-d -o name,model",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        });
                        
                        if (lsblkProc != null)
                        {
                            var output = lsblkProc.StandardOutput.ReadToEnd();
                            lsblkProc.WaitForExit(3000);
                            
                            var lines = output.Split('\n');
                            if (lines.Length > 1)
                            {
                                var parts = lines[1].Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 1)
                                    diskInfo.Model = string.Join(" ", parts.Skip(1));
                            }
                        }
                    }
                    catch { }
                    
                    // Disk sıcaklığı - hddtemp veya smartctl
                    diskInfo.TemperatureCelsius = GetDiskTemperature();
                    
                    diskInfo.HealthStatus = diskInfo.UsagePercent > 90 ? "Warning" : "Good";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Disk bilgisi alınamadı: {ex.Message}");
                }
                return diskInfo;
            });
        }

        private double GetDiskTemperature()
        {
            try
            {
                // hddtemp dene
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "hddtemp",
                    Arguments = "/dev/sda",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);
                    
                    // Çıktı: /dev/sda: ST1000DM003-1CH162: 34°C
                    var parts = output.Split(':');
                    if (parts.Length >= 3)
                    {
                        var tempStr = parts[2].Trim().TrimEnd('°', 'C');
                        if (double.TryParse(tempStr, out var temp))
                            return temp;
                    }
                }
            }
            catch { }
            return -1;
        }

        public async Task<GpuInfo> GetGpuInfoAsync()
        {
            return await Task.Run(() =>
            {
                var gpuInfo = new GpuInfo();
                try
                {
                    // NVIDIA GPU - nvidia-smi
                    var nvidiaProc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        Arguments = "--query-gpu=name,utilization.gpu,temperature.gpu,memory.used,memory.total,fan.speed,power.draw --format=csv,noheader,nounits",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    
                    if (nvidiaProc != null)
                    {
                        var output = nvidiaProc.StandardOutput.ReadToEnd();
                        nvidiaProc.WaitForExit(3000);
                        
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
                                    gpuInfo.PowerUsageWatts = double.TryParse(parts[6].Trim().Replace("W",""), out var p) ? p : 0;
                                
                                gpuInfo.MemoryUsagePercent = gpuInfo.TotalMemoryBytes > 0 ? 
                                    (double)gpuInfo.UsedMemoryBytes / gpuInfo.TotalMemoryBytes * 100 : 0;
                                
                                gpuInfo.HealthStatus = gpuInfo.TemperatureCelsius > 85 ? "Warning" : 
                                                    gpuInfo.TemperatureCelsius > 100 ? "Critical" : "Good";
                                return gpuInfo;
                            }
                        }
                    }
                    
                    // AMD GPU - rocm-smi
                    var amdProc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "rocm-smi",
                        Arguments = "--showuse --showtemp --showmem --showpower",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    
                    if (amdProc != null)
                    {
                        var output = amdProc.StandardOutput.ReadToEnd();
                        amdProc.WaitForExit(3000);
                        
                        // Çıktıyı parse et (AMD formatı farklı)
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            gpuInfo.Name = "AMD GPU";
                            // Parse işlemi...
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"GPU bilgisi alınamadı: {ex.Message}");
                }
                
                // Generic GPU bilgisi - lspci
                try
                {
                    var lspciProc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "lspci",
                        Arguments = "-nn | grep -i vga",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    
                    if (lspciProc != null)
                    {
                        var output = lspciProc.StandardOutput.ReadToEnd();
                        lspciProc.WaitForExit(3000);
                        
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            gpuInfo.Name = output.Trim();
                        }
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
                    // Anakart bilgisi - dmidecode
                    var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "dmidecode",
                        Arguments = "-t baseboard",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(3000);
                        
                        var lines = output.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains("Manufacturer:"))
                                mbInfo.Manufacturer = line.Split(':')[1].Trim();
                            else if (line.Contains("Product Name:"))
                                mbInfo.Model = line.Split(':')[1].Trim();
                            else if (line.Contains("Version:"))
                                mbInfo.BiosVersion = line.Split(':')[1].Trim();
                        }
                    }
                    
                    // BIOS sıcaklığı
                    mbInfo.TemperatureCelsius = GetCpuTemperature(); // CPU sıcaklığını kullan
                    
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
            return await Task.Run(() =>
            {
                var psuInfo = new PsuInfo();
                try
                {
                    // Linux'ta PSU bilgisi genellikle doğrudan okunamaz
                    // /sys/class/hwmon veya ipmi kullanılabilir
                    
                    psuInfo.Name = "Power Supply";
                    psuInfo.Wattage = 550; // Varsayılan değer
                    
                    // Toplam sistem gücünü hesapla (yaklaşık)
                    try
                    {
                        // /sys/class/hwmon'dan Voltages okunabilir
                        var hwmonDirs = Directory.GetDirectories("/sys/class/hwmon/");
                        foreach (var dir in hwmonDirs)
                        {
                            var nameFile = Path.Combine(dir, "name");
                            if (File.Exists(nameFile))
                            {
                                var name = File.ReadAllText(nameFile).Trim();
                                if (name.Contains("psu") || name.Contains("power"))
                                {
                                    psuInfo.Name = name;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                    
                    // Yük - sistem güç tüketiminden hesaplanabilir
                    psuInfo.LoadPercent = 45; // Varsayılan
                    psuInfo.CurrentLoadWatts = psuInfo.Wattage * (psuInfo.LoadPercent / 100.0);
                    
                    psuInfo.TemperatureCelsius = -1;
                    psuInfo.EfficiencyPercent = 85.0;
                    
                    psuInfo.HealthStatus = psuInfo.LoadPercent > 90 ? "Warning" : "Good";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PSU bilgisi alınamadı: {ex.Message}");
                    psuInfo.Name = "Power Supply";
                    psuInfo.HealthStatus = "Unknown";
                }
                return psuInfo;
            });
        }
    }
}