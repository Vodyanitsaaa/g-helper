using System.Diagnostics;
using System.Text;
using HidSharp;
using HidSharp.Reports;

namespace GHelperWatcher
{
    static class Program
    {
        private const string MutexName = "Global\\GHelperWatcher_SingleInstance_Mutex";
        private const string TaskName = "GHelperWatcher";
        private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper");
        private static readonly string LogFile = Path.Combine(LogDir, "watcher.log");

        private static readonly CancellationTokenSource _cts = new();

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                string cmd = args[0].ToLowerInvariant().TrimStart('-', '/');
                if (cmd is "install" or "i")
                {
                    InstallTask();
                    return;
                }
                if (cmd is "uninstall" or "u")
                {
                    UninstallTask();
                    return;
                }
                if (cmd is "status" or "s")
                {
                    ShowStatus();
                    return;
                }
            }

            // Ensure single instance
            using var mutex = new Mutex(true, MutexName, out bool isNew);
            if (!isNew)
            {
                return;
            }

            Log("=== GHelperWatcher started ===");

            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log($"Unhandled: {e.ExceptionObject}");
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                _cts.Cancel();
                Log("=== GHelperWatcher exiting ===");
            };

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    // Check if GHelper is already running
                    if (Process.GetProcessesByName("GHelper").Length > 0)
                    {
                        Log("GHelper is running. Pausing HID listener...");
                        while (Process.GetProcessesByName("GHelper").Length > 0 && !_cts.IsCancellationRequested)
                        {
                            Thread.Sleep(1000);
                        }
                        Log("GHelper exited. Waiting 1.5s before taking over HID...");
                        Thread.Sleep(1500);
                        continue;
                    }

                    // GHelper is NOT running. Open ASUS HID device.
                    var dev = FindInputDevice();
                    if (dev == null)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    HidStream? stream = null;
                    try
                    {
                        stream = dev.Open();
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to open HID device: {ex.Message}");
                        Thread.Sleep(2000);
                        continue;
                    }

                    using (stream)
                    {
                        SendHandshake(stream, dev);
                        Log($"Listening for M4 key on {dev.DevicePath}");

                        while (!_cts.IsCancellationRequested)
                        {
                            // If GHelper launched by user elsewhere, yield immediately
                            if (Process.GetProcessesByName("GHelper").Length > 0)
                            {
                                Log("GHelper detected. Releasing HID stream.");
                                break;
                            }

                            byte[] data;
                            try
                            {
                                stream.ReadTimeout = 2000;
                                data = stream.Read();
                            }
                            catch (TimeoutException)
                            {
                                continue;
                            }
                            catch (Exception ex)
                            {
                                Log($"Stream read error: {ex.Message}");
                                break;
                            }

                            if (data.Length > 1 && data[0] == 0x5a && data[1] == 56) // Key 56 = M4 / ROG
                            {
                                Log(">>> M4 key pressed! Releasing HID and launching GHelper...");

                                // Dispose stream first so GHelper can acquire it cleanly
                                stream.Dispose();

                                string ghelperPath = GetGHelperPath();
                                if (File.Exists(ghelperPath))
                                {
                                    try
                                    {
                                        Process.Start(new ProcessStartInfo(ghelperPath) { UseShellExecute = true });
                                        Log($"Successfully launched: {ghelperPath}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log($"Failed to launch GHelper: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Log($"GHelper.exe not found at: {ghelperPath}");
                                }

                                Thread.Sleep(3000);
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Watcher loop exception: {ex.Message}");
                    Thread.Sleep(2000);
                }
            }
        }

        static HidDevice? FindInputDevice()
        {
            try
            {
                var allDevices = DeviceList.Local.GetHidDevices(0x0b05);
                return allDevices.FirstOrDefault(d =>
                {
                    try
                    {
                        return d.CanOpen && d.GetReportDescriptor().TryGetReport(ReportType.Feature, 0x5a, out _);
                    }
                    catch { return false; }
                });
            }
            catch (Exception ex)
            {
                Log($"FindInputDevice error: {ex.Message}");
                return null;
            }
        }

        static void SendHandshake(HidStream stream, HidDevice dev)
        {
            try
            {
                byte[] payload = new byte[dev.GetMaxFeatureReportLength()];
                payload[0] = 0x5a;
                byte[] ascii = Encoding.ASCII.GetBytes("ASUS Tech.Inc.");
                Array.Copy(ascii, 0, payload, 1, ascii.Length);
                stream.SetFeature(payload);
            }
            catch (Exception ex)
            {
                Log($"SendHandshake error: {ex.Message}");
            }
        }

        static string GetGHelperPath()
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GHelper.exe");
            if (File.Exists(localPath)) return localPath;

            const string deployedPath = @"D:\softwares\G-Helper\GHelper.exe";
            if (File.Exists(deployedPath)) return deployedPath;

            return localPath;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        static void InstallTask()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName
                             ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GHelperWatcher.exe");

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.SetValue(TaskName, $"\"{exePath}\"");
            }
            catch (Exception ex)
            {
                Log("Registry install error: " + ex.Message);
            }

            RunCommand($"schtasks /create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /rl highest /f");

            if (Process.GetProcessesByName("GHelperWatcher").Length <= 1)
            {
                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            }

            MessageBox(IntPtr.Zero, $"GHelperWatcher 已成功设置为自启！\n\n执行程序: {exePath}",
                       "安装成功", 0x40);
        }

        static void UninstallTask()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue(TaskName, false);
            }
            catch { }

            RunCommand($"schtasks /delete /tn \"{TaskName}\" /f");

            foreach (var p in Process.GetProcessesByName("GHelperWatcher"))
            {
                try { if (p.Id != Process.GetCurrentProcess().Id) p.Kill(); } catch { }
            }

            MessageBox(IntPtr.Zero, "GHelperWatcher 自启已移除，并已停止运行。",
                       "卸载成功", 0x40);
        }

        static void ShowStatus()
        {
            bool isRunning = Process.GetProcessesByName("GHelperWatcher").Length > 0;
            MessageBox(IntPtr.Zero, $"GHelperWatcher 运行状态: {(isRunning ? "正在运行中" : "未在运行")}",
                       "状态查询", 0x40);
        }

        static void RunCommand(string command)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });
                p?.WaitForExit();
            }
            catch { }
        }

        static void Log(string msg)
        {
            try
            {
                if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);

                // Prune log if too big (> 100KB)
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 100 * 1024)
                {
                    try { File.Delete(LogFile); } catch { }
                }

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}{Environment.NewLine}";
                File.AppendAllText(LogFile, line);
            }
            catch { }
        }
    }
}
