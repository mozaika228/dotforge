namespace Dotforge.Runtime.Interop;

internal sealed class PInvokeRegistry
{
    private readonly Dictionary<string, Func<object?[], object?>> _exports = new(StringComparer.OrdinalIgnoreCase);

    public PInvokeRegistry()
    {
        Register("dotforge_native", "abs", args => Math.Abs(ToInt32(args, 0)));
        Register("dotforge_native", "strlen", args => (args.ElementAtOrDefault(0)?.ToString() ?? string.Empty).Length);
        Register("dotforge_native", "toupper_first", args =>
        {
            var value = args.ElementAtOrDefault(0)?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(value[0]) + value[1..];
        });
    }

    public void Register(string module, string entryPoint, Func<object?[], object?> callback)
    {
        var key = BuildKey(module, entryPoint);
        _exports[key] = callback;
    }

    public bool TryInvoke(string module, string entryPoint, object?[] args, out object? result)
    {
        var key = BuildKey(module, entryPoint);
        if (_exports.TryGetValue(key, out var callback))
        {
            result = callback(args);
            return true;
        }

        result = null;
        return false;
    }

    private static string BuildKey(string module, string entryPoint) => $"{module}!{entryPoint}";

    private static int ToInt32(object?[] args, int index)
    {
        if (index < 0 || index >= args.Length)
        {
            throw new IndexOutOfRangeException($"Interop argument {index} is out of range.");
        }

        return Convert.ToInt32(args[index]);
    }
}
