using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Prova (não só declara) a indexação de artigo na busca (arquitetura.md §5.A,
/// analise-backend.md §8) usando um <see cref="Artigo"/> REAL e o <see cref="IndiceBuscaWriter"/>
/// real — os mesmos três métodos que <c>ArtigoServicoInterno</c> (BACK-B) vai chamar depois.
///
/// <para>
/// O cenário de remoção é o item #1 desta unidade (PQ-04): sem o <c>case TipoItemIndexado.artigo</c>
/// em <see cref="IndiceBuscaWriter"/>, criar/publicar artigo passa em qualquer teste — só a
/// PRIMEIRA remoção de um artigo publicado lança <see cref="ArgumentOutOfRangeException"/>, porque
/// o switch de dependentes tem <c>default =&gt; throw</c>. Nenhum teste de criar/publicar cobre
/// esse caminho — só remover cobre.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ArtigoIndexacaoBuscaTests
{
    private readonly PostgresContainerFixture _fixture;

    public ArtigoIndexacaoBuscaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static IndiceBuscaWriter CriarWriter(CodexDbContext contexto) => new(contexto);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>
    /// <c>ExecuteReaderAsync</c> + <c>GetFieldValue&lt;DateTimeOffset&gt;</c> — nunca
    /// <c>ExecuteScalarAsync() as DateTimeOffset?</c>: o Npgsql desserializa <c>timestamptz</c>
    /// via <c>object</c> como <c>DateTime</c>, não <c>DateTimeOffset</c>; o cast <c>as</c> entre
    /// os dois structs falha em silêncio (devolve <c>null</c>) mesmo com valor real na coluna.
    /// </summary>
    private async Task<DateTimeOffset?> DesativadoEmDaLinhaAsync(int artigoId)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT desativadoem FROM codex.itensbusca WHERE tipoitem = 'artigo' AND itemid = @id";
        comando.Parameters.AddWithValue("id", artigoId);

        await using var leitor = await comando.ExecuteReaderAsync();
        if (!await leitor.ReadAsync())
            return null;

        return leitor.IsDBNull(0) ? null : leitor.GetFieldValue<DateTimeOffset>(0);
    }

    private async Task<int> ContarLinhasAsync(int artigoId)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM codex.itensbusca WHERE tipoitem = 'artigo' AND itemid = @id";
        comando.Parameters.AddWithValue("id", artigoId);

        return (int)(long)(await comando.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task AtualizarPesosAsync_IndexaArtigoComUrlCanonicaEForaDaBuscaPublica_AteSerPublicado()
    {
        var sufixo = NovoSufixo();
        var termo = $"artigoindexado{sufixo}";
        int artigoId;
        string slugEsperado;

        await using (var contexto = _fixture.CriarContexto())
        {
            var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Titulo {termo}", $"Corpo do {termo}");
            contexto.Artigos.Add(artigo);
            await contexto.SaveChangesAsync();
            artigoId = artigo.Id;
            slugEsperado = artigo.Slug;

            await CriarWriter(contexto).AtualizarPesosAsync(
                TipoItemIndexado.artigo, artigoId, pesoA: artigo.Titulo, pesoB: null, pesoC: artigo.Corpo,
                pesoD: artigo.Tipo.ToString(), CancellationToken.None);
        }

        await using (var conexao = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = "SELECT url, publicado FROM codex.itensbusca WHERE tipoitem = 'artigo' AND itemid = @id";
            comando.Parameters.AddWithValue("id", artigoId);
            await using var leitor = await comando.ExecuteReaderAsync();
            (await leitor.ReadAsync()).Should().BeTrue();
            leitor.GetString(0).Should().Be(SlugsReservadosArtigo.RotaCanonica(slugEsperado),
                because: "a URL indexada vem da fonte única RotaCanonica, não de interpolação manual");
            leitor.GetBoolean(1).Should().BeFalse(because: "artigo nasce despublicado (Artigo.Criar)");
        }

        await using var contextoBusca = _fixture.CriarContexto();
        var publico = await CriarConsulta(contextoBusca).BuscarAsync(
            termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: false, CancellationToken.None);
        publico.TotalGeral.Should().Be(0, because: "artigo indexado mas ainda não publicado não pode aparecer na busca pública");

        var interno = await CriarConsulta(contextoBusca).BuscarAsync(
            termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: true, CancellationToken.None);
        interno.TotalGeral.Should().Be(1, because: "a busca interna enxerga rascunho");
        interno.Grupos.Should().ContainSingle().Which.Tipo.Should().Be("artigo");
    }

    [Fact]
    public async Task RecalcularVisibilidadeAsync_PublicarEDespublicarArtigo_AlternaVisibilidadePublica()
    {
        var sufixo = NovoSufixo();
        var termo = $"artigociclo{sufixo}";
        int artigoId;

        await using (var contexto = _fixture.CriarContexto())
        {
            var artigo = Artigo.Criar(TipoArtigo.Procedimento, $"Titulo {termo}", $"Corpo do {termo}");
            contexto.Artigos.Add(artigo);
            await contexto.SaveChangesAsync();
            artigoId = artigo.Id;

            await CriarWriter(contexto).AtualizarPesosAsync(
                TipoItemIndexado.artigo, artigoId, artigo.Titulo, null, artigo.Corpo, artigo.Tipo.ToString(), CancellationToken.None);
        }

        await using (var contexto = _fixture.CriarContexto())
        {
            var artigoRastreado = await contexto.Artigos.FirstAsync(a => a.Id == artigoId);
            artigoRastreado.Publicar();
            await contexto.SaveChangesAsync();

            await CriarWriter(contexto).RecalcularVisibilidadeAsync(TipoItemBusca.Artigo, artigoId, CancellationToken.None);
        }

        await using (var contextoBusca = _fixture.CriarContexto())
        {
            var resultado = await CriarConsulta(contextoBusca).BuscarAsync(
                termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: false, CancellationToken.None);
            resultado.TotalGeral.Should().Be(1, because: "publicar o artigo tem de refletir na busca pública na mesma operação, sem passo assíncrono");
        }

        await using (var contexto = _fixture.CriarContexto())
        {
            var artigoRastreado = await contexto.Artigos.FirstAsync(a => a.Id == artigoId);
            artigoRastreado.Despublicar();
            await contexto.SaveChangesAsync();

            await CriarWriter(contexto).RecalcularVisibilidadeAsync(TipoItemBusca.Artigo, artigoId, CancellationToken.None);
        }

        await using (var contextoBusca = _fixture.CriarContexto())
        {
            var publico = await CriarConsulta(contextoBusca).BuscarAsync(
                termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: false, CancellationToken.None);
            publico.TotalGeral.Should().Be(0, because: "despublicar o artigo tem de remover da busca pública imediatamente");

            var interno = await CriarConsulta(contextoBusca).BuscarAsync(
                termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: true, CancellationToken.None);
            interno.TotalGeral.Should().Be(1, because: "despublicar não remove a linha do índice, só da superfície pública");
        }
    }

    /// <summary>
    /// PQ-04 — item #1 obrigatório. Sem <c>case TipoItemIndexado.artigo</c> em
    /// <c>DesativarDependentesAsync</c>, esta chamada lança <see cref="ArgumentOutOfRangeException"/>
    /// na primeira remoção de artigo em produção — nenhum teste de criar/publicar cobre este caminho.
    /// </summary>
    [Fact]
    public async Task DesativarAsync_ArtigoPublicado_MarcaDesativadoEmNaLinhaDeProjecao_SemLancar()
    {
        var sufixo = NovoSufixo();
        var termo = $"artigoremovido{sufixo}";
        int artigoId;

        await using (var contexto = _fixture.CriarContexto())
        {
            var artigo = Artigo.Criar(TipoArtigo.Mudanca, $"Titulo {termo}", $"Corpo do {termo}");
            artigo.Publicar();
            contexto.Artigos.Add(artigo);
            await contexto.SaveChangesAsync();
            artigoId = artigo.Id;

            await CriarWriter(contexto).AtualizarPesosAsync(
                TipoItemIndexado.artigo, artigoId, artigo.Titulo, null, artigo.Corpo, artigo.Tipo.ToString(), CancellationToken.None);
            await CriarWriter(contexto).RecalcularVisibilidadeAsync(TipoItemBusca.Artigo, artigoId, CancellationToken.None);
        }

        (await ContarLinhasAsync(artigoId)).Should().Be(1);
        (await DesativadoEmDaLinhaAsync(artigoId)).Should().BeNull(because: "ainda não foi removido");

        await using (var contexto = _fixture.CriarContexto())
        {
            var artigoRastreado = await contexto.Artigos.FirstAsync(a => a.Id == artigoId);
            artigoRastreado.Desativar();
            await contexto.SaveChangesAsync();

            Exception? excecao = null;
            try
            {
                await CriarWriter(contexto).DesativarAsync(TipoItemIndexado.artigo, artigoId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                excecao = ex;
            }

            excecao.Should().BeNull(
                because: "artigo é folha do grafo de projeção (§5.A) — precisa de case EXPLÍCITO em DesativarDependentesAsync (PQ-04), mesmo sem dependente nenhum");
        }

        (await DesativadoEmDaLinhaAsync(artigoId)).Should().NotBeNull(
            because: "remover o artigo publicado precisa desativar a linha de projeção correspondente");

        await using var contextoBusca = _fixture.CriarContexto();
        var publico = await CriarConsulta(contextoBusca).BuscarAsync(
            termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: false, CancellationToken.None);
        var interno = await CriarConsulta(contextoBusca).BuscarAsync(
            termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: true, CancellationToken.None);

        publico.TotalGeral.Should().Be(0, because: "linha desativada não pode aparecer na busca pública");
        interno.TotalGeral.Should().Be(0, because: "o índice GIN interno filtra desativadoem IS NULL — desativado some das duas superfícies");
    }

    [Fact]
    public async Task ArtigoVerificado_PreencheMetaEstruturadaComADeVerificacao()
    {
        var sufixo = NovoSufixo();
        var termo = $"artigoverificado{sufixo}";
        int artigoId;
        var dataVerificacao = new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero);

        await using (var contexto = _fixture.CriarContexto())
        {
            var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Titulo {termo}", $"Corpo do {termo}");
            artigo.Publicar();
            artigo.RegistrarVerificacao(dataVerificacao);
            contexto.Artigos.Add(artigo);
            await contexto.SaveChangesAsync();
            artigoId = artigo.Id;

            await CriarWriter(contexto).AtualizarPesosAsync(
                TipoItemIndexado.artigo, artigoId, artigo.Titulo, null, artigo.Corpo, artigo.Tipo.ToString(), CancellationToken.None);
            await CriarWriter(contexto).RecalcularVisibilidadeAsync(TipoItemBusca.Artigo, artigoId, CancellationToken.None);
        }

        await using var contextoBusca = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contextoBusca).BuscarAsync(
            termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: false, CancellationToken.None);

        var item = resultado.Grupos.SelectMany(g => g.Itens).Should().ContainSingle().Subject;

        item.Meta.Should().NotBeNull(because: "B-5: artigo verificado mostra o mesmo selo que já existe em versaoprovedor");
        item.Meta!.Data.Should().Be(DateOnly.FromDateTime(dataVerificacao.UtcDateTime));
        item.Publicado.Should().BeTrue();
    }
}
