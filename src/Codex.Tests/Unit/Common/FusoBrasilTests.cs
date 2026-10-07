using Codex.Application.Common;
using FluentAssertions;

namespace Codex.Tests.Unit.Common;

public sealed class FusoBrasilTests
{
    [Theory]
    [InlineData("2026-10-05T15:00:00Z", "2026-10-05")]
    [InlineData("2026-10-06T02:59:59Z", "2026-10-05")]
    [InlineData("2026-10-06T03:00:00Z", "2026-10-06")]
    [InlineData("2026-10-05T00:00:00Z", "2026-10-04")]
    [InlineData("2027-01-01T01:00:00Z", "2026-12-31")]
    public void Hoje_EODiaDeBrasilia_NaoODiaEmUtc(string instanteUtc, string diaEsperado)
    {
        var relogio = new RelogioFixo(DateTimeOffset.Parse(instanteUtc));

        FusoBrasil.Hoje(relogio).Should().Be(DateOnly.Parse(diaEsperado));
    }

    private sealed class RelogioFixo : TimeProvider
    {
        private readonly DateTimeOffset _agora;

        public RelogioFixo(DateTimeOffset agora)
        {
            _agora = agora;
        }

        public override DateTimeOffset GetUtcNow() => _agora;
    }
}
