namespace FluentAzure.Analyzers.Tests;

public class RequiredKeyNotBoundAnalyzerTests
{
    private const string Settings = """
        public class DatabaseSettings { public string Host { get; set; } = ""; public int Port { get; set; } }
        public class AppSettings
        {
            public string ConnectionString { get; set; } = "";
            public string DatabaseUrl { get; set; } = "";
            public DatabaseSettings Database { get; set; } = new();
            public System.Collections.Generic.Dictionary<string, string> Tags { get; set; } = new();
            public string[] Hosts { get; set; } = System.Array.Empty<string>();
        }
        """;

    [Fact]
    public Task KeysMatchingProperties_AreNotReported() =>
        AnalyzerVerifier<RequiredKeyNotBoundAnalyzer>.VerifyAsync(Settings + """
            class C
            {
                async System.Threading.Tasks.Task M()
                {
                    await FluentAzure.FluentConfig.Create()
                        .Required("ConnectionString")
                        .Required("connectionstring")
                        .Required("DATABASE_URL")
                        .Required("Database:Host")
                        .Required("Database__Port")
                        .Required("Database")
                        .Required("Tags:Anything:Below")
                        .Required("Hosts:0")
                        .BuildAsync<AppSettings>();
                }
            }
            """);

    [Fact]
    public Task MisspelledKeys_AreReported() =>
        AnalyzerVerifier<RequiredKeyNotBoundAnalyzer>.VerifyAsync(Settings + """
            class C
            {
                async System.Threading.Tasks.Task M()
                {
                    await FluentAzure.FluentConfig.Create()
                        .FromEnvironment()
                        .Required({|FAZ0001:"ConectionString"|})
                        .Optional("Timeout", "30")
                        .Required({|FAZ0001:"Database:Hots"|})
                        .BuildAsync<AppSettings>();
                }
            }
            """);

    [Fact]
    public Task BuildOptionalAsync_IsAnalyzedToo() =>
        AnalyzerVerifier<RequiredKeyNotBoundAnalyzer>.VerifyAsync(Settings + """
            class C
            {
                async System.Threading.Tasks.Task M()
                {
                    await FluentAzure.FluentConfig.Create().Required({|FAZ0001:"Nope"|}).BuildOptionalAsync<AppSettings>();
                }
            }
            """);

    [Fact]
    public Task NonGenericBuild_IsNotAnalyzed() =>
        AnalyzerVerifier<RequiredKeyNotBoundAnalyzer>.VerifyAsync("""
            class C
            {
                async System.Threading.Tasks.Task M()
                {
                    await FluentAzure.FluentConfig.Create().Required("Anything").BuildAsync();
                }
            }
            """);

    [Fact]
    public Task RecordConstructorParameters_AreBindable() =>
        AnalyzerVerifier<RequiredKeyNotBoundAnalyzer>.VerifyAsync("""
            public record ApiSettings(string BaseUrl, int TimeoutSeconds)
            {
                public ApiSettings() : this("", 0) { }
            }
            class C
            {
                async System.Threading.Tasks.Task M()
                {
                    await FluentAzure.FluentConfig.Create()
                        .Required("BaseUrl")
                        .Required("TimeoutSeconds")
                        .Required({|FAZ0001:"Retries"|})
                        .BuildAsync<ApiSettings>();
                }
            }
            """);
}
