using System;
using System.Collections.Generic;
using System.Text;

namespace VoidOS;


public static class PathHelper
{
    public static string Resolve(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input == ".")
            return Kernel.CurrentPath;

        if (input == "..")
            return Path.GetFullPath(Path.Combine(Kernel.CurrentPath, ".."));

        return Path.IsPathRooted(input)
            ? Path.GetFullPath(input)
            : Path.GetFullPath(Path.Combine(Kernel.CurrentPath, input));
    }
}