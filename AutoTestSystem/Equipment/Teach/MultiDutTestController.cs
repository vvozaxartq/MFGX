using AutoTestSystem.Base;
using AutoTestSystem.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoTestSystem.Equipment.Teach
{
    public class MultiDutTestController : TeachBase, IDisposable
    {
        private readonly object _taskLock = new object();
        private readonly List<Task> _runTasks = new List<Task>();
        private CancellationTokenSource _startCts;
        private Task _startSequenceTask;
        private bool _running;

        [Category("Param"), Description("DUT 清單 (多選)") , Editor(typeof(DUT_BASE.Muti_DeviceEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string DutTable { get; set; } = string.Empty;

        [Category("Param"), Description("固定啟用 DUT 數量 (0 = 使用清單/全部)")]
        public int FixedDutCount { get; set; } = 0;

        [Category("Param"), Description("每個 DUT 啟動間隔 (ms)")]
        public int StartIntervalMs { get; set; } = 0;

        public override bool Init(string jsonParam)
        {
            _runTasks.Clear();
            _running = false;
            return true;
        }

        public override bool UnInit()
        {
            _startCts?.Cancel();
            _runTasks.Clear();
            _running = false;
            return true;
        }

        public bool Start()
        {
            if (_running)
            {
                return false;
            }

            List<DUT_BASE> targets = ResolveTargetDuts();
            if (targets.Count == 0)
            {
                MessageBox.Show("沒有任何 DUT_BASE 裝置");
                return false;
            }

            _running = true;
            GlobalNew.g_shouldStop = false;
            GlobalNew.g_isRunning = true;
            _startCts = new CancellationTokenSource();

            _startSequenceTask = Task.Run(async () =>
            {
                foreach (DUT_BASE dut in targets)
                {
                    if (_startCts.IsCancellationRequested)
                    {
                        break;
                    }

                    Task runTask = dut.RunLoopAsync();
                    lock (_taskLock)
                    {
                        _runTasks.Add(runTask);
                    }

                    if (StartIntervalMs > 0)
                    {
                        try
                        {
                            await Task.Delay(StartIntervalMs, _startCts.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }
            });

            return true;
        }

        public async Task StopAsync()
        {
            if (!_running)
            {
                return;
            }

            GlobalNew.g_shouldStop = true;
            _startCts?.Cancel();

            Task[] tasks;
            lock (_taskLock)
            {
                tasks = _runTasks.ToArray();
            }

            if (_startSequenceTask != null)
            {
                await _startSequenceTask;
            }

            if (tasks.Length > 0)
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    LogMessage($"MultiDutTestController StopAsync wait error: {ex.Message}");
                }
            }

            _runTasks.Clear();
            _running = false;
            GlobalNew.g_isRunning = false;
        }

        public override void Dispose()
        {
            _startCts?.Cancel();
            _runTasks.Clear();
            _running = false;
        }

        public override bool Show() => true;

        protected override string GetJsonParamString() => throw new NotImplementedException();

        private List<DUT_BASE> ResolveTargetDuts()
        {
            List<DUT_BASE> targets = new List<DUT_BASE>();

            if (!string.IsNullOrWhiteSpace(DutTable))
            {
                try
                {
                    List<MultiDeviceEntry> entries = JsonConvert.DeserializeObject<List<MultiDeviceEntry>>(DutTable);
                    if (entries != null)
                    {
                        foreach (MultiDeviceEntry entry in entries)
                        {
                            if (!string.Equals(entry.DeviceType, "DUT", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(entry.DeviceObject))
                            {
                                continue;
                            }

                            if (GlobalNew.Devices.TryGetValue(entry.DeviceObject, out var device) && device is DUT_BASE dut)
                            {
                                targets.Add(dut);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"MultiDutTestController DutTable parse fail: {ex.Message}");
                }
            }

            if (targets.Count == 0)
            {
                targets = GlobalNew.Devices.Values
                    .OfType<DUT_BASE>()
                    .Where(dut => dut.Enable)
                    .OrderBy(dut => dut.Description)
                    .ToList();
            }

            if (FixedDutCount > 0 && targets.Count > FixedDutCount)
            {
                targets = targets.Take(FixedDutCount).ToList();
            }

            return targets;
        }

        private class MultiDeviceEntry
        {
            public string SharedName { get; set; }
            public string DeviceType { get; set; }
            public string DeviceObject { get; set; }
        }
    }
}
