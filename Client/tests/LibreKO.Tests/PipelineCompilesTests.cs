using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PipelineCompilesTests
{
    [Fact]
    public void EachKindThatCompiledIsNamed() =>
        Assert.Equal("pipelines +7 (mesh 2, specialization 5)", PipelineCompiles.Describe(new long[] { 0, 2, 0, 0, 5 }));

    [Fact]
    public void NothingCompiledSaysSo() =>
        Assert.Equal("pipelines +0", PipelineCompiles.Describe(new long[5]));
}
