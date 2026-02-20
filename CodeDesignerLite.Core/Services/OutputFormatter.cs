using CodeDesignerLite.Core.Enums;

namespace CodeDesignerLite.Core.Services
{
    public static class OutputFormatter
    {
        public static string FormatOutputLine(uint address, string hexDataValue, OutputFormatMode mode, string addressFormatChar)
        {
            if (mode == OutputFormatMode.PS2)
            {
                string addrStrOutput = address.ToString("X8");

                // Apply address format character (replaces first character of address)
                if (addressFormatChar != "-" && addressFormatChar.Length == 1)
                {
                    addrStrOutput = addressFormatChar[0] + addrStrOutput.Substring(1);
                }
                return $"{addrStrOutput} {hexDataValue}";
            }
            else // OutputFormatMode.Pnach
            {
                string pnachAddress = address.ToString("X8");
                if (addressFormatChar != "-" && addressFormatChar.Length == 1)
                {
                    pnachAddress = addressFormatChar[0] + pnachAddress.Substring(1);
                }
                return $"patch=1,EE,{pnachAddress},extended,{hexDataValue}";
            }
        }
    }
}
