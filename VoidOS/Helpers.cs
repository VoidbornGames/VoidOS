using System;
using System.Collections.Generic;
using System.Text;

namespace VoidOS;


public static class PathHelper
{
    public static string Resolve(string input)
    {
        input = input.Replace("\\", "/");

        if (input.StartsWith("0:/")) return input;
        if (input.StartsWith("/")) return "0:" + input;

        return Kernel.CurrentPath + input;
    }
}
