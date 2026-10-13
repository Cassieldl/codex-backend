/**
 * Exemplos reais dos XML dos provedores: lê as fontes versionadas em `provedores/material/` e
 * decide, por campo, qual valor pode ser publicado como exemplo. Só funções puras, exceto a leitura
 * do disco.
 *
 * Um valor só vira exemplo quando o caminho completo do destino existe na fonte. Nunca se casa por
 * nome de folha nem por sufixo, e nenhum valor é alterado. Todo descarte é resultado e vai para o
 * relatório com o motivo; nenhum descarte lança.
 */

import { readFile } from 'node:fs/promises'

import { lerXml } from './provedores/xml.mjs'

const PASTA_DO_MATERIAL = new URL('./provedores/material/', import.meta.url)

export const TETO_DO_EXEMPLO = 500

export const FONTES_DE_EXEMPLO = {
  'dps-nacional': {
    id: 'dps-nacional',
    raiz: 'DPS',
    prefixoDoEnvelope: '',
    arquivos: [
      'dps-nacional-1.01/exemplos/exemplo_dps.xml',
      'dps-nacional-1.01/exemplos/exemplo_dps_ibscbs.xml',
    ],
  },
  elotech: {
    id: 'elotech',
    raiz: 'EnviarLoteRpsEnvio',
    prefixoDoEnvelope: 'EnviarLoteRpsEnvio/LoteRps/ListaRps/Rps/',
    arquivos: ['elotech/exemplos/EnviarLoteRpsEnvio.xml'],
  },
}

export const MOTIVOS = {
  ATRIBUTO: 'a tag é atributo XML',
  GRUPO: 'a tag é um grupo (tem filhos)',
  ENVELOPE: 'a tag fica no envelope do lote, fora do trecho que a fonte exemplifica',
  ALTERNATIVA: 'a tag recebe um campo OU outro (ex.: CPF ou CNPJ)',
  NAO_ESTA_NA_FONTE: 'o caminho não aparece na fonte',
  FONTE_COM_VALOR_VAZIO: 'a fonte traz a tag sem valor',
  IDENTIFICACAO: 'dado de identificação, omitido (U1)',
  AMBIGUO: 'a fonte traz mais de um valor diferente nesse caminho',
  DESCARTE_EDITORIAL: 'valor descartado por decisão editorial',
  MARCADOR_TESTE: 'valor de teste (U2)',
  MARCADOR_LETRA_REPETIDA: 'valor de preenchimento com letra repetida (U2)',
  DESTINOS_DIVERGEM: 'os destinos do campo trazem valores diferentes',
  FORA_DO_TIPO: 'o valor não é do tipo do campo',
  FORA_DO_DOMINIO: 'o valor não está entre os valores aceitos',
  ACIMA_DO_TETO: `o valor passa de ${TETO_DO_EXEMPLO} caracteres`,
}

const MOTIVO_DA_COMBINACAO_FISCAL =
  'Valores de preenchimento de teste do XML: CST 123, cClassTrib 745896, cCredPres 23, CSTReg 859 e cClassTribReg 123569 não formam uma combinação fiscal coerente.'

const GRUPO_DO_IBSCBS = 'InfDeclaracaoPrestacaoServico/InfIbsCbs/valores/trib/gIBSCBS'

/**
 * Caminhos (relativos ao envelope) cujo valor é preenchimento de teste da fonte: o XSD só impõe o
 * formato (`[0-9]{3}`, `[0-9]{6}`), então nenhum filtro de tipo ou domínio os pega. Por id de fonte.
 */
export const DESCARTES_EDITORIAIS = {
  elotech: new Map(
    [
      `${GRUPO_DO_IBSCBS}/Cst`,
      `${GRUPO_DO_IBSCBS}/cClassTrib`,
      `${GRUPO_DO_IBSCBS}/cCredPres`,
      `${GRUPO_DO_IBSCBS}/gTribRegular/CstReg`,
      `${GRUPO_DO_IBSCBS}/gTribRegular/cClassTribReg`,
    ].map((caminho) => [caminho, MOTIVO_DA_COMBINACAO_FISCAL]),
  ),
}

/** Folhas de identificação em qualquer contexto (comparação exata, sensível a maiúscula). */
const NOMES_DE_IDENTIFICACAO = new Set([
  'CNPJ',
  'CPF',
  'CAEPF',
  'NIF',
  'IM',
  'Cnpj',
  'Cpf',
  'NifTomador',
  'InscricaoMunicipal',
  'InscricaoEstadual',
  'inscImobFisc',
  'cCIB',
  'xNome',
  'XNome',
  'RazaoSocial',
  'fone',
  'Telefone',
  'email',
  'Email',
  'xLgr',
  'nro',
  'xCpl',
  'xBairro',
  'CEP',
  'Cep',
  'cEndPost',
  'xCidade',
  'xEstProvReg',
  // A chave de acesso embute o CNPJ do emitente.
  'chSubstda',
  'chNFSe',
  'chNFe',
  'refNFSe',
])

/** `Numero` também é o número do RPS: só é endereço quando o pai direto é de endereço. */
const NOMES_DE_ENDERECO = new Set(['Endereco', 'Numero', 'Complemento', 'Bairro'])
const PAIS_DE_ENDERECO = new Set(['Endereco', 'end'])

const PADRAO_NUMERICO = /^-?\d+(\.\d+)?$/
const PADRAO_DATA = /^\d{4}-\d{2}-\d{2}$/
const PADRAO_DATA_HORA = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?$/
const PADRAO_DE_TESTE = /\b(teste|test)\b/i
const PADRAO_DE_LETRA_REPETIDA = /^([A-Za-z])\1*$/

/** A decisão é sobre o caminho do destino, não sobre o da fonte. */
export function ehFolhaDeIdentificacao(caminho) {
  const [folha, pai] = caminho.split('/').reverse()
  if (NOMES_DE_IDENTIFICACAO.has(folha)) return true
  return NOMES_DE_ENDERECO.has(folha) && PAIS_DE_ENDERECO.has(pai)
}

function percorrer(no, caminhoDoPai, visitar) {
  // Assinatura digital traz certificado com dado pessoal e nunca é exemplo.
  if (no.nome === 'Signature') return

  const caminho = caminhoDoPai ? `${caminhoDoPai}/${no.nome}` : no.nome
  const ehFolha = no.filhos.length === 0
  visitar({ caminho, ehFolha, valor: no.texto.trim() })
  for (const filho of no.filhos) percorrer(filho, caminho, visitar)
}

function caminhoSemEnvelope(caminho, prefixoDoEnvelope) {
  if (!prefixoDoEnvelope) return caminho
  return caminho.startsWith(prefixoDoEnvelope) ? caminho.slice(prefixoDoEnvelope.length) : null
}

/**
 * Lê os textos XML da fonte. `valoresPorCaminho` guarda todas as ocorrências de cada folha;
 * `grupos` guarda os caminhos que têm filhos; `folhasLidas` conta toda folha lida e
 * `foraDoEnvelope` as que ficaram fora do trecho exemplificado e não foram guardadas.
 */
export function lerFonteDeExemplo(textos, fonte) {
  const valoresPorCaminho = new Map()
  const grupos = new Set()
  let folhasLidas = 0
  let foraDoEnvelope = 0

  for (const texto of textos) {
    const raiz = lerXml(texto)
    if (raiz.nome !== fonte.raiz) {
      throw new Error(
        `A fonte de exemplo "${fonte.id}" exige a raiz <${fonte.raiz}> e o arquivo traz <${raiz.nome}>.`,
      )
    }

    percorrer(raiz, '', ({ caminho, ehFolha, valor }) => {
      const caminhoNaFonte = caminhoSemEnvelope(caminho, fonte.prefixoDoEnvelope)
      if (ehFolha) folhasLidas++
      if (caminhoNaFonte === null) {
        if (ehFolha) foraDoEnvelope++
        return
      }

      if (!ehFolha) {
        grupos.add(caminhoNaFonte)
        return
      }
      const valoresDoCaminho = valoresPorCaminho.get(caminhoNaFonte) ?? []
      valoresDoCaminho.push(valor)
      valoresPorCaminho.set(caminhoNaFonte, valoresDoCaminho)
    })
  }

  return { id: fonte.id, valoresPorCaminho, grupos, folhasLidas, foraDoEnvelope }
}

/** Lê os arquivos da fonte em `provedores/material/`. Fonte ou arquivo ausente interrompe a carga. */
export async function lerFonteDeExemploDoDisco(idDaFonte) {
  const fonte = FONTES_DE_EXEMPLO[idDaFonte]
  if (!fonte) throw new Error(`Fonte de exemplo desconhecida: "${idDaFonte}".`)

  const textos = await Promise.all(
    fonte.arquivos.map(async (arquivo) => {
      try {
        return await readFile(new URL(arquivo, PASTA_DO_MATERIAL), 'utf8')
      } catch (erro) {
        throw new Error(`Arquivo de exemplo ausente para a fonte "${idDaFonte}": ${arquivo}.`, {
          cause: erro,
        })
      }
    }),
  )
  return lerFonteDeExemplo(textos, fonte)
}

function codigosDoDominio(dominio) {
  if (!dominio) return []
  if (!dominio.includes(' = ')) return dominio.split(', ')
  return dominio.split('; ').map((par) => par.split(' = ')[0])
}

const semValor = (motivo, detalhe = null) => ({ estagio: 'sem-valor', motivo, detalhe })
const descartado = (motivo, detalhe = null) => ({ estagio: 'descartado', motivo, detalhe })

/** Só dica para quem lê o relatório: o casamento nunca usa a grafia próxima. */
function dicaDeGrafia(caminhoNaFonte, valoresPorCaminho) {
  const procurado = caminhoNaFonte.toLowerCase()
  const parecido = [...valoresPorCaminho.keys()].find((chave) => chave.toLowerCase() === procurado)
  return parecido ? `a fonte traz "${parecido}" (grafia diferente)` : null
}

function descreverMarcador(valor, campoVersao) {
  if (PADRAO_DE_TESTE.test(valor)) return 'MARCADOR_TESTE'
  const ehCodigoDoDominio = codigosDoDominio(campoVersao.dominio).includes(valor)
  if (PADRAO_DE_LETRA_REPETIDA.test(valor) && !ehCodigoDoDominio) return 'MARCADOR_LETRA_REPETIDA'
  return null
}

function avaliarValores(valores, caminhoNaFonte, fonteLida, campoVersao) {
  const distintos = [...new Set(valores.filter((valor) => valor !== ''))]
  if (!distintos.length) return semValor('FONTE_COM_VALOR_VAZIO')
  if (distintos.length > 1) return descartado('AMBIGUO', `valores: ${distintos.join(' | ')}`)

  const [valor] = distintos
  const motivoEditorial = DESCARTES_EDITORIAIS[fonteLida.id]?.get(caminhoNaFonte)
  if (motivoEditorial) return descartado('DESCARTE_EDITORIAL', motivoEditorial)

  const marcador = descreverMarcador(valor, campoVersao)
  if (marcador) return descartado(marcador, `valor "${valor}"`)

  return { estagio: 'aceito', motivo: null, detalhe: null, valor }
}

function avaliarDestino(destino, fonteLida, campoVersao, prefixoDestino) {
  const { caminho, elemento, alternativa } = destino
  if (alternativa) return semValor('ALTERNATIVA')
  if (elemento === 'A') return semValor('ATRIBUTO')
  if (elemento === 'G' || elemento === 'CG') return semValor('GRUPO')
  if (prefixoDestino && !caminho.startsWith(prefixoDestino)) return semValor('ENVELOPE')

  const caminhoNaFonte = caminho.slice(prefixoDestino.length)
  if (fonteLida.grupos.has(caminhoNaFonte)) {
    return semValor('GRUPO', 'a fonte traz filhos nesse caminho')
  }

  const valores = fonteLida.valoresPorCaminho.get(caminhoNaFonte)
  if (!valores) {
    return semValor('NAO_ESTA_NA_FONTE', dicaDeGrafia(caminhoNaFonte, fonteLida.valoresPorCaminho))
  }
  if (ehFolhaDeIdentificacao(caminho)) return descartado('IDENTIFICACAO')
  return avaliarValores(valores, caminhoNaFonte, fonteLida, campoVersao)
}

function verificarConformidade(valor, { tipo, dominio }) {
  if (tipo === 'Numérico' && !PADRAO_NUMERICO.test(valor)) {
    return { motivo: 'FORA_DO_TIPO', detalhe: `valor "${valor}" não é numérico` }
  }
  if (tipo === 'Data' && !PADRAO_DATA.test(valor)) {
    return { motivo: 'FORA_DO_TIPO', detalhe: `valor "${valor}" não é uma data AAAA-MM-DD` }
  }
  if (tipo === 'Data e hora' && !PADRAO_DATA_HORA.test(valor)) {
    return { motivo: 'FORA_DO_TIPO', detalhe: `valor "${valor}" não é data e hora` }
  }
  if (dominio && !codigosDoDominio(dominio).includes(valor)) {
    return { motivo: 'FORA_DO_DOMINIO', detalhe: `valor "${valor}" fora de: ${dominio}` }
  }
  if (valor.length > TETO_DO_EXEMPLO) {
    return { motivo: 'ACIMA_DO_TETO', detalhe: `o valor tem ${valor.length} caracteres` }
  }
  return null
}

function semExemplo(motivo, detalhe, destinos) {
  return { exemplo: null, motivo, detalhe, destinos }
}

/**
 * Escolhe o exemplo de um campo a partir dos destinos dele (`{ caminho, elemento, alternativa }`).
 * Um destino com valor proibido deixa o campo sem exemplo, mesmo que outro destino tenha valor.
 */
export function escolherExemplo({ destinos, fonteLida, campoVersao, prefixoDestino = '' }) {
  const avaliacoes = destinos.map((destino) =>
    avaliarDestino(destino, fonteLida, campoVersao, prefixoDestino),
  )
  const resumo = avaliacoes.map(({ estagio, motivo }, indice) => ({
    caminho: destinos[indice].caminho,
    estagio,
    motivo,
  }))

  const primeiroDescartado = avaliacoes.find(({ estagio }) => estagio === 'descartado')
  if (primeiroDescartado) {
    return semExemplo(primeiroDescartado.motivo, primeiroDescartado.detalhe, resumo)
  }

  const aceitos = avaliacoes.filter(({ estagio }) => estagio === 'aceito')
  if (!aceitos.length) {
    const primeiroSemValor = avaliacoes[0] ?? {
      motivo: 'NAO_ESTA_NA_FONTE',
      detalhe: 'o campo não tem tag de destino',
    }
    return semExemplo(primeiroSemValor.motivo, primeiroSemValor.detalhe, resumo)
  }

  const valoresAceitos = [...new Set(aceitos.map(({ valor }) => valor))]
  if (valoresAceitos.length > 1) {
    return semExemplo('DESTINOS_DIVERGEM', `valores: ${valoresAceitos.join(' | ')}`, resumo)
  }

  const [valor] = valoresAceitos
  const desconformidade = verificarConformidade(valor, campoVersao)
  if (desconformidade) {
    return semExemplo(desconformidade.motivo, desconformidade.detalhe, resumo)
  }
  return { exemplo: valor, motivo: null, detalhe: null, destinos: resumo }
}
