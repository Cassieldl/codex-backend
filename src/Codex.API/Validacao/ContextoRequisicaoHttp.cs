using Codex.Application.Interfaces;

namespace Codex.API.Validacao;

/// <summary>Implementação de <see cref="IContextoRequisicaoHttp"/> sobre <see cref="IHttpContextAccessor"/> (ver interface para o porquê).</summary>
public sealed class ContextoRequisicaoHttp : IContextoRequisicaoHttp
{
    private readonly IHttpContextAccessor _acessorContexto;

    public ContextoRequisicaoHttp(IHttpContextAccessor acessorContexto)
    {
        _acessorContexto = acessorContexto;
    }

    public int? ObterIdDaRota(string chave = "id")
    {
        var valor = _acessorContexto.HttpContext?.Request.RouteValues[chave]?.ToString();
        return int.TryParse(valor, out var id) ? id : null;
    }
}
