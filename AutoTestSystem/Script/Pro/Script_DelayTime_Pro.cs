
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static AutoTestSystem.BLL.Bd;

namespace AutoTestSystem.Script
{
    internal class Script_DelayTime_Pro : Script_Extra_Base
    {
        [Category("Params"), Description("DelayTime(ms)")]
        public int DelayTime { get; set; } = 500;

        public Script_DelayTime_Pro()
        {
            Description = "Delay";
            ShowItem = false;
        }

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }
        public override bool PreProcess()
        {

            return true;

        }
        public override bool Process(ref string output)
        {
            LogMessage($"Waiting {DelayTime}ms..");
            Sleep(DelayTime);
            
            var data = new Dictionary<string, object>
                        {
                            { "STATUS", "PASS" }
                        };
            output = JsonConvert.SerializeObject(data);

            return true;
        }
        public override bool PostProcess()
        {
            
            return true;

        }

    }
}
