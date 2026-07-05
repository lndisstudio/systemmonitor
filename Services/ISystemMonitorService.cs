using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SystemMonitor.Services
{
    public interface ISystemMonitorService
    {
        Task<CpuInfo> GetCpuInfoAsync();
        Task<MemoryInfo> GetMemoryInfoAsync();
        Task<DiskInfo> GetDiskInfoAsync();
        Task<GpuInfo> GetGpuInfoAsync();
        Task<MotherboardInfo> GetMotherboardInfoAsync();
        Task<PsuInfo> GetPsuInfoAsync();
    }

    public class CpuInfo
    {
        public string Name { get; set; } = string.Empty;
        public double UsagePercent { get; set; }
        public double TemperatureCelsius { get; set; }
        public int CoreCount { get; set; }
        public int ThreadCount { get; set; }
        public double CurrentFrequencyMHz { get; set; }
        public double MaxFrequencyMHz { get; set; }
        public string HealthStatus { get; set; } = "Good";
    }

    public class MemoryInfo
    {
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public long AvailableBytes { get; set; }
        public double UsagePercent { get; set; }
        public double SpeedMHz { get; set; }
        public string Type { get; set; } = "DDR4";
        public int SlotCount { get; set; }
        public string HealthStatus { get; set; } = "Good";
    }

    public class DiskInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public long FreeBytes { get; set; }
        public double UsagePercent { get; set; }
        public double TemperatureCelsius { get; set; }
        public string HealthStatus { get; set; } = "Good";
        public string FileSystem { get; set; } = "NTFS";
    }

    public class GpuInfo
    {
        public string Name { get; set; } = string.Empty;
        public double UsagePercent { get; set; }
        public double TemperatureCelsius { get; set; }
        public long UsedMemoryBytes { get; set; }
        public long TotalMemoryBytes { get; set; }
        public double MemoryUsagePercent { get; set; }
        public double FanSpeedPercent { get; set; }
        public double PowerUsageWatts { get; set; }
        public string HealthStatus { get; set; } = "Good";
    }

    public class MotherboardInfo
    {
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string BiosVersion { get; set; } = string.Empty;
        public double TemperatureCelsius { get; set; }
        public string HealthStatus { get; set; } = "Good";
    }

    public class PsuInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Wattage { get; set; }
        public double CurrentLoadWatts { get; set; }
        public double LoadPercent { get; set; }
        public double TemperatureCelsius { get; set; }
        public double EfficiencyPercent { get; set; }
        public double Voltage3V { get; set; } = 3.3;
        public double Voltage5V { get; set; } = 5.0;
        public double Voltage12V { get; set; } = 12.0;
        public string HealthStatus { get; set; } = "Good";
    }
}
