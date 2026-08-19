using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MikrotikSwitch
{
    public partial class MainForm : Form
    {
        private Config? _cfg;
        private MikrotikClient? _client;
        private RadioButton[] _radios = null!;
        private Label[] _statusLabels = null!;
        private Button _applyBtn = null!;
        private Label _statusBar = null!;
        private volatile bool _suppressChange;
        private volatile bool _switching;
        private bool _initialized;
        private CancellationTokenSource? _pollCts;

        private const int PortCount = 6;

        public MainForm()
        {
            InitializeComponent();
            LoadConfigAndConnect();
        }

        private void InitializeComponent()
        {
            this.Text = "MikroTik RB5009 — переключение FPV";
            this.Size = new Size(520, 380);
            this.MinimumSize = new Size(520, 380);
            this.StartPosition = FormStartPosition.CenterScreen;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 3,
                ColumnCount = 1,
                RowStyles = {
                    new RowStyle(SizeType.Percent, 80),
                    new RowStyle(SizeType.Absolute, 40),
                    new RowStyle(SizeType.Absolute, 30)
                }
            };

            var groupBox = new GroupBox
            {
                Text = "Активный FPV",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };

            _radios = new RadioButton[PortCount];
            _statusLabels = new Label[PortCount];

            for (int i = 0; i < PortCount; i++)
            {
                var row = new TableLayoutPanel
                {
                    ColumnCount = 2,
                    RowCount = 1,
                    AutoSize = true,
                    Margin = new Padding(0, 3, 0, 3),
                    ColumnStyles = {
                        new ColumnStyle(SizeType.Percent, 60),
                        new ColumnStyle(SizeType.Percent, 40)
                    }
                };

                var radio = new RadioButton
                {
                    Text = $"FPV {i+1}",
                    AutoSize = true,
                    Tag = i
                };
                radio.CheckedChanged += Radio_CheckedChanged;

                var label = new Label
                {
                    Text = "нет данных",
                    AutoSize = true,
                    ForeColor = Color.Gray
                };

                row.Controls.Add(radio, 0, 0);
                row.Controls.Add(label, 1, 0);

                flow.Controls.Add(row);

                _radios[i] = radio;
                _statusLabels[i] = label;
            }

            groupBox.Controls.Add(flow);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 5, 0, 5)
            };
            _applyBtn = new Button
            {
                Text = "Применить",
                Size = new Size(120, 30),
                Enabled = false
            };
            _applyBtn.Click += ApplyBtn_Click;
            buttonPanel.Controls.Add(_applyBtn);

            _statusBar = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Подключение...",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DimGray
            };

            mainLayout.Controls.Add(groupBox, 0, 0);
            mainLayout.Controls.Add(buttonPanel, 0, 1);
            mainLayout.Controls.Add(_statusBar, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void LoadConfigAndConnect()
        {
            try
            {
                _cfg = Config.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Конфиг", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            try
            {
                _client = new MikrotikClient(_cfg.Address, _cfg.User, _cfg.Pass);
                _statusBar.Text = $"Подключено к {_cfg.Address}";
                _applyBtn.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Подключение к RouterOS", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusBar.Text = "Нет соединения: " + ex.Message;
                _applyBtn.Enabled = false;
            }

            _pollCts = new CancellationTokenSource();
            Task.Run(() => PollLoop(_pollCts.Token));
        }

        private void PollLoop(CancellationToken ct)
        {
            var interval = TimeSpan.FromSeconds(_cfg?.PollSeconds ?? 5);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_client != null)
                    {
                        _client.EnsureConnected();
                        var ports = _client.ListEthernetPorts();
                        UpdateUI(ports);
                        SetStatus($"Подключено к {_cfg?.Address}, обновлено {DateTime.Now:HH:mm:ss}");
                    }
                }
                catch (Exception ex)
                {
                    SetStatus("Ошибка опроса: " + ex.Message);
                }

                try { Thread.Sleep(interval); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void UpdateUI(List<PortInfo> ports)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action)(() => UpdateUI(ports)));
                return;
            }

            var byName = new Dictionary<string, PortInfo>();
            foreach (var p in ports)
            {
                var key = p.Name ?? "";
                if (!byName.ContainsKey(key))
                    byName[key] = p;
            }

            _suppressChange = true;
            for (int i = 0; i < PortCount; i++)
            {
                var name = _cfg?.PortNames[i];
                if (name != null && byName.TryGetValue(name, out var p))
                {
                    _statusLabels[i].Text = p.Disabled ? "выключен" : "▶ включён";
                    _statusLabels[i].Text += ", " + (p.Running ? "линк есть ●" : "линка нет ○");
                    _statusLabels[i].ForeColor = p.Running ? Color.DarkGreen : Color.Gray;

                    if (!_initialized)
                    {
                        if (!p.Disabled)
                            _radios[i].Checked = true;
                    }
                }
                else
                {
                    _statusLabels[i].Text = "нет данных";
                    _statusLabels[i].ForeColor = Color.Gray;
                }
            }
            _initialized = true;
            _suppressChange = false;
        }

        private void SetStatus(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action)(() => SetStatus(text)));
                return;
            }
            _statusBar.Text = text;
        }

        private void Radio_CheckedChanged(object? sender, EventArgs e)
        {
            var radio = sender as RadioButton;
            if (radio == null || _suppressChange) return;

            if (radio.Checked)
            {
                int idx = (int?)radio.Tag ?? -1;
                if (idx >= 0)
                    SetStatus($"Выбран FPV {idx+1} — нажмите «Применить»");
            }
        }

        private async void ApplyBtn_Click(object? sender, EventArgs e)
        {
            if (_client == null || _switching) return;

            int selected = -1;
            for (int i = 0; i < PortCount; i++)
            {
                if (_radios[i].Checked)
                {
                    selected = i;
                    break;
                }
            }
            if (selected == -1)
            {
                SetStatus("Сначала выберите FPV");
                return;
            }

            List<PortInfo> ports;
            try
            {
                _client.EnsureConnected();
                ports = _client.ListEthernetPorts();
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка подключения: " + ex.Message);
                return;
            }

            var byName = new Dictionary<string, PortInfo>();
            foreach (var p in ports)
            {
                var key = p.Name ?? "";
                if (!byName.ContainsKey(key))
                    byName[key] = p;
            }

            var targetName = _cfg?.PortNames[selected];
            if (targetName == null || !byName.TryGetValue(targetName, out var target) || !target.Running)
            {
                SetStatus($"На FPV {selected+1} нет линка — переключение отклонено");
                return;
            }

            _switching = true;
            _applyBtn.Enabled = false;
            SetStatus($"Переключаю на FPV {selected+1}...");

            try
            {
                foreach (var p in ports)
                {
                    if (p.Name == targetName) continue;
                    if (!p.Disabled && p.Id != null)
                    {
                        _client.SetPortEnabled(p.Id, false);
                        await Task.Delay(50);
                    }
                }

                ports = _client.ListEthernetPorts();
                bool allOff = true;
                foreach (var p in ports)
                {
                    if (p.Name != targetName && !p.Disabled)
                    {
                        allOff = false;
                        break;
                    }
                }
                if (!allOff)
                {
                    SetStatus("Остальные FPV не выключились — переключение прервано");
                    return;
                }

                if (target.Id != null)
                {
                    _client.SetPortEnabled(target.Id, true);
                }

                ports = _client.ListEthernetPorts();
                UpdateUI(ports);
                SetStatus($"FPV {selected+1} включён, остальные подтверждённо выключены");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
            }
            finally
            {
                _switching = false;
                _applyBtn.Enabled = true;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _pollCts?.Cancel();
            _client?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
