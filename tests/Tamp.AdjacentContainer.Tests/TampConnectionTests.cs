using System;
using System.Threading.Tasks;
using Xunit;

namespace Tamp.AdjacentContainer.Tests;

/// <summary>
/// TampConnection — pure unit tests for the value-holder semantics (mode, connection
/// string, disposal ownership). No Docker required.
/// </summary>
public sealed class TampConnectionTests
{
    [Fact]
    public void Constructor_Rejects_Empty_ConnectionString()
    {
        Assert.Throws<ArgumentException>(() =>
            new TampConnection("", AdjacentMode.Adjacent, dispose: null));
    }

    [Fact]
    public async Task Adjacent_DisposeAsync_Is_Safe_NoOp()
    {
        var conn = new TampConnection("Host=foo", AdjacentMode.Adjacent, dispose: null);

        // Two disposals — no exception, no double-call of any callback.
        await conn.DisposeAsync();
        await conn.DisposeAsync();

        Assert.Equal(AdjacentMode.Adjacent, conn.Mode);
        Assert.Equal("Host=foo", conn.ConnectionString);
    }

    [Fact]
    public async Task LocalSpawned_DisposeAsync_Invokes_Callback_Exactly_Once()
    {
        var calls = 0;
        var conn = new TampConnection("Host=local", AdjacentMode.LocalSpawned,
            dispose: () => { calls++; return ValueTask.CompletedTask; });

        await conn.DisposeAsync();
        await conn.DisposeAsync();
        await conn.DisposeAsync();

        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("Host=db;Database=test;Username=u;Password=p")]
    [InlineData("AccountName=devstoreaccount1;AccountKey=Eby8vd...")]
    [InlineData("Endpoint=sb://sb.example/;SharedAccessKeyName=root;SharedAccessKey=k")]
    [InlineData("unicode 🦀 Ω 한국어 in a connection string")]
    public void ConnectionString_Roundtrips_Verbatim(string input)
    {
        var conn = new TampConnection(input, AdjacentMode.Adjacent, dispose: null);
        Assert.Equal(input, conn.ConnectionString);
    }
}
