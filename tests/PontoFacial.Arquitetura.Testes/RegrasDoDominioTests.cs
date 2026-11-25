using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using PontoFacial.Dominio;

namespace PontoFacial.Arquitetura.Testes;

/*
 * Regras de arquitetura verificadas por teste, não por convenção. Quem
 * acrescentar uma referência a um pacote no domínio "só para resolver rápido"
 * descobre no build, e não seis meses depois.
 */
public sealed class RegrasDoDominioTests
{
    private static readonly Assembly Dominio = typeof(ReferenciaDoAssembly).Assembly;

    [Fact]
    public void O_dominio_depende_apenas_da_biblioteca_padrao()
    {
        /*
         * Nem pacote de terceiro, nem projeto da própria solução: é o que mantém
         * as regras do ponto e da biometria testáveis sem banco, sem câmera e
         * sem rede.
         */
        var externas = Dominio
            .GetReferencedAssemblies()
            .Select(referencia => referencia.Name ?? string.Empty)
            .Where(nome => !EhDaBibliotecaPadrao(nome))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(externas);
    }

    [Fact]
    public void As_classes_do_dominio_sao_seladas()
    {
        /*
         * Herança é um ponto de extensão, e ponto de extensão precisa ser
         * projetado: classe aberta por omissão convida a sobrescrever o que a
         * entidade assume ser fixo. Classe estática conta como selada — o
         * compilador a emite como `abstract sealed`.
         */
        var abertas = Dominio
            .GetExportedTypes()
            .Where(tipo => tipo.IsClass && !tipo.IsSealed && !tipo.IsAbstract)
            .Select(tipo => tipo.FullName)
            .ToArray();

        Assert.Empty(abertas);
    }

    [Fact]
    public void O_estado_do_dominio_nao_tem_setter_publico()
    {
        /*
         * O estado muda por método com nome de negócio, que valida antes. Um
         * setter público deixaria qualquer camada escrever um valor que o
         * método recusaria. `init` é permitido: só vale na construção.
         */
        var comSetterPublico = Dominio
            .GetExportedTypes()
            .SelectMany(tipo => tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(propriedade => propriedade.SetMethod is { IsPublic: true } setter && !EhInit(setter))
            .Select(propriedade => $"{propriedade.DeclaringType?.Name}.{propriedade.Name}")
            .ToArray();

        Assert.Empty(comSetterPublico);
    }

    [Fact]
    public void Os_valores_lidos_de_texto_nao_tem_construtor_publico()
    {
        /*
         * CNPJ e CPF só nascem pela leitura — `Cnpj.De` —, que valida. Um
         * construtor público faria um CPF que não confere. A regra vale para
         * todo tipo que tem um `De`: o que entra no domínio lido de um texto
         * entra por ali.
         */
        var valores = Dominio
            .GetExportedTypes()
            .Where(tipo => tipo.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(metodo => metodo.Name == "De" && tipo.IsAssignableFrom(metodo.ReturnType)))
            .ToList();

        Assert.NotEmpty(valores);
        Assert.All(valores, valor => Assert.Empty(valor.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));
    }

    [Fact]
    public void O_dominio_nao_le_o_relogio_do_sistema()
    {
        /*
         * A hora de cada marcação é a do relógio do registrador, que a
         * Portaria 671 manda sincronizar com a Hora Legal Brasileira (anexo IX,
         * item 2) — e o ajuste desse relógio é, ele mesmo, uma operação
         * registrada. O domínio recebe a hora; não a lê. Assim os testes
         * escolhem o instante, e nenhuma regra depende de quando ela roda.
         *
         * O teste procura, nas referências do assembly compilado, as leituras
         * do relógio que a biblioteca padrão oferece.
         */
        string[] proibidas =
        [
            "System.DateTime.get_Now", "System.DateTime.get_UtcNow", "System.DateTime.get_Today",
            "System.DateTimeOffset.get_Now", "System.DateTimeOffset.get_UtcNow", "System.TimeProvider.get_System",
        ];

        using var arquivo = File.OpenRead(Dominio.Location);
        using var pe = new PEReader(arquivo);
        var metadados = pe.GetMetadataReader();

        var lidas = metadados.MemberReferences
            .Select(metadados.GetMemberReference)
            .Where(referencia => referencia.Parent.Kind == HandleKind.TypeReference)
            .Select(referencia =>
            {
                var tipo = metadados.GetTypeReference((TypeReferenceHandle)referencia.Parent);
                return $"{metadados.GetString(tipo.Namespace)}.{metadados.GetString(tipo.Name)}.{metadados.GetString(referencia.Name)}";
            })
            .Where(proibidas.Contains)
            .Distinct()
            .ToArray();

        Assert.Empty(lidas);
    }

    [Fact]
    public void O_dominio_nao_guarda_imagem()
    {
        /*
         * O rosto entra no domínio como vetor — o ModeloFacial —, nunca como
         * foto (ADR-0004). Nenhum tipo do domínio, público ou não, tem campo
         * ou propriedade de bytes ou de fluxo: é onde uma foto caberia.
         */
        Type[] deImagem = [typeof(byte[]), typeof(Stream), typeof(Memory<byte>), typeof(ReadOnlyMemory<byte>), typeof(ReadOnlySpan<byte>)];
        const BindingFlags Todos = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var guardamBytes = Dominio
            .GetTypes()
            .SelectMany(tipo => tipo.GetFields(Todos).Select(campo => (tipo, campo.Name, Tipo: campo.FieldType))
                .Concat(tipo.GetProperties(Todos).Select(propriedade => (tipo, propriedade.Name, Tipo: propriedade.PropertyType))))
            .Where(membro => deImagem.Any(tipo => tipo == membro.Tipo || tipo.IsAssignableFrom(membro.Tipo)))
            .Select(membro => $"{membro.tipo.Name}.{membro.Name}")
            .ToArray();

        Assert.Empty(guardamBytes);
    }

    private static bool EhDaBibliotecaPadrao(string nome) =>
        nome is "System" or "netstandard" or "mscorlib"
        || nome.StartsWith("System.", StringComparison.Ordinal);

    private static bool EhInit(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
}
