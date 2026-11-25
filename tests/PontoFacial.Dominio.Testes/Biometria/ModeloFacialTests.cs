using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria;

public sealed class ModeloFacialTests
{
    [Fact]
    public void O_vetor_e_guardado_com_norma_1()
    {
        var modelo = ModeloFacial.De("teste-2d", [3f, 4f]);

        Assert.Equal([0.6f, 0.8f], modelo.Valores.ToArray());
        Assert.Equal(2, modelo.Dimensao);
        Assert.Equal("teste-2d", modelo.Reconhecedor);
    }

    [Fact]
    public void A_similaridade_e_o_cosseno_entre_os_vetores()
    {
        var a = ModeloFacial.De("teste-2d", [1f, 0f]);

        Assert.Equal(1.0, a.Similaridade(ModeloFacial.De("teste-2d", [5f, 0f])), 6);
        Assert.Equal(0.0, a.Similaridade(ModeloFacial.De("teste-2d", [0f, 2f])), 6);
        Assert.Equal(-1.0, a.Similaridade(ModeloFacial.De("teste-2d", [-1f, 0f])), 6);
        Assert.Equal(Math.Sqrt(0.5), a.Similaridade(ModeloFacial.De("teste-2d", [1f, 1f])), 6);
    }

    [Fact]
    public void Vetores_de_reconhecedores_diferentes_nao_se_comparam()
    {
        var sface = ModeloFacial.De("sface-2021dec", [1f, 0f]);

        var outroNome = Assert.Throws<ExcecaoDeDominio>(() => sface.Similaridade(ModeloFacial.De("arcface", [1f, 0f])));
        var outraDimensao = Assert.Throws<ExcecaoDeDominio>(() => sface.Similaridade(ModeloFacial.De("sface-2021dec", [1f, 0f, 0f])));

        Assert.Equal("Vetores de reconhecedores diferentes não se comparam: sface-2021dec e arcface.", outroNome.Message);
        Assert.Equal("Vetores de reconhecedores diferentes não se comparam: sface-2021dec e sface-2021dec.", outraDimensao.Message);
    }

    [Fact]
    public void O_vetor_e_copiado_e_nao_muda_por_fora()
    {
        float[] valores = [1f, 0f];
        var modelo = ModeloFacial.De("teste-2d", valores);

        valores[0] = 0f;
        valores[1] = 1f;

        Assert.Equal([1f, 0f], modelo.Valores.ToArray());
    }

    [Fact]
    public void Vetor_vazio_nulo_ou_com_valor_que_nao_e_numero_e_recusado()
    {
        var vazio = Assert.Throws<ExcecaoDeDominio>(() => ModeloFacial.De("teste", []));
        var nulo = Assert.Throws<ExcecaoDeDominio>(() => ModeloFacial.De("teste", [0f, 0f]));
        var naoNumero = Assert.Throws<ExcecaoDeDominio>(() => ModeloFacial.De("teste", [1f, float.NaN]));
        var infinito = Assert.Throws<ExcecaoDeDominio>(() => ModeloFacial.De("teste", [float.PositiveInfinity, 0f]));

        Assert.Equal("O vetor do rosto está vazio.", vazio.Message);
        Assert.Equal("O vetor do rosto é nulo: não descreve rosto nenhum.", nulo.Message);
        Assert.Equal("O vetor do rosto tem um valor que não é número.", naoNumero.Message);
        Assert.Equal("O vetor do rosto tem um valor que não é número.", infinito.Message);
    }

    [Fact]
    public void Sem_o_nome_do_reconhecedor_o_vetor_e_recusado()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => ModeloFacial.De(" ", [1f]));

        Assert.Equal("Falta o nome do reconhecedor.", erro.Message);
    }
}
