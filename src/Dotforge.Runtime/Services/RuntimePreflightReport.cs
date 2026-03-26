using Dotforge.Metadata.Verification;

namespace Dotforge.Runtime.Services;

public sealed class RuntimePreflightReport
{
    public RuntimePreflightReport(
        VerificationReport metadataReport,
        VerificationReport ilReport,
        IReadOnlyList<string> unresolvedReferences)
    {
        MetadataReport = metadataReport;
        IlReport = ilReport;
        UnresolvedReferences = unresolvedReferences;
    }

    public VerificationReport MetadataReport { get; }
    public VerificationReport IlReport { get; }
    public IReadOnlyList<string> UnresolvedReferences { get; }

    public bool HasErrors => MetadataReport.HasErrors || IlReport.HasErrors;
}
