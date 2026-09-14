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
        private static string _currentEventTitle = "";
        private static string _cachedStatus = "";
        private static bool _inPrechargeWindow = false;
        private static DateTime _lastLoadedTime = DateTime.MinValue;

        public static bool IsEnabled => _config.Enabled;
        public static string CurrentEventTitle => _currentEventTitle;
        public static bool IsScheduleCharging => _inPrechargeWindow;

        public static void Init()
        {
            LoadConfig();

            _timer = new System.Timers.Timer(30000); // Check every 30 seconds
            _timer.Elapsed += (_, _) => CheckSchedule();
            _timer.AutoReset = true;
            _timer.Start();

            CheckSchedule(force: true);
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
            CheckSchedule(force: true);
        }

        public static void OnPowerStatusChanged(PowerLineStatus status)
        {
            Logger.WriteLine($"[ScheduleManager] Power status changed: {status}. Re-evaluating schedule charging state.");
            CheckSchedule(force: true);
        }

        public static void CheckSchedule(bool force = false)
        {
            try
            {
                // Reload config if file modified recently
                if (File.Exists(ConfigPath) && File.GetLastWriteTime(ConfigPath) > _lastLoadedTime)
                {
                    LoadConfig();
                }

                DateTime now = DateTime.Now;
                var todayEvents = GetTodayEvents(now);
                var sortedEvents = todayEvents.OrderBy(e => e.StartTime).ToList();

                // Find active class (now >= Start && now < End) or next upcoming class (now < Start)
                var currentClass = sortedEvents.FirstOrDefault(e => now >= e.StartTime && now < e.EndTime);
                var nextClass = sortedEvents.FirstOrDefault(e => now < e.StartTime);

                int baseCareLimit = AppConfig.Get("charge_limit", 80);
                if (baseCareLimit < 40 || baseCareLimit > 85) baseCareLimit = 80;

                int targetLimit = baseCareLimit;
                string statusText;

                if (!_config.Enabled)
                {
                    targetLimit = baseCareLimit;
                    statusText = "课表调度: 未启用 (点击 [课表] 导入并开启)";
                    _currentEventTitle = "";
                    _inPrechargeWindow = false;
                }
                else if (currentClass != null)
                {
                    // In class! Strict battery care (80%)
                    targetLimit = baseCareLimit;
                    _currentEventTitle = currentClass.Title;
                    _inPrechargeWindow = false;
                    statusText = $"课表调度: 上课中 - {currentClass.Title} ({baseCareLimit}%保养中)";
                }
                else if (nextClass != null)
                {
                    // Next class today
                    DateTime leaveTime = nextClass.StartTime.AddMinutes(-_config.LeaveBufferMinutes);
                    DateTime prechargeTime = leaveTime.AddMinutes(-_config.PrechargeMinutes);

                    if (now >= prechargeTime && now < leaveTime)
                    {
                        // In pre-charge window! Target 100%
                        targetLimit = 100;
                        _currentEventTitle = nextClass.Title;
                        _inPrechargeWindow = true;
                        statusText = $"课表调度: [充满中] {nextClass.Title} ({nextClass.StartTime:HH:mm}上课, {leaveTime:HH:mm}出门)";
                    }
                    else if (now >= leaveTime && now < nextClass.StartTime)
                    {
                        // Departure buffer! Target 80%
                        targetLimit = baseCareLimit;
                        _currentEventTitle = nextClass.Title;
                        _inPrechargeWindow = false;
                        statusText = $"课表调度: 动身前往 - {nextClass.Title} ({nextClass.StartTime:HH:mm}上课)";
                    }
                    else
                    {
                        // Before pre-charge window! Target 80%
                        targetLimit = baseCareLimit;
                        _currentEventTitle = nextClass.Title;
                        _inPrechargeWindow = false;
                        statusText = $"课表调度: 下一节 {nextClass.Title} ({nextClass.StartTime:HH:mm}上课, {prechargeTime:HH:mm}开始充满)";
                    }
                }
                else
                {
                    // Finished all classes or no classes today! Target 80%
                    targetLimit = baseCareLimit;
                    _currentEventTitle = "";
                    _inPrechargeWindow = false;
                    statusText = sortedEvents.Count > 0
                        ? $"课表调度: 今日已无后续课程 ({baseCareLimit}%保养中)"
                        : $"课表调度: 今日无课程 ({baseCareLimit}%保养中)";
                }

                _cachedStatus = statusText;

                // Level-triggered Ground Truth Enforcement
                int currentEffectiveLimit = BatteryControl.chargeFull ? 100 : AppConfig.Get("charge_limit", 80);
                if (currentEffectiveLimit != targetLimit || force)
                {
                    if (targetLimit == 100)
                    {
                        BatteryControl.SetBatteryLimitFull();
                        Program.toast.RunToast($"[课表充电] 即将上课: {_currentEventTitle}\n已自动切换至 100% 满电准备", ToastIcon.Charger);
                        Logger.WriteLine($"[ScheduleManager] State -> PRECHARGE (100%) for '{_currentEventTitle}'. Target: 100%");
                    }
                    else
                    {
                        BatteryControl.SetBatteryChargeLimit(targetLimit);
                        Logger.WriteLine($"[ScheduleManager] State -> CARE ({targetLimit}%) active. Target: {targetLimit}%");
                    }
                }

                // Push UI refresh to main settings form
                if (Program.settingsForm != null && !Program.settingsForm.IsDisposed && Program.settingsForm.IsHandleCreated)
                {
                    Program.settingsForm.BeginInvoke(Program.settingsForm.VisualiseScheduleStatus);
                    Program.settingsForm.BeginInvoke(Program.settingsForm.VisualiseBatteryTitleCurrent);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleManager] CheckSchedule error: {ex.Message}");
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
            if (string.IsNullOrEmpty(_cachedStatus))
            {
                CheckSchedule();
            }
            return _cachedStatus;
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
            return GetStatusDetailed();
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
            if (!File.Exists(filePath)) return results;

            try
            {
                string[] rawLines = File.ReadAllLines(filePath);
                List<string> lines = new(rawLines.Length);

                // Handle ICS line continuations (starting with space or tab)
                foreach (var rawLine in rawLines)
                {
                    if (string.IsNullOrEmpty(rawLine)) continue;
                    if ((rawLine.StartsWith(' ') || rawLine.StartsWith('\t')) && lines.Count > 0)
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
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleManager] ParseIcsEvents error: {ex.Message}");
            }

            return results;
        }

        private static void ProcessIcsEvent(string summary, string dtStart, string dtEnd, string rrule, DateTime targetDate, List<CalendarEvent> results)
        {
            try
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
                    // Check if recurring on this day of week
                    bool dayMatches = false;

                    // Check BYDAY if specified (e.g. BYDAY=MO,WE)
                    var byDayMatch = Regex.Match(rrule, @"BYDAY=([A-Z,]+)", RegexOptions.IgnoreCase);
                    if (byDayMatch.Success)
                    {
                        string days = byDayMatch.Groups[1].Value.ToUpperInvariant();
                        string targetCode = targetDate.DayOfWeek switch
                        {
                            DayOfWeek.Monday => "MO",
                            DayOfWeek.Tuesday => "TU",
                            DayOfWeek.Wednesday => "WE",
                            DayOfWeek.Thursday => "TH",
                            DayOfWeek.Friday => "FR",
                            DayOfWeek.Saturday => "SA",
                            DayOfWeek.Sunday => "SU",
                            _ => ""
                        };
                        dayMatches = !string.IsNullOrEmpty(targetCode) && days.Contains(targetCode);
                    }
                    else
                    {
                        dayMatches = (start.DayOfWeek == targetDate.DayOfWeek);
                    }

                    if (dayMatches && targetDate.Date >= start.Date)
                    {
                        // Check INTERVAL if bi-weekly
                        var intervalMatch = Regex.Match(rrule, @"INTERVAL=(\d+)", RegexOptions.IgnoreCase);
                        int interval = intervalMatch.Success && int.TryParse(intervalMatch.Groups[1].Value, out int inv) ? inv : 1;
                        int weeksDiff = (int)((targetDate.Date - start.Date).TotalDays / 7);
                        if (interval <= 1 || (weeksDiff % interval == 0))
                        {
                            // Check UNTIL if present
                            var untilMatch = Regex.Match(rrule, @"UNTIL=(\d{8}(?:T\d{6}Z?)?)", RegexOptions.IgnoreCase);
                            if (untilMatch.Success && TryParseIcsDateTime(untilMatch.Groups[1].Value, out DateTime untilDate))
                            {
                                if (targetDate.Date <= untilDate.Date) occursToday = true;
                            }
                            else
                            {
                                occursToday = true;
                            }
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
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleManager] ProcessIcsEvent error: {ex.Message}");
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
