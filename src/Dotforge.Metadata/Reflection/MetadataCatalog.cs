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
        var genericParamsByOwner = BuildGenericParametersByOwner(metadata);
        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(typeHandle);
            var typeName = ReadTypeName(metadata, typeHandle);
            var typeToken = MetadataTokens.GetToken(typeHandle);
            var typeGenericParams = genericParamsByOwner.TryGetValue(typeToken, out var tgp)
                ? tgp
                : [];
            var fields = new List<DotFieldInfo>();
            var methods = new List<DotMethodInfo>();

            foreach (var fieldHandle in typeDef.GetFields())
            {
                var fieldDef = metadata.GetFieldDefinition(fieldHandle);
                var fieldSig = ReadFieldSignature(metadata, fieldDef.Signature);
                fields.Add(new DotFieldInfo(
                    Token: MetadataTokens.GetToken(fieldHandle),
                    Name: metadata.GetString(fieldDef.Name),
                    DeclaringType: typeName,
                    IsStatic: (fieldDef.Attributes & System.Reflection.FieldAttributes.Static) != 0,
                    FieldTypeCode: fieldSig.TypeCode));
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
                    GenericArity: genericParamsByOwner.TryGetValue(MetadataTokens.GetToken(methodHandle), out var mgp) ? mgp.Count : 0,
                    GenericParameters: genericParamsByOwner.TryGetValue(MetadataTokens.GetToken(methodHandle), out var mgp2) ? mgp2 : [],
                    IsStatic: !sig.IsInstance,
                    ReturnTypeCode: sig.ReturnTypeCode));
            }

            result.Add(new DotTypeInfo(
                Token: typeToken,
                FullName: typeName,
                GenericArity: typeGenericParams.Count,
                GenericParameters: typeGenericParams,
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
    private readonly record struct FieldSig(string TypeCode);

    private static FieldSig ReadFieldSignature(MetadataReader metadata, BlobHandle signatureHandle)
    {
        var reader = metadata.GetBlobReader(signatureHandle);
        var header = reader.ReadSignatureHeader();
        if (header.Kind != SignatureKind.Field)
        {
            throw new NotSupportedException($"Unsupported field signature kind: {header.Kind}.");
        }

        var typeCode = reader.ReadSignatureTypeCode().ToString();
        return new FieldSig(typeCode);
    }

    private static Dictionary<int, IReadOnlyList<string>> BuildGenericParametersByOwner(MetadataReader metadata)
    {
        var map = new Dictionary<int, List<string>>();
        var rowCount = metadata.GetTableRowCount(TableIndex.GenericParam);
        for (var rowId = 1; rowId <= rowCount; rowId++)
        {
            var gpHandle = MetadataTokens.GenericParameterHandle(rowId);
            var gp = metadata.GetGenericParameter(gpHandle);
            var ownerToken = MetadataTokens.GetToken(gp.Parent);
            if (!map.TryGetValue(ownerToken, out var list))
            {
                list = [];
                map[ownerToken] = list;
            }

            list.Add(metadata.GetString(gp.Name));
        }

        return map.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<string>)kvp.Value);
    }
}
