using System;
using Microsoft.CodeAnalysis;

namespace LiCo;

public static class GeneratorExtensions
{
    private const string SourceItemGroupMetadata = "build_metadata.AdditionalFiles.SourceItemGroup";
    public static IncrementalValuesProvider<(string, AdditionalText)> GetMSBuildItems(this IncrementalGeneratorInitializationContext context,
        Func<string, bool> nameMatcher)
    {
        return context.AdditionalTextsProvider.Combine(context.AnalyzerConfigOptionsProvider).Select((tuple, _) =>
        {
            if (tuple.Right.GetOptions(tuple.Left).TryGetValue(SourceItemGroupMetadata, out var sourceItemGroup)
                && nameMatcher(sourceItemGroup))
                return (sourceItemGroup, tuple.Left);
            return (null, tuple.Left);
        }).Where((x) => x.sourceItemGroup is not null);
    }
}