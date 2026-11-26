using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Registro;

namespace PontoFacial.Persistencia.Testes;

/// <summary>
/// Grava no banco, pelos repositórios, o que um registro e um tratamento em
/// memória fizeram: as mesmas operações, nas mesmas horas, na mesma ordem.
/// O que volta do banco se compara com o original.
/// </summary>
/// <remarks>
/// O estabelecimento é cadastrado no empregador da empresa do teste, com o
/// CNPJ dela: as linhas do arquivo e os hashes não levam o CNPJ, e dão o
/// mesmo que no registro em memória.
/// </remarks>
internal static class Copia
{
    public static async Task<Guid> DoRegistro(EmpresaNoBanco empresa, RegistroDePonto origem, params Trabalhador[] trabalhadores)
    {
        var empregador = await empresa.Empregador();
        var estabelecimento = await empresa.Estabelecimentos.Cadastrar(
            Estabelecimento.Novo(empregador, origem.Estabelecimento.Local, origem.Estabelecimento.Fuso.Id));

        foreach (var trabalhador in trabalhadores)
        {
            await empresa.Trabalhadores.Gravar(trabalhador);
        }

        var porCpf = trabalhadores.ToDictionary(trabalhador => trabalhador.Cpf);
        var abertura = (InclusaoDoEmpregador)origem.Operacoes[0];
        await empresa.Registros.Abrir(estabelecimento, abertura.Responsavel, abertura.GravadaEm);

        foreach (var operacao in origem.Operacoes.Skip(1))
        {
            await empresa.Registros.Gravar(estabelecimento, registro => Repetir(registro, operacao, empregador, porCpf));
        }

        return estabelecimento;
    }

    public static async Task DoTratamento(EmpresaNoBanco empresa, Guid estabelecimento, TratamentoDoPonto origem)
    {
        foreach (var tratada in origem.Marcacoes)
        {
            await empresa.Tratamentos.Tratar(
                estabelecimento,
                origem.Trabalhador,
                tratamento => tratada.Tipo == TipoDeTratamento.Incluida
                    ? tratamento.Incluir(tratada.MarcadaEm, tratada.Motivo, tratada.Responsavel, tratada.Em)
                    : tratamento.Desconsiderar(tratada.Nsr!.Value, tratada.Motivo, tratada.Responsavel, tratada.Em));
        }

        foreach (var abono in origem.Abonos.Values)
        {
            await empresa.Tratamentos.Tratar(estabelecimento, origem.Trabalhador, tratamento => tratamento.Abonar(abono.Dia, abono.Motivo, abono.Responsavel, abono.Em));
        }
    }

    /// <summary>A mesma operação, feita de novo no registro retomado do banco. Toda marcação é refeita pelo código pessoal.</summary>
    private static Operacao Repetir(RegistroDePonto registro, Operacao operacao, Empregador empregador, Dictionary<Cpf, Trabalhador> trabalhadores) =>
        operacao switch
        {
            MudancaNoTrabalhador { Mudanca: TipoDeMudanca.Inclusao } mudanca =>
                registro.IncluirTrabalhador(trabalhadores[mudanca.Cpf], mudanca.Responsavel, mudanca.GravadaEm),
            MudancaNoTrabalhador { Mudanca: TipoDeMudanca.Alteracao } mudanca =>
                registro.AlterarTrabalhador(
                    Trabalhador.Novo(mudanca.Nome, mudanca.Cpf, trabalhadores[mudanca.Cpf].Admissao, trabalhadores[mudanca.Cpf].Cargo),
                    mudanca.Responsavel,
                    mudanca.GravadaEm),
            MudancaNoTrabalhador mudanca => registro.ExcluirTrabalhador(mudanca.Cpf, mudanca.Responsavel, mudanca.GravadaEm),
            AjusteDoRelogio ajuste => registro.AjustarRelogio(ajuste.Antes, ajuste.Ajustado, ajuste.Responsavel),
            OcorrenciaDeEventoSensivel ocorrencia => registro.RegistrarEvento(ocorrencia.Evento, ocorrencia.GravadaEm),
            MarcacaoDePonto { OnLine: true } marcacao => registro.MarcarOnLine(
                IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, trabalhadores[marcacao.Cpf], marcacao.MarcadaEm),
                marcacao.Coletor),
            MarcacaoDePonto marcacao => registro.MarcarOffLine(
                IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, trabalhadores[marcacao.Cpf], marcacao.MarcadaEm),
                marcacao.Coletor,
                marcacao.GravadaEm),
            _ => throw new InvalidOperationException($"Operação de tipo {operacao.TipoDoRegistro} sem cópia."),
        };
}
