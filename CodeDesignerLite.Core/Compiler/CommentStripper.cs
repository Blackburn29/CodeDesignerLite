using System.Text;

namespace CodeDesignerLite.Core.Compiler
{
    public static class CommentStripper
    {
        public static string StripCommentsFromLine(string line, ref bool inBlockComment)
        {
            StringBuilder sb = new StringBuilder();
            int currentSearchIndex = 0;

            while (currentSearchIndex < line.Length)
            {
                if (inBlockComment)
                {
                    int endCommentIndex = line.IndexOf("*/", currentSearchIndex);
                    if (endCommentIndex != -1)
                    {
                        inBlockComment = false;
                        currentSearchIndex = endCommentIndex + 2;
                    }
                    else
                    {
                        // Block comment continues to the next line, so we append nothing from this line.
                        currentSearchIndex = line.Length; // Effectively stop processing this line
                    }
                }
                else // Not in a block comment
                {
                    int startBlockCommentIndex = line.IndexOf("/*", currentSearchIndex);
                    int startSingleLineComment1 = line.IndexOf("//", currentSearchIndex);
                    int startSingleLineComment2 = line.IndexOf("#", currentSearchIndex);

                    int determinedNextCommentStart = -1;

                    // Check for /*
                    if (startBlockCommentIndex != -1)
                    {
                        determinedNextCommentStart = startBlockCommentIndex;
                    }

                    // Check for //, update if it's earlier than /*
                    if (startSingleLineComment1 != -1)
                    {
                        if (determinedNextCommentStart == -1 || startSingleLineComment1 < determinedNextCommentStart)
                        {
                            determinedNextCommentStart = startSingleLineComment1;
                        }
                    }

                    // Check for #, update if it's earlier AND NOT inside a string literal
                    if (startSingleLineComment2 != -1)
                    {
                        bool hashIsInsideString = false;
                        int quoteBalance = 0;
                        // Count unescaped quotes from currentSearchIndex up to where '#' was found
                        for (int k = currentSearchIndex; k < startSingleLineComment2; k++)
                        {
                            if (line[k] == '\\' && k + 1 < startSingleLineComment2) // Check bounds for k+1
                            {
                                k++; // Skip the character after backslash (it's escaped)
                            }
                            else if (line[k] == '"')
                            {
                                quoteBalance++;
                            }
                        }

                        if (quoteBalance % 2 == 1) // Odd number of unescaped quotes means '#' is inside a string
                        {
                            hashIsInsideString = true;
                        }

                        if (!hashIsInsideString) // If '#' is not inside a string, consider it a comment
                        {
                            if (determinedNextCommentStart == -1 || startSingleLineComment2 < determinedNextCommentStart)
                            {
                                determinedNextCommentStart = startSingleLineComment2;
                            }
                        }
                        // If hashIsInsideString is true, startSingleLineComment2 is ignored as a comment starter.
                    }

                    // 'determinedNextCommentStart' now holds the beginning of the earliest valid comment, or -1

                    if (determinedNextCommentStart == -1) // No valid comment found on the rest of the line
                    {
                        sb.Append(line.Substring(currentSearchIndex));
                        currentSearchIndex = line.Length; // Done with this line
                    }
                    else // A valid comment was found
                    {
                        // Append text before the comment
                        sb.Append(line.Substring(currentSearchIndex, determinedNextCommentStart - currentSearchIndex));

                        // Now determine what type of comment it was to advance currentSearchIndex correctly
                        if (determinedNextCommentStart == startBlockCommentIndex && startBlockCommentIndex != -1)
                        {
                            inBlockComment = true;
                            currentSearchIndex = determinedNextCommentStart + 2; // Move past "/*"
                                                                                 // Check if the block comment also ends on this same line
                            int endCommentIndexOnSameLine = line.IndexOf("*/", currentSearchIndex);
                            if (endCommentIndexOnSameLine != -1)
                            {
                                inBlockComment = false; // It ended
                                currentSearchIndex = endCommentIndexOnSameLine + 2; // Move past "*/"
                            }
                            else
                            {
                                // Block comment continues to next line, so we're done with this line's content
                                currentSearchIndex = line.Length;
                            }
                        }
                        else // It's a single-line comment (// or a valid #)
                        {
                            // The rest of the line is a comment, so we're done with this line's content
                            currentSearchIndex = line.Length;
                        }
                    }
                }
            }
            return sb.ToString().Trim(); // Trim leading/trailing whitespace from the processed line
        }
    }
}
