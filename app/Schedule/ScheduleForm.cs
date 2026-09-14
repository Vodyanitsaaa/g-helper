using GHelper.Helpers;
using GHelper.UI;

namespace GHelper.Schedule
{
    public class ScheduleForm : RForm
    {
        private CheckBox checkEnabled = new();
        private NumericUpDown numPrecharge = new();
        private NumericUpDown numLeave = new();
        private Label labelIcsPath = new();
        private ListBox listEvents = new();
        private Label labelStatus = new();

        public ScheduleForm()
        {
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = "智能日程与课表管理 - ROG Flow X13";
            Size = new Size(560, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Padding = new Padding(20);
            Font = new Font("Segoe UI", 9F);

            var panelTop = new Panel { Dock = DockStyle.Top, Height = 45 };
            var labelHeader = new Label
            {
                Text = "📅 课程表/日程感知与自动满电设置",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 5)
            };
            panelTop.Controls.Add(labelHeader);

            checkEnabled.Text = "启用智能日程/课表识别（离电前自动充至100%，出门后恢复80%）";
            checkEnabled.AutoSize = true;
            checkEnabled.Location = new Point(20, 55);
            checkEnabled.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            // Settings group
            var grpTimes = new GroupBox
            {
                Text = "时间策略配置",
                Location = new Point(20, 95),
                Size = new Size(500, 105)
            };

            var lblPre = new Label { Text = "离电前充电时间:", Location = new Point(15, 30), AutoSize = true };
            numPrecharge.Location = new Point(130, 27);
            numPrecharge.Size = new Size(60, 25);
            numPrecharge.Minimum = 15;
            numPrecharge.Maximum = 180;
            var lblPreUnit = new Label { Text = "分钟开始充满至100%", Location = new Point(195, 30), AutoSize = true };

            var lblLeave = new Label { Text = "上课前出门提前:", Location = new Point(15, 65), AutoSize = true };
            numLeave.Location = new Point(130, 62);
            numLeave.Size = new Size(60, 25);
            numLeave.Minimum = 5;
            numLeave.Maximum = 60;
            var lblLeaveUnit = new Label { Text = "分钟动身前往教室", Location = new Point(195, 65), AutoSize = true };

            grpTimes.Controls.AddRange(new Control[] { lblPre, numPrecharge, lblPreUnit, lblLeave, numLeave, lblLeaveUnit });

            // File group
            var grpFile = new GroupBox
            {
                Text = "课程表数据源（支持 ICS 日历或 JSON 周排课）",
                Location = new Point(20, 210),
                Size = new Size(500, 105)
            };

            var btnIcs = new RButton
            {
                Text = "📂 选择 .ics 课表文件...",
                Location = new Point(15, 25),
                Size = new Size(180, 32),
                Secondary = true
            };
            btnIcs.Click += BtnIcs_Click;

            var btnJson = new RButton
            {
                Text = "📝 编辑 schedule.json (周排课)",
                Location = new Point(210, 25),
                Size = new Size(220, 32),
                Secondary = true
            };
            btnJson.Click += (_, _) => ScheduleManager.OpenConfigFile();

            labelIcsPath.Text = "未选择外部 .ics 文件（正在使用 schedule.json 默认排课）";
            labelIcsPath.Location = new Point(15, 68);
            labelIcsPath.Size = new Size(470, 25);
            labelIcsPath.ForeColor = SystemColors.ControlDark;

            grpFile.Controls.AddRange(new Control[] { btnIcs, btnJson, labelIcsPath });

            // Today events preview
            var lblEvents = new Label
            {
                Text = "今日已解析到的课程/日程预览：",
                Location = new Point(20, 325),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            listEvents.Location = new Point(20, 350);
            listEvents.Size = new Size(500, 95);

            labelStatus.Location = new Point(20, 455);
            labelStatus.Size = new Size(500, 25);
            labelStatus.ForeColor = Color.FromArgb(0, 150, 136);

            var btnSave = new RButton
            {
                Text = "保存并应用",
                Location = new Point(290, 485),
                Size = new Size(110, 36)
            };
            btnSave.Click += BtnSave_Click;

            var btnClose = new RButton
            {
                Text = "关闭",
                Location = new Point(410, 485),
                Size = new Size(110, 36),
                Secondary = true
            };
            btnClose.Click += (_, _) => Close();

            Controls.AddRange(new Control[]
            {
                panelTop, checkEnabled, grpTimes, grpFile, lblEvents, listEvents, labelStatus, btnSave, btnClose
            });
        }

        private void LoadData()
        {
            var cfg = ScheduleManager.GetConfig();
            checkEnabled.Checked = cfg.Enabled;
            numPrecharge.Value = Math.Max(15, Math.Min(180, cfg.PrechargeMinutes));
            numLeave.Value = Math.Max(5, Math.Min(60, cfg.LeaveBufferMinutes));

            if (!string.IsNullOrWhiteSpace(cfg.IcsPath) && File.Exists(cfg.IcsPath))
            {
                labelIcsPath.Text = "当前 ICS: " + cfg.IcsPath;
                labelIcsPath.ForeColor = SystemColors.ControlText;
            }
            else
            {
                labelIcsPath.Text = "当前使用: schedule.json 周排课";
            }

            RefreshEventsList();
        }

        private void RefreshEventsList()
        {
            listEvents.Items.Clear();
            var list = ScheduleManager.GetTodayEventsDisplayList();
            if (list.Count == 0)
            {
                listEvents.Items.Add("今日暂无待上课程（维持 80% 电池保养）");
            }
            else
            {
                foreach (var item in list)
                {
                    listEvents.Items.Add(item);
                }
            }
            labelStatus.Text = ScheduleManager.GetStatusDetailed();
        }

        private void BtnIcs_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "选择课程表/日历 .ics 文件",
                Filter = "iCalendar 日历文件 (*.ics)|*.ics|所有文件 (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var cfg = ScheduleManager.GetConfig();
                cfg.IcsPath = ofd.FileName;
                ScheduleManager.SaveConfig(cfg);
                labelIcsPath.Text = "当前 ICS: " + ofd.FileName;
                labelIcsPath.ForeColor = SystemColors.ControlText;
                RefreshEventsList();
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var cfg = ScheduleManager.GetConfig();
            cfg.Enabled = checkEnabled.Checked;
            cfg.PrechargeMinutes = (int)numPrecharge.Value;
            cfg.LeaveBufferMinutes = (int)numLeave.Value;
            ScheduleManager.SaveConfig(cfg);
            ScheduleManager.CheckSchedule();
            RefreshEventsList();
            Program.settingsForm.VisualiseBatteryTitleCurrent();
            Program.toast.RunToast("课程表与智能充电设置已更新！", ToastIcon.Charger);
            Close();
        }
    }
}
