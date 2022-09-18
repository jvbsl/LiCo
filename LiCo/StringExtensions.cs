using System;
using System.Collections.Generic;

namespace LiCo;

public static class StringExtensions
{
    public static bool EndsWith(this string value, char character)
    {
        if (value.Length == 0)
            return false;
        return value[^1] == character;
    }
    public static bool StartsWith(this string value, char character)
    {
        if (value.Length == 0)
            return false;
        return value[0] == character;
    }

    public static string[] TrimmedSplit(this string value, char[] splitters, int count)
    {
        if (value is null)
            return Array.Empty<string>();
        if (value.Length == 0)
            return new[] { "" };
        var res = new List<string>();

        int index = 0;

        do
        {
            var newIndex = value.IndexOfAny(splitters);

            if (newIndex == -1 || res.Count == count - 1)
                newIndex = value.Length;

            var subStr = value[index..newIndex].Trim();
            if (!string.IsNullOrEmpty(subStr))
                res.Add(subStr);

            index = newIndex;
        } while (index < value.Length);
        
        
        return res.ToArray();
    }
}