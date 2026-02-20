using System.Collections.Generic;

namespace CodeDesignerLite.Core.Models
{
    public class CompilationResult
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public List<MipsErrorInfo> Errors { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<int> ErrorLineNumbers { get; set; }

        public CompilationResult()
        {
            Errors = new List<MipsErrorInfo>();
            ErrorLineNumbers = new List<int>();
            Output = string.Empty;
        }
    }
}
