using System.Collections;
using System.Reflection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Values;

namespace PulseStack.Core.Persistence.AIAssets.Mapping;

internal static class WorkflowDefinitionDocumentMapper
{
    internal static WorkflowAssetDocument ToDocument(
        WorkflowAsset workflow,
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        IReadOnlyList<AIAssetReferenceDocument> references,
        IReadOnlyList<AIAssetDependencyDocument> dependencies)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var steps = new List<WorkflowStepDocument>();
        var index = 0;
        foreach (var step in workflow.Options.Steps)
        {
            steps.Add(ToDocument(step, $"$.steps[{index}]"));
            index++;
        }

        return new WorkflowAssetDocument(
            schemaVersion,
            identity,
            metadata,
            lifecycle,
            steps,
            references,
            dependencies);
    }

    private static WorkflowStepDocument ToDocument(WorkflowStepDefinition step, string path)
    {
        ArgumentNullException.ThrowIfNull(step);
        var stepId = step.Id.Value.ToString("D");

        return step switch
        {
            RunStepDefinition run => new RunStepDocument(stepId, ToDocument(run.Agent)),
            ParallelStepDefinition parallel => new ParallelStepDocument(
                stepId,
                parallel.Name,
                MapSteps(parallel.Steps, $"{path}.steps")),
            ConditionalStepDefinition conditional => new ConditionalStepDocument(
                stepId,
                conditional.Name,
                ToDocument(conditional.Condition, $"{path}.condition"),
                ToDocument(conditional.ThenStep, $"{path}.thenStep"),
                conditional.ElseStep is null ? null : ToDocument(conditional.ElseStep, $"{path}.elseStep")),
            RetryStepDefinition retry => new RetryStepDocument(
                stepId,
                retry.Name,
                ToDocument(retry.Step, $"{path}.step"),
                retry.MaxAttempts),
            LoopStepDefinition loop => new LoopStepDocument(
                stepId,
                loop.Name,
                ToDocument(loop.Items, $"{path}.items"),
                ToDocument(loop.Step, $"{path}.step")),
            SwitchStepDefinition @switch => new SwitchStepDocument(
                stepId,
                @switch.Name,
                ToDocument(@switch.Selector, $"{path}.selector"),
                MapCases(@switch.Cases, $"{path}.cases"),
                @switch.DefaultStep is null ? null : ToDocument(@switch.DefaultStep, $"{path}.defaultStep")),
            _ => throw new NotSupportedException(
                $"Workflow step type '{step.GetType().FullName}' is not supported for mapping at '{path}'.")
        };
    }

    private static IReadOnlyList<WorkflowStepDocument> MapSteps(
        IReadOnlyList<WorkflowStepDefinition> steps,
        string path)
    {
        var mapped = new WorkflowStepDocument[steps.Count];
        for (var index = 0; index < steps.Count; index++)
        {
            mapped[index] = ToDocument(steps[index], $"{path}[{index}]");
        }

        return mapped;
    }

    private static IReadOnlyList<SwitchCaseDocument> MapCases(
        IReadOnlyList<SwitchCaseDefinition> cases,
        string path)
    {
        var mapped = new SwitchCaseDocument[cases.Count];
        for (var index = 0; index < cases.Count; index++)
        {
            var @case = cases[index];
            mapped[index] = new SwitchCaseDocument(
                @case.Value,
                ToDocument(@case.Step, $"{path}[{index}].step"));
        }

        return mapped;
    }

    private static WorkflowConditionDocument ToDocument(ConditionDefinition condition, string path)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return condition switch
        {
            NamedConditionDefinition named => new NamedConditionDocument(named.Name),
            _ => throw new NotSupportedException(
                $"Workflow condition type '{condition.GetType().FullName}' is not supported for mapping at '{path}'.")
        };
    }

    private static WorkflowValueDocument ToDocument(WorkflowValueDefinition value, string path)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            InputValueDefinition => new InputValueDocument(),
            CurrentOutputValueDefinition => new CurrentOutputValueDocument(),
            ContextItemValueDefinition contextItem => new ContextItemValueDocument(contextItem.Key),
            LiteralValueDefinition literal => new LiteralValueDocument(
                NormalizeLiteral(
                    literal.Value,
                    $"{path}.literal",
                    new HashSet<object>(ReferenceEqualityComparer.Instance))),
            _ => throw new NotSupportedException(
                $"Workflow value type '{value.GetType().FullName}' is not supported for mapping at '{path}'.")
        };
    }

    private static WorkflowLiteralDocument NormalizeLiteral(
        object? value,
        string path,
        HashSet<object> activeContainers)
    {
        if (value is null)
        {
            return new NullWorkflowLiteralDocument();
        }

        switch (value)
        {
            case string text:
                return new StringWorkflowLiteralDocument(text);
            case bool boolean:
                return new BooleanWorkflowLiteralDocument(boolean);
            case byte number:
                return new IntegerWorkflowLiteralDocument(number);
            case sbyte number:
                return new IntegerWorkflowLiteralDocument(number);
            case short number:
                return new IntegerWorkflowLiteralDocument(number);
            case ushort number:
                return new IntegerWorkflowLiteralDocument(number);
            case int number:
                return new IntegerWorkflowLiteralDocument(number);
            case uint number:
                return new IntegerWorkflowLiteralDocument(number);
            case long number:
                return new IntegerWorkflowLiteralDocument(number);
            case ulong number when number <= long.MaxValue:
                return new IntegerWorkflowLiteralDocument((long)number);
            case ulong:
                throw Unsupported(path, value, "Unsigned integer exceeds Int64.MaxValue.");
            case decimal number:
                return new DecimalWorkflowLiteralDocument(number);
            case float:
            case double:
                throw Unsupported(path, value, "Floating-point values are not supported.");
        }

        if (TryGetMapContract(value, path, out var mapContract))
        {
            return NormalizeMap(value, mapContract, path, activeContainers);
        }

        var valueType = value.GetType();
        if (ImplementsGeneric(valueType, typeof(ISet<>))
            || ImplementsGeneric(valueType, typeof(IReadOnlySet<>)))
        {
            throw Unsupported(path, value, "Set values are not supported.");
        }

        if (value is Array array)
        {
            return NormalizeArray(array, path, activeContainers);
        }

        if (TryGetListContract(value, path, out var listContract))
        {
            return NormalizeList(value, listContract, path, activeContainers);
        }

        if (value is IEnumerable)
        {
            throw Unsupported(
                path,
                value,
                "Enumerable values require an explicitly supported finite ordered collection contract.");
        }

        throw Unsupported(path, value, "CLR object type is not part of the portable literal vocabulary.");
    }

    private static WorkflowLiteralDocument NormalizeArray(
        Array array,
        string path,
        HashSet<object> activeContainers)
    {
        if (array.Rank != 1 || array.GetLowerBound(0) != 0)
        {
            throw Unsupported(path, array, "Only one-dimensional, zero-based arrays are supported.");
        }

        EnterContainer(array, path, activeContainers);
        try
        {
            var items = new WorkflowLiteralDocument[array.Length];
            for (var index = 0; index < array.Length; index++)
            {
                items[index] = NormalizeLiteral(
                    array.GetValue(index),
                    $"{path}.items[{index}]",
                    activeContainers);
            }

            return new ArrayWorkflowLiteralDocument(items);
        }
        finally
        {
            activeContainers.Remove(array);
        }
    }

    private static WorkflowLiteralDocument NormalizeList(
        object list,
        ListContract contract,
        string path,
        HashSet<object> activeContainers)
    {
        EnterContainer(list, path, activeContainers);
        try
        {
            int count;
            try
            {
                count = contract.GetCount(list);
            }
            catch (Exception exception)
            {
                throw Inconsistent(path, "Supported ordered collection failed while reading Count.", exception);
            }

            if (count < 0)
            {
                throw Inconsistent(path, "Supported ordered collection reported a negative Count.");
            }

            var items = new WorkflowLiteralDocument[count];
            for (var index = 0; index < count; index++)
            {
                object? item;
                try
                {
                    item = contract.GetItem(list, index);
                }
                catch (Exception exception)
                {
                    throw Inconsistent(
                        $"{path}.items[{index}]",
                        "Supported ordered collection failed while reading its indexed item.",
                        exception);
                }

                items[index] = NormalizeLiteral(item, $"{path}.items[{index}]", activeContainers);
            }

            return new ArrayWorkflowLiteralDocument(items);
        }
        finally
        {
            activeContainers.Remove(list);
        }
    }

    private static WorkflowLiteralDocument NormalizeMap(
        object map,
        MapContract contract,
        string path,
        HashSet<object> activeContainers)
    {
        EnterContainer(map, path, activeContainers);
        try
        {
            int expectedCount;
            try
            {
                expectedCount = contract.GetCount(map);
            }
            catch (Exception exception)
            {
                throw Inconsistent(path, "Supported map failed while reading Count.", exception);
            }

            if (expectedCount < 0)
            {
                throw Inconsistent(path, "Supported map reported a negative Count.");
            }

            var entries = new List<MapEntry>(expectedCount);
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            IEnumerator enumerator;
            try
            {
                enumerator = ((IEnumerable)map).GetEnumerator();
            }
            catch (Exception exception)
            {
                throw Inconsistent(path, "Supported map failed to create its enumerator.", exception);
            }

            try
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = enumerator.MoveNext();
                    }
                    catch (Exception exception)
                    {
                        throw Inconsistent(path, "Supported map failed during materialization.", exception);
                    }

                    if (!moved)
                    {
                        break;
                    }

                    if (entries.Count == expectedCount)
                    {
                        throw Inconsistent(
                            path,
                            $"Supported map advertised Count {expectedCount} but emitted additional entries.");
                    }

                    var entry = contract.ReadEntry(enumerator.Current, path);
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        throw Inconsistent(path, "Supported map emitted a null, empty, or whitespace key.");
                    }

                    var key = entry.Key;
                    if (!seenKeys.Add(key))
                    {
                        throw Inconsistent(
                            path,
                            $"Supported map emitted duplicate key '{key}' under ordinal comparison.");
                    }

                    entries.Add(new MapEntry(key, entry.Value));
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }

            if (entries.Count != expectedCount)
            {
                throw Inconsistent(
                    path,
                    $"Supported map advertised Count {expectedCount} but emitted {entries.Count} entries.");
            }

            entries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));

            var properties = new WorkflowLiteralPropertyDocument[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var key = entry.Key
                    ?? throw Inconsistent(path, "Supported map emitted a null key after validation.");
                var valuePath = $"{path}.properties[\"{EscapePathKey(key)}\"].value";
                properties[index] = new WorkflowLiteralPropertyDocument(
                    key,
                    NormalizeLiteral(entry.Value, valuePath, activeContainers));
            }

            return new ObjectWorkflowLiteralDocument(properties);
        }
        finally
        {
            activeContainers.Remove(map);
        }
    }

    private static void EnterContainer(object container, string path, ISet<object> activeContainers)
    {
        if (!activeContainers.Add(container))
        {
            throw Inconsistent(path, "Cyclic workflow literal collection graph is not supported.");
        }
    }

    private static bool TryGetListContract(object value, string path, out ListContract contract)
    {
        var candidates = value.GetType().GetInterfaces()
            .Where(static candidate => candidate.IsGenericType)
            .Where(static candidate =>
            {
                var generic = candidate.GetGenericTypeDefinition();
                return generic == typeof(IReadOnlyList<>) || generic == typeof(IList<>);
            })
            .Distinct()
            .ToArray();

        if (candidates.Length == 0)
        {
            contract = default;
            return false;
        }

        var elementTypes = candidates
            .Select(static candidate => candidate.GetGenericArguments()[0])
            .Distinct()
            .ToArray();

        if (elementTypes.Length != 1)
        {
            throw Inconsistent(
                path,
                "Supported ordered collection exposes conflicting generic element contracts.");
        }

        var elementType = elementTypes[0];
        var listInterface = candidates
            .Where(candidate => candidate.GetGenericArguments()[0] == elementType)
            .OrderBy(static candidate =>
                candidate.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ? 0 : 1)
            .ThenBy(static candidate => candidate.FullName, StringComparer.Ordinal)
            .First();

        var countProperty = FindProperty(listInterface, "Count");
        var itemProperty = FindProperty(listInterface, "Item", typeof(int));
        if (countProperty is null || itemProperty is null)
        {
            throw Inconsistent(
                path,
                $"Supported ordered collection contract '{listInterface}' does not expose Count and integer indexer.");
        }

        contract = new ListContract(
            target => (int)(countProperty.GetValue(target)
                ?? throw new InvalidOperationException("Collection Count returned null.")),
            (target, index) => itemProperty.GetValue(target, [index]));
        return true;
    }

    private static bool TryGetMapContract(object value, string path, out MapContract contract)
    {
        var candidates = value.GetType().GetInterfaces()
            .Where(static candidate => candidate.IsGenericType)
            .Where(static candidate =>
            {
                var generic = candidate.GetGenericTypeDefinition();
                return generic == typeof(IReadOnlyDictionary<,>) || generic == typeof(IDictionary<,>);
            })
            .Where(static candidate => candidate.GetGenericArguments()[0] == typeof(string))
            .Distinct()
            .ToArray();

        if (candidates.Length == 0)
        {
            contract = default;
            return false;
        }

        var valueTypes = candidates
            .Select(static candidate => candidate.GetGenericArguments()[1])
            .Distinct()
            .ToArray();

        if (valueTypes.Length != 1)
        {
            throw Inconsistent(path, "Supported map exposes conflicting generic value contracts.");
        }

        var valueType = valueTypes[0];
        var mapInterface = candidates
            .Where(candidate => candidate.GetGenericArguments()[1] == valueType)
            .OrderBy(static candidate =>
                candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) ? 0 : 1)
            .ThenBy(static candidate => candidate.FullName, StringComparer.Ordinal)
            .First();

        var countProperty = FindProperty(mapInterface, "Count");
        if (countProperty is null)
        {
            throw Inconsistent(path, $"Supported map contract '{mapInterface}' does not expose Count.");
        }

        var pairType = typeof(KeyValuePair<,>).MakeGenericType(typeof(string), valueType);
        var keyProperty = pairType.GetProperty("Key")!;
        var valueProperty = pairType.GetProperty("Value")!;

        contract = new MapContract(
            target => (int)(countProperty.GetValue(target)
                ?? throw new InvalidOperationException("Map Count returned null.")),
            (entry, entryPath) =>
            {
                if (entry is null || !pairType.IsInstanceOfType(entry))
                {
                    throw Inconsistent(
                        entryPath,
                        "Supported map emitted an entry incompatible with its advertised key/value contract.");
                }

                return new MapEntry(
                    (string?)keyProperty.GetValue(entry),
                    valueProperty.GetValue(entry));
            });
        return true;
    }

    private static Type? FindGenericInterface(Type type, Type genericDefinition)
        => type.GetInterfaces()
            .Where(candidate => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == genericDefinition)
            .OrderBy(static candidate => candidate.FullName, StringComparer.Ordinal)
            .FirstOrDefault();

    private static bool ImplementsGeneric(Type type, Type genericDefinition)
        => FindGenericInterface(type, genericDefinition) is not null;

    private static PropertyInfo? FindProperty(
        Type interfaceType,
        string name,
        params Type[] indexParameterTypes)
    {
        var interfaces = interfaceType.GetInterfaces().Append(interfaceType);
        foreach (var candidate in interfaces)
        {
            foreach (var property in candidate.GetProperties())
            {
                if (!string.Equals(property.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = property.GetIndexParameters();
                if (parameters.Select(parameter => parameter.ParameterType)
                    .SequenceEqual(indexParameterTypes))
                {
                    return property;
                }
            }
        }

        return null;
    }

    private static AIAssetReferenceDocument ToDocument(AssetReference reference)
        => new()
        {
            AssetType = reference.Type switch
            {
                AssetType.Project => AIAssetDocumentType.Project,
                AssetType.Library => AIAssetDocumentType.Library,
                AssetType.Package => AIAssetDocumentType.Package,
                AssetType.Workflow => AIAssetDocumentType.Workflow,
                AssetType.Agent => AIAssetDocumentType.Agent,
                AssetType.Prompt => AIAssetDocumentType.Prompt,
                AssetType.Tool => AIAssetDocumentType.Tool,
                AssetType.Knowledge => AIAssetDocumentType.Knowledge,
                AssetType.Memory => AIAssetDocumentType.Memory,
                AssetType.Policy => AIAssetDocumentType.Policy,
                AssetType.Provider => AIAssetDocumentType.Provider,
                AssetType.Model => AIAssetDocumentType.Model,
                _ => throw new NotSupportedException(
                    $"Asset reference type '{reference.Type}' is not supported by AI Asset document schema v1.")
            },
            AssetId = reference.Id.ToString(),
            Urn = reference.Urn.Value,
            Version = reference.Version.Value
        };

    private static NotSupportedException Unsupported(string path, object value, string reason)
        => new(
            $"Workflow literal value at '{path}' is not supported. {reason} CLR type: '{value.GetType().FullName}'.");

    private static InvalidOperationException Inconsistent(
        string path,
        string reason,
        Exception? innerException = null)
        => new($"Workflow literal value at '{path}' is invalid. {reason}", innerException);

    private static string EscapePathKey(string key)
        => key.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private readonly record struct ListContract(
        Func<object, int> GetCount,
        Func<object, int, object?> GetItem);

    private readonly record struct MapContract(
        Func<object, int> GetCount,
        Func<object?, string, MapEntry> ReadEntry);

    private readonly record struct MapEntry(string? Key, object? Value);
}
