using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotforge.Metadata.Verification;

public static class MetadataValidator
{
    public static VerificationReport Validate(ManagedAssembly assembly)
    {
        var report = new VerificationReport();
        var metadata = assembly.Metadata;

        if (metadata.TypeDefinitions.Count == 0)
        {
            report.AddError("MD0001", "Assembly contains no TypeDefinition rows.");
        }

        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(typeHandle);
            var typeName = ReadTypeName(metadata, typeHandle);
            if (string.IsNullOrWhiteSpace(metadata.GetString(typeDef.Name)))
            {
                report.AddError("MD0002", $"Type with token 0x{MetadataTokens.GetToken(typeHandle):X8} has empty name.");
            }

            foreach (var methodHandle in typeDef.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                var methodName = metadata.GetString(method.Name);
                if (string.IsNullOrWhiteSpace(methodName))
                {
                    report.AddError("MD0003", $"Method in '{typeName}' has empty name.");
                }

                if (method.RelativeVirtualAddress != 0)
                {
                    try
                    {
                        _ = assembly.GetMethodBody(methodHandle);
                    }
                    catch (Exception ex)
                    {
                        report.AddError("MD0004", $"Method body read failed for {typeName}::{methodName}: {ex.Message}");
                    }
                }
            }
        }

        return report;
    }

    private static string ReadTypeName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var typeDef = metadata.GetTypeDefinition(handle);
        var ns = metadata.GetString(typeDef.Namespace);
        var name = metadata.GetString(typeDef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }
}
