using AutoTestSystem.Base;
using AutoTestSystem.DUT;
using AutoTestSystem.DynamicProperty;
using AutoTestSystem.Equipment.IO;
using AutoTestSystem.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using static AutoTestSystem.BLL.Bd;

namespace AutoTestSystem.Script
{
    /// <summary>
    /// 整合控件：結合 Script_IO_InstantAI_Pro、Script_IO_Related_SetIO_Pro、Script_IO_Related_GetIO_Pro
    /// 使用 MODE 選擇不同功能模式
    /// </summary>
    internal class Script_IO_Combined_Pro : ScriptIOBase, INotifyPropertyChanged
    {
        /// <summary>
        /// 控件模式枚舉
        /// </summary>
        public enum Control_Mode
        {
            InstantAI,  // InstantAI 模式
            SetIO,      // SetIO 模式
            GetIO       // GetIO 模式
        }

        string strOutData = string.Empty;
        private DynamicPropertyManager _propertyManager;

        #region Mode Selection
        private Control_Mode _mode;
        [Category("Common Parameters"), Description("選擇控件模式: InstantAI, SetIO, GetIO")]
        public Control_Mode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    OnPropertyChanged(nameof(Mode));
                    this.RefreshDynamicProperties();
                    TypeDescriptor.Refresh(this);
                }
            }
        }
        #endregion

        #region InstantAI Parameters
        [Category("InstantAI Parameters"), Description("AI通道編號")]
        public int Channel { get; set; }
        #endregion

        #region SetIO Parameters
        [Category("SetIO Parameters"), Description("設定ONOFF")]
        public bool ON_OFF { get; set; }

        [Category("SetIO Parameters"), Description("設定位元")]
        public int SetBit { get; set; }

        [Category("SetIO Parameters"), Description("端口編號")]
        public int SetPortNum { get; set; } = 0;

        [Category("SetIO Support Replace Param(s)"), Description("支援取代%%可輸入字串 - 端口編號")]
        public string strSetPortNum { get; set; } = string.Empty;

        [Category("SetIO Support Replace Param(s)"), Description("支援取代%%可輸入字串 - 設定位元")]
        public string strSetBit { get; set; } = string.Empty;
        #endregion

        #region GetIO Parameters
        [Category("GetIO Parameters"), Description("取得位元")]
        public int GetBit { get; set; } = 0;

        [Category("GetIO Parameters"), Description("端口編號")]
        public int GetPortNum { get; set; } = 0;

        [Category("GetIO Parameters"), Description("檢查狀態")]
        public bool Check { get; set; } = true;

        [Category("GetIO Parameters"), Description("超時時間(ms)")]
        public int Timeout { get; set; } = 2000;
        #endregion

        // INotifyPropertyChanged 實作
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Script_IO_Combined_Pro()
        {
            _propertyManager = new DynamicPropertyManager(this);
            ConfigurePropertyVisibility();
        }

        private void ConfigurePropertyVisibility()
        {
            var rules = new PropertyVisibilityBuilder()
                // InstantAI 模式參數
                .When(nameof(Channel), () => Mode == Control_Mode.InstantAI)
                // SetIO 模式參數
                .When(nameof(ON_OFF), () => Mode == Control_Mode.SetIO)
                .When(nameof(SetBit), () => Mode == Control_Mode.SetIO)
                .When(nameof(SetPortNum), () => Mode == Control_Mode.SetIO)
                .When(nameof(strSetPortNum), () => Mode == Control_Mode.SetIO)
                .When(nameof(strSetBit), () => Mode == Control_Mode.SetIO)
                // GetIO 模式參數
                .When(nameof(GetBit), () => Mode == Control_Mode.GetIO)
                .When(nameof(GetPortNum), () => Mode == Control_Mode.GetIO)
                .When(nameof(Check), () => Mode == Control_Mode.GetIO)
                .When(nameof(Timeout), () => Mode == Control_Mode.GetIO)
                .Build();

            _propertyManager.AddVisibilityRules(rules);
            _propertyManager.Initialize();
        }

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }

        public override bool PreProcess()
        {
            strOutData = string.Empty;
            return true;
        }

        public override bool Process(IOBase Device, ref string strDataout)
        {
            try
            {
                strOutData = string.Empty;

                switch (Mode)
                {
                    case Control_Mode.InstantAI:
                        return ProcessInstantAI(Device, ref strDataout);
                    case Control_Mode.SetIO:
                        return ProcessSetIO(Device, ref strDataout);
                    case Control_Mode.GetIO:
                        return ProcessGetIO(Device, ref strDataout);
                    default:
                        LogMessage($"Unknown Mode: {Mode}", MessageLevel.Error);
                        return false;
                }
            }
            catch (Exception ex)
            {
                strDataout = $"{{\"Exception\":\"{ex.Message}\"}}";
                LogMessage($"Process Exception: {ex.Message}", MessageLevel.Error);
                return false;
            }
        }

        #region InstantAI Processing
        private bool ProcessInstantAI(IOBase Device, ref string strDataout)
        {
            strOutData = string.Empty;

            bool ret = Device.InstantAI(Channel, ref strOutData);

            if (ret)
                LogMessage($"Data:{strOutData}");
            else
                LogMessage($"InstantAI Fail.{strOutData}", MessageLevel.Error);

            strDataout = strOutData;
            LogMessage($"Data:{strDataout}");

            return ret;
        }
        #endregion

        #region SetIO Processing
        private bool ProcessSetIO(IOBase Device, ref string strDataout)
        {
            strOutData = string.Empty;
            bool ret = false;

            if (string.IsNullOrEmpty(strSetBit))
            {
                LogMessage($"Set IO Bit({SetBit}) = {ON_OFF}");
                ret = Device.SETIO(SetPortNum, SetBit, ON_OFF);
            }
            else
            {
                int bitnum;
                int portnum;

                if (!int.TryParse(ReplaceProp(strSetBit), out bitnum))
                {
                    string msg = ReplaceProp(strSetBit);
                    var data = new Dictionary<string, object>
                    {
                        { "errorCode", -2 },
                        { "Message", $"Input Params Fail [{ReplaceProp(strSetBit)}]" }
                    };
                    strOutData = JsonConvert.SerializeObject(data, Formatting.Indented);
                    strDataout = strOutData;
                    return false;
                }

                if (!int.TryParse(ReplaceProp(strSetPortNum), out portnum))
                {
                    LogMessage($"{strSetPortNum} int.TryParse fail");
                    var data = new Dictionary<string, object>
                    {
                        { "errorCode", -3 },
                        { "Message", $"Input Params Fail [{ReplaceProp(strSetPortNum)}]" }
                    };
                    strOutData = JsonConvert.SerializeObject(data, Formatting.Indented);
                    strDataout = strOutData;
                    return false;
                }

                LogMessage($"Set IO Bit[{portnum}][{bitnum}] = {ON_OFF}");
                ret = Device.SETIO(portnum, bitnum, ON_OFF);
            }

            if (ret)
            {
                var data = new Dictionary<string, object>
                {
                    { "errorCode", 0 }
                };
                strOutData = JsonConvert.SerializeObject(data, Formatting.Indented);
                LogMessage($"Set IO Bit Success. Data({strOutData}) ");
            }
            else
            {
                var data = new Dictionary<string, object>
                {
                    { "errorCode", -1 },
                    { "Message", $"Device.SETIO() return {ret}" }
                };
                strOutData = JsonConvert.SerializeObject(data, Formatting.Indented);
                LogMessage($"Device.SETIO() Fail. return {ret}");
            }

            strDataout = strOutData;

            return ret;
        }
        #endregion

        #region GetIO Processing
        private bool ProcessGetIO(IOBase Device, ref string output)
        {
            strOutData = string.Empty;
            int MAX_ELAPSE_MS = Timeout;
            const int INTERVAL = 20;
            bool InputStatus = false;
            bool ret = false;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            LogMessage("Waiting...", MessageLevel.Debug);

            while (stopwatch.ElapsedMilliseconds < MAX_ELAPSE_MS)
            {
                ret = Device.GETIO(GetPortNum, GetBit, ref InputStatus);
                if (ret)
                {
                    if (InputStatus == Check)
                    {
                        LogMessage($"Check IO Bit({GetBit}) = {Check} success");

                        var data = new Dictionary<string, object>
                        {
                            { "errorCode", 0 },
                            { "Message", $"Check IO Bit({GetBit}) = {Check} success" }
                        };

                        strOutData = JsonConvert.SerializeObject(data);

                        output = strOutData;
                        return true;
                    }
                }
                else // GETIO 函數返回失敗
                {
                    var data = new Dictionary<string, object>
                    {
                        { "errorCode", -1 },
                        { "Message", $"Device.GETIO() return {ret}" }
                    };
                    strOutData = JsonConvert.SerializeObject(data);
                    output = strOutData;

                    LogMessage($"Device.GETIO() return {ret}");
                    return false;
                }

                System.Threading.Thread.Sleep(INTERVAL);
            }

            LogMessage("GetIO Timeout...", MessageLevel.Error);

            // Timeout
            var timeoutData = new Dictionary<string, object>
            {
                { "errorCode", -2 }
            };

            strOutData = JsonConvert.SerializeObject(timeoutData);
            output = strOutData;

            return false;
        }
        #endregion

        public override bool PostProcess()
        {
            LogMessage($"Check Spec:{Spec}");

            string result = CheckRule(strOutData, Spec);

            if (result == "PASS" || Spec == "" || Spec == string.Empty)
            {
                return true;
            }
            else
            {
                LogMessage($"CheckRule: {result}", MessageLevel.Error);
                return false;
            }
        }
    }
}