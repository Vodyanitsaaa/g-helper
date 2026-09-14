using GHelper.Battery;
using GHelper.Helpers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GHelper.Schedule
{
    public class WeeklyCourse
    {
        public string Day { get; set; } = "Monday"; // Monday..Sunday or 周一..周日
        public string Start { get; set; } = "08:30"; // HH:mm
        public string End { get; set; } = "10:05";   // HH:mm
        public string Title { get; set; } = "课程";
    }

    public class ScheduleConfig
    {
        public bool Enabled { get; set; } = true;
        public int PrechargeMinutes { get; set; } = 45;      // 出门前提前多少分钟开始充到100%
        public int LeaveBufferMinutes { get; set; } = 20;    // 上课前多少分钟出门
        public string IcsPath { get; set; } = "";            // 可选：指定.ics课表文件绝对路径
        public List<WeeklyCourse> WeeklyEvents { get; set; } = new();
    }

    public class CalendarEvent
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Title { get; set; } = "";
    }

    public static class ScheduleManager
    {
        private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "schedule.json");
        private static readonly string DefaultIcsPath = Path.Combine(ConfigDir, "timetable.ics");

        private static ScheduleConfig _config = new();
        private static System.Timers.Timer? _timer;
        private static bool _isScheduleCharged = false;
        private static string _currentEventTitle = "";
        private static DateTime _lastLoadedTime = DateTime.MinValue;

        public static bool IsEnabled => _config.Enabled;
        public static string CurrentEventTitle => _currentEventTitle;
        public static bool IsScheduleCharging => _isScheduleCharged;

        public static void Init()
        {
            LoadConfig();

            _timer = new System.Timers.Timer(30000); // Check every 30 seconds
            _timer.Elapsed += (_, _) => CheckSchedule();
            _timer.AutoReset = true;
            _timer.Start();

            Logger.WriteLine($"[ScheduleManager] Initialized. Enabled: {_config.Enabled}, Precharge: {_config.PrechargeMinutes}m, LeaveBuffer: {_config.LeaveBufferMinutes}m");
        }

        public static void LoadConfig()
        {
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);

                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var loaded = JsonSerializer.Deserialize<ScheduleConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (loaded != null) _config = loaded;
                }
                else
                {
                    // Create default sample configuration
                    _config = new ScheduleConfig
                    {
                        Enabled = true,
                        PrechargeMinutes = 45,
                        LeaveBufferMinutes = 20,
                        IcsPath = "",
                        WeeklyEvents = new List<WeeklyCourse>
                        {
                            new() { Day = "Monday", Start = "08:30", End = "10:05", Title = "早八课程(示例)" },
                            new() { Day = "Wednesday", Start = "14:00", End = "15:35", Title = "午后课程(示例)" }
                        }
                    };
                    SaveConfig();
                }
                _lastLoadedTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleManager] LoadConfig error: {ex.Message}");
            }
        }

        public static void SaveConfig()
        {
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleManager] SaveConfig error: {ex.Message}");
            }
        }

        public static void OpenConfigFile()
        {
            if (!File.Exists(ConfigPath)) SaveConfig();
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ConfigPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleManager] Failed to open config file: {ex.Message}");
            }
        }

        public static void ToggleEnabled()
        {
            _config.Enabled = !_config.Enabled;
            SaveConfig();
            if (!_config.Enabled && _isScheduleCharged)
            {
                _isScheduleCharged = false;
                BatteryControl.SetBatteryChargeLimit();
            }
        }

        public static void OnPowerStatusChanged(PowerLineStatus status)
        {
            // When disconnected from AC, reset the schedule charge full flag so when user plugs back in later,
            // the limit reverts to normal (80%)
            if (status != PowerLineStatus.Online)
            {
                if (_isScheduleCharged || BatteryControl.chargeFull)
                {
                    Logger.WriteLine("[ScheduleManager] Disconnected from AC power. Resetting chargeFull state to return to 80% on next connection.");
                    BatteryControl.chargeFull = false;
                    _isScheduleCharged = false;
                }
            }
            else
            {
                // Plugged back in: check if we should charge or maintain 80%
                CheckSchedule();
            }
        }

        public static void CheckSchedule()
        {
            if (!_config.Enabled) return;

            // Reload config if file modified recently
            if (File.Exists(ConfigPath) && File.GetLastWriteTime(ConfigPath) > _lastLoadedTime)
            {
                LoadConfig();
            }

            DateTime now = DateTime.Now;
            var todayEvents = GetTodayEvents(now);

            // Find next or active event today where now < EndTime
            var upcoming = todayEvents.Where(e => now < e.EndTime).OrderBy(e => e.StartTime).FirstOrDefault();

            if (upcoming == null)
            {
                if (_isScheduleCharged)
                {
                    _isScheduleCharged = false;
                    _currentEventTitle = "";
                    BatteryControl.SetBatteryChargeLimit();
                    Logger.WriteLine("[ScheduleManager] No more events today. Restored normal charge limit (80%).");
                }
                return;
            }

            DateTime leaveTime = upcoming.StartTime.AddMinutes(-_config.LeaveBufferMinutes);
            DateTime prechargeTime = leaveTime.AddMinutes(-_config.PrechargeMinutes);

            bool inPrechargeWindow = (now >= prechargeTime && now < leaveTime);

            if (inPrechargeWindow)
            {
                bool onAc = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
                if (onAc)
                {
                    if (!BatteryControl.chargeFull)
                    {
                        _isScheduleCharged = true;
                        _currentEventTitle = upcoming.Title;
                        BatteryControl.SetBatteryLimitFull();
                        Program.toast.RunToast($"[课表充电] 即将出门: {upcoming.Title}\n出发时间: {leaveTime:HH:mm}，已切换至100%充满", ToastIcon.Charger);
                        Logger.WriteLine($"[ScheduleManager] In precharge window for '{upcoming.Title}'. Set battery limit to 100%. Leave time: {leaveTime:HH:mm}");
                    }
                }
            }
            else if (_isScheduleCharged && now >= leaveTime)
            {
                // Past leave time
                _isScheduleCharged = false;
                _currentEventTitle = "";
                BatteryControl.SetBatteryChargeLimit();
                Logger.WriteLine($"[ScheduleManager] Reached departure time {leaveTime:HH:mm}. Restored normal battery limit.");
            }
        }

        public static ScheduleConfig GetConfig() => _config;

        public static void SaveConfig(ScheduleConfig config)
        {
            _config = config;
            SaveConfig();
        }

        public static string GetStatusDetailed()
        {
            if (!_config.Enabled) return "课表调度: 未启用 (点击 [📅 课表] 导入并开启)";
            var summary = GetNextEventSummary();
            if (string.IsNullOrEmpty(summary)) return "课表调度: 已开启 (今日无日程，维持保养电量)";
            return $"课表调度: {summary}";
        }

        public static List<string> GetTodayEventsDisplayList()
        {
            var list = new List<string>();
            var events = GetTodayEvents(DateTime.Now).OrderBy(e => e.StartTime).ToList();
            foreach (var ev in events)
            {
                DateTime leaveTime = ev.StartTime.AddMinutes(-_config.LeaveBufferMinutes);
                DateTime prechargeTime = leaveTime.AddMinutes(-_config.PrechargeMinutes);
                list.Add($"{ev.StartTime:HH:mm} - {ev.EndTime:HH:mm} | {ev.Title} ({leaveTime:HH:mm}出发, {prechargeTime:HH:mm}开始充至100%)");
            }
            return list;
        }

        public static string GetNextEventSummary()
        {
            if (!_config.Enabled) return "";

            DateTime now = DateTime.Now;
            var todayEvents = GetTodayEvents(now);
            var upcoming = todayEvents.Where(e => now < e.EndTime).OrderBy(e => e.StartTime).FirstOrDefault();

            if (upcoming == null) return "今日无后续课程";

            DateTime leaveTime = upcoming.StartTime.AddMinutes(-_config.LeaveBufferMinutes);
            if (now < leaveTime.AddMinutes(-_config.PrechargeMinutes))
            {
                return $"{upcoming.Title} ({leaveTime:HH:mm}出门)";
            }
            else if (now < leaveTime)
            {
                return $"[充满中] {upcoming.Title} ({leaveTime:HH:mm}出门)";
            }
            else
            {
                return $"进行中: {upcoming.Title}";
            }
        }

        private static List<CalendarEvent> GetTodayEvents(DateTime today)
        {
            var events = new List<CalendarEvent>();

            // 1. Parse WeeklyEvents from schedule.json
            DayOfWeek currentDayOfWeek = today.DayOfWeek;
            foreach (var we in _config.WeeklyEvents)
            {
                if (MatchesDayOfWeek(we.Day, currentDayOfWeek))
                {
                    if (TimeSpan.TryParse(we.Start, out var st) && TimeSpan.TryParse(we.End, out var et))
                    {
                        events.Add(new CalendarEvent
                        {
                            StartTime = today.Date + st,
                            EndTime = today.Date + et,
                            Title = string.IsNullOrWhiteSpace(we.Title) ? "课程" : we.Title
                        });
                    }
                }
            }

            // 2. Parse ICS file if present
            string icsFile = !string.IsNullOrWhiteSpace(_config.IcsPath) && File.Exists(_config.IcsPath)
                ? _config.IcsPath
                : (File.Exists(DefaultIcsPath) ? DefaultIcsPath : "");

            if (!string.IsNullOrEmpty(icsFile) && File.Exists(icsFile))
            {
                try
                {
                    var icsEvents = ParseIcsEvents(icsFile, today.Date);
                    events.AddRange(icsEvents);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"[ScheduleManager] Error parsing ICS file {icsFile}: {ex.Message}");
                }
            }

            return events;
        }

        private static bool MatchesDayOfWeek(string dayStr, DayOfWeek dow)
        {
            if (string.IsNullOrWhiteSpace(dayStr)) return false;
            string d = dayStr.Trim().ToLowerInvariant();

            return dow switch
            {
                DayOfWeek.Monday => d is "monday" or "mon" or "周一" or "星期一" or "1",
                DayOfWeek.Tuesday => d is "tuesday" or "tue" or "周二" or "星期二" or "2",
                DayOfWeek.Wednesday => d is "wednesday" or "wed" or "周三" or "星期三" or "3",
                DayOfWeek.Thursday => d is "thursday" or "thu" or "周四" or "星期四" or "4",
                DayOfWeek.Friday => d is "friday" or "fri" or "周五" or "星期五" or "5",
                DayOfWeek.Saturday => d is "saturday" or "sat" or "周六" or "星期六" or "6",
                DayOfWeek.Sunday => d is "sunday" or "sun" or "周日" or "星期日" or "天" or "0" or "7",
                _ => false,
            };
        }

        private static List<CalendarEvent> ParseIcsEvents(string filePath, DateTime targetDate)
        {
            var results = new List<CalendarEvent>();
            string[] rawLines = File.ReadAllLines(filePath);
            List<string> lines = new();

            // Handle ICS line continuations (starting with space or tab)
            foreach (var rawLine in rawLines)
            {
                if ((rawLine.StartsWith(" ") || rawLine.StartsWith("\t")) && lines.Count > 0)
                {
                    lines[^1] += rawLine[1..];
                }
                else
                {
                    lines.Add(rawLine.TrimEnd('\r', '\n'));
                }
            }

            bool inEvent = false;
            string summary = "";
            string dtStart = "";
            string dtEnd = "";
            string rrule = "";

            foreach (var line in lines)
            {
                if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    inEvent = true;
                    summary = "";
                    dtStart = "";
                    dtEnd = "";
                    rrule = "";
                }
                else if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (inEvent)
                    {
                        ProcessIcsEvent(summary, dtStart, dtEnd, rrule, targetDate, results);
                        inEvent = false;
                    }
                }
                else if (inEvent)
                {
                    if (line.StartsWith("SUMMARY", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = line.IndexOf(':');
                        if (idx >= 0) summary = line[(idx + 1)..].Trim();
                    }
                    else if (line.StartsWith("DTSTART", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = line.IndexOf(':');
                        if (idx >= 0) dtStart = line[(idx + 1)..].Trim();
                    }
                    else if (line.StartsWith("DTEND", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = line.IndexOf(':');
                        if (idx >= 0) dtEnd = line[(idx + 1)..].Trim();
                    }
                    else if (line.StartsWith("RRULE", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = line.IndexOf(':');
                        if (idx >= 0) rrule = line[(idx + 1)..].Trim();
                    }
                }
            }

            return results;
        }

        private static void ProcessIcsEvent(string summary, string dtStart, string dtEnd, string rrule, DateTime targetDate, List<CalendarEvent> results)
        {
            if (!TryParseIcsDateTime(dtStart, out DateTime start)) return;
            if (!TryParseIcsDateTime(dtEnd, out DateTime end)) end = start.AddMinutes(90);

            bool occursToday = false;

            if (start.Date == targetDate.Date)
            {
                occursToday = true;
            }
            else if (!string.IsNullOrEmpty(rrule) && rrule.Contains("FREQ=WEEKLY", StringComparison.OrdinalIgnoreCase))
            {
                // Weekly recurrence
                if (start.DayOfWeek == targetDate.DayOfWeek && targetDate >= start.Date)
                {
                    // Check UNTIL if present
                    var match = Regex.Match(rrule, @"UNTIL=(\d{8}(?:T\d{6}Z?)?)", RegexOptions.IgnoreCase);
                    if (match.Success && TryParseIcsDateTime(match.Groups[1].Value, out DateTime untilDate))
                    {
                        if (targetDate.Date <= untilDate.Date) occursToday = true;
                    }
                    else
                    {
                        occursToday = true;
                    }
                }
            }

            if (occursToday)
            {
                results.Add(new CalendarEvent
                {
                    StartTime = targetDate.Date + start.TimeOfDay,
                    EndTime = targetDate.Date + end.TimeOfDay,
                    Title = string.IsNullOrWhiteSpace(summary) ? "日程事项" : summary
                });
            }
        }

        private static bool TryParseIcsDateTime(string str, out DateTime dt)
        {
            dt = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(str)) return false;

            // Format: YYYYMMDDTHHMMSS or YYYYMMDDTHHMMSSZ or YYYYMMDD
            str = str.Trim();
            if (str.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) str = str[..^1];

            if (str.Length >= 15 && str.Contains('T'))
            {
                if (DateTime.TryParseExact(str[..15], "yyyyMMdd'T'HHmmss", null, System.Globalization.DateTimeStyles.AssumeLocal, out dt))
                    return true;
            }
            else if (str.Length >= 8)
            {
                if (DateTime.TryParseExact(str[..8], "yyyyMMdd", null, System.Globalization.DateTimeStyles.AssumeLocal, out dt))
                    return true;
            }

            return DateTime.TryParse(str, out dt);
        }
    }
}
