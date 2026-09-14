using GHelper.Helpers;
using GHelper.UI;

namespace GHelper.Schedule
{
    public class ScheduleForm : RForm
    {
        private CheckBox checkEnabled = new();
        private NumericUpDownWithUnit numPrecharge = new();
        private NumericUpDownWithUnit numLeave = new();
        private Label labelIcsPath = new();
        private ListBox listEvents = new();
        private Label labelStatus = new();

        public ScheduleForm()
        {
            InitializeComponent();
            LoadData();
            InitTheme(true);
        }

        private void InitializeComponent()
        {
            Text = "课表与智能充电设置 - ROG Flow X13";
            ClientSize = new Size(540, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);

            // Header Section
            var panelTop = new Panel
            {
                Location = new Point(20, 16),
                Size = new Size(500, 48),
                BackColor = Color.Transparent
            };

            var labelHeader = new Label
            {
                Text = "课程表感知与智能充电策略",
                Font = new Font("Segoe UI", 11.25F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            var labelSubtitle = new Label
            {
                Text = "上课动身前自动充至 100%，离电出发后自动切回 80% 保养上限",
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = SystemColors.GrayText,
                AutoSize = true,
                Location = new Point(0, 24)
            };

            panelTop.Controls.Add(labelHeader);
            panelTop.Controls.Add(labelSubtitle);

            // Enable Switch
            checkEnabled.Text = " 启用智能课表感知与满电调度";
            checkEnabled.Font = new Font("Segoe UI", 9.25F, FontStyle.Bold);
            checkEnabled.Location = new Point(20, 72);
            checkEnabled.Size = new Size(500, 28);
            checkEnabled.UseVisualStyleBackColor = true;

            // Timing Section
            var panelTimes = new Panel
            {
                Location = new Point(20, 108),
                Size = new Size(500, 106),
                BackColor = Color.Transparent
            };

            var labelTimesTitle = new Label
            {
                Text = "时间策略配置",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, 0),
                AutoSize = true
            };

            var lblPre = new Label
            {
                Text = "离电前充电时间:",
                Location = new Point(12, 32),
                AutoSize = true
            };

            numPrecharge.Location = new Point(124, 27);
            numPrecharge.Size = new Size(115, 32);
            numPrecharge.Minimum = 15;
            numPrecharge.Maximum = 180;
            numPrecharge.Unit = " 分钟";

            var lblPreUnit = new Label
            {
                Text = "提前开始充满至 100%",
                Location = new Point(248, 32),
                ForeColor = SystemColors.GrayText,
                AutoSize = true
            };

            var lblLeave = new Label
            {
                Text = "上课前出门提前:",
                Location = new Point(12, 70),
                AutoSize = true
            };

            numLeave.Location = new Point(124, 65);
            numLeave.Size = new Size(115, 32);
            numLeave.Minimum = 5;
            numLeave.Maximum = 90;
            numLeave.Unit = " 分钟";

            var lblLeaveUnit = new Label
            {
                Text = "预感动身前往教室时间",
                Location = new Point(248, 70),
                ForeColor = SystemColors.GrayText,
                AutoSize = true
            };

            panelTimes.Controls.AddRange(new Control[]
            {
                labelTimesTitle, lblPre, numPrecharge, lblPreUnit, lblLeave, numLeave, lblLeaveUnit
            });

            // Data Source Section
            var panelFile = new Panel
            {
                Location = new Point(20, 222),
                Size = new Size(500, 100),
                BackColor = Color.Transparent
            };

            var labelFileTitle = new Label
            {
                Text = "课程表数据源（支持 ICS 日历或 JSON 周排课）",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, 0),
                AutoSize = true
            };

            var btnIcs = new RButton
            {
                Text = "选择 .ics 课表文件...",
                Location = new Point(8, 26),
                Size = new Size(175, 34),
                Secondary = true
            };
            btnIcs.Click += BtnIcs_Click;

            var btnJson = new RButton
            {
                Text = "编辑 schedule.json (周排课)",
                Location = new Point(195, 26),
                Size = new Size(210, 34),
                Secondary = true
            };
            btnJson.Click += (_, _) => ScheduleManager.OpenConfigFile();

            labelIcsPath.Text = "当前使用: schedule.json 默认排课";
            labelIcsPath.Location = new Point(10, 68);
            labelIcsPath.Size = new Size(480, 24);
            labelIcsPath.ForeColor = SystemColors.GrayText;

            panelFile.Controls.AddRange(new Control[] { labelFileTitle, btnIcs, btnJson, labelIcsPath });

            // Today Events Preview Section
            var lblEvents = new Label
            {
                Text = "今日已解析到的课程/日程预览：",
                Location = new Point(20, 330),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            listEvents.Location = new Point(20, 355);
            listEvents.Size = new Size(500, 95);
            listEvents.Font = new Font("Segoe UI", 8.75F);
            listEvents.BorderStyle = BorderStyle.FixedSingle;
            listEvents.IntegralHeight = false;

            // Status indicator
            labelStatus.Location = new Point(20, 460);
            labelStatus.Size = new Size(500, 24);
            labelStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelStatus.ForeColor = colorEco;

            // Bottom Buttons
            var btnSave = new RButton
            {
                Text = "保存并应用",
                Location = new Point(285, 498),
                Size = new Size(115, 36)
            };
            btnSave.Click += BtnSave_Click;

            var btnClose = new RButton
            {
                Text = "关闭",
                Location = new Point(410, 498),
                Size = new Size(110, 36),
                Secondary = true
            };
            btnClose.Click += (_, _) => Close();

            Controls.AddRange(new Control[]
            {
                panelTop, checkEnabled, panelTimes, panelFile, lblEvents, listEvents, labelStatus, btnSave, btnClose
            });
        }

        private void LoadData()
        {
            var cfg = ScheduleManager.GetConfig();
            checkEnabled.Checked = cfg.Enabled;
            numPrecharge.Value = Math.Max(15, Math.Min(180, cfg.PrechargeMinutes));
            numLeave.Value = Math.Max(5, Math.Min(90, cfg.LeaveBufferMinutes));

            if (!string.IsNullOrWhiteSpace(cfg.IcsPath) && File.Exists(cfg.IcsPath))
            {
                labelIcsPath.Text = "当前 ICS: " + cfg.IcsPath;
                labelIcsPath.ForeColor = SystemColors.ControlText;
            }
            else
            {
                labelIcsPath.Text = "当前使用: schedule.json 默认排课";
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
