using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeDesignerLite.Core.Compiler
{
    public class OperandParser
    {
        private readonly Dictionary<string, int> mipsRegisters;

        public OperandParser(Dictionary<string, int> registers)
        {
            mipsRegisters = registers ?? throw new ArgumentNullException(nameof(registers));
        }

        public int ParseOperand(string op, Dictionary<string, uint> labels, bool isImmediateContext = false)
        {
            op = op.Trim();

            // PRIORITY 1: If this operand is in an immediate context and starts with '$',
            // it MUST be a hexadecimal value. This check comes before any register lookup for such strings.
            if (isImmediateContext && op.StartsWith("$"))
            {
                if (op.Length == 1) // Just "$" is invalid
                {
                    throw new ArgumentException($"Invalid immediate format: '{op}'");
                }
                try
                {
                    return Convert.ToInt32(op.Substring(1), 16); // Convert "value" part from hex
                }
                catch (FormatException) { throw new ArgumentException($"Invalid hex format for immediate value: '{op}'"); }
                catch (OverflowException) { throw new ArgumentException($"Hex immediate value out of range: '{op}'"); }
            }

            // PRIORITY 2: Try to parse as a register name using 'potentialReg'
            // (which is 'op' after your specific cleaning for register names, e.g., stripping colons)
            string potentialReg = op;
            // --- Paste your existing 'potentialReg' cleaning logic here ---
            // This logic usually handles cases where labels might have colons but are used in register positions.
            // Example snippet of your cleaning logic:
            if ((potentialReg.StartsWith(":") || potentialReg.StartsWith(";")) && potentialReg.Length > 1)
            {
                string strippedStart = potentialReg.Substring(1);
                if (mipsRegisters.ContainsKey(strippedStart.TrimEnd(':', ';'))) { potentialReg = strippedStart.TrimEnd(':', ';'); }
                else if (mipsRegisters.ContainsKey(potentialReg.TrimEnd(':', ';'))) { potentialReg = potentialReg.TrimEnd(':', ';'); }
            }
            if ((potentialReg.EndsWith(":") || potentialReg.EndsWith(";")) && potentialReg.Length > 1)
            {
                string strippedEnd = potentialReg.Substring(0, potentialReg.Length - 1);
                if (mipsRegisters.ContainsKey(strippedEnd)) { potentialReg = strippedEnd; }
            }
            potentialReg = potentialReg.Trim();
            // --- End of potentialReg cleaning ---

            if (mipsRegisters.TryGetValue(potentialReg, out int regNum))
            {
                return regNum; // Successfully parsed as a register
            }

            // PRIORITY 3: Try standard literal formats (0xHEX, non-immediate $, decimal)
            // 'op' is the original trimmed string for these checks.
            if (op.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (op.Length == 2) throw new ArgumentException($"Invalid 0x prefixed hex value: '{op}'"); // "0x" alone
                try { return Convert.ToInt32(op.Substring(2), 16); }
                catch (FormatException) { throw new ArgumentException($"Invalid 0x prefixed hex value: '{op}'"); }
                catch (OverflowException) { throw new ArgumentException($"0x prefixed hex value out of range: '{op}'"); }
            }

            // This '$' check is for non-immediate contexts (isImmediateContext was false).
            // If isImmediateContext was true, the $HEX was handled at PRIORITY 1.
            if (op.StartsWith("$")) // This implies isImmediateContext is false if this point is reached
            {
                if (op.Length == 1) throw new ArgumentException($"Invalid $ prefixed value: '{op}'"); // "$" alone
                                                                                                      // Check if it's a register name like "$a0" or "$4" (if "$4" is a key in mipsRegisters)
                if (mipsRegisters.TryGetValue(op, out regNum))
                {
                    return regNum;
                }
                // If not a register alias, then it's treated as a $HEX literal.
                try { return Convert.ToInt32(op.Substring(1), 16); }
                catch (FormatException) { throw new ArgumentException($"Invalid $ prefixed hex value: '{op}'"); }
                catch (OverflowException) { throw new ArgumentException($"$ prefixed hex value out of range: '{op}'"); }
            }

            if (int.TryParse(op, NumberStyles.Integer, CultureInfo.InvariantCulture, out int decVal))
            {
                return decVal; // Parsed as a decimal immediate
            }

            // PRIORITY 4: Try to parse as a label
            string labelNameToTry = op;
            if (labels.TryGetValue(labelNameToTry, out uint labelAddr)) return (int)labelAddr; // Direct match

            // Cleaned label lookup (use your most robust label name cleaning logic)
            bool changedByCleaning = false;
            if (labelNameToTry.StartsWith(":"))
            {
                labelNameToTry = labelNameToTry.Substring(1);
                changedByCleaning = true;
            }
            if (labelNameToTry.EndsWith(":") && labelNameToTry.Length > 0) // Check length before Substring
            {
                labelNameToTry = labelNameToTry.Substring(0, labelNameToTry.Length - 1);
                changedByCleaning = true;
            }
            else if (labelNameToTry.EndsWith(":") && labelNameToTry.Length == 0 && changedByCleaning) // Handles case like ":" -> ""
            {
                // Invalid label if it becomes empty after stripping colons from just ":" or "::"
            }


            if (!string.IsNullOrEmpty(labelNameToTry) && labels.TryGetValue(labelNameToTry, out labelAddr))
            {
                return (int)labelAddr;
            }
            // If cleaning didn't help, and original op wasn't empty and wasn't already tried (it was).

            throw new ArgumentException($"Invalid operand, value, or unknown label: '{op}'");
        }


        public (int imm, int rs) ParseMemOffset(string memStr, Dictionary<string, uint> labels)
        {
            var match = Regex.Match(memStr, @"([$0-9a-fA-FxX.:\w-]+)\s*\(\s*([$\w.:]+)\s*\)"); // Adjusted regex for offset part slightly for robustness
            if (!match.Success) throw new ArgumentException($"Invalid memory offset format: {memStr}");

            string offsetValStr = match.Groups[1].Value.Trim();
            string regNameStr = match.Groups[2].Value;
            int imm;

            if (offsetValStr.StartsWith("$"))
            {
                string hexPart = offsetValStr.Substring(1);
                if (Regex.IsMatch(hexPart, @"^[0-9a-fA-F]+$"))
                {
                    try { imm = Convert.ToInt32(hexPart, 16); }
                    catch (FormatException) { throw new ArgumentException($"Invalid hex format in offset: {offsetValStr}"); }
                    catch (OverflowException) { throw new ArgumentException($"Hex offset value too large: {offsetValStr}"); }
                }
                else
                {
                    throw new ArgumentException($"Invalid characters after $ in offset: {offsetValStr}");
                }
            }
            else if (offsetValStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string hexPart = offsetValStr.Substring(2);
                if (Regex.IsMatch(hexPart, @"^[0-9a-fA-F]+$"))
                {
                    try { imm = Convert.ToInt32(hexPart, 16); }
                    catch (FormatException) { throw new ArgumentException($"Invalid hex format in offset: {offsetValStr}"); }
                    catch (OverflowException) { throw new ArgumentException($"Hex offset value too large: {offsetValStr}"); }
                }
                else
                {
                    throw new ArgumentException($"Invalid characters after 0x in offset: {offsetValStr}");
                }
            }
            else if (int.TryParse(offsetValStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int decVal))
            {
                imm = decVal;
            }
            else if (labels.TryGetValue(offsetValStr, out uint labelAddr)) // Optional: if labels can be used as offsets
            {
                imm = (int)labelAddr;
            }
            else
            {
                // If it's not $HEX, not 0xHEX, not a valid decimal, (and not a label, if supported)
                // it could be an attempt to use a bare hex string like "FF" without a prefix.
                // Decide if you want to support bare hex or treat it as an error or a label lookup.
                // For strictness, and based on typical MIPS syntax, bare numbers are decimal.
                // If it wasn't parsed as decimal, it's likely an error or an unrecognized label.
                throw new ArgumentException($"Invalid or unrecognized immediate offset value: {offsetValStr}");
            }

            int rs = ParseOperand(regNameStr, labels);
            return (imm, rs);
        }
    }
}
