using System.Globalization;
using Npgsql;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;

namespace PontoFacial.Persistencia.Registro;

/// <summary>
/// Como cada operação do registro vira uma linha de <c>ponto.operacoes</c>,
/// e a linha volta a ser a operação — conferida.
/// </summary>
/// <remarks>
/// <para>
/// A marcação lida é refeita pelo domínio com as colunas — a hora, o CPF, o
/// coletor — e o hash anterior, e o domínio recalcula os campos do arquivo e
/// o hash. Se não dão os gravados, a linha foi mexida por fora: o banco
/// confere que o hash gravado é o dos campos gravados, e a leitura confere
/// que os campos gravados são os das colunas.
/// </para>
/// <para>
/// As outras operações também passam pelas regras: o CPF tem de conferir, o
/// código do evento tem de existir, o empregador da inclusão tem de ser um
/// empregador válido.
/// </para>
/// </remarks>
internal static class OperacoesNoBanco
{
    public const string Inserir = """
        insert into ponto.operacoes (
            empregador_id, estabelecimento_id, nsr, tipo, gravada_em, gravada_em_deslocamento, responsavel,
            documento_do_empregador, nome_do_empregador, cei_caepf_ou_cno, local, fuso,
            ajustado_para, ajustado_para_deslocamento,
            mudanca, nome, evento, cpf,
            marcada_em, marcada_em_deslocamento, coletor, on_line, meio, campos, hash, hash_anterior)
        values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26)
        """;

    /// <summary>As colunas que a leitura espera, nesta ordem.</summary>
    public const string Colunas = """
        nsr, tipo, gravada_em, gravada_em_deslocamento, responsavel,
        documento_do_empregador, nome_do_empregador, cei_caepf_ou_cno, local, fuso,
        ajustado_para, ajustado_para_deslocamento,
        mudanca, nome, evento, cpf,
        marcada_em, marcada_em_deslocamento, coletor, on_line, meio, campos, hash, hash_anterior
        """;

    /// <summary>Os valores da inserção, na ordem de <see cref="Inserir"/>.</summary>
    public static object?[] Valores(Guid empregador, Guid estabelecimento, Operacao operacao)
    {
        var valores = new object?[26];
        valores[0] = empregador;
        valores[1] = estabelecimento;
        valores[2] = operacao.Nsr;
        valores[3] = (short)operacao.TipoDoRegistro;
        valores[4] = Instantes.Utc(operacao.GravadaEm);
        valores[5] = Instantes.Deslocamento(operacao.GravadaEm);

        switch (operacao)
        {
            case InclusaoDoEmpregador inclusao:
                valores[6] = inclusao.Responsavel.Numero;
                valores[7] = inclusao.Estabelecimento.Empregador.Documento.Numero;
                valores[8] = inclusao.Estabelecimento.Empregador.Nome;
                valores[9] = inclusao.Estabelecimento.Empregador.CeiCaepfOuCno;
                valores[10] = inclusao.Estabelecimento.Local;
                valores[11] = inclusao.Estabelecimento.Fuso.Id;
                break;

            case AjusteDoRelogio ajuste:
                valores[6] = ajuste.Responsavel.Numero;
                valores[12] = Instantes.Utc(ajuste.Ajustado);
                valores[13] = Instantes.Deslocamento(ajuste.Ajustado);
                break;

            case MudancaNoTrabalhador mudanca:
                valores[6] = mudanca.Responsavel.Numero;
                valores[14] = ((char)mudanca.Mudanca).ToString();
                valores[15] = mudanca.Nome;
                valores[17] = mudanca.Cpf.Numero;
                break;

            case OcorrenciaDeEventoSensivel ocorrencia:
                valores[16] = (short)ocorrencia.Evento;
                break;

            case MarcacaoDePonto marcacao:
                valores[17] = marcacao.Cpf.Numero;
                valores[18] = Instantes.Utc(marcacao.MarcadaEm);
                valores[19] = Instantes.Deslocamento(marcacao.MarcadaEm);
                valores[20] = (short)marcacao.Coletor;
                valores[21] = marcacao.OnLine;
                valores[22] = (short)marcacao.Meio;
                valores[23] = marcacao.Campos;
                valores[24] = marcacao.Hash;
                valores[25] = marcacao.HashAnterior;
                break;

            default:
                throw new InvalidOperationException($"Operação de tipo {operacao.TipoDoRegistro} sem lugar no banco.");
        }

        return valores;
    }

    /// <summary>A operação da linha lida com <see cref="Colunas"/>, conferida.</summary>
    public static Operacao Ler(NpgsqlDataReader linha)
    {
        var nsr = linha.GetInt64(0);
        var tipo = linha.GetInt16(1);
        var oQue = string.Create(CultureInfo.InvariantCulture, $"A operação do NSR {nsr}");

        return Leitura.Validada<Operacao>(
            () =>
            {
                var gravadaEm = Instantes.Ler(linha, 2, 3);

                return tipo switch
                {
                    2 => new InclusaoDoEmpregador(
                        nsr,
                        gravadaEm,
                        Estabelecimento.Novo(
                            Empregador.Novo(Documento.De(linha.GetString(5)), linha.GetString(6), Leitura.TextoOuNulo(linha, 7)),
                            linha.GetString(8),
                            linha.GetString(9)),
                        Cpf.De(linha.GetString(4))),
                    4 => new AjusteDoRelogio(nsr, gravadaEm, Instantes.Ler(linha, 10, 11), Cpf.De(linha.GetString(4))),
                    5 => new MudancaNoTrabalhador(
                        nsr,
                        gravadaEm,
                        Definido((TipoDeMudanca)linha.GetString(12)[0]),
                        Cpf.De(linha.GetString(15)),
                        linha.GetString(13),
                        Cpf.De(linha.GetString(4))),
                    6 => new OcorrenciaDeEventoSensivel(nsr, gravadaEm, Definido((EventoSensivel)linha.GetInt16(14))),
                    7 => Marcacao(linha, nsr, gravadaEm),
                    _ => throw new ArgumentException($"O tipo {tipo} não é de operação do registro de ponto."),
                };
            },
            oQue);
    }

    private static MarcacaoDePonto Marcacao(NpgsqlDataReader linha, long nsr, DateTimeOffset gravadaEm)
    {
        var marcacao = new MarcacaoDePonto(
            nsr,
            Instantes.Ler(linha, 16, 17),
            gravadaEm,
            Cpf.De(linha.GetString(15)),
            Definido((Coletor)linha.GetInt16(18)),
            linha.GetBoolean(19),
            Definido((MeioDeIdentificacao)linha.GetInt16(20)),
            Leitura.TextoOuNulo(linha, 23));

        if (marcacao.Campos != linha.GetString(21) || marcacao.Hash != linha.GetString(22))
        {
            throw new ConteudoAdulterado(string.Create(
                CultureInfo.InvariantCulture,
                $"A marcação do NSR {nsr} no banco não confere com o hash dela: a hora, o CPF ou o coletor foram trocados por fora do sistema."));
        }

        return marcacao;
    }

    private static T Definido<T>(T valor)
        where T : struct, Enum =>
        Enum.IsDefined(valor) ? valor : throw new ArgumentException($"O código {valor} não existe em {typeof(T).Name}.");
}
