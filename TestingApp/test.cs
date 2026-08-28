using DiffMatchPatch;

namespace TestingApp;

using System.Collections;
using System.Collections.Generic;

public class MyDmpWrapper : diff_match_patch
{
    public List<Diff> MyLineModeDiff(string text1, string text2)
    {
        // Use a wrapper to call the private diff_linesToChars.
        object[] result = diff_linesToChars(text1, text2);
        var lineText1 = (string)result[0];
        var lineText2 = (string)result[1];
        var lineArray = (List<string>)result[2];

        List<Diff> diffs = diff_main(lineText1, lineText2, false);
        diff_charsToLines(diffs, lineArray);
        diff_cleanupSemantic(diffs);

        return diffs;
    }
}
