
using AutoTestSystem.Base;
using AutoTestSystem.Equipment.ControlDevice;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;   // MultilineStringEditor
using System.Drawing.Design;          // UITypeEditor
using System.IO;
using System.Linq;
using System.Text;

namespace AutoTestSystem.Script
{
    internal class Script_ControlDevice_OpenShort : Script_ControlDevice_Base
    {
        [Category("Command"), Description("支援用%%方式做變數值取代"), TypeConverter(typeof(CommandConverter))]
        public string Send { get; set; } = "73 74 61 72 74 00 03 00 01 65 6e 64";

        [Category("Mode"), Description("設定模式：SAVE 或 COMPARE")]
        public string Mode { get; set; } = "COMPARE";

        [Category("GoldenFile"), Description("Golden 檔案路徑")]
        public string GoldenFilePath { get; set; } = @"D:\Golden.txt";

        // ====== Compare Mask（精簡：只保留 Source + Inline） ======
        public enum MaskSourceMode
        {
            None = 0,
            InlineText = 1
        }

        [Category("CompareMask"), Description("遮罩來源：None / InlineText")]
        [DefaultValue(MaskSourceMode.None)]
        public MaskSourceMode MaskSource { get; set; } = MaskSourceMode.None;

        [Category("CompareMask"), Description(
            "遮罩規則（1-based）：\r\n" +
            "- P<sel>：忽略指定 PIN 全部 bits\r\n" +
            "- B<sel>：忽略所有 PIN 的指定 bits\r\n" +
            "- P<sel>:B<sel>：忽略指定 PIN 的指定 bits\r\n" +
            "sel 支援：1,3,5 或 1-10 或 *\r\n" +
            "規則以 ; 或換行分隔，# 或 // 開頭為註解\r\n" +
            "例：\r\n" +
            "B1-2\r\n" +
            "P10:B33-40\r\n" +
            "P120-160"
        )]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string MaskInline { get; set; } = "";

        private string l_strOutData = string.Empty;

        public override bool PreProcess()
        {
            l_strOutData = string.Empty;
            return true;
        }

        public override bool Process(ControlDeviceBase ControlDevice, ref string output)
        {
            try
            {
                byte[] packet = null;

                // 發送指令
                ControlDevice.SEND(Send);

                // 讀取回應
                if (!ControlDevice.READ(ref packet, 5000))
                {
                    LogMessage("Timeout or read failed.", MessageLevel.Error);
                    return false;
                }

                // 解析測試結果
                int[,] testBits = ParsePacketToBitArray(packet);

                if (Mode.Equals("SAVE", StringComparison.OrdinalIgnoreCase))
                {
                    SaveGoldenFile(GoldenFilePath, testBits);
                    LogMessage($"Golden file saved at {GoldenFilePath}", MessageLevel.Info);
                    return true;
                }

                if (!Mode.Equals("COMPARE", StringComparison.OrdinalIgnoreCase))
                {
                    LogMessage($"Invalid MODE: {Mode}. Use SAVE or COMPARE.", MessageLevel.Error);
                    return false;
                }

                // ====== COMPARE ======
                if (!File.Exists(GoldenFilePath))
                {
                    LogMessage("Golden file not found. Please run in SAVE mode first.", MessageLevel.Error);
                    return false;
                }

                int[,] goldenBits = LoadGoldenFile(GoldenFilePath);
                if (goldenBits.GetLength(0) == 0 || goldenBits.GetLength(1) == 0)
                {
                    LogMessage("Golden file invalid or empty.", MessageLevel.Error);
                    return false;
                }

                // 基本尺寸檢查
                if (goldenBits.GetLength(0) != testBits.GetLength(0) || goldenBits.GetLength(1) != testBits.GetLength(1))
                {
                    LogMessage($"Dimension mismatch. Test={testBits.GetLength(0)}x{testBits.GetLength(1)}, Golden={goldenBits.GetLength(0)}x{goldenBits.GetLength(1)}",
                        MessageLevel.Error);
                    return false;
                }

                int pinCount = testBits.GetLength(0);
                int bitCount = testBits.GetLength(1);

                // ====== 建立遮罩（含格式驗證） ======
                var maskBuild = BuildIgnoreMaskWithValidation(pinCount, bitCount);
                if (!maskBuild.Success)
                {
                    // Mask 格式錯誤：直接 Fail，避免誤判
                    LogMessage("Mask specification invalid. Compare aborted.", MessageLevel.Error);
                    return false;
                }

                var ignoreMask = maskBuild.Mask;
                int ignoredCount = maskBuild.IgnoredCount;
                int ruleCount = maskBuild.RuleCount;

                // 比較（套用遮罩）
                var diffs = CompareBitArrays(testBits, goldenBits, ignoreMask);

                // 建立 JSON 結構（順便帶上 mask 統計，方便追 log）
                var resultObj = new
                {
                    Mode = Mode,
                    GoldenFile = GoldenFilePath,
                    DiffCount = diffs.Count,
                    MaskSource = MaskSource.ToString(),
                    MaskRuleCount = ruleCount,
                    MaskIgnoredPoints = ignoredCount
                };

                string jsonResult = JsonConvert.SerializeObject(resultObj, Formatting.Indented);

                // 保存 JSON 檔案（可選）
                File.WriteAllText(@"D:\CompareResult.json", jsonResult);

                LogMessage(jsonResult, MessageLevel.Info);
                l_strOutData = jsonResult;
                output = jsonResult;

                if (diffs.Count == 0)
                {
                    LogMessage("All pins match golden data. (after mask applied)", MessageLevel.Info);
                    return true;
                }

                // 有差異：一次輸出所有差異
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Found {diffs.Count} mismatches (after mask applied):");
                foreach (var (pin, bit, expected, actual) in diffs)
                {
                    sb.AppendLine($"Mismatch at PIN_{pin:D3}, Bit_{bit:D3} (Expected={expected}, Actual={actual})");
                }
                LogMessage(sb.ToString(), MessageLevel.Warn);

                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Process Exception: {ex.Message}", MessageLevel.Fatal);
                return false;
            }
        }

        // =========================
        // Packet -> BitArray
        // =========================
        private int[,] ParsePacketToBitArray(byte[] packet)
        {
            int headerLen = 3;
            int pinLineLen = 21;
            int footerLen = 3;
            int pinLines = (packet.Length - headerLen - footerLen) / pinLineLen;

            // 你的 bit 固定 160
            int[,] bitArray = new int[pinLines, 160];

            for (int line = 0; line < pinLines; line++)
            {
                int offset = headerLen + line * pinLineLen;
                byte[] pinBytes = packet.Skip(offset).Take(pinLineLen).ToArray();

                int bitIdx = 0;
                for (int i = 1; i < pinBytes.Length; i++)
                {
                    string bits = Convert.ToString(pinBytes[i], 2).PadLeft(8, '0');
                    bits = new string(bits.Reverse().ToArray());
                    for (int b = 0; b < 8 && bitIdx < 160; b++)
                    {
                        bitArray[line, bitIdx++] = bits[b] == '1' ? 1 : 0;
                    }
                }
            }
            return bitArray;
        }

        // =========================
        // Golden I/O
        // =========================
        private void SaveGoldenFile(string filePath, int[,] bitArray)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    int pinCount = bitArray.GetLength(0);
                    int bitCount = bitArray.GetLength(1);
                    for (int pin = 0; pin < pinCount; pin++)
                    {
                        StringBuilder line = new StringBuilder(bitCount);
                        for (int bit = 0; bit < bitCount; bit++)
                            line.Append(bitArray[pin, bit]);
                        writer.WriteLine(line.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"SaveGoldenFile Exception: {ex.Message}", MessageLevel.Error);
            }
        }

        private int[,] LoadGoldenFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                int pinCount = lines.Length;
                int bitCount = lines[0].Length;

                int[,] bitArray = new int[pinCount, bitCount];

                for (int pin = 0; pin < pinCount; pin++)
                {
                    if (lines[pin].Length != bitCount)
                        throw new InvalidDataException($"Golden line length mismatch at line {pin + 1}");

                    for (int bit = 0; bit < bitCount; bit++)
                        bitArray[pin, bit] = lines[pin][bit] == '1' ? 1 : 0;
                }
                return bitArray;
            }
            catch (Exception ex)
            {
                LogMessage($"LoadGoldenFile Exception: {ex.Message}", MessageLevel.Error);
                return new int[0, 0];
            }
        }

        // =========================
        // Mask Build + Validation
        // =========================
        private sealed class MaskBuildResult
        {
            public bool Success { get; set; }
            public BitArray[] Mask { get; set; }
            public int RuleCount { get; set; }
            public int IgnoredCount { get; set; }
        }

        private MaskBuildResult BuildIgnoreMaskWithValidation(int pinCount, int bitCount)
        {
            // 預設：不忽略任何點
            var mask = new BitArray[pinCount];
            for (int p = 0; p < pinCount; p++)
                mask[p] = new BitArray(bitCount, false);

            // 沒啟用 mask
            if (MaskSource == MaskSourceMode.None)
                return new MaskBuildResult { Success = true, Mask = mask, RuleCount = 0, IgnoredCount = 0 };

            // 啟用 InlineText，但內容空：視為合法（等同沒遮罩）
            var spec = MaskInline ?? "";
            if (string.IsNullOrWhiteSpace(spec))
                return new MaskBuildResult { Success = true, Mask = mask, RuleCount = 0, IgnoredCount = 0 };

            // 解析 + 驗證
            var parse = ParseMaskSpec(spec, pinCount, bitCount);

            if (parse.Errors.Count > 0)
            {
                // 將錯誤完整列出
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Mask format errors:");
                foreach (var err in parse.Errors)
                    sb.AppendLine(err);
                LogMessage(sb.ToString(), MessageLevel.Error);

                return new MaskBuildResult { Success = false, Mask = mask, RuleCount = 0, IgnoredCount = 0 };
            }

            // 套用規則
            foreach (var rule in parse.Rules)
            {
                ApplyRule(mask, rule, pinCount, bitCount);
            }

            // 統計忽略點數
            int ignored = 0;
            for (int p = 0; p < pinCount; p++)
            {
                for (int b = 0; b < bitCount; b++)
                {
                    if (mask[p][b]) ignored++;
                }
            }

            LogMessage($"Compare mask applied. Rules={parse.Rules.Count}, IgnoredPoints={ignored}", MessageLevel.Info);

            return new MaskBuildResult
            {
                Success = true,
                Mask = mask,
                RuleCount = parse.Rules.Count,
                IgnoredCount = ignored
            };
        }

        private enum RuleKind { PinAllBits, BitAllPins, PinBits }

        private sealed class MaskRule
        {
            public int LineNo { get; set; }
            public string Raw { get; set; }
            public RuleKind Kind { get; set; }
            public List<int> Pins1Based { get; set; } = new List<int>(); // empty means "all pins" for BitAllPins
            public List<int> Bits1Based { get; set; } = new List<int>(); // empty means "all bits" for PinAllBits
        }

        private sealed class MaskParseResult
        {
            public List<MaskRule> Rules { get; } = new List<MaskRule>();
            public List<string> Errors { get; } = new List<string>();
        }

        private MaskParseResult ParseMaskSpec(string spec, int pinCount, int bitCount)
        {
            var result = new MaskParseResult();

            // 支援換行與 ; 分隔
            var normalized = (spec ?? "").Replace("\r\n", "\n");
            var chunks = normalized.Split(new[] { '\n', ';' }, StringSplitOptions.None);

            int logicalLine = 0;
            foreach (var chunk in chunks)
            {
                logicalLine++;
                var raw = (chunk ?? "").Trim();
                if (raw.Length == 0) continue;

                // 註解
                if (raw.StartsWith("#") || raw.StartsWith("//")) continue;

                // 不區分大小寫
                var r = raw.ToUpperInvariant();

                // 允許：P<sel> / B<sel> / P<sel>:B<sel>
                string pSel = null, bSel = null;

                if (r.Contains(":"))
                {
                    var parts = r.Split(new[] { ':' }, 2);
                    ReadSide(parts[0], ref pSel, ref bSel);
                    ReadSide(parts[1], ref pSel, ref bSel);
                }
                else
                {
                    ReadSide(r, ref pSel, ref bSel);
                }

                if (pSel == null && bSel == null)
                {
                    result.Errors.Add($"Line {logicalLine}: Unrecognized rule '{raw}'. Expect P... or B... or P...:B...");
                    continue;
                }

                // PinAllBits：P<sel> only
                if (pSel != null && bSel == null)
                {
                    var pins = ExpandSelector(pSel, pinCount, "P", logicalLine, result.Errors);
                    if (pins.Count == 0)
                    {
                        result.Errors.Add($"Line {logicalLine}: P selector yields no pins. Rule='{raw}'");
                        continue;
                    }

                    result.Rules.Add(new MaskRule
                    {
                        LineNo = logicalLine,
                        Raw = raw,
                        Kind = RuleKind.PinAllBits,
                        Pins1Based = pins
                    });
                    continue;
                }

                // BitAllPins：B<sel> only
                if (pSel == null && bSel != null)
                {
                    var bits = ExpandSelector(bSel, bitCount, "B", logicalLine, result.Errors);
                    if (bits.Count == 0)
                    {
                        result.Errors.Add($"Line {logicalLine}: B selector yields no bits. Rule='{raw}'");
                        continue;
                    }

                    result.Rules.Add(new MaskRule
                    {
                        LineNo = logicalLine,
                        Raw = raw,
                        Kind = RuleKind.BitAllPins,
                        Bits1Based = bits
                    });
                    continue;
                }

                // PinBits：P<sel>:B<sel>
                if (pSel != null && bSel != null)
                {
                    var pins = ExpandSelector(pSel, pinCount, "P", logicalLine, result.Errors);
                    var bits = ExpandSelector(bSel, bitCount, "B", logicalLine, result.Errors);

                    if (pins.Count == 0)
                    {
                        result.Errors.Add($"Line {logicalLine}: P selector yields no pins. Rule='{raw}'");
                        continue;
                    }
                    if (bits.Count == 0)
                    {
                        result.Errors.Add($"Line {logicalLine}: B selector yields no bits. Rule='{raw}'");
                        continue;
                    }

                    result.Rules.Add(new MaskRule
                    {
                        LineNo = logicalLine,
                        Raw = raw,
                        Kind = RuleKind.PinBits,
                        Pins1Based = pins,
                        Bits1Based = bits
                    });
                    continue;
                }
            }

            return result;
        }

        private void ReadSide(string side, ref string pSel, ref string bSel)
        {
            var s = (side ?? "").Trim();
            if (s.Length == 0) return;

            if (s.StartsWith("P"))
                pSel = s.Substring(1).Trim();
            else if (s.StartsWith("B"))
                bSel = s.Substring(1).Trim();
        }

        /// <summary>
        /// 展開 selector：支援 "*", "1", "1-10", "1,3,5"
        /// 回傳 1-based index list（去重）
        /// </summary>
        private List<int> ExpandSelector(string sel, int max, string label, int lineNo, List<string> errors)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(sel))
            {
                errors.Add($"Line {lineNo}: Missing selector after '{label}'.");
                return list;
            }

            sel = sel.Trim();

            if (sel == "*")
            {
                list.AddRange(Enumerable.Range(1, max));
                return list;
            }

            var tokens = sel.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .Where(t => t.Length > 0);

            foreach (var t in tokens)
            {
                if (t.Contains("-"))
                {
                    var ab = t.Split(new[] { '-' }, 2);
                    if (!int.TryParse(ab[0], out int a) || !int.TryParse(ab[1], out int b))
                    {
                        errors.Add($"Line {lineNo}: Invalid range '{label}{t}'.");
                        continue;
                    }
                    if (a > b) { var tmp = a; a = b; b = tmp; }

                    if (b < 1 || a > max)
                    {
                        errors.Add($"Line {lineNo}: Range out of bounds '{label}{t}' (valid 1..{max}).");
                        continue;
                    }

                    a = Math.Max(1, a);
                    b = Math.Min(max, b);
                    for (int i = a; i <= b; i++) list.Add(i);
                }
                else
                {
                    if (!int.TryParse(t, out int v))
                    {
                        errors.Add($"Line {lineNo}: Invalid number '{label}{t}'.");
                        continue;
                    }
                    if (v < 1 || v > max)
                    {
                        errors.Add($"Line {lineNo}: Value out of bounds '{label}{v}' (valid 1..{max}).");
                        continue;
                    }
                    list.Add(v);
                }
            }

            return list.Distinct().ToList();
        }

        private void ApplyRule(BitArray[] mask, MaskRule rule, int pinCount, int bitCount)
        {
            switch (rule.Kind)
            {
                case RuleKind.PinAllBits:
                    foreach (var p1 in rule.Pins1Based)
                    {
                        int p = p1 - 1;
                        if (p >= 0 && p < pinCount)
                            mask[p].SetAll(true);
                    }
                    break;

                case RuleKind.BitAllPins:
                    foreach (var b1 in rule.Bits1Based)
                    {
                        int b = b1 - 1;
                        if (b < 0 || b >= bitCount) continue;
                        for (int p = 0; p < pinCount; p++)
                            mask[p][b] = true;
                    }
                    break;

                case RuleKind.PinBits:
                    foreach (var p1 in rule.Pins1Based)
                    {
                        int p = p1 - 1;
                        if (p < 0 || p >= pinCount) continue;

                        foreach (var b1 in rule.Bits1Based)
                        {
                            int b = b1 - 1;
                            if (b < 0 || b >= bitCount) continue;
                            mask[p][b] = true;
                        }
                    }
                    break;
            }
        }

        // =========================
        // Compare (with mask)
        // =========================
        private List<(int pin, int bit, int expected, int actual)> CompareBitArrays(int[,] testArray, int[,] goldenArray, BitArray[] ignoreMask)
        {
            var diffs = new List<(int pin, int bit, int expected, int actual)>();

            try
            {
                int pinCount = testArray.GetLength(0);
                int bitCount = testArray.GetLength(1);

                for (int pin = 0; pin < pinCount; pin++)
                {
                    var ignoreRow = (ignoreMask != null && pin < ignoreMask.Length) ? ignoreMask[pin] : null;

                    for (int bit = 0; bit < bitCount; bit++)
                    {
                        // 遮罩：true -> 跳過
                        if (ignoreRow != null && ignoreRow[bit]) continue;

                        if (testArray[pin, bit] != goldenArray[pin, bit])
                            diffs.Add((pin + 1, bit + 1, goldenArray[pin, bit], testArray[pin, bit]));
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"CompareBitArrays Exception: {ex.Message}", MessageLevel.Error);
            }

            return diffs;
        }
    }
}
