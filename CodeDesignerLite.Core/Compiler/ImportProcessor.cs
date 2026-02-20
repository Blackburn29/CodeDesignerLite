using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CodeDesignerLite.Core.Models;

namespace CodeDesignerLite.Core.Compiler
{
    public class ImportProcessor
    {
        private const int MAX_IMPORT_DEPTH = 10;

        public List<LineSourceInfo> PreprocessImports(string[] initialLines, string currentFileName, string baseDirectoryPath, ref int globalLineCounter, int currentDepth = 0)
        {
            if (currentDepth > MAX_IMPORT_DEPTH)
            {
                throw new Exception($"Maximum import depth of {MAX_IMPORT_DEPTH} exceeded. Check for circular imports.");
            }

            var processedLineInfos = new List<LineSourceInfo>();
            int localLineNumber = 0;
            foreach (var lineText in initialLines)
            {
                localLineNumber++;
                var importMatch = Regex.Match(lineText.Trim(), @"^import\s+""([^""]+)""", RegexOptions.IgnoreCase);
                if (importMatch.Success)
                {
                    string relativePath = importMatch.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);
                    string fullPath = Path.Combine(baseDirectoryPath ?? Directory.GetCurrentDirectory(), relativePath);

                    if (File.Exists(fullPath))
                    {
                        Console.WriteLine($"Importing: {fullPath} (depth {currentDepth})");
                        // Read imported files using ISO-8859-1 encoding
                        string[] importedFileLines = File.ReadAllLines(fullPath, Encoding.GetEncoding("ISO-8859-1"));
                        processedLineInfos.AddRange(PreprocessImports(importedFileLines, Path.GetFileName(fullPath), Path.GetDirectoryName(fullPath), ref globalLineCounter, currentDepth + 1));
                    }
                    else
                    {
                        Console.WriteLine($"Warning - Import failed: File not found \"{fullPath}\"");
                        processedLineInfos.Add(new LineSourceInfo($"// Import failed (not found): {lineText.Trim()}", currentFileName, localLineNumber, globalLineCounter++));
                    }
                }
                else
                {
                    processedLineInfos.Add(new LineSourceInfo(lineText, currentFileName, localLineNumber, globalLineCounter++));
                }
            }
            return processedLineInfos;
        }
    }
}
