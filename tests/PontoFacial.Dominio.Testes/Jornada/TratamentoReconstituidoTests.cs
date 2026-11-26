using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Dominio.Testes.Jornada;

/*
 * O tratamento refeito sobre as marcações lidas do banco, e não sobre o
 * registro inteiro: os mesmos tratamentos, repetidos na ordem, dão o mesmo
 * espelho; e o que as regras recusam ao tratar, recusam ao refazer.
 */
public sealed class TratamentoReconstituidoTests
{
    [Fact]
    public void O_tratamento_refeito_sobre_as_marcacoes_do_trabalhador_da_o_mesmo_espelho()
    {
        var (registro, original, _) = SemanaDoJoao.Montar();
        var refeito = TratamentoDoPonto.Reconstituir(Exemplos.Obra, Exemplos.Joao.Cpf, incluidoNoRegistro: true, original.Registradas());

        foreach (var tratada in original.Marcacoes)
        {
            _ = tratada.Tipo == TipoDeTratamento.Incluida
                ? refeito.Incluir(tratada.MarcadaEm, tratada.Motivo, tratada.Responsavel, tratada.Em)
                : refeito.Desconsiderar(tratada.Nsr!.Value, tratada.Motivo, tratada.Responsavel, tratada.Em);
        }

        Assert.Equal(SemanaDoJoao.Espelho(original).Linhas(), SemanaDoJoao.Espelho(refeito).Linhas());
        Assert.Equal(registro.Estabelecimento, refeito.Estabelecimento);
    }

    [Fact]
    public void O_refeito_recusa_o_que_o_original_recusaria()
    {
        var (_, original, repetida) = SemanaDoJoao.Montar();
        var refeito = TratamentoDoPonto.Reconstituir(Exemplos.Obra, Exemplos.Joao.Cpf, incluidoNoRegistro: true, original.Registradas());
        refeito.Desconsiderar(repetida, "Repetida.", RegistroDeExemplo.Rh, Exemplos.Em(23, 7, 32));

        Assert.Equal(
            $"A marcação do NSR {repetida} já foi desconsiderada.",
            Assert.Throws<ExcecaoDeDominio>(() => refeito.Desconsiderar(repetida, "De novo.", RegistroDeExemplo.Rh, Exemplos.Em(23, 7, 33))).Message);
        Assert.Equal(
            "Já há marcação às 07:58 de 16/11/2026.",
            Assert.Throws<ExcecaoDeDominio>(() => refeito.Incluir(Horarios.Em(16, "07:58"), "Por engano.", RegistroDeExemplo.Rh, Exemplos.Em(23, 7, 34))).Message);
    }

    [Fact]
    public void Sem_inclusao_no_registro_nao_ha_tratamento()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => TratamentoDoPonto.Reconstituir(Exemplos.Obra, Exemplos.Maria.Cpf, incluidoNoRegistro: false, []));

        Assert.Equal("O trabalhador não foi incluído neste registro de ponto.", erro.Message);
    }

    [Fact]
    public void As_marcacoes_tem_de_ser_do_trabalhador_e_em_ordem_de_nsr()
    {
        var (_, original, _) = SemanaDoJoao.Montar();
        var doJoao = original.Registradas();

        Assert.Throws<ArgumentException>(() => TratamentoDoPonto.Reconstituir(Exemplos.Obra, Exemplos.Maria.Cpf, incluidoNoRegistro: true, doJoao));
        Assert.Throws<ArgumentException>(() => TratamentoDoPonto.Reconstituir(Exemplos.Obra, Exemplos.Joao.Cpf, incluidoNoRegistro: true, [.. doJoao.Reverse()]));
    }
}
