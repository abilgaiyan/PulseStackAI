namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Identifies the outer MS-009.7 operation in which a failure occurred.
/// Codec serialization/deserialization remains classified by the MS-009.6 codec contract.
/// </summary>
public enum AIAssetStorageOperation
{
    WriteDocument,
    WriteRepresentation,
    Load
}
