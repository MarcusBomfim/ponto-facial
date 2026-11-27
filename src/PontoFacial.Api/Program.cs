using Npgsql;
using PontoFacial.Api.Configuracao;
using PontoFacial.Api.Rotas;
using PontoFacial.Persistencia.Migracoes;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AdicionarPontoFacial(builder.Configuration, builder.Environment);

var app = builder.Build();

// "--migrar" leva o banco à versão deste código, com o dono do esquema, e sai.
if (args.Contains("--migrar"))
{
    await using var dono = NpgsqlDataSource.Create(
        app.Configuration["Banco:ConexaoDoDono"] ?? throw new InvalidOperationException("Para migrar, falta Banco:ConexaoDoDono."));
    var aplicadas = await Migrador.Aplicar(dono);
    app.Logger.MigracoesAplicadas(aplicadas.Count == 0 ? "nenhuma, o banco já estava em dia" : string.Join(", ", aplicadas.Select(migracao => migracao.Nome)));

    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapCadastro();
app.MapRegistro();
app.MapMarcacoes();
app.MapTratamento();
app.MapEspelho();
app.MapArquivos();
app.MapBiometria();

await app.RunAsync();

/// <summary>O ponto de entrada, visível para os testes da API.</summary>
public partial class Program;
