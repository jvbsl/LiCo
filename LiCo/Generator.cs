using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;

namespace LiCo;

[Generator]
public class Generator : IIncrementalGenerator
{
    private const string FilteredPackages = "LiCoFilteredPackages";
    private const string AdditionalLicenses = "LiCoAdditionalLicenses";
    private const string OutputFile = "LiCoOutput";
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var packagesAndLicenses = context.GetMSBuildItems((s) => s is FilteredPackages or AdditionalLicenses or OutputFile);

        context.RegisterSourceOutput(packagesAndLicenses.Collect(), Generate);
    }

    private void Generate(SourceProductionContext context, ImmutableArray<(string sourceItemGroup, AdditionalText file)> packagesAndLicenses)
    {
        var additionalLicenses = new List<string>();
        var packages = new HashSet<Package>();
        AdditionalText outputFile = null;
        foreach (var pOrL in packagesAndLicenses)
        {
            switch (pOrL.sourceItemGroup)
            {
                case AdditionalLicenses:
                    var content = (pOrL.file.GetText()?.ToString());
                    if (content is not null)
                        additionalLicenses.Add(content);
                    break;
                case FilteredPackages:
                    packages.Add(Program.ParsePackageTuple(Path.GetFileName(pOrL.file.Path)));
                    break;
                case OutputFile:
                    outputFile = pOrL.file;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (outputFile is null)
        {
            return;
        }

        var lico = new LiCo();

        lico.OnError += (sender, s) =>
                        {
                            context.ReportDiagnostic(Diagnostic.Create("LIC01", "LiCo",
                                $"Error while processing: {s}", DiagnosticSeverity.Warning,
                                DiagnosticSeverity.Warning, true, 1));
                        };
        
        lico.GenerateLicenseContent(outputFile.Path, additionalLicenses, packages);
    }
}