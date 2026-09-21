using GHelper.Helpers;
using GHelper.UI;

namespace GHelper.Schedule
{
    public class ScheduleForm : RForm
    {
        private CheckBox checkEnabled = new();
        private RNumericUpDown numPrecharge = new();
        private RNumericUpDown numLeave = new();
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
            Text = "课表与智能充电设置 - ROG Flow X13 (时区锁死: UTC+8 北京时间)";
            ClientSize = new Size(550, 565);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);
            AllowDrop = true;

            // Universal Drag and Drop support for .ics files
            void WireDragDrop(Control control)
            {
                control.AllowDrop = true;
                control.DragEnter += (_, e) =>
                {
                    if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                        e.Effect = DragDropEffects.Copy;
                };
                control.DragDrop += (_, e) =>
                {
                    if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    {
                        string file = files[0];
                        if (file.EndsWith(".ics", StringComparison.OrdinalIgnoreCase))
                        {
                            LoadIcsFile(file);
                        }
                        else
                        {
                            MessageBox.Show(this, "请拖入 .ics 格式的课表日历文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                };
            }
            WireDragDrop(this);

            // Header Section
            var panelTop = new Panel
            {
                Location = new Point(20, 16),
                Size = new Size(510, 52),
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
                Text = "上课动身前自动充至 100%，离电出发后自动切回 80% 保养上限（时区锁死: UTC+8 北京时间）",
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = SystemColors.GrayText,
                AutoSize = true,
                Location = new Point(0, 26)
            };

            panelTop.Controls.Add(labelHeader);
            panelTop.Controls.Add(labelSubtitle);

            // Enable Switch
            checkEnabled.Text = " 启用智能课表感知与满电调度";
            checkEnabled.Font = new Font("Segoe UI", 9.25F, FontStyle.Bold);
            checkEnabled.Location = new Point(20, 74);
            checkEnabled.Size = new Size(510, 30);
            checkEnabled.UseVisualStyleBackColor = true;

            // Timing Section Title
            var labelTimesTitle = new Label
            {
                Text = "时间策略配置",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 114),
                AutoSize = true
            };

            // Timing TableLayoutPanel: completely immune to DPI and font overlap
            var tableTimes = new TableLayoutPanel
            {
                Location = new Point(20, 138),
                Size = new Size(510, 76),
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            tableTimes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableTimes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95F));
            tableTimes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableTimes.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableTimes.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            var lblPre = new Label
            {
                Text = "离电前充电时间:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 12, 6)
            };

            numPrecharge.Size = new Size(85, 28);
            numPrecharge.Minimum = 0;
            numPrecharge.Maximum = 180;
            numPrecharge.Value = 45;
            numPrecharge.Minimum = 15;
            numPrecharge.TextAlign = HorizontalAlignment.Center;
            numPrecharge.Margin = new Padding(0, 3, 10, 3);
            numPrecharge.Anchor = AnchorStyles.Left;

            var lblPreUnit = new Label
            {
                Text = "分钟（动身前将电池充满至 100%）",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 6, 0, 6)
            };

            var lblLeave = new Label
            {
                Text = "上课前出门提前:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 12, 6)
            };

            numLeave.Size = new Size(85, 28);
            numLeave.Minimum = 0;
            numLeave.Maximum = 90;
            numLeave.Value = 20;
            numLeave.Minimum = 5;
            numLeave.TextAlign = HorizontalAlignment.Center;
            numLeave.Margin = new Padding(0, 3, 10, 3);
            numLeave.Anchor = AnchorStyles.Left;

            var lblLeaveUnit = new Label
            {
                Text = "分钟（预留动身前往教室的时间）",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 6, 0, 6)
            };

            tableTimes.Controls.Add(lblPre, 0, 0);
            tableTimes.Controls.Add(numPrecharge, 1, 0);
            tableTimes.Controls.Add(lblPreUnit, 2, 0);

            tableTimes.Controls.Add(lblLeave, 0, 1);
            tableTimes.Controls.Add(numLeave, 1, 1);
            tableTimes.Controls.Add(lblLeaveUnit, 2, 1);

            // Data Source Section Title
            var labelFileTitle = new Label
            {
                Text = "课程表数据源（支持 ICS 日历或 JSON 周排课，亦可直接拖拽文件入内）",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 224),
                AutoSize = true
            };

            var panelFile = new Panel
            {
                Location = new Point(20, 248),
                Size = new Size(510, 74),
                BackColor = Color.Transparent
            };

            var btnIcs = new RButton
            {
                Text = "选择 .ics 课表文件...",
                Location = new Point(0, 0),
                Size = new Size(180, 34),
                Secondary = true
            };
            btnIcs.Click += BtnIcs_Click;

            var btnJson = new RButton
            {
                Text = "编辑 schedule.json (周排课)",
                Location = new Point(195, 0),
                Size = new Size(210, 34),
                Secondary = true
            };
            btnJson.Click += (_, _) => ScheduleManager.OpenConfigFile();

            labelIcsPath.Text = "当前使用: schedule.json 默认排课";
            labelIcsPath.Location = new Point(0, 44);
            labelIcsPath.Size = new Size(510, 24);
            labelIcsPath.ForeColor = SystemColors.GrayText;

            panelFile.Controls.AddRange(new Control[] { btnIcs, btnJson, labelIcsPath });

            // Today Events Preview Section
            var lblEvents = new Label
            {
                Text = "今日已解析到的课程/日程预览：",
                Location = new Point(20, 332),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            listEvents.Location = new Point(20, 356);
            listEvents.Size = new Size(510, 95);
            listEvents.Font = new Font("Segoe UI", 8.75F);
            listEvents.BorderStyle = BorderStyle.FixedSingle;
            listEvents.IntegralHeight = false;

            // Status indicator
            labelStatus.Location = new Point(20, 460);
            labelStatus.Size = new Size(510, 24);
            labelStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelStatus.ForeColor = colorEco;

            // Bottom Buttons
            var btnSave = new RButton
            {
                Text = "保存并应用",
                Location = new Point(295, 502),
                Size = new Size(115, 36)
            };
            btnSave.Click += BtnSave_Click;

            var btnClose = new RButton
            {
                Text = "关闭",
                Location = new Point(420, 502),
                Size = new Size(110, 36),
                Secondary = true
            };
            btnClose.Click += (_, _) => Close();

            WireDragDrop(panelTop);
            WireDragDrop(tableTimes);
            WireDragDrop(panelFile);
            WireDragDrop(listEvents);

            Controls.AddRange(new Control[]
            {
                panelTop, checkEnabled, labelTimesTitle, tableTimes, labelFileTitle, panelFile, lblEvents, listEvents, labelStatus, btnSave, btnClose
            });
        }

        private void LoadData()
        {
            try
            {
                var cfg = ScheduleManager.GetConfig();
                checkEnabled.Checked = cfg.Enabled;
                numPrecharge.Value = Math.Max(numPrecharge.Minimum, Math.Min(numPrecharge.Maximum, cfg.PrechargeMinutes));
                numLeave.Value = Math.Max(numLeave.Minimum, Math.Min(numLeave.Maximum, cfg.LeaveBufferMinutes));

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
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleForm] LoadData error: {ex.Message}");
            }
        }

        private void RefreshEventsList()
        {
            listEvents.Items.Clear();
            try
            {
                var list = ScheduleManager.GetTodayEventsDisplayList();
                if (list.Count == 0)
                {
                    listEvents.Items.Add($"今日 ({ScheduleManager.Now:MM-dd dddd}) 暂无待上课程（维持 80% 电池保养 | 北京时间）");
                }
                else
                {
                    foreach (var item in list)
                    {
                        listEvents.Items.Add(item);
                    }
                }
                labelStatus.Text = ScheduleManager.GetStatusDetailed() + $" (北京时间: {ScheduleManager.Now:HH:mm})";
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleForm] RefreshEventsList error: {ex.Message}");
                listEvents.Items.Add($"解析日程列表异常: {ex.Message}");
            }
        }

        private void BtnIcs_Click(object? sender, EventArgs e)
        {
            try
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "选择课程表/日历 .ics 文件",
                    Filter = "iCalendar 日历文件 (*.ics)|*.ics|所有文件 (*.*)|*.*",
                    RestoreDirectory = true
                };

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    LoadIcsFile(ofd.FileName);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleForm] OpenFileDialog exception: {ex.Message}");
                MessageBox.Show(this, $"打开文件选择窗口失败: {ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadIcsFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            try
            {
                var cfg = ScheduleManager.GetConfig();
                cfg.IcsPath = path;
                ScheduleManager.SaveConfig(cfg);
                labelIcsPath.Text = "当前 ICS: " + path;
                labelIcsPath.ForeColor = SystemColors.ControlText;

                ScheduleManager.CheckSchedule(force: true);
                RefreshEventsList();
                Program.settingsForm.VisualiseBatteryTitleCurrent();
                Program.settingsForm.VisualiseScheduleStatus();
                Program.toast.RunToast($"已关联课表: {Path.GetFileName(path)}", ToastIcon.Charger);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleForm] Load selected ICS error: {ex.Message}");
                MessageBox.Show(this, $"解析所选课表文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                var cfg = ScheduleManager.GetConfig();
                cfg.Enabled = checkEnabled.Checked;
                cfg.PrechargeMinutes = (int)numPrecharge.Value;
                cfg.LeaveBufferMinutes = (int)numLeave.Value;
                ScheduleManager.SaveConfig(cfg);
                ScheduleManager.CheckSchedule(force: true);
                RefreshEventsList();
                Program.settingsForm.VisualiseBatteryTitleCurrent();
                Program.settingsForm.VisualiseScheduleStatus();
                Program.toast.RunToast("课程表与智能充电设置已更新！", ToastIcon.Charger);
                Close();
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[ScheduleForm] BtnSave_Click error: {ex.Message}");
                MessageBox.Show(this, $"保存配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
