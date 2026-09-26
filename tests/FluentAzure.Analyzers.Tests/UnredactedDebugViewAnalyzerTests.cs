namespace FluentAzure.Analyzers.Tests;

public class UnredactedDebugViewAnalyzerTests
{
    [Fact]
    public Task GetDebugView_IsReported_AndRedactedViewIsNot() =>
        AnalyzerVerifier<UnredactedDebugViewAnalyzer>.VerifyAsync("""
            using FluentAzure;
            using Microsoft.Extensions.Configuration;
            class C
            {
                void M(IConfigurationRoot root)
                {
                    _ = {|FAZ0004:root.GetDebugView()|};
                    _ = root.GetRedactedDebugView();
                    _ = root.GetDebugView(context => "***");
                }
            }
            """);
}
