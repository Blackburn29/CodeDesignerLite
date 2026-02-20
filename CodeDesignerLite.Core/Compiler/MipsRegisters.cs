using System;
using System.Collections.Generic;

namespace CodeDesignerLite.Core.Compiler
{
    public static class MipsRegisters
    {
        public static Dictionary<string, int> Initialize()
        {
            var registers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Initialize GPR (General Purpose Registers) names
            string[] gprNames =
            [
                "zero", "at", "v0", "v1", "a0", "a1", "a2", "a3",
                                  "t0", "t1", "t2", "t3", "t4", "t5", "t6", "t7",
                                  "s0", "s1", "s2", "s3", "s4", "s5", "s6", "s7",
                                  "t8", "t9", "k0", "k1", "gp", "sp", "fp", "ra"
            ];

            for (int i = 0; i < gprNames.Length; i++)
            {
                registers[gprNames[i]] = i;
                registers["$" + gprNames[i]] = i;
                registers[i.ToString()] = i;
            }

            // Initialize FPR (Floating Point Registers) - f0 to f31
            for (int i = 0; i < 32; i++)
            {
                registers[$"f{i}"] = i;
                registers[$"$f{i}"] = i;
            }

            return registers;
        }

        public static bool IsFPRName(string operandString)
        {
            if (string.IsNullOrEmpty(operandString))
                return false;

            string trimmed = operandString.Trim();

            // Remove leading '$' if present
            if (trimmed.StartsWith("$"))
                trimmed = trimmed.Substring(1);

            // Check if it starts with 'f' followed by a number
            if (trimmed.StartsWith("f", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 1)
            {
                string numberPart = trimmed.Substring(1);
                if (int.TryParse(numberPart, out int regNum))
                {
                    return regNum >= 0 && regNum < 32;
                }
            }

            return false;
        }
    }
}
