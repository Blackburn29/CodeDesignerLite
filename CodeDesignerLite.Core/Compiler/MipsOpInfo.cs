namespace CodeDesignerLite.Core.Compiler
{
    public class MipsOpInfo
    {
        public string Type { get; set; } = "";
        public uint Opcode { get; set; }
        public uint Funct { get; set; }
        public uint Fmt { get; set; }
        public uint CopOp { get; set; }
        public uint RtField { get; set; }
        public uint CcBit { get; set; }
        public uint CustomValue { get; set; }
    }
}
