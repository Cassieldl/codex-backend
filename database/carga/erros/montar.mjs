/**
 * Erros conhecidos a partir das tabelas de rejeição copiadas da Documentação. Só funções puras: o
 * que vira erro, o que fica de fora e por quê.
 */

import {
  LIMITE_CAUSA,
  LIMITE_VARIANTE,
  derivarTitulo,
  origemDoCodigo,
} from '../emissor-nacional/transformar.mjs'
import {
  identificadoresEntreCrases,
  lerTabelas,
  normalizarComparacao,
  temMarcaDePendencia,
  textoSimples,
} from './documentacao.mjs'
import { PROVEDOR_NO_CODEX } from './fontes.mjs'

const FORMATO_DE_CODIGO = /^[A-Za-z]{0,6}\d{1,5}[A-Za-z]?$/
const CELULA_VAZIA = /^-?$/

function textoDaCelula(celula) {
  const texto = textoSimples(celula ?? '')
  return CELULA_VAZIA.test(texto) ? null : texto
}

function celulaDaColuna(tabela, linha, nomeDaColuna, arquivo) {
  const indice = tabela.cabecalho.indexOf(nomeDaColuna)
  if (indice < 0) {
    throw new Error(`A tabela "${tabela.secao}" de ${arquivo} não tem a coluna "${nomeDaColuna}".`)
  }
  return linha.celulas[indice] ?? ''
}

/** "P0010 / P0011" vale para os dois códigos; o resto é um código só. */
function separarCodigos(texto) {
  const partes = texto.split(/\s*\/\s*/)
  return partes.length > 1 && partes.every((parte) => FORMATO_DE_CODIGO.test(parte))
    ? partes
    : [texto]
}

/** "E0037 - Município emissor inexistente" -> código e o texto que o acompanha. */
function lerCodigoNoInicio(texto) {
  const achado = /^(\S+)(?:\s+-\s+(.+))?$/.exec(texto)
  if (!achado || !FORMATO_DE_CODIGO.test(achado[1])) return null
  return { codigo: achado[1], texto: achado[2] ?? null }
}

function lerCodigos(texto, regra) {
  if (!texto) return { motivo: 'linha sem código' }

  if (regra.codigoNoInicio) {
    const lido = lerCodigoNoInicio(texto)
    if (!lido) return { motivo: 'linha sem código: só a mensagem ou o sintoma' }
    return { codigos: [lido.codigo], reserva: lido.texto }
  }

  const codigos = separarCodigos(texto)
  const invalido = codigos.find((codigo) => !FORMATO_DE_CODIGO.test(codigo))
  if (invalido) return { motivo: `"${invalido}" não é código de erro` }
  return { codigos, reserva: null }
}

function provedorDaLinha(fonte, tabela, linha, regra) {
  if (fonte.provedor !== 'PorLinha') return { provedor: fonte.provedor }

  const nome = textoDaCelula(celulaDaColuna(tabela, linha, regra.colunas.provedor, fonte.arquivo))
  const noCodex = PROVEDOR_NO_CODEX[nome]
  return noCodex
    ? { provedor: noCodex }
    : { motivo: `provedor ${nome} não está no Codex`, foraDoCodex: nome }
}

function lerLinha(fonte, tabela, regra, linha) {
  const base = { fonte: fonte.arquivo, linha: linha.numero, secao: tabela.secao }
  if (temMarcaDePendencia(linha.texto)) {
    return { pulado: { ...base, motivo: 'linha marcada como pendente' } }
  }

  const {
    provedor,
    motivo: semProvedor,
    foraDoCodex,
  } = provedorDaLinha(fonte, tabela, linha, regra)
  if (semProvedor) return { pulado: { ...base, provedor: foraDoCodex, motivo: semProvedor } }

  const celula = (coluna) => (coluna ? celulaDaColuna(tabela, linha, coluna, fonte.arquivo) : '')
  const textoDoCodigo = textoSimples(celula(regra.colunas.codigo), { manterEnderecos: false })
  const { codigos, reserva, motivo } = lerCodigos(textoDoCodigo, regra)
  if (motivo) return { pulado: { ...base, provedor, codigo: null, motivo } }

  const foraDoPadrao =
    regra.codigoValido && codigos.find((codigo) => !regra.codigoValido.test(codigo))
  if (foraDoPadrao) {
    return {
      pulado: {
        ...base,
        provedor,
        codigo: foraDoPadrao,
        motivo: `código ${foraDoPadrao} não é desta lista`,
      },
    }
  }

  const colunasDeTexto = [regra.colunas.mensagem, regra.colunas.causa, regra.colunas.solucao]
  const nomesDeCampo = fonte.camposInvoicy
    ? identificadoresEntreCrases(colunasDeTexto.map(celula).join(' '))
    : []

  return {
    itens: codigos.map((codigo) => ({
      ...base,
      provedor,
      codigo,
      origem: fonte.origem === 'PorCodigo' ? origemDoCodigo(codigo) : fonte.origem,
      mensagem: regra.colunas.mensagem ? textoDaCelula(celula(regra.colunas.mensagem)) : null,
      reserva,
      causa: textoDaCelula(celula(regra.colunas.causa)),
      solucao: textoDaCelula(celula(regra.colunas.solucao)),
      nomesDeCampo,
    })),
  }
}

/**
 * Linha de uma tabela secundária cujo código está na lista oficial com outra mensagem: a lista do
 * provedor é a autoridade, e juntar as duas faria um erro só com dois significados.
 */
function separarDivergentes(itens, fonte) {
  const regrasComConferencia = fonte.tabelas.filter((regra) => regra.conferirCom)
  if (!regrasComConferencia.length) return { aceitos: itens, divergentes: [] }

  const aceitos = []
  const divergentes = []
  for (const item of itens) {
    const regra = regrasComConferencia.find((candidata) => candidata.secao.test(item.secao))
    const oficiais = regra
      ? itens.filter((outro) => regra.conferirCom.test(outro.secao) && outro.codigo === item.codigo)
      : []
    const confere =
      !oficiais.length ||
      oficiais.some(
        (oficial) =>
          normalizarComparacao(oficial.mensagem ?? '') ===
          normalizarComparacao(item.mensagem ?? ''),
      )
    if (confere) {
      aceitos.push(item)
      continue
    }
    divergentes.push({
      fonte: item.fonte,
      linha: item.linha,
      secao: item.secao,
      provedor: item.provedor,
      codigo: item.codigo,
      motivo: `mensagem diverge da lista oficial do provedor ("${oficiais[0].mensagem}")`,
    })
  }
  return { aceitos, divergentes }
}

/**
 * Lê as tabelas de uma fonte. Devolve as linhas que viram erro (`itens`), as que ficaram de fora
 * com o motivo (`pulados`) e as tabelas inteiras fora do recorte (`tabelasFora`). Seção marcada
 * como pendente fica de fora inteira, sem precisar de regra.
 */
export function lerFonte(fonte, markdown) {
  const lidos = []
  const pulados = []
  const tabelasFora = []

  for (const tabela of lerTabelas(markdown)) {
    if (tabela.secaoPendente) {
      for (const linha of tabela.linhas) {
        pulados.push({
          fonte: fonte.arquivo,
          linha: linha.numero,
          secao: tabela.secao,
          provedor: fonte.provedor,
          codigo: textoSimples(linha.celulas[0] ?? '', { manterEnderecos: false }),
          motivo: 'seção marcada como pendente',
        })
      }
      continue
    }

    const fora = fonte.fora.find((regra) => regra.secao.test(tabela.secao))
    if (fora) {
      tabelasFora.push({
        fonte: fonte.arquivo,
        secao: tabela.secao,
        linhas: tabela.linhas.length,
        motivo: fora.motivo,
      })
      continue
    }

    const regra = fonte.tabelas.find((candidata) => candidata.secao.test(tabela.secao))
    if (!regra) {
      throw new Error(`A tabela "${tabela.secao}" de ${fonte.arquivo} não tem regra de carga.`)
    }

    for (const linha of tabela.linhas) {
      const lida = lerLinha(fonte, tabela, regra, linha)
      if (lida.pulado) pulados.push(lida.pulado)
      else lidos.push(...lida.itens)
    }
  }

  const { aceitos, divergentes } = separarDivergentes(lidos, fonte)
  return { itens: aceitos, pulados: [...pulados, ...divergentes], tabelasFora }
}

function exigirLimite(texto, limite, descricao) {
  if (texto && texto.length > limite) {
    throw new Error(`${descricao} tem ${texto.length} caracteres e o limite é ${limite}.`)
  }
  return texto
}

function novoGrupo(item) {
  return {
    provedor: item.provedor,
    codigo: item.codigo,
    origem: item.origem,
    fonte: item.fonte,
    linhas: [],
    variantes: [],
    reservas: [],
    causaProvavel: null,
    solucaoSugerida: null,
    nomesDeCampo: new Set(),
  }
}

/**
 * Um erro por provedor e código. A mesma linha repetida na fonte vira uma variante só; mensagens
 * diferentes com o mesmo código viram variantes do mesmo erro. Causa e solução: a primeira que a
 * fonte traz. O texto ao lado do código (`reserva`) só vira variante quando não há mensagem.
 */
export function agruparErros(itens) {
  const grupos = new Map()

  for (const item of itens) {
    const chave = `${item.provedor ?? ''}|${item.codigo}`
    const grupo = grupos.get(chave) ?? novoGrupo(item)
    grupos.set(chave, grupo)

    grupo.linhas.push(item.linha)
    const conhecidas = new Set(grupo.variantes.map(normalizarComparacao))
    if (item.mensagem && !conhecidas.has(normalizarComparacao(item.mensagem))) {
      grupo.variantes.push(item.mensagem)
    }
    if (item.reserva) grupo.reservas.push(item.reserva)
    grupo.causaProvavel ??= item.causa
    grupo.solucaoSugerida ??= item.solucao
    for (const nome of item.nomesDeCampo) grupo.nomesDeCampo.add(nome)
  }

  return [...grupos.values()].map(({ reservas, nomesDeCampo, ...grupo }) => {
    const descricao = `${grupo.provedor ?? 'InvoiCy'} ${grupo.codigo}`
    const variantesParaCriar = grupo.variantes.length ? grupo.variantes : reservas.slice(0, 1)
    for (const variante of variantesParaCriar) {
      exigirLimite(variante, LIMITE_VARIANTE, `Variante de ${descricao}`)
    }

    return {
      ...grupo,
      titulo: variantesParaCriar.length ? derivarTitulo(variantesParaCriar[0]) : null,
      variantesParaCriar,
      causaProvavel: exigirLimite(grupo.causaProvavel, LIMITE_CAUSA, `Causa de ${descricao}`),
      solucaoSugerida: exigirLimite(grupo.solucaoSugerida, LIMITE_CAUSA, `Solução de ${descricao}`),
      nomesDeCampo: [...nomesDeCampo],
    }
  })
}
