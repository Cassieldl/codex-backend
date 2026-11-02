/**
 * Grava pela API os erros montados da Documentação. Só cria e acrescenta: nunca apaga, nunca
 * reescreve o que já está no erro e nunca publica.
 *
 * Erro que já existe (mesmo provedor e código) prevalece. A carga só acrescenta o que falta:
 * variante nova, causa ou solução vazias, campo relacionado. Se o erro estiver publicado, o
 * acréscimo apareceria no portal na hora, então fica só no relatório.
 */

import { chaveDeSlug } from '../emissor-nacional/transformar-layout.mjs'
import { normalizarComparacao } from './documentacao.mjs'

/** Coluna `slug` de `codex.erros` (LimitesErro.Slug). */
const LIMITE_SLUG = 200

function vazio(texto) {
  return !texto || !texto.trim()
}

function uniao(atuais, novos) {
  return [...new Set([...atuais, ...novos])].sort((a, b) => a - b)
}

export function slugDoErro(codigo, titulo) {
  return chaveDeSlug(`${codigo}-${titulo}`)
}

/**
 * O que acrescentar a um erro existente. Devolve o corpo completo do PUT (a API substitui o erro
 * inteiro) ou `null` quando não falta nada, e a lista legível do que seria acrescentado.
 * `versoesIds` só vem quando o erro de outro provedor passa a valer também para este.
 */
export function planejarAcrescimo(detalhe, desejado, camposLayoutIds, versoesIds = []) {
  const acrescimos = []

  const variantes = [...detalhe.variantes]
  const conhecidas = new Set(variantes.map(normalizarComparacao))
  for (const variante of desejado.variantes) {
    if (conhecidas.has(normalizarComparacao(variante))) continue
    variantes.push(variante)
    conhecidas.add(normalizarComparacao(variante))
    acrescimos.push(`variante "${variante}"`)
  }

  let { causaProvavel, solucaoSugerida } = detalhe
  if (vazio(causaProvavel) && desejado.causaProvavel) {
    causaProvavel = desejado.causaProvavel
    acrescimos.push('causa provável')
  }
  if (vazio(solucaoSugerida) && desejado.solucaoSugerida) {
    solucaoSugerida = desejado.solucaoSugerida
    acrescimos.push('solução sugerida')
  }

  const campos = uniao(detalhe.camposLayoutIds, camposLayoutIds)
  if (campos.length !== detalhe.camposLayoutIds.length) {
    acrescimos.push(`${campos.length - detalhe.camposLayoutIds.length} campo(s) relacionado(s)`)
  }

  const versoes = uniao(detalhe.versoesProvedorIds, versoesIds)
  if (versoes.length !== detalhe.versoesProvedorIds.length) {
    acrescimos.push(
      `versão ${versoes.filter((id) => !detalhe.versoesProvedorIds.includes(id)).join(', ')}`,
    )
  }

  if (!acrescimos.length) return { corpo: null, acrescimos }
  return {
    acrescimos,
    corpo: {
      origem: detalhe.origem,
      titulo: detalhe.titulo,
      causaProvavel,
      solucaoSugerida,
      variantes,
      versoesProvedorIds: versoes,
      camposLayoutIds: campos,
    },
  }
}

/**
 * Ids dos campos do Layout InvoiCy citados pelo nome. Nome que se repete em grupos diferentes é
 * ambíguo e fica de fora, como nome que não existe no layout.
 */
export function resolverCamposPorNome(nomes, campos) {
  const idsPorNome = new Map()
  for (const campo of campos) {
    const nome = campo.caminhoXml.split('/').pop()
    idsPorNome.set(nome, [...(idsPorNome.get(nome) ?? []), campo.id])
  }

  const ids = []
  const ignorados = []
  for (const nome of nomes) {
    const encontrados = idsPorNome.get(nome) ?? []
    if (encontrados.length === 1) ids.push(encontrados[0])
    else ignorados.push(`${nome} (${encontrados.length ? 'ambíguo' : 'não está no layout'})`)
  }
  return { ids: [...new Set(ids)], ignorados }
}

function rotuloDoErro(erro) {
  return `${erro.provedor ?? 'InvoiCy'} ${erro.codigo}`
}

/**
 * Grava o acréscimo planejado, ou só registra quando o erro está publicado: no portal, o acréscimo
 * apareceria na hora.
 */
async function aplicarAcrescimo(api, detalhe, plano, registro, resultado, destino) {
  if (!plano.corpo) {
    resultado.semMudanca.push(registro)
    return
  }
  if (detalhe.publicado) {
    resultado.soRelatorio.push({ ...registro, acrescimos: plano.acrescimos })
    return
  }
  await api.chamar(
    'PUT',
    `/interno/erros/${detalhe.id}`,
    plano.corpo,
    String(detalhe.versaoConcorrencia),
  )
  destino.push({ ...registro, acrescimos: plano.acrescimos })
}

/**
 * Cria ou acrescenta cada erro. `contexto` traz o que já foi lido da API: provedores por nome,
 * versões por provedor, erros existentes por provedor e todos os slugs ocupados.
 */
export async function sincronizarErros(api, erros, contexto) {
  const resultado = {
    criados: [],
    acrescentados: [],
    vinculados: [],
    soRelatorio: [],
    semMudanca: [],
    colisoes: [],
    aguardando: [],
    pulados: [],
  }

  for (const erro of erros) {
    const registro = { fonte: erro.fonte, provedor: erro.provedor, codigo: erro.codigo }
    const provedor = erro.provedor ? contexto.provedorPorNome.get(erro.provedor) : null

    if (erro.provedor && !provedor) {
      const destino = contexto.provedoresEmCriacao.has(erro.provedor)
        ? resultado.aguardando
        : resultado.pulados
      destino.push({ ...registro, motivo: `provedor ${erro.provedor} não existe no banco` })
      continue
    }
    if (contexto.aguardar.has(erro.provedor)) {
      resultado.aguardando.push({
        ...registro,
        motivo: `provedor ${erro.provedor} ainda em carga; rodar de novo sem --aguardar`,
      })
      continue
    }

    const versoesIds = provedor ? contexto.versoesPorProvedor.get(provedor.id) : []
    if (erro.origem === 'Prefeitura' && !versoesIds.length) {
      resultado.pulados.push({ ...registro, motivo: `provedor ${erro.provedor} não tem versão` })
      continue
    }

    const { ids: camposLayoutIds, ignorados } = resolverCamposPorNome(
      erro.nomesDeCampo,
      contexto.camposDoLayout,
    )
    if (ignorados.length) registro.camposIgnorados = ignorados

    const existentes = contexto.errosPorProvedor.get(provedor?.slug ?? 'InvoiCy')
    const existente = existentes.get(erro.codigo)
    if (existente) {
      const { dados: detalhe } = await api.chamar('GET', `/interno/erros/${existente.id}`)
      const plano = planejarAcrescimo(detalhe, erro, camposLayoutIds)
      await aplicarAcrescimo(api, detalhe, plano, registro, resultado, resultado.acrescentados)
      continue
    }

    if (!erro.titulo) {
      resultado.pulados.push({ ...registro, motivo: 'sem mensagem na fonte para criar o erro' })
      continue
    }

    const slug = slugDoErro(erro.codigo, erro.titulo)
    if (slug.length > LIMITE_SLUG) {
      resultado.pulados.push({ ...registro, motivo: `slug com ${slug.length} caracteres` })
      continue
    }
    // Mesmo código e mesma mensagem em outro provedor (padrão ABRASF) é o mesmo erro: a versão
    // deste provedor entra nas versões aplicáveis dele, em vez de um erro novo com outro endereço.
    const dono = contexto.slugsOcupados.get(slug)
    if (dono) {
      const { dados: detalhe } = await api.chamar('GET', `/interno/erros/${dono.id}`)
      if (detalhe.codigo !== erro.codigo || detalhe.origem !== erro.origem) {
        resultado.colisoes.push({ ...registro, slug, dono: dono.rotulo })
        continue
      }
      const plano = planejarAcrescimo(detalhe, erro, camposLayoutIds, versoesIds)
      const vinculo = { ...registro, slug, dono: dono.rotulo }
      await aplicarAcrescimo(api, detalhe, plano, vinculo, resultado, resultado.vinculados)
      existentes.set(erro.codigo, { id: dono.id })
      continue
    }

    try {
      const { dados: criado } = await api.chamar('POST', '/interno/erros', {
        codigo: erro.codigo,
        origem: erro.origem,
        titulo: erro.titulo,
        causaProvavel: erro.causaProvavel,
        solucaoSugerida: erro.solucaoSugerida,
        variantes: erro.variantesParaCriar,
        versoesProvedorIds: versoesIds,
        camposLayoutIds,
      })
      contexto.slugsOcupados.set(slug, { id: criado.id, rotulo: rotuloDoErro(erro) })
      existentes.set(erro.codigo, { id: criado.id })
      resultado.criados.push({ ...registro, id: criado.id, slug })
    } catch (falha) {
      // Erro removido continua com o slug reservado e não aparece na listagem.
      if (!/-> 409:/.test(falha.message)) throw falha
      resultado.colisoes.push({ ...registro, slug, dono: 'erro removido (slug ainda reservado)' })
    }
  }

  return resultado
}
