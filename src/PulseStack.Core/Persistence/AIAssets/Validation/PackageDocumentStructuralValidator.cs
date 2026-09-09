using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;

namespace PulseStack.Core.Persistence.AIAssets.Validation;

internal static class PackageDocumentStructuralValidator
{
    internal static void Validate(
        PackageAssetDocument package,
        ICollection<AIAssetDocumentValidationError> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (package.Members.Count == 0)
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.EmptyPackageMembers,
                "A Package must declare at least one direct member Asset definition.",
                "$.members");
        }

        var membersByKey = new Dictionary<ReferenceKey, AIAssetReferenceDocument>();
        var validMembers = new List<AIAssetReferenceDocument>();
        var projectionSourceValid = true;
        var hasPackageKey = TryCreatePackageKey(package.Identity, out var packageKey);

        for (var index = 0; index < package.Members.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var member = package.Members[index];
            var path = $"$.members[{index}]";

            if (member is null)
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.MissingPackageMember,
                    "The Package member reference is required.",
                    path);
                projectionSourceValid = false;
                continue;
            }

            ValidateReference(member, path, errors);
            if (!IsStructurallyUsableReference(member))
            {
                projectionSourceValid = false;
                continue;
            }

            var key = CreateReferenceKey(member);
            if (membersByKey.TryGetValue(key, out var first))
            {
                if (string.Equals(first.Urn, member.Urn, StringComparison.Ordinal))
                {
                    AddError(
                        errors,
                        AIAssetDocumentValidationCodes.DuplicatePackageMember,
                        "The Package contains a duplicate direct member Asset definition.",
                        path);
                }
                else
                {
                    AddError(
                        errors,
                        AIAssetDocumentValidationCodes.ConflictingPackageMemberUrn,
                        "The Package contains member references with the same definition identity but conflicting URNs.",
                        $"{path}.urn");
                }

                projectionSourceValid = false;
                continue;
            }

            membersByKey.Add(key, member);
            validMembers.Add(member);

            if (hasPackageKey && key == packageKey)
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.DirectSelfPackageMember,
                    "A Package cannot include its own exact Asset definition as a direct member.",
                    path);
                projectionSourceValid = false;
            }
        }

        var dependenciesByKey = new Dictionary<ReferenceKey, AIAssetDependencyDocument>();
        for (var index = 0; index < package.Dependencies.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dependency = package.Dependencies[index];
            if (dependency?.Reference is not { } reference || !IsStructurallyUsableReference(reference))
            {
                continue;
            }

            var path = $"$.dependencies[{index}]";
            var key = CreateReferenceKey(reference);
            if (dependenciesByKey.TryGetValue(key, out var first))
            {
                if (!string.Equals(first.Reference.Urn, reference.Urn, StringComparison.Ordinal))
                {
                    AddError(
                        errors,
                        AIAssetDocumentValidationCodes.ConflictingPackageDependencyUrn,
                        "The Package contains dependency references with the same definition identity but conflicting URNs.",
                        $"{path}.reference.urn");
                    continue;
                }

                if (first.Required != dependency.Required)
                {
                    AddError(
                        errors,
                        AIAssetDocumentValidationCodes.ConflictingPackageDependencyRequiredness,
                        "The Package contains the same dependency definition with conflicting Required values.",
                        $"{path}.required");
                    continue;
                }

                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.DuplicateDependency,
                    "The AI Asset document contains a duplicate dependency.",
                    path);
                continue;
            }

            dependenciesByKey.Add(key, dependency);

            if (hasPackageKey && key == packageKey)
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.DirectSelfPackageDependency,
                    "A Package cannot declare its own exact Asset definition as an external dependency.",
                    $"{path}.reference");
            }
        }

        for (var index = 0; index < package.Dependencies.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dependency = package.Dependencies[index];
            if (dependency?.Reference is not { } reference || !IsStructurallyUsableReference(reference))
            {
                continue;
            }

            var key = CreateReferenceKey(reference);
            if (!membersByKey.TryGetValue(key, out var member))
            {
                continue;
            }

            var path = $"$.dependencies[{index}].reference";
            if (!string.Equals(member.Urn, reference.Urn, StringComparison.Ordinal))
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.PackageMemberDependencyUrnConflict,
                    "A Package member and external dependency use conflicting URNs for the same Asset definition identity.",
                    $"{path}.urn");
            }
            else
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.PackageMemberDependencyBoundaryContradiction,
                    "The same Asset definition cannot be both a direct Package member and an external dependency.",
                    path);
            }
        }

        if (!projectionSourceValid)
        {
            return;
        }

        if (!package.References.SequenceEqual(validMembers))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.PackageReferenceProjectionMismatch,
                "Package envelope references must exactly match the canonical authored Members sequence.",
                "$.references");
        }
    }

    private static void ValidateReference(
        AIAssetReferenceDocument reference,
        string path,
        ICollection<AIAssetDocumentValidationError> errors)
    {
        if (!Enum.IsDefined(reference.AssetType))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.UnsupportedReferenceAssetType,
                "The referenced AI Asset type is not supported by this schema.",
                $"{path}.assetType");
        }

        if (!Guid.TryParse(reference.AssetId, out var id) || id == Guid.Empty)
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.InvalidReferenceAssetId,
                "The referenced AI Asset ID must be a non-empty GUID.",
                $"{path}.assetId");
        }

        if (string.IsNullOrWhiteSpace(reference.Urn))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingReferenceUrn,
                "The referenced AI Asset URN is required.",
                $"{path}.urn");
        }

        if (string.IsNullOrWhiteSpace(reference.Version))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingReferenceVersion,
                "The referenced AI Asset version is required.",
                $"{path}.version");
        }
    }

    private static bool TryCreatePackageKey(
        AIAssetIdentityDocument? identity,
        out ReferenceKey key)
    {
        if (identity is null
            || !Guid.TryParse(identity.Id, out var id)
            || id == Guid.Empty
            || string.IsNullOrWhiteSpace(identity.Version))
        {
            key = default;
            return false;
        }

        key = new ReferenceKey(AIAssetDocumentType.Package, id, identity.Version);
        return true;
    }

    private static bool IsStructurallyUsableReference(AIAssetReferenceDocument reference) =>
        Enum.IsDefined(reference.AssetType)
        && Guid.TryParse(reference.AssetId, out var assetId) && assetId != Guid.Empty
        && !string.IsNullOrWhiteSpace(reference.Version)
        && !string.IsNullOrWhiteSpace(reference.Urn);

    private static ReferenceKey CreateReferenceKey(AIAssetReferenceDocument reference) =>
        new(reference.AssetType, Guid.Parse(reference.AssetId), reference.Version);

    private static void AddError(
        ICollection<AIAssetDocumentValidationError> errors,
        string code,
        string message,
        string path) =>
        errors.Add(new AIAssetDocumentValidationError(code, message, path));

    private readonly record struct ReferenceKey(
        AIAssetDocumentType AssetType,
        Guid AssetId,
        string Version);
}
