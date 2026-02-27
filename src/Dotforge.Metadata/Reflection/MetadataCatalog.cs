using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotforge.Metadata.Reflection;

public sealed class MetadataCatalog
{
    private readonly ManagedAssembly _assembly;

    public MetadataCatalog(ManagedAssembly assembly)
    {
        _assembly = assembly;
    }

    public IReadOnlyList<DotTypeInfo> GetTypes()
    {
        var metadata = _assembly.Metadata;
        var result = new List<DotTypeInfo>();
        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(typeHandle);
            var typeName = ReadTypeName(metadata, typeHandle);
            var fields = new List<DotFieldInfo>();
            var methods = new List<DotMethodInfo>();

            foreach (var fieldHandle in typeDef.GetFields())
            {
                var fieldDef = metadata.GetFieldDefinition(fieldHandle);
                fields.Add(new DotFieldInfo(
                    Token: MetadataTokens.GetToken(fieldHandle),
                    Name: metadata.GetString(fieldDef.Name),
                    DeclaringType: typeName));
            }

            foreach (var methodHandle in typeDef.GetMethods())
            {
                var methodDef = metadata.GetMethodDefinition(methodHandle);
                var sig = ReadMethodSignature(metadata, methodDef.Signature);
                methods.Add(new DotMethodInfo(
                    Token: MetadataTokens.GetToken(methodHandle),
                    Name: metadata.GetString(methodDef.Name),
                    DeclaringType: typeName,
                    ParameterCount: sig.ParameterCount,
                    IsStatic: !sig.IsInstance,
                    ReturnTypeCode: sig.ReturnTypeCode));
            }

            result.Add(new DotTypeInfo(
                Token: MetadataTokens.GetToken(typeHandle),
                FullName: typeName,
                Fields: fields,
                Methods: methods));
        }

        return result;
    }

    private static string ReadTypeName(MetadataReader metadata, TypeDefinitionHandle typeHandle)
    {
        var typeDef = metadata.GetTypeDefinition(typeHandle);
        var ns = metadata.GetString(typeDef.Namespace);
        var name = metadata.GetString(typeDef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static MethodSig ReadMethodSignature(MetadataReader metadata, BlobHandle signatureHandle)
    {
        var reader = metadata.GetBlobReader(signatureHandle);
        var header = reader.ReadSignatureHeader();
        if (header.Kind != SignatureKind.Method)
        {
            throw new NotSupportedException($"Unsupported signature kind: {header.Kind}.");
        }

        if (header.IsGeneric)
        {
            _ = reader.ReadCompressedInteger();
        }

        var parameterCount = reader.ReadCompressedInteger();
        var retType = reader.ReadSignatureTypeCode().ToString();
        return new MethodSig(parameterCount, header.IsInstance, retType);
    }

    private readonly record struct MethodSig(int ParameterCount, bool IsInstance, string ReturnTypeCode);
}
