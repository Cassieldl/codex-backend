using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;

namespace Codex.Application.Services;

/// <summary>
/// <c>GET /comunicados</c>: o que está no ar para a página que o leitor abriu. "Agora" vem do
/// <see cref="TimeProvider"/> injetado e a vigência é avaliada a cada leitura — comunicado que
/// vence pela passagem do tempo não toca linha nenhuma, então nada aqui pode depender de ETag nem
/// de o job de expiração ter rodado.
/// </summary>
public sealed class ComunicadoServicoPublico : IComunicadoServicoPublico
{
    /// <summary>Teto de provedores por consulta — a página do erro lista as versões aplicáveis, que são poucas.</summary>
    public const int MaximoProvedores = 10;

    /// <summary>Largura das colunas <c>slug</c> de provedor e de versão.</summary>
    public const int MaximoSlug = 160;

    /// <summary>Defesa de leitura: nenhuma página legítima tem dezenas de comunicados ao mesmo tempo.</summary>
    public const int MaximoComunicados = 50;

    private readonly IComunicadoRepositorioPublico _repositorio;
    private readonly TimeProvider _relogio;

    public ComunicadoServicoPublico(IComunicadoRepositorioPublico repositorio, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _relogio = relogio;
    }

    public async Task<IReadOnlyCollection<ComunicadoPublicoDto>> ListarAsync(
        IReadOnlyCollection<string>? provedorSlugs, string? versaoSlug, CancellationToken ct)
    {
        var filtro = Validar(provedorSlugs, versaoSlug);

        var comunicados = await _repositorio.ListarVisiveisAsync(_relogio.GetUtcNow(), filtro, MaximoComunicados, ct);

        return comunicados.Select(Mapear).ToArray();
    }

    private static FiltroComunicadoPublico Validar(IReadOnlyCollection<string>? provedorSlugs, string? versaoSlug)
    {
        var provedores = provedorSlugs ?? [];

        if (provedores.Count > MaximoProvedores)
            throw new ParametroConsultaInvalidoException($"Informe no máximo {MaximoProvedores} provedores por consulta.");

        if (provedores.Any(SlugInvalido))
            throw new ParametroConsultaInvalidoException($"O slug do provedor deve ter de 1 a {MaximoSlug} caracteres.");

        var provedoresDistintos = provedores.Distinct(StringComparer.Ordinal).ToArray();

        if (versaoSlug is null)
            return new FiltroComunicadoPublico(provedoresDistintos, VersaoSlug: null);

        if (SlugInvalido(versaoSlug))
            throw new ParametroConsultaInvalidoException($"O slug da versão deve ter de 1 a {MaximoSlug} caracteres.");

        // O slug de versão é único por provedor: sem exatamente um provedor, não diz de qual versão se fala.
        if (provedoresDistintos.Length != 1)
            throw new ParametroConsultaInvalidoException("A versão só pode ser informada junto de exatamente um provedor.");

        return new FiltroComunicadoPublico(provedoresDistintos, versaoSlug);
    }

    private static bool SlugInvalido(string slug) =>
        string.IsNullOrWhiteSpace(slug) || slug.Length > MaximoSlug;

    private static ComunicadoPublicoDto Mapear(ComunicadoPublicoBrutoDto comunicado) => new()
    {
        Id = comunicado.Id,
        Titulo = comunicado.Titulo,
        Corpo = comunicado.Corpo,
        Severidade = comunicado.Severidade.ToString(),
        Abrangencia = comunicado.Abrangencia.ToString(),
        ExpiraEm = comunicado.ExpiraEm,
        Provedor = comunicado.ProvedorSlug is null || comunicado.ProvedorNome is null
            ? null
            : new ProvedorDoComunicadoDto { Nome = comunicado.ProvedorNome, Slug = comunicado.ProvedorSlug },
        Versao = comunicado.VersaoSlug is null || comunicado.VersaoRotulo is null
            ? null
            : new VersaoDoComunicadoDto { Rotulo = comunicado.VersaoRotulo, Slug = comunicado.VersaoSlug },
        Url = UrlComunicado.Montar(comunicado.Abrangencia, comunicado.ProvedorSlug, comunicado.VersaoSlug),
    };
}
