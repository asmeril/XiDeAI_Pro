using System;
using System.Threading;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    public class SchedulerService : IDisposable
    {
        private System.Threading.Timer? _dailyReportTimer;
        private System.Threading.Timer? _weeklyReportTimer;
        private System.Threading.Timer? _midnightResetTimer;
        private System.Threading.Timer? _morningTimer;
        private System.Threading.Timer? _closeTimer;
        private System.Threading.Timer? _infraTimer;
        private System.Threading.Timer? _guruCheckTimer;

        public event Action<string, string>? OnLog;
        public event Func<Task>? OnDailyReportTime;      // 18:30
        public event Func<Task>? OnWeeklyReportTime;     // Cuma 18:30
        public event Func<Task>? OnMorningMotivation;    // 08:00
        public event Func<Task>? OnMarketClose;          // 18:05
        public event Func<Task>? OnInfrastructureCheck;  // 02:00 (Nightly)
        public event Action? OnMidnightReset;            // 00:00
        public event Func<Task>? OnGuruCheckTime;        // Recurring (e.g. Every 3h)

        public void Start()
        {
            ScheduleDailyReport();
            ScheduleWeeklyReport();
            ScheduleMidnightReset();
            ScheduleMorningMotivation();
            ScheduleMarketClose();
            ScheduleInfrastructureCheck();
            ScheduleGuruCheck();
        }

        private void ScheduleInfrastructureCheck(bool skipToday = false)
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0);
            
            // v2.1: 30 dk tolerans
            if (skipToday || now > target.AddMinutes(30)) target = target.AddDays(1);
            else if (now > target) target = now.AddSeconds(1);

            var delay = target - now;
            OnLog?.Invoke($"⏰ Infra Check scheduled for {target:HH:mm:ss} (In {delay.TotalHours:N1}h). Reasons: skipToday={skipToday}, now={now:HH:mm}", "System");

            _infraTimer?.Dispose();
            _infraTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    if (OnInfrastructureCheck != null) await OnInfrastructureCheck.Invoke();
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ Infra Check Error: {ex.Message}", "System");
                }
                finally
                {
                    ScheduleInfrastructureCheck(true);
                }
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        private void ScheduleDailyReport(bool skipToday = false)
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, 18, 30, 0);
            if (skipToday || now > target.AddMinutes(15)) target = target.AddDays(1);
            else if (now > target) target = now.AddSeconds(1);

            var delay = target - now;
            OnLog?.Invoke($"⏰ Daily Report scheduled for {target:HH:mm:ss} (In {delay.TotalHours:N1}h)", "System");

            _dailyReportTimer?.Dispose();
            _dailyReportTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    if (OnDailyReportTime != null) await OnDailyReportTime.Invoke();
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ Daily Report Error: {ex.Message}", "System");
                }
                finally
                {
                    ScheduleDailyReport(true);
                }
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        private void ScheduleWeeklyReport(bool skipToday = false)
        {
            var now = DateTime.Now;
            var daysUntilFriday = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
            
            if (skipToday) 
            {
                if (daysUntilFriday == 0) daysUntilFriday = 7;
            }
            else if (daysUntilFriday == 0) // Bugün Cuma
            {
                if (now.TimeOfDay > new TimeSpan(18, 45, 0)) daysUntilFriday = 7;
                // Değilse delay 0 olacak (hemen çalışacak)
            }

            var target = new DateTime(now.Year, now.Month, now.Day, 18, 30, 0).AddDays(daysUntilFriday);
            
            // Eğer tolerans payındaysak (Cuma 18:30-18:45) hedefi şimdiye çek
            if (!skipToday && daysUntilFriday == 0 && now > target && now <= target.AddMinutes(15))
                target = now.AddSeconds(1);

            var delay = target - now;
            OnLog?.Invoke($"⏰ Weekly Report scheduled for {target:dd.MM HH:mm} (In {delay.TotalDays:N1} days)", "System");

            _weeklyReportTimer?.Dispose();
            _weeklyReportTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    if (OnWeeklyReportTime != null) await OnWeeklyReportTime.Invoke();
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ Weekly Report Error: {ex.Message}", "System");
                }
                finally
                {
                    ScheduleWeeklyReport(true);
                }
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        private void ScheduleMidnightReset()
        {
            var now = DateTime.Now;
            var midnight = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0).AddDays(1);
            var delay = midnight - now;
            OnLog?.Invoke($"⏰ Midnight Reset scheduled for tomorrow (In {delay.TotalHours:N1}h)", "System");

            _midnightResetTimer?.Dispose();
            _midnightResetTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    OnMidnightReset?.Invoke();
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ Midnight Reset Error: {ex.Message}", "System");
                }
                finally
                {
                    ScheduleMidnightReset();
                }
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        private void ScheduleMorningMotivation(bool skipToday = false)
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, 8, 0, 0);
            
            if (skipToday || now > target.AddMinutes(30)) target = target.AddDays(1);
            else if (now > target) target = now.AddSeconds(1);

            var delay = target - now;
            OnLog?.Invoke($"⏰ Morning Motivation scheduled for {target:HH:mm:ss} (In {delay.TotalHours:N1}h). Reasons: skipToday={skipToday}, now={now:HH:mm}", "System");

            _morningTimer?.Dispose();
            _morningTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    if (OnMorningMotivation != null) await OnMorningMotivation.Invoke();
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ Morning Motivation Error: {ex.Message}", "System");
                }
                finally
                {
                    ScheduleMorningMotivation(true);
                }
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        private void ScheduleGuruCheck()
        {
            var now = DateTime.Now;
            // Every 3 hours, but first check in 1 minute
            var delay = TimeSpan.FromHours(3);
            var firstDelay = TimeSpan.FromMinutes(1);
            
            OnLog?.Invoke($"⏰ Guru Check scheduled for recurring 3h cycle (First in 1m).", "System");

            _guruCheckTimer?.Dispose();
            _guruCheckTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    if (OnGuruCheckTime != null) await OnGuruCheckTime.Invoke();
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ Guru Check Error: {ex.Message}", "System");
                }
            }, null, firstDelay, delay); // First in 1m, then every 3h
        }

        private void ScheduleMarketClose(bool skipToday = false)
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, 18, 10, 0); // BIST Close + 10m
        
            if (skipToday || now > target.AddMinutes(10)) 
                target = target.AddDays(1);
            else if (now > target)
                target = now.AddSeconds(1); // Hemen çalıştır

            var delay = target - now;
            OnLog?.Invoke($"⏰ Market Close summary scheduled for {target:HH:mm:ss} (In {delay.TotalHours:N1}h)", "System");

            _closeTimer?.Dispose();
            _closeTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    if (OnMarketClose != null) await OnMarketClose.Invoke();
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ Market Close Error: {ex.Message}", "System");
                }
                finally
                {
                    ScheduleMarketClose(true);
                }
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            _dailyReportTimer?.Dispose();
            _weeklyReportTimer?.Dispose();
            _midnightResetTimer?.Dispose();
            _morningTimer?.Dispose();
            _closeTimer?.Dispose();
            _infraTimer?.Dispose();
            _guruCheckTimer?.Dispose();
        }
    }
}

