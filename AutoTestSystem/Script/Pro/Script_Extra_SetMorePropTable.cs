
using AutoTestSystem.Model;
using Manufacture;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AutoTestSystem.BLL.Bd;

namespace AutoTestSystem.Script
{
    internal class Script_Extra_SetMorePropTable : Script_Extra_Base
    {

        [Category("VariableTable Parameters"), Description("Variable_Table parameter"), Editor(typeof(SetTableEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string Variable_Table { get; set; }


        public override void Dispose()
        {
            //throw new NotImplementedException();
        }
        public override bool PreProcess()
        {

            return true;
        }
        public override bool Process(ref string strOutData)
        {

            Dictionary<string, string> tmp_log = new Dictionary<string, string>();
            string ReplaceValue = ReplaceProp(Variable_Table);
            //string ReplaceValue = Variable_Table;

            // 將 JSON 轉換成 Dictionary
            var dataDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(ReplaceValue);

            // 將 Key-Value 存入 PushMoreData
            foreach (var kvp in dataDict)
            {
                bool ret = false;
                string Add_Prefix = string.Empty;
                if(!string.IsNullOrEmpty(Prefix))
                    Add_Prefix = $"{Prefix}{kvp.Key}";
                else
                    Add_Prefix = $"{kvp.Key}";

                ret = PushMoreData(Add_Prefix, kvp.Value);

                if (ret)
                {
                    if (!tmp_log.ContainsKey(Add_Prefix))
                        tmp_log.Add(Add_Prefix, kvp.Value);
                    else
                        tmp_log[Add_Prefix] = kvp.Value;
                }
                else
                {
                    tmp_log.Add($"{Add_Prefix}_Err", kvp.Value);
                    strOutData = JsonConvert.SerializeObject(tmp_log, Formatting.Indented); 
                    LogMessage(strOutData);
                    return false;
                }
            }

            strOutData = JsonConvert.SerializeObject(tmp_log, Formatting.Indented);
            LogMessage(strOutData);
            return true;
        }
        public override bool PostProcess()
        {
            return true;

        }

        public class SetTableEditor : UITypeEditor
        {
            public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
            {
                string Sample = string.Empty;

                using (TableForm Tableform = new TableForm((string)value))
                {
                    var result = Tableform.ShowDialog();

                    if (result == DialogResult.OK)
                    {
                        return Tableform.ConvertDataGridViewToJson();
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            {
                return UITypeEditorEditStyle.Modal;
            }
        }


    }
}
