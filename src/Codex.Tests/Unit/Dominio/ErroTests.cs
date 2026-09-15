using Codex.Domain.Entities;
using Codex.Domain.Enums;
using FluentAssertions;

namespace Codex.Tests.Unit.Dominio;

/// <summary>U9 — erro pode não ter versão de provedor aplicável. A regra de obrigatoriedade para origem Prefeitura é validada no Application, nunca no domínio.</summary>
public sealed class ErroTests
{
    [Theory]
    [InlineData(OrigemErro.Prefeitura)]
    [InlineData(OrigemErro.InvoiCy)]
    [InlineData(OrigemErro.Schema)]
    public void SubstituirVersoesAplicaveis_ComColecaoVazia_NuncaLancaNoDominio(OrigemErro origem)
    {
        var erro = Erro.Criar("E-001", origem, "Título do erro");

        var acao = () => erro.SubstituirVersoesAplicaveis([]);

        acao.Should().NotThrow(because: "U9: a obrigatoriedade de ao menos uma versão para Prefeitura é regra de Application, não de domínio");
        erro.VersoesAplicaveisIds.Should().BeEmpty();
    }

    [Fact]
    public void SubstituirVersoesAplicaveis_ComColecaoPreenchida_Substitui()
    {
        var erro = Erro.Criar("E-002", OrigemErro.Prefeitura, "Título");

        erro.SubstituirVersoesAplicaveis([1, 2, 3]);

        erro.VersoesAplicaveisIds.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void Criar_GeraSlugDeCodigoETitulo()
    {
        var erro = Erro.Criar("E-003", OrigemErro.Schema, "Falha de Validação");

        erro.Slug.Should().Contain("e-003");
    }

    [Fact]
    public void SubstituirVariantes_SubstituiColecaoInteira()
    {
        var erro = Erro.Criar("E-004", OrigemErro.InvoiCy, "Título");

        erro.SubstituirVariantes(["mensagem 1", "mensagem 2"]);

        erro.Variantes.Should().HaveCount(2);
        erro.Variantes.Select(v => v.Texto).Should().BeEquivalentTo(["mensagem 1", "mensagem 2"]);
    }

    [Fact]
    public void SubstituirCamposRelacionados_SubstituiColecaoInteira()
    {
        var erro = Erro.Criar("E-005", OrigemErro.Schema, "Título");

        erro.SubstituirCamposRelacionados([10, 20]);

        erro.CamposRelacionadosIds.Should().BeEquivalentTo([10, 20]);
    }

    [Fact]
    public void PublicarEDespublicar_AlteramApenasPublicado()
    {
        var erro = Erro.Criar("E-006", OrigemErro.Schema, "Título");

        erro.Publicado.Should().BeFalse();

        erro.Publicar();
        erro.Publicado.Should().BeTrue();

        erro.Despublicar();
        erro.Publicado.Should().BeFalse();
    }
}
