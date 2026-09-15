using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Mappings;

/// <summary>Mapeamento compartilhado entre <c>ProvedorServicoInterno</c> (I2) e <c>AvisoServicoInterno</c> (I14a/I14a2/I14b/I14d).</summary>
public static class AvisoAdminMapper
{
    public static AvisoAdminDto Mapear(Aviso aviso) => new()
    {
        Id = aviso.Id,
        Escopo = aviso.ProvedorId is not null ? "Provedor" : "Versao",
        ProvedorId = aviso.ProvedorId,
        VersaoProvedorId = aviso.VersaoProvedorId,
        Severidade = aviso.Severidade.ToString(),
        Titulo = aviso.Titulo,
        Corpo = aviso.Corpo,
        VigenciaInicio = aviso.VigenciaInicio,
        Publicado = aviso.Publicado,
        CamposAfetadosIds = aviso.CamposAfetadosIds.ToArray(),
        VersaoConcorrencia = aviso.Versao,
    };
}
