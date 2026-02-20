using System.IO;

namespace CodeDesignerLite.Core.Models
{
    public class MipsErrorInfo
    {
        public string FileName { get; set; }
        public int LineNumberInFile { get; set; }
        public int GlobalLineIndex { get; set; }
        public uint AddressAtError { get; set; }
        public string AttemptedData { get; set; }
        public string ErrorMessage { get; set; }
        public string OriginalLineText { get; set; }
        public bool IsFromMainInput { get; set; }
        public int OriginalLineNumber { get; set; }

        public MipsErrorInfo(string fileName, int lineNumberInFile, int globalLineIndex, uint addressAtError, string attemptedData, string errorMessage, string originalLineText, bool isFromMainInput)
        {
            FileName = fileName;
            LineNumberInFile = lineNumberInFile;
            OriginalLineNumber = lineNumberInFile;
            GlobalLineIndex = globalLineIndex;
            AddressAtError = addressAtError;
            AttemptedData = attemptedData ?? "N/A";
            ErrorMessage = errorMessage;
            OriginalLineText = originalLineText;
            IsFromMainInput = isFromMainInput;
        }

        public override string ToString()
        {
            return $"{LineNumberInFile} {Path.GetFileName(FileName)}: {ErrorMessage}";
        }
    }
}
