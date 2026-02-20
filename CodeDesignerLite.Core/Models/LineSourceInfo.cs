namespace CodeDesignerLite.Core.Models
{
    public class LineSourceInfo
    {
        public string Text { get; private set; }
        public string FileName { get; private set; }
        public int OriginalLineNumber { get; private set; }
        public int GlobalIndex { get; private set; }

        public LineSourceInfo(string text, string fileName, int originalLineNumber, int globalIndex)
        {
            Text = text;
            FileName = fileName;
            OriginalLineNumber = originalLineNumber;
            GlobalIndex = globalIndex;
        }
    }
}
