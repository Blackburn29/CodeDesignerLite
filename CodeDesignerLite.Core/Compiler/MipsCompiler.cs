using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodeDesignerLite.Core.Enums;
using CodeDesignerLite.Core.Models;
using CodeDesignerLite.Core.Services;

namespace CodeDesignerLite.Core.Compiler
{
    /// <summary>
    /// Two-pass MIPS assembly compiler that converts MIPS assembly code into machine code.
    /// Supports R-type, I-type, J-type instructions, FPU operations, pseudo-instructions, and directives.
    /// </summary>
    public class MipsCompiler
    {
        private readonly Dictionary<string, int> mipsRegisters;
        private readonly Dictionary<string, MipsOpInfo> mipsOps;
        private readonly OperandParser operandParser;
        private readonly ImportProcessor importProcessor;

        /// <summary>
        /// Initializes a new instance of the MipsCompiler class.
        /// </summary>
        public MipsCompiler()
        {
            mipsRegisters = MipsRegisters.Initialize();
            operandParser = new OperandParser(mipsRegisters);
            importProcessor = new ImportProcessor();
            mipsOps = new Dictionary<string, MipsOpInfo>(StringComparer.OrdinalIgnoreCase);
            InitializeMipsInstructions();
        }

        /// <summary>
        /// Compiles MIPS assembly code into machine code asynchronously.
        /// </summary>
        /// <param name="inputLines">Array of input assembly lines to compile</param>
        /// <param name="currentFilePath">Path to the current file being compiled</param>
        /// <param name="outputMode">Output format mode (PS2 or Pnach)</param>
        /// <param name="addressFormatChar">Character to use for address formatting</param>
        /// <returns>A CompilationResult containing output, errors, and status</returns>
        public async Task<CompilationResult> CompileAsync(string[] inputLines, string currentFilePath, OutputFormatMode outputMode, string addressFormatChar)
        {
            return await Task.Run(() => Compile(inputLines, currentFilePath, outputMode, addressFormatChar));
        }

        /// <summary>
        /// Synchronous compilation method that performs the actual two-pass compilation.
        /// </summary>
        private CompilationResult Compile(string[] inputLines, string currentFilePath, OutputFormatMode outputMode, string addressFormatChar)
        {
            var result = new CompilationResult();
            var errors = new List<MipsErrorInfo>();
            var processedLineInfos = new List<LineSourceInfo>();

            string mainFileName = currentFilePath ?? "main_input.asm";
            int globalLineCounter = 0;

            // Preprocess imports
            try
            {
                string baseDir = currentFilePath != null ? Path.GetDirectoryName(currentFilePath) : Directory.GetCurrentDirectory();
                processedLineInfos = importProcessor.PreprocessImports(inputLines, mainFileName, baseDir, ref globalLineCounter);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Output = $"Error during import preprocessing: {ex.Message}";
                result.ErrorCount = 1;
                return result;
            }

            var labels = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            bool blockCommentActiveForCompilerPass = false;

            // PASS 1: Build label table and calculate addresses
            uint tempAddress = 0;
            for (int idx = 0; idx < processedLineInfos.Count; idx++)
            {
                LineSourceInfo lineInfo = processedLineInfos[idx];
                string effectiveLineContent = CommentStripper.StripCommentsFromLine(lineInfo.Text, ref blockCommentActiveForCompilerPass);

                if (string.IsNullOrEmpty(effectiveLineContent)) continue;

                string lowerLine = effectiveLineContent.ToLower();
                bool isMain = lineInfo.FileName == mainFileName;

                // Handle address directive
                if (lowerLine.StartsWith("address"))
                {
                    string[] parts = effectiveLineContent.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        string addrStr = parts[1].StartsWith("$") ? parts[1].Substring(1) : parts[1];
                        if (uint.TryParse(addrStr, NumberStyles.HexNumber, null, out uint parsedAddr))
                            tempAddress = parsedAddr;
                        else
                            errors.Add(new MipsErrorInfo(lineInfo.FileName, lineInfo.OriginalLineNumber, lineInfo.GlobalIndex, tempAddress, "N/A", $"Invalid address '{parts[1]}'", lineInfo.Text, isMain));
                    }
                    continue;
                }

                // Handle print directive
                else if (lowerLine.StartsWith("print"))
                {
                    Match strMatch = Regex.Match(effectiveLineContent, @"print\s+""((?:\\.|[^""])*)""", RegexOptions.IgnoreCase);
                    if (strMatch.Success)
                    {
                        string str = strMatch.Groups[1].Value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\t", "\t");
                        tempAddress += (uint)Math.Ceiling(str.Length / 4.0) * 4;
                    }
                    else
                        errors.Add(new MipsErrorInfo(lineInfo.FileName, lineInfo.OriginalLineNumber, lineInfo.GlobalIndex, tempAddress, "N/A", $"Invalid print: {effectiveLineContent}", lineInfo.Text, isMain));
                    continue;
                }
                else if (lowerLine.StartsWith("hexcode") || lowerLine.StartsWith("float")) { tempAddress += 4; continue; }
                else if (lowerLine.StartsWith("setreg")) { tempAddress += 8; continue; }

                // Handle label definitions
                Match labelMatch = Regex.Match(effectiveLineContent, @"^([\w.:]+):");
                if (labelMatch.Success)
                {
                    string labelName = labelMatch.Groups[1].Value;
                    if (labels.ContainsKey(labelName))
                        errors.Add(new MipsErrorInfo(lineInfo.FileName, lineInfo.OriginalLineNumber, lineInfo.GlobalIndex, tempAddress, "N/A", $"Duplicate label '{labelName}'", lineInfo.Text, isMain));
                    else
                        labels[labelName] = tempAddress;

                    if (effectiveLineContent.Length > labelMatch.Length + 1)
                    {
                        effectiveLineContent = effectiveLineContent.Substring(labelMatch.Length + 1).Trim();
                    }
                    else
                    {
                        effectiveLineContent = "";
                    }
                    if (string.IsNullOrEmpty(effectiveLineContent)) continue;
                }

                if (!string.IsNullOrEmpty(effectiveLineContent)) tempAddress += 4;
            }

            // Return if Pass 1 had errors
            if (errors.Any())
            {
                result.Success = false;
                result.Errors = errors;
                result.ErrorCount = errors.Count;
                result.ErrorLineNumbers = errors.Where(e => e.IsFromMainInput).Select(e => e.OriginalLineNumber).Distinct().ToList();
                result.Output = "Errors (Pass 1):\n" + string.Join("\n", errors.Select(e => e.ToString()));
                return result;
            }

            // PASS 2: Generate machine code
            var outputLines = new List<string>();
            uint currentAddress = 0;
            blockCommentActiveForCompilerPass = false;

            for (int idx = 0; idx < processedLineInfos.Count; idx++)
            {
                LineSourceInfo lineInfo = processedLineInfos[idx];
                string originalLineForErrorDisplay = lineInfo.Text.Trim();
                bool isMain = lineInfo.FileName == mainFileName;
                string attemptedData = "N/A";

                try
                {
                    string effectiveLineContent = CommentStripper.StripCommentsFromLine(lineInfo.Text, ref blockCommentActiveForCompilerPass);
                    if (string.IsNullOrEmpty(effectiveLineContent)) continue;

                    string lowerLine = effectiveLineContent.ToLower();

                    // Handle address directive
                    if (lowerLine.StartsWith("address"))
                    {
                        string[] parts = effectiveLineContent.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                        {
                            string addrStr = parts[1].StartsWith("$") ? parts[1].Substring(1) : parts[1];
                            if (!uint.TryParse(addrStr, NumberStyles.HexNumber, null, out currentAddress))
                                throw new Exception($"Invalid address '{parts[1]}'");
                        }
                        continue;
                    }

                    // Handle print directive
                    else if (lowerLine.StartsWith("print"))
                    {
                        Match strMatch = Regex.Match(effectiveLineContent, @"print\s+""((?:\\.|[^""])*)""", RegexOptions.IgnoreCase);
                        if (strMatch.Success)
                        {
                            string str = strMatch.Groups[1].Value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\t", "\t");
                            for (int k = 0; k < str.Length; k += 4)
                            {
                                string chunk = str.Substring(k, Math.Min(4, str.Length - k));
                                byte[] bytes = new byte[4];
                                Encoding.GetEncoding("ISO-8859-1").GetBytes(chunk, 0, chunk.Length, bytes, 0);
                                uint wordValue = BitConverter.ToUInt32(bytes, 0);
                                string hexWord = wordValue.ToString("X8");
                                outputLines.Add(OutputFormatter.FormatOutputLine(currentAddress, hexWord, outputMode, addressFormatChar));
                                currentAddress += 4;
                            }
                        }
                        else
                        {
                            throw new Exception($"Invalid print syntax. Line: '{effectiveLineContent}'");
                        }
                        continue;
                    }

                    // Handle hexcode directive
                    else if (lowerLine.StartsWith("hexcode"))
                    {
                        string[] parts = effectiveLineContent.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) throw new Exception("Invalid hexcode syntax.");
                        string hexValueStr;
                        string operand = parts[1];

                        if (operand.StartsWith("$"))
                        {
                            hexValueStr = operand.Substring(1);
                            if (!Regex.IsMatch(hexValueStr, @"^[0-9a-fA-F]+$")) throw new Exception($"Invalid hex value: {operand}");
                        }
                        else if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            hexValueStr = operand.Substring(2);
                            if (!Regex.IsMatch(hexValueStr, @"^[0-9a-fA-F]+$")) throw new Exception($"Invalid hex value: {operand}");
                        }
                        else if (operand.StartsWith(":"))
                        {
                            string labelName = operand.Substring(1);
                            if (!labels.TryGetValue(labelName, out uint labelAddr)) throw new Exception($"Label '{labelName}' not found.");
                            hexValueStr = labelAddr.ToString("X");
                        }
                        else if (int.TryParse(operand, out int decVal))
                        {
                            hexValueStr = ((uint)decVal).ToString("X");
                        }
                        else
                        {
                            if (!labels.TryGetValue(operand, out uint labelAddr)) throw new Exception($"Label '{operand}' not found.");
                            hexValueStr = labelAddr.ToString("X");
                        }

                        hexValueStr = hexValueStr.ToUpper().PadLeft(8, '0');
                        if (hexValueStr.Length > 8) hexValueStr = hexValueStr.Substring(hexValueStr.Length - 8);
                        outputLines.Add(OutputFormatter.FormatOutputLine(currentAddress, hexValueStr, outputMode, addressFormatChar));
                        currentAddress += 4;
                        continue;
                    }

                    // Handle float directive
                    else if (lowerLine.StartsWith("float"))
                    {
                        string[] parts = effectiveLineContent.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) throw new Exception("Invalid float syntax.");
                        string floatStr = parts[1];
                        if (floatStr.StartsWith("$")) floatStr = floatStr.Substring(1);

                        if (!float.TryParse(floatStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal))
                        {
                            throw new Exception($"Invalid float value: {parts[1]}");
                        }
                        byte[] floatBytes = BitConverter.GetBytes(floatVal);
                        uint uintVal = BitConverter.ToUInt32(floatBytes, 0);
                        string hexWord = uintVal.ToString("X8");
                        outputLines.Add(OutputFormatter.FormatOutputLine(currentAddress, hexWord, outputMode, addressFormatChar));
                        currentAddress += 4;
                        continue;
                    }

                    // Handle label on its own line
                    Match labelMatch = Regex.Match(effectiveLineContent, @"^([\w.:]+):");
                    if (labelMatch.Success)
                    {
                        if (effectiveLineContent.Length > labelMatch.Length + 1)
                        {
                            effectiveLineContent = effectiveLineContent.Substring(labelMatch.Length + 1).Trim();
                        }
                        else
                        {
                            effectiveLineContent = "";
                        }
                        if (string.IsNullOrEmpty(effectiveLineContent)) continue;
                    }

                    // Parse instruction
                    string[] instructionParts = Regex.Split(effectiveLineContent, @"[,\s]+").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                    if (instructionParts.Length == 0) continue;
                    string mnemonic = instructionParts[0].ToLower();

                    // Parse operands (special handling for memory operations)
                    string[] ops;
                    if ((mnemonic.StartsWith("lw") || mnemonic.StartsWith("sw") || mnemonic.StartsWith("lb") || mnemonic.StartsWith("sb") ||
                         mnemonic.StartsWith("lh") || mnemonic.StartsWith("sh") || mnemonic.StartsWith("ld") || mnemonic.StartsWith("sd") ||
                         mnemonic.StartsWith("lq") || mnemonic.StartsWith("sq") || mnemonic.StartsWith("lwc1") || mnemonic.StartsWith("swc1") ||
                         mnemonic.StartsWith("ldc1") || mnemonic.StartsWith("sdc1"))
                        && effectiveLineContent.Contains("(") && effectiveLineContent.Contains(")"))
                    {
                        ops = new string[2];
                        ops[0] = instructionParts[1];
                        int firstCommaIndex = effectiveLineContent.IndexOf(',');
                        if (firstCommaIndex != -1 && firstCommaIndex + 1 < effectiveLineContent.Length)
                        {
                            ops[1] = effectiveLineContent.Substring(firstCommaIndex + 1).Trim();
                        }
                        else
                        {
                            throw new Exception($"Missing comma in memory operand for {mnemonic}: {originalLineForErrorDisplay}");
                        }
                    }
                    else
                    {
                        ops = instructionParts.Skip(1).ToArray();
                    }

                    if (!mipsOps.TryGetValue(mnemonic, out MipsOpInfo instrInfo) || instrInfo == null)
                        throw new Exception($"unk cmd: {mnemonic}");

                    uint machineCode = 0;

                    // Encode instruction based on type
                    if (instrInfo.Type == "CUSTOM")
                        machineCode = instrInfo.CustomValue;

                    else if (instrInfo.Type == "PSEUDO_SETREG")
                    {
                        machineCode = EncodePseudoSetreg(ops, labels, originalLineForErrorDisplay, outputLines, ref currentAddress, outputMode, addressFormatChar);
                        continue;
                    }

                    else if (instrInfo.Type == "PSEUDO_BRANCH")
                    {
                        uint targetAddr = (uint)operandParser.ParseOperand(ops[0], labels);
                        int offset = (int)(targetAddr - (currentAddress + 4)) / 4;
                        if (offset < -32768 || offset > 32767) throw new Exception("Branch offset out of range for 'b' instruction.");
                        machineCode = (mipsOps["beq"].Opcode << 26) | (0 << 21) | (0 << 16) | ((uint)offset & 0xFFFF);
                    }

                    else if (instrInfo.Type == "R")
                    {
                        machineCode = EncodeRType(instrInfo, ops, labels, mnemonic);
                    }

                    else if (instrInfo.Type == "R_JALR")
                    {
                        machineCode = EncodeRJalr(instrInfo, ops, labels, mnemonic);
                    }

                    else if (instrInfo.Type == "R_SHIFT" || instrInfo.Type == "R_SHIFT_PLUS32")
                    {
                        machineCode = EncodeRShift(instrInfo, ops, labels);
                    }

                    else if (instrInfo.Type == "R_SHIFT_V")
                    {
                        machineCode = EncodeRShiftV(instrInfo, ops, labels);
                    }

                    else if (instrInfo.Type == "R_MULTDIV")
                    {
                        machineCode = EncodeRMultDiv(instrInfo, ops, labels, mnemonic, originalLineForErrorDisplay);
                    }

                    else if (instrInfo.Type == "R_MFHI_MFLO")
                    {
                        machineCode = (instrInfo.Opcode << 26) | instrInfo.Funct;
                        int rd_val = operandParser.ParseOperand(ops[0], labels);
                        machineCode |= ((uint)rd_val & 0x1F) << 11;
                    }

                    else if (instrInfo.Type == "R_MTHI_MTLO")
                    {
                        machineCode = (instrInfo.Opcode << 26) | instrInfo.Funct;
                        int rs_val = operandParser.ParseOperand(ops[0], labels);
                        machineCode |= ((uint)rs_val & 0x1F) << 21;
                    }

                    else if (instrInfo.Type == "R_SYSCALL_BREAK" || instrInfo.Type == "R_SYNC")
                    {
                        machineCode = (instrInfo.Opcode << 26) | instrInfo.Funct;
                        if (ops.Length > 0)
                        {
                            int code = operandParser.ParseOperand(ops[0], labels);
                            machineCode |= ((uint)code & 0x03FFFFF) << 6;
                        }
                    }

                    else if (instrInfo.Type == "R_ERET")
                    {
                        machineCode = (instrInfo.Opcode << 26) | (1 << 25) | instrInfo.Funct;
                    }

                    else if (instrInfo.Type == "I" || instrInfo.Type == "I_LD_SD")
                    {
                        machineCode = EncodeIType(instrInfo, ops, labels, mnemonic, originalLineForErrorDisplay);
                    }

                    else if (instrInfo.Type == "I_BRANCH" || instrInfo.Type == "I_BRANCH_LIKELY")
                    {
                        machineCode = EncodeIBranch(instrInfo, ops, labels, currentAddress);
                    }

                    else if (instrInfo.Type == "I_BRANCH_RS_ZERO")
                    {
                        machineCode = EncodeIBranchRsZero(instrInfo, ops, labels, currentAddress);
                    }

                    else if (instrInfo.Type == "I_BRANCH_RS_RTFMT")
                    {
                        machineCode = EncodeIBranchRsRtFmt(instrInfo, ops, labels, currentAddress);
                    }

                    else if (instrInfo.Type == "COP0_MOV" || instrInfo.Type == "COP0_MOV_D")
                    {
                        machineCode = (instrInfo.Opcode << 26);
                        machineCode |= (instrInfo.Fmt & 0x1F) << 21;
                        int rt_val = operandParser.ParseOperand(ops[0], labels);
                        int rd_cop_val = operandParser.ParseOperand(ops[1], labels);
                        machineCode |= ((uint)rt_val & 0x1F) << 16;
                        machineCode |= ((uint)rd_cop_val & 0x1F) << 11;
                    }

                    else if (instrInfo.Type == "IFPU_LS" || instrInfo.Type == "IFPU_LS_D")
                    {
                        machineCode = (instrInfo.Opcode << 26);
                        int ft_val = operandParser.ParseOperand(ops[0], labels);
                        var mem = operandParser.ParseMemOffset(ops[1], labels);
                        machineCode |= ((uint)mem.rs & 0x1F) << 21;
                        machineCode |= ((uint)ft_val & 0x1F) << 16;
                        machineCode |= ((uint)mem.imm & 0xFFFF);
                    }

                    else if (instrInfo.Type == "FPU_MOV")
                    {
                        machineCode = EncodeFpuMov(instrInfo, ops, labels, mnemonic, originalLineForErrorDisplay);
                    }

                    else if (instrInfo.Type == "FPU_R")
                    {
                        machineCode = (instrInfo.Opcode << 26) | (instrInfo.Fmt << 21) | instrInfo.Funct;
                        int fd_val = operandParser.ParseOperand(ops[0], labels);
                        int fs_val = operandParser.ParseOperand(ops[1], labels);
                        int ft_val = operandParser.ParseOperand(ops[2], labels);
                        machineCode |= ((uint)ft_val & 0x1F) << 16;
                        machineCode |= ((uint)fs_val & 0x1F) << 11;
                        machineCode |= ((uint)fd_val & 0x1F) << 6;
                    }

                    else if (instrInfo.Type == "FPU_R_UN")
                    {
                        machineCode = EncodeFpuRUn(instrInfo, ops, labels, mnemonic, originalLineForErrorDisplay);
                    }

                    else if (instrInfo.Type == "FPU_CVT" || instrInfo.Type == "FPU_CVT_D" || instrInfo.Type == "FPU_CVT_S" || instrInfo.Type == "FPU_CVT_L")
                    {
                        machineCode = (instrInfo.Opcode << 26) | (instrInfo.Fmt << 21) | instrInfo.Funct;
                        int fd_val = operandParser.ParseOperand(ops[0], labels);
                        int fs_val = operandParser.ParseOperand(ops[1], labels);
                        machineCode |= (0 & 0x1F) << 16;
                        machineCode |= ((uint)fs_val & 0x1F) << 11;
                        machineCode |= ((uint)fd_val & 0x1F) << 6;
                    }

                    else if (instrInfo.Type == "FPU_CMP")
                    {
                        machineCode = (instrInfo.Opcode << 26) | (instrInfo.Fmt << 21) | instrInfo.Funct;
                        int fs_val = operandParser.ParseOperand(ops[0], labels);
                        int ft_val = operandParser.ParseOperand(ops[1], labels);
                        machineCode |= ((uint)ft_val & 0x1F) << 16;
                        machineCode |= ((uint)fs_val & 0x1F) << 11;
                    }

                    else if (instrInfo.Type == "FPU_BRANCH")
                    {
                        machineCode = (instrInfo.Opcode << 26) | (instrInfo.Fmt << 21) | (instrInfo.CcBit << 16);
                        uint targetAddr = (uint)operandParser.ParseOperand(ops[0], labels);
                        int offset = (int)(targetAddr - (currentAddress + 4)) / 4;
                        if (offset < -32768 || offset > 32767) throw new Exception("Branch offset out of range.");
                        machineCode |= ((uint)offset & 0xFFFF);
                    }

                    else if (instrInfo.Type == "J")
                    {
                        machineCode = (instrInfo.Opcode << 26);
                        uint targetAddr = (uint)operandParser.ParseOperand(ops[0], labels);
                        machineCode |= (targetAddr >> 2) & 0x03FFFFFF;
                    }

                    else
                    {
                        throw new Exception($"Encoding for instruction type '{instrInfo.Type}' not fully implemented for '{mnemonic}'.");
                    }

                    attemptedData = machineCode.ToString("X8");
                    string hexMachineCode = machineCode.ToString("X8");
                    outputLines.Add(OutputFormatter.FormatOutputLine(currentAddress, hexMachineCode, outputMode, addressFormatChar));
                    currentAddress += 4;
                }
                catch (Exception ex)
                {
                    errors.Add(new MipsErrorInfo(lineInfo.FileName, lineInfo.OriginalLineNumber, lineInfo.GlobalIndex, currentAddress, attemptedData, ex.Message, originalLineForErrorDisplay, isMain));
                }
            }

            // Build final result
            if (errors.Any())
            {
                result.Success = false;
                result.Errors = errors;
                result.ErrorCount = errors.Count;
                result.ErrorLineNumbers = errors.Where(e => e.IsFromMainInput).Select(e => e.OriginalLineNumber).Distinct().ToList();
                result.Output = outputLines.Any() ? string.Join("\n", outputLines) : string.Empty;
            }
            else if (outputLines.Any())
            {
                result.Success = true;
                result.Output = string.Join("\n", outputLines);
            }
            else if (processedLineInfos.Any(l => !string.IsNullOrWhiteSpace(l.Text) && !l.Text.Trim().StartsWith("//") && !l.Text.Trim().StartsWith("#")))
            {
                result.Success = false;
                result.Output = "No compilable instructions or data found after imports.";
            }
            else
            {
                result.Success = false;
                result.Output = "No input provided.";
            }

            return result;
        }

        #region Instruction Initialization

        /// <summary>
        /// Initializes all 100+ MIPS instruction definitions with their opcodes, function codes, and types.
        /// </summary>
        private void InitializeMipsInstructions()
        {
            // R-type instructions
            mipsOps["add"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x20 };
            mipsOps["addu"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x21 };
            mipsOps["sub"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x22 };
            mipsOps["subu"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x23 };
            mipsOps["and"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x24 };
            mipsOps["or"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x25 };
            mipsOps["xor"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x26 };
            mipsOps["nor"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x27 };
            mipsOps["slt"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x2A };
            mipsOps["sltu"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x2B };

            // Shift instructions
            mipsOps["sll"] = new MipsOpInfo { Type = "R_SHIFT", Opcode = 0x00, Funct = 0x00 };
            mipsOps["srl"] = new MipsOpInfo { Type = "R_SHIFT", Opcode = 0x00, Funct = 0x02 };
            mipsOps["sra"] = new MipsOpInfo { Type = "R_SHIFT", Opcode = 0x00, Funct = 0x03 };
            mipsOps["sllv"] = new MipsOpInfo { Type = "R_SHIFT_V", Opcode = 0x00, Funct = 0x04 };
            mipsOps["srlv"] = new MipsOpInfo { Type = "R_SHIFT_V", Opcode = 0x00, Funct = 0x06 };
            mipsOps["srav"] = new MipsOpInfo { Type = "R_SHIFT_V", Opcode = 0x00, Funct = 0x07 };

            // Jump register instructions
            mipsOps["jr"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x08 };
            mipsOps["jalr"] = new MipsOpInfo { Type = "R_JALR", Opcode = 0x00, Funct = 0x09 };

            // Multiply/Divide instructions
            mipsOps["mult"] = new MipsOpInfo { Type = "R_MULTDIV", Opcode = 0x00, Funct = 0x18 };
            mipsOps["multu"] = new MipsOpInfo { Type = "R_MULTDIV", Opcode = 0x00, Funct = 0x19 };
            mipsOps["div"] = new MipsOpInfo { Type = "R_MULTDIV", Opcode = 0x00, Funct = 0x1A };
            mipsOps["divu"] = new MipsOpInfo { Type = "R_MULTDIV", Opcode = 0x00, Funct = 0x1B };
            mipsOps["mfhi"] = new MipsOpInfo { Type = "R_MFHI_MFLO", Opcode = 0x00, Funct = 0x10 };
            mipsOps["mflo"] = new MipsOpInfo { Type = "R_MFHI_MFLO", Opcode = 0x00, Funct = 0x12 };
            mipsOps["mthi"] = new MipsOpInfo { Type = "R_MTHI_MTLO", Opcode = 0x00, Funct = 0x11 };
            mipsOps["mtlo"] = new MipsOpInfo { Type = "R_MTHI_MTLO", Opcode = 0x00, Funct = 0x13 };

            // System instructions
            mipsOps["syscall"] = new MipsOpInfo { Type = "R_SYSCALL_BREAK", Opcode = 0x00, Funct = 0x0C };
            mipsOps["break"] = new MipsOpInfo { Type = "R_SYSCALL_BREAK", Opcode = 0x00, Funct = 0x0D };
            mipsOps["sync"] = new MipsOpInfo { Type = "R_SYNC", Opcode = 0x00, Funct = 0x0F };
            mipsOps["eret"] = new MipsOpInfo { Type = "R_ERET", Opcode = 0x10, Funct = 0x18 };

            // 64-bit R-type instructions
            mipsOps["dadd"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x2C };
            mipsOps["daddu"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x2D };
            mipsOps["dsub"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x2E };
            mipsOps["dsubu"] = new MipsOpInfo { Type = "R", Opcode = 0x00, Funct = 0x2F };
            mipsOps["dsll"] = new MipsOpInfo { Type = "R_SHIFT", Opcode = 0x00, Funct = 0x38 };
            mipsOps["dsrl"] = new MipsOpInfo { Type = "R_SHIFT", Opcode = 0x00, Funct = 0x3A };
            mipsOps["dsra"] = new MipsOpInfo { Type = "R_SHIFT", Opcode = 0x00, Funct = 0x3B };
            mipsOps["dsllv"] = new MipsOpInfo { Type = "R_SHIFT_V", Opcode = 0x00, Funct = 0x14 };
            mipsOps["dsrlv"] = new MipsOpInfo { Type = "R_SHIFT_V", Opcode = 0x00, Funct = 0x16 };
            mipsOps["dsrav"] = new MipsOpInfo { Type = "R_SHIFT_V", Opcode = 0x00, Funct = 0x17 };
            mipsOps["dsll32"] = new MipsOpInfo { Type = "R_SHIFT_PLUS32", Opcode = 0x00, Funct = 0x3C };
            mipsOps["dsrl32"] = new MipsOpInfo { Type = "R_SHIFT_PLUS32", Opcode = 0x00, Funct = 0x3E };
            mipsOps["dsra32"] = new MipsOpInfo { Type = "R_SHIFT_PLUS32", Opcode = 0x00, Funct = 0x3F };
            mipsOps["dmult"] = new MipsOpInfo { Type = "R_MULTDIV", Opcode = 0x00, Funct = 0x1C };
            mipsOps["dmultu"] = new MipsOpInfo { Type = "R_MULTDIV", Opcode = 0x00, Funct = 0x1D };
            mipsOps["ddiv"] = new MipsOpInfo { Type = "R_MULTDIV", Opcode = 0x00, Funct = 0x1E };
            mipsOps["ddivu"] = new MipsOpInfo { Type = "R_MULTDIV", Opcode = 0x00, Funct = 0x1F };

            // I-type instructions
            mipsOps["addi"] = new MipsOpInfo { Type = "I", Opcode = 0x08 };
            mipsOps["addiu"] = new MipsOpInfo { Type = "I", Opcode = 0x09 };
            mipsOps["andi"] = new MipsOpInfo { Type = "I", Opcode = 0x0C };
            mipsOps["ori"] = new MipsOpInfo { Type = "I", Opcode = 0x0D };
            mipsOps["xori"] = new MipsOpInfo { Type = "I", Opcode = 0x0E };
            mipsOps["lui"] = new MipsOpInfo { Type = "I", Opcode = 0x0F };
            mipsOps["slti"] = new MipsOpInfo { Type = "I", Opcode = 0x0A };
            mipsOps["sltiu"] = new MipsOpInfo { Type = "I", Opcode = 0x0B };
            mipsOps["daddi"] = new MipsOpInfo { Type = "I", Opcode = 0x18 };
            mipsOps["daddiu"] = new MipsOpInfo { Type = "I", Opcode = 0x19 };

            // Branch instructions
            mipsOps["beq"] = new MipsOpInfo { Type = "I_BRANCH", Opcode = 0x04 };
            mipsOps["bne"] = new MipsOpInfo { Type = "I_BRANCH", Opcode = 0x05 };
            mipsOps["beql"] = new MipsOpInfo { Type = "I_BRANCH_LIKELY", Opcode = 0x14 };
            mipsOps["bnel"] = new MipsOpInfo { Type = "I_BRANCH_LIKELY", Opcode = 0x15 };
            mipsOps["blez"] = new MipsOpInfo { Type = "I_BRANCH_RS_ZERO", Opcode = 0x06 };
            mipsOps["bgtz"] = new MipsOpInfo { Type = "I_BRANCH_RS_ZERO", Opcode = 0x07 };
            mipsOps["bltz"] = new MipsOpInfo { Type = "I_BRANCH_RS_RTFMT", Opcode = 0x01, RtField = 0x00 };
            mipsOps["bgez"] = new MipsOpInfo { Type = "I_BRANCH_RS_RTFMT", Opcode = 0x01, RtField = 0x01 };
            mipsOps["bltzal"] = new MipsOpInfo { Type = "I_BRANCH_RS_RTFMT", Opcode = 0x01, RtField = 0x10 };
            mipsOps["bgezal"] = new MipsOpInfo { Type = "I_BRANCH_RS_RTFMT", Opcode = 0x01, RtField = 0x11 };

            // Load/Store instructions
            mipsOps["sw"] = new MipsOpInfo { Type = "I", Opcode = 0x2B };
            mipsOps["lw"] = new MipsOpInfo { Type = "I", Opcode = 0x23 };
            mipsOps["lwu"] = new MipsOpInfo { Type = "I", Opcode = 0x27 };
            mipsOps["lb"] = new MipsOpInfo { Type = "I", Opcode = 0x20 };
            mipsOps["lbu"] = new MipsOpInfo { Type = "I", Opcode = 0x24 };
            mipsOps["lh"] = new MipsOpInfo { Type = "I", Opcode = 0x21 };
            mipsOps["lhu"] = new MipsOpInfo { Type = "I", Opcode = 0x25 };
            mipsOps["sb"] = new MipsOpInfo { Type = "I", Opcode = 0x28 };
            mipsOps["sh"] = new MipsOpInfo { Type = "I", Opcode = 0x29 };
            mipsOps["ld"] = new MipsOpInfo { Type = "I_LD_SD", Opcode = 0x37 };
            mipsOps["sd"] = new MipsOpInfo { Type = "I_LD_SD", Opcode = 0x3F };
            mipsOps["lq"] = new MipsOpInfo { Type = "I_LD_SD", Opcode = 0x1E };
            mipsOps["sq"] = new MipsOpInfo { Type = "I_LD_SD", Opcode = 0x1F };

            // J-type instructions
            mipsOps["j"] = new MipsOpInfo { Type = "J", Opcode = 0x02 };
            mipsOps["jal"] = new MipsOpInfo { Type = "J", Opcode = 0x03 };

            // Special instructions
            mipsOps["nop"] = new MipsOpInfo { Type = "CUSTOM", CustomValue = 0x00000000 };

            // Floating point load/store
            mipsOps["lwc1"] = new MipsOpInfo { Type = "IFPU_LS", Opcode = 0x31 };
            mipsOps["swc1"] = new MipsOpInfo { Type = "IFPU_LS", Opcode = 0x39 };
            mipsOps["ldc1"] = new MipsOpInfo { Type = "IFPU_LS_D", Opcode = 0x35 };
            mipsOps["sdc1"] = new MipsOpInfo { Type = "IFPU_LS_D", Opcode = 0x3D };

            // Coprocessor 0 move instructions
            mipsOps["mfc0"] = new MipsOpInfo { Type = "COP0_MOV", Opcode = 0x10, Fmt = 0x00 };
            mipsOps["mtc0"] = new MipsOpInfo { Type = "COP0_MOV", Opcode = 0x10, Fmt = 0x04 };

            // Floating point move instructions
            mipsOps["mfc1"] = new MipsOpInfo { Type = "FPU_MOV", Opcode = 0x11, Fmt = 0x00 };
            mipsOps["mtc1"] = new MipsOpInfo { Type = "FPU_MOV", Opcode = 0x11, Fmt = 0x04 };

            // Single precision FPU instructions
            mipsOps["add.s"] = new MipsOpInfo { Type = "FPU_R", Opcode = 0x11, Fmt = 0x10, Funct = 0x00 };
            mipsOps["sub.s"] = new MipsOpInfo { Type = "FPU_R", Opcode = 0x11, Fmt = 0x10, Funct = 0x01 };
            mipsOps["mul.s"] = new MipsOpInfo { Type = "FPU_R", Opcode = 0x11, Fmt = 0x10, Funct = 0x02 };
            mipsOps["div.s"] = new MipsOpInfo { Type = "FPU_R", Opcode = 0x11, Fmt = 0x10, Funct = 0x03 };
            mipsOps["abs.s"] = new MipsOpInfo { Type = "FPU_R_UN", Opcode = 0x11, Fmt = 0x10, Funct = 0x05 };
            mipsOps["mov.s"] = new MipsOpInfo { Type = "FPU_R_UN", Opcode = 0x11, Fmt = 0x10, Funct = 0x06 };
            mipsOps["neg.s"] = new MipsOpInfo { Type = "FPU_R_UN", Opcode = 0x11, Fmt = 0x10, Funct = 0x07 };
            mipsOps["sqrt.s"] = new MipsOpInfo { Type = "FPU_R_UN", Opcode = 0x11, Fmt = 0x10, Funct = 0x04 };

            // Double precision FPU instructions
            mipsOps["add.d"] = new MipsOpInfo { Type = "FPU_R", Opcode = 0x11, Fmt = 0x11, Funct = 0x00 };
            mipsOps["sub.d"] = new MipsOpInfo { Type = "FPU_R", Opcode = 0x11, Fmt = 0x11, Funct = 0x01 };
            mipsOps["mul.d"] = new MipsOpInfo { Type = "FPU_R", Opcode = 0x11, Fmt = 0x11, Funct = 0x02 };
            mipsOps["div.d"] = new MipsOpInfo { Type = "FPU_R", Opcode = 0x11, Fmt = 0x11, Funct = 0x03 };
            mipsOps["abs.d"] = new MipsOpInfo { Type = "FPU_R_UN", Opcode = 0x11, Fmt = 0x11, Funct = 0x05 };
            mipsOps["neg.d"] = new MipsOpInfo { Type = "FPU_R_UN", Opcode = 0x11, Fmt = 0x11, Funct = 0x07 };

            // FPU comparison instructions
            mipsOps["c.eq.s"] = new MipsOpInfo { Type = "FPU_CMP", Opcode = 0x11, Fmt = 0x10, Funct = 0x32 };
            mipsOps["c.lt.s"] = new MipsOpInfo { Type = "FPU_CMP", Opcode = 0x11, Fmt = 0x10, Funct = 0x34 };
            mipsOps["c.le.s"] = new MipsOpInfo { Type = "FPU_CMP", Opcode = 0x11, Fmt = 0x10, Funct = 0x36 };

            // FPU branch instructions
            mipsOps["bc1t"] = new MipsOpInfo { Type = "FPU_BRANCH", Opcode = 0x11, Fmt = 0x08, CcBit = 1 };
            mipsOps["bc1f"] = new MipsOpInfo { Type = "FPU_BRANCH", Opcode = 0x11, Fmt = 0x08, CcBit = 0 };

            // FPU conversion instructions
            mipsOps["cvt.s.w"] = new MipsOpInfo { Type = "FPU_CVT", Opcode = 0x11, Fmt = 0x14, Funct = 0x20 };
            mipsOps["cvt.w.s"] = new MipsOpInfo { Type = "FPU_CVT", Opcode = 0x11, Fmt = 0x10, Funct = 0x24 };
            mipsOps["cvt.d.s"] = new MipsOpInfo { Type = "FPU_CVT_D", Opcode = 0x11, Fmt = 0x10, Funct = 0x21 };
            mipsOps["cvt.s.d"] = new MipsOpInfo { Type = "FPU_CVT_S", Opcode = 0x11, Fmt = 0x11, Funct = 0x20 };
            mipsOps["cvt.d.w"] = new MipsOpInfo { Type = "FPU_CVT_D", Opcode = 0x11, Fmt = 0x14, Funct = 0x21 };
            mipsOps["cvt.w.d"] = new MipsOpInfo { Type = "FPU_CVT_S", Opcode = 0x11, Fmt = 0x11, Funct = 0x24 };
            mipsOps["cvt.l.s"] = new MipsOpInfo { Type = "FPU_CVT_L", Opcode = 0x11, Fmt = 0x10, Funct = 0x25 };
            mipsOps["cvt.s.l"] = new MipsOpInfo { Type = "FPU_CVT", Opcode = 0x11, Fmt = 0x15, Funct = 0x20 };
            mipsOps["cvt.l.d"] = new MipsOpInfo { Type = "FPU_CVT_L", Opcode = 0x11, Fmt = 0x11, Funct = 0x25 };
            mipsOps["cvt.d.l"] = new MipsOpInfo { Type = "FPU_CVT_D", Opcode = 0x11, Fmt = 0x15, Funct = 0x21 };

            // Pseudo-instructions
            mipsOps["setreg"] = new MipsOpInfo { Type = "PSEUDO_SETREG" };
            mipsOps["b"] = new MipsOpInfo { Type = "PSEUDO_BRANCH" };
        }

        #endregion

        #region Instruction Encoding Methods

        private uint EncodeRType(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels, string mnemonic)
        {
            uint machineCode = (instrInfo.Opcode << 26) | instrInfo.Funct;
            int rd_val = 0, rs_val = 0, rt_val = 0;

            if (mnemonic == "jr")
            {
                rs_val = operandParser.ParseOperand(ops[0], labels);
            }
            else
            {
                rd_val = operandParser.ParseOperand(ops[0], labels);
                rs_val = operandParser.ParseOperand(ops[1], labels);
                rt_val = operandParser.ParseOperand(ops[2], labels);
            }

            machineCode |= ((uint)rs_val & 0x1F) << 21;
            machineCode |= ((uint)rt_val & 0x1F) << 16;
            machineCode |= ((uint)rd_val & 0x1F) << 11;
            return machineCode;
        }

        private uint EncodeRJalr(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels, string mnemonic)
        {
            uint machineCode = (instrInfo.Opcode << 26) | instrInfo.Funct;
            int rs_val = 0;
            int rd_val = 31;

            if (ops.Length == 1)
            {
                rs_val = operandParser.ParseOperand(ops[0], labels);
            }
            else if (ops.Length == 2)
            {
                rd_val = operandParser.ParseOperand(ops[0], labels);
                rs_val = operandParser.ParseOperand(ops[1], labels);
            }
            else
            {
                throw new Exception($"Incorrect operands for {mnemonic}");
            }

            machineCode |= ((uint)rs_val & 0x1F) << 21;
            machineCode |= ((uint)rd_val & 0x1F) << 11;
            return machineCode;
        }

        private uint EncodeRShift(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels)
        {
            uint machineCode = (instrInfo.Opcode << 26) | instrInfo.Funct;
            int rd_val = operandParser.ParseOperand(ops[0], labels);
            int rt_val = operandParser.ParseOperand(ops[1], labels);
            int shamt_val = operandParser.ParseOperand(ops[2], labels);

            machineCode |= ((uint)rt_val & 0x1F) << 16;
            machineCode |= ((uint)rd_val & 0x1F) << 11;
            machineCode |= ((uint)shamt_val & 0x1F) << 6;
            return machineCode;
        }

        private uint EncodeRShiftV(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels)
        {
            uint machineCode = (instrInfo.Opcode << 26) | instrInfo.Funct;
            int rd_val = operandParser.ParseOperand(ops[0], labels);
            int rt_val = operandParser.ParseOperand(ops[1], labels);
            int rs_shift_val = operandParser.ParseOperand(ops[2], labels);

            machineCode |= ((uint)rs_shift_val & 0x1F) << 21;
            machineCode |= ((uint)rt_val & 0x1F) << 16;
            machineCode |= ((uint)rd_val & 0x1F) << 11;
            return machineCode;
        }

        private uint EncodeRMultDiv(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels, string mnemonic, string originalLineForErrorDisplay)
        {
            uint machineCode = (instrInfo.Opcode << 26) | instrInfo.Funct;

            if (ops.Length == 2)
            {
                if (string.IsNullOrEmpty(ops[0])) throw new Exception($"Missing source register rs for {mnemonic}: {originalLineForErrorDisplay}");
                if (string.IsNullOrEmpty(ops[1])) throw new Exception($"Missing source register rt for {mnemonic}: {originalLineForErrorDisplay}");

                int rs_val = operandParser.ParseOperand(ops[0], labels);
                int rt_val = operandParser.ParseOperand(ops[1], labels);

                machineCode |= ((uint)rs_val & 0x1F) << 21;
                machineCode |= ((uint)rt_val & 0x1F) << 16;
            }
            else if (ops.Length == 3)
            {
                if (string.IsNullOrEmpty(ops[0])) throw new Exception($"Missing destination register rd for {mnemonic}: {originalLineForErrorDisplay}");
                if (string.IsNullOrEmpty(ops[1])) throw new Exception($"Missing source register rs for {mnemonic}: {originalLineForErrorDisplay}");
                if (string.IsNullOrEmpty(ops[2])) throw new Exception($"Missing source register rt for {mnemonic}: {originalLineForErrorDisplay}");

                int rd_val = operandParser.ParseOperand(ops[0], labels);
                int rs_val = operandParser.ParseOperand(ops[1], labels);
                int rt_val = operandParser.ParseOperand(ops[2], labels);

                machineCode |= ((uint)rs_val & 0x1F) << 21;
                machineCode |= ((uint)rt_val & 0x1F) << 16;
                machineCode |= ((uint)rd_val & 0x1F) << 11;
            }
            else
            {
                throw new Exception($"Instruction {mnemonic} (type {instrInfo.Type}) expects 2 or 3 operands, but {ops.Length} were provided: {originalLineForErrorDisplay}");
            }

            return machineCode;
        }

        private uint EncodeIType(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels, string mnemonic, string originalLineForErrorDisplay)
        {
            uint machineCode = (instrInfo.Opcode << 26);
            int rt_val;
            int rs_val;
            int imm_val;
            string currentMnemonic = mnemonic.ToLower();

            if (currentMnemonic == "lui")
            {
                if (ops.Length != 2) throw new Exception($"LUI expects 2 operands (rt, immediate), got {ops.Length} for line: {originalLineForErrorDisplay}");
                rt_val = operandParser.ParseOperand(ops[0], labels);
                imm_val = operandParser.ParseOperand(ops[1], labels, true);
                rs_val = 0;
            }
            else if (ops.Length == 2 &&
                     (currentMnemonic == "lw" || currentMnemonic == "sw" || currentMnemonic == "lwu" ||
                      currentMnemonic == "lb" || currentMnemonic == "sb" || currentMnemonic == "lbu" ||
                      currentMnemonic == "lh" || currentMnemonic == "sh" || currentMnemonic == "lhu" ||
                      currentMnemonic == "ld" || currentMnemonic == "sd" || currentMnemonic == "lq" || currentMnemonic == "sq"))
            {
                if (string.IsNullOrEmpty(ops[0])) throw new Exception($"Missing destination/source register (rt) for {currentMnemonic}: {originalLineForErrorDisplay}");
                rt_val = operandParser.ParseOperand(ops[0], labels);

                if (string.IsNullOrEmpty(ops[1])) throw new Exception($"Missing memory operand (offset(base)) for {currentMnemonic}: {originalLineForErrorDisplay}");
                var mem = operandParser.ParseMemOffset(ops[1], labels);
                rs_val = mem.rs;
                imm_val = mem.imm;
            }
            else if (ops.Length == 3 && instrInfo.Type == "I")
            {
                if (string.IsNullOrEmpty(ops[0])) throw new Exception($"Missing destination register (rt) for {currentMnemonic}: {originalLineForErrorDisplay}");
                rt_val = operandParser.ParseOperand(ops[0], labels);

                if (string.IsNullOrEmpty(ops[1])) throw new Exception($"Missing source register (rs) for {currentMnemonic}: {originalLineForErrorDisplay}");
                rs_val = operandParser.ParseOperand(ops[1], labels);

                if (string.IsNullOrEmpty(ops[2])) throw new Exception($"Missing immediate value for {currentMnemonic}: {originalLineForErrorDisplay}");
                imm_val = operandParser.ParseOperand(ops[2], labels, true);
            }
            else
            {
                throw new Exception($"Unhandled or ambiguous operand structure for instruction '{currentMnemonic}' (Type: {instrInfo.Type}, Operands found: {ops.Length}): {originalLineForErrorDisplay}");
            }

            if (currentMnemonic == "lui")
            {
                machineCode |= ((uint)rs_val & 0x1F) << 21;
                machineCode |= ((uint)rt_val & 0x1F) << 16;
                machineCode |= ((uint)imm_val & 0xFFFF);
            }
            else
            {
                machineCode |= ((uint)rs_val & 0x1F) << 21;
                machineCode |= ((uint)rt_val & 0x1F) << 16;
                machineCode |= ((uint)imm_val & 0xFFFF);
            }

            return machineCode;
        }

        private uint EncodeIBranch(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels, uint currentAddress)
        {
            uint machineCode = (instrInfo.Opcode << 26);
            int rs_val = operandParser.ParseOperand(ops[0], labels);
            int rt_val = operandParser.ParseOperand(ops[1], labels);
            uint targetAddr = (uint)operandParser.ParseOperand(ops[2], labels);
            int offset = (int)(targetAddr - (currentAddress + 4)) / 4;
            if (offset < -32768 || offset > 32767) throw new Exception("Branch offset out of range.");

            machineCode |= ((uint)rs_val & 0x1F) << 21;
            machineCode |= ((uint)rt_val & 0x1F) << 16;
            machineCode |= ((uint)offset & 0xFFFF);
            return machineCode;
        }

        private uint EncodeIBranchRsZero(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels, uint currentAddress)
        {
            uint machineCode = (instrInfo.Opcode << 26);
            int rs_val = operandParser.ParseOperand(ops[0], labels);
            uint targetAddr = (uint)operandParser.ParseOperand(ops[1], labels);
            int offset = (int)(targetAddr - (currentAddress + 4)) / 4;
            if (offset < -32768 || offset > 32767) throw new Exception("Branch offset out of range.");

            machineCode |= ((uint)rs_val & 0x1F) << 21;
            machineCode |= (0 & 0x1F) << 16;
            machineCode |= ((uint)offset & 0xFFFF);
            return machineCode;
        }

        private uint EncodeIBranchRsRtFmt(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels, uint currentAddress)
        {
            uint machineCode = (instrInfo.Opcode << 26);
            int rs_val = operandParser.ParseOperand(ops[0], labels);
            uint targetAddr = (uint)operandParser.ParseOperand(ops[1], labels);
            int offset = (int)(targetAddr - (currentAddress + 4)) / 4;
            if (offset < -32768 || offset > 32767) throw new Exception("Branch offset out of range.");

            machineCode |= ((uint)rs_val & 0x1F) << 21;
            machineCode |= (instrInfo.RtField & 0x1F) << 16;
            machineCode |= ((uint)offset & 0xFFFF);
            return machineCode;
        }

        private uint EncodeFpuMov(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels, string mnemonic, string originalLineForErrorDisplay)
        {
            uint machineCode = (instrInfo.Opcode << 26) | (instrInfo.Fmt << 21);

            if (ops.Length < 2) throw new Exception($"Not enough operands for {mnemonic}: {originalLineForErrorDisplay}");

            string operand1String = ops[0];
            string operand2String = ops[1];

            int value1 = operandParser.ParseOperand(operand1String, labels);
            int value2 = operandParser.ParseOperand(operand2String, labels);

            int gprValue;
            int fprValue;

            bool op1IsFPR = MipsRegisters.IsFPRName(operand1String);
            bool op2IsFPR = MipsRegisters.IsFPRName(operand2String);

            if (op1IsFPR && !op2IsFPR)
            {
                fprValue = value1;
                gprValue = value2;
            }
            else if (!op1IsFPR && op2IsFPR)
            {
                gprValue = value1;
                fprValue = value2;
            }
            else if (op1IsFPR && op2IsFPR)
            {
                throw new Exception($"Instruction {mnemonic} requires one GPR and one FPR, but received two FPRs: '{operand1String}', '{operand2String}' from line: {originalLineForErrorDisplay}");
            }
            else
            {
                throw new Exception($"Instruction {mnemonic} requires one GPR and one FPR, but received two GPRs (or non-FPRs): '{operand1String}', '{operand2String}' from line: {originalLineForErrorDisplay}");
            }

            machineCode |= ((uint)gprValue & 0x1F) << 16;
            machineCode |= ((uint)fprValue & 0x1F) << 11;
            return machineCode;
        }

        private uint EncodeFpuRUn(MipsOpInfo instrInfo, string[] ops, Dictionary<string, uint> labels, string mnemonic, string originalLineForErrorDisplay)
        {
            uint machineCode = (instrInfo.Opcode << 26) | (instrInfo.Fmt << 21) | (instrInfo.Funct & 0x3F);

            if (ops.Length < 2) throw new Exception($"Not enough operands for {mnemonic}: {originalLineForErrorDisplay}");

            int fd_val = operandParser.ParseOperand(ops[0], labels);
            int fs_val = operandParser.ParseOperand(ops[1], labels);

            uint ft_field_val = 0;
            uint fs_field_val = (uint)fs_val;
            uint fd_field_val = (uint)fd_val;

            if (mnemonic.Equals("sqrt.s", StringComparison.OrdinalIgnoreCase) && fd_val == fs_val)
            {
                ft_field_val = (uint)fd_val;
                fs_field_val = 0;
            }

            machineCode |= (ft_field_val & 0x1F) << 16;
            machineCode |= (fs_field_val & 0x1F) << 11;
            machineCode |= (fd_field_val & 0x1F) << 6;
            return machineCode;
        }

        private uint EncodePseudoSetreg(string[] ops, Dictionary<string, uint> labels, string originalLineForErrorDisplay, List<string> outputLines, ref uint currentAddress, OutputFormatMode outputMode, string addressFormatChar)
        {
            if (ops.Length < 2) throw new Exception($"Not enough operands for setreg: {originalLineForErrorDisplay}");

            int rd = operandParser.ParseOperand(ops[0], labels, true);
            string valueString = ops[1];
            uint value32;

            if (valueString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                try { value32 = Convert.ToUInt32(valueString.Substring(2), 16); }
                catch (Exception ex) { throw new Exception($"Invalid hex value '{valueString}' for setreg: {ex.Message}"); }
            }
            else if (valueString.StartsWith("$"))
            {
                try { value32 = Convert.ToUInt32(valueString.Substring(1), 16); }
                catch (Exception ex) { throw new Exception($"Invalid hex value '{valueString}' for setreg: {ex.Message}"); }
            }
            else
            {
                string labelNameToTry = valueString.Trim(':');

                if (labels.TryGetValue(labelNameToTry, out value32))
                {
                    // Value successfully retrieved from label
                }
                else
                {
                    try
                    {
                        value32 = (uint)Convert.ToInt32(valueString, 10);
                    }
                    catch (FormatException)
                    {
                        throw new Exception($"Invalid value or unrecognized label '{valueString}' for setreg. Must be hex, decimal, or a defined label.");
                    }
                    catch (OverflowException)
                    {
                        try
                        {
                            value32 = Convert.ToUInt32(valueString, 10);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Value '{valueString}' for setreg is out of range or invalid: {ex.Message}");
                        }
                    }
                }
            }

            uint upper16 = (value32 >> 16) & 0xFFFF;
            uint lower16 = value32 & 0xFFFF;

            // LUI instruction
            uint luiCode = (mipsOps["lui"].Opcode << 26) | ((uint)rd << 16) | upper16;
            string hexLui = luiCode.ToString("X8");
            outputLines.Add(OutputFormatter.FormatOutputLine(currentAddress, hexLui, outputMode, addressFormatChar));
            currentAddress += 4;

            // ORI instruction
            uint oriCode = (mipsOps["ori"].Opcode << 26) | ((uint)rd << 21) | ((uint)rd << 16) | lower16;
            string hexCombine = oriCode.ToString("X8");
            outputLines.Add(OutputFormatter.FormatOutputLine(currentAddress, hexCombine, outputMode, addressFormatChar));
            currentAddress += 4;

            return 0; // Return value not used since we directly added to outputLines
        }

        #endregion
    }
}
