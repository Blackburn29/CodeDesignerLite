using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CodeDesignerLite.Core.Services
{
    /// <summary>
    /// Provides file I/O operations with proper encoding support for MIPS assembly files.
    /// Reads files with Windows-1252 encoding and writes with ISO-8859-1 encoding.
    /// </summary>
    public class FileService
    {
        /// <summary>
        /// Reads a file asynchronously using Windows-1252 encoding.
        /// </summary>
        /// <param name="filePath">Path to the file to read</param>
        /// <returns>File content as string</returns>
        public async Task<string> ReadFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            try
            {
                // Read with Windows-1252 encoding
                var encoding = Encoding.GetEncoding("Windows-1252");
                return await File.ReadAllTextAsync(filePath, encoding);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error reading file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reads all lines from a file asynchronously using Windows-1252 encoding.
        /// </summary>
        /// <param name="filePath">Path to the file to read</param>
        /// <returns>Array of lines</returns>
        public async Task<string[]> ReadAllLinesAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            try
            {
                // Read with Windows-1252 encoding
                var encoding = Encoding.GetEncoding("Windows-1252");
                return await File.ReadAllLinesAsync(filePath, encoding);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error reading file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Writes text to a file asynchronously using ISO-8859-1 encoding.
        /// </summary>
        /// <param name="filePath">Path to the file to write</param>
        /// <param name="content">Content to write</param>
        public async Task WriteFileAsync(string filePath, string content)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write with ISO-8859-1 encoding
                var encoding = Encoding.GetEncoding("ISO-8859-1");
                await File.WriteAllTextAsync(filePath, content, encoding);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error writing file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if a file exists.
        /// </summary>
        /// <param name="filePath">Path to check</param>
        /// <returns>True if file exists, false otherwise</returns>
        public bool FileExists(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        }

        /// <summary>
        /// Gets the directory path from a file path.
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Directory path or empty string if file path is null/empty</returns>
        public string GetDirectoryPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            return Path.GetDirectoryName(filePath) ?? string.Empty;
        }

        /// <summary>
        /// Gets the file name from a file path.
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>File name or empty string if file path is null/empty</returns>
        public string GetFileName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            return Path.GetFileName(filePath);
        }
    }
}
