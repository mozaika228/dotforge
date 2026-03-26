namespace Dotforge.Metadata.Verification;

public sealed class VerificationReport
{
    private readonly List<VerificationMessage> _messages = [];

    public IReadOnlyList<VerificationMessage> Messages => _messages;
    public bool HasErrors => _messages.Any(m => m.IsError);

    public void AddError(string code, string message) => _messages.Add(new VerificationMessage(code, message, true));
    public void AddWarning(string code, string message) => _messages.Add(new VerificationMessage(code, message, false));
}
