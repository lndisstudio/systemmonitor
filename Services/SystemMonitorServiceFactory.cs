using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SystemMonitor.Services
{
    public static class SystemMonitorServiceFactory
    {
        public static ISystemMonitorService Create()
        {
            // İşletim sistemine göre uygun servisi döndür
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsSystemMonitorService();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxSystemMonitorService();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS için de Linux implementasyonunu kullanabiliriz
                return new LinuxSystemMonitorService();
            }
            
            // Varsayılan olarak Windows servisi
            return new WindowsSystemMonitorService();
        }
    }
}