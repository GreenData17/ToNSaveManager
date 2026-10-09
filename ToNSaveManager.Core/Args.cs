namespace ToNSaveManager;

internal static class Args
{
    static string[] Arguments = Array.Empty<string>();

    internal static bool ContainsArg(string arg) => Array.IndexOf(Arguments, arg) != -1;
    
    internal static void SetArguments(string[] args)
    {
        Arguments = args;
    }
    
    internal static string? GetArgValue(string arg)
    {
        int index = Array.IndexOf(Arguments, arg);
        return index >= 0 && index + 1 < Arguments.Length ? Arguments[index + 1] : null;
    }
}