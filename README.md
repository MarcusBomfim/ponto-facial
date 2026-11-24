# Ponto Facial

[![CI](https://github.com/MarcusBomfim/ponto-facial/actions/workflows/ci.yml/badge.svg)](https://github.com/MarcusBomfim/ponto-facial/actions/workflows/ci.yml)
[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-blue.svg)](LICENSE)

Um ponto eletrônico com reconhecimento facial, feito dentro da Portaria MTP nº 671/2021 e da LGPD. O trabalhador marca o ponto olhando para a câmera do celular ou do tablet da portaria. O sistema grava a marcação numa sequência que não se apaga, amarrada por hash, e entrega o comprovante. Quem não quer biometria marca pelo código pessoal, e a marcação vale o mesmo.

C# 14 · .NET 10 · xUnit v3 · GitHub Actions

## O problema

Ponto por aplicativo com reconhecimento facial é um dos produtos de RH mais vendidos no Brasil. Fazê-lo direito esbarra em duas leis que puxam para lados diferentes.

**A Portaria 671 quer um registro que ninguém consiga mexer.**

- Cada estabelecimento tem a sua numeração de operações, o NSR.
- O que foi gravado não se apaga.
- O sistema não pode restringir a marcação, nem marcar sozinho, nem alterar o que o trabalhador registrou (art. 74).
- Cada marcação leva um hash SHA-256, calculado com o hash da anterior.
- O trabalhador recebe um comprovante com os campos que o art. 79 define.

**A LGPD trata o rosto como dado sensível** (art. 5º, II, e art. 11).

- O consentimento é para uma finalidade só.
- O trabalhador pode revogá-lo a qualquer momento, sem custo e sem burocracia.
- Terminado o tratamento, o dado sai.
- Consentimento dado a quem paga o salário só é livre se recusar não custar nada.

Este projeto resolve as duas: a biometria é opcional, sempre há outro meio, e o registro do ponto não depende de qual meio foi usado.

## Exemplo

O exemplo inteiro está em [`ExemploDoReadmeTests`](tests/PontoFacial.Dominio.Testes/ExemploDoReadmeTests.cs). É um teste: se o código mudar e alguma frase desta página deixar de ser verdade, o build quebra.

A construtora tem uma obra em Santos, e o João é pedreiro nela. O registro da obra abre com os dados do empregador, no NSR 1; a inclusão do João é o NSR 2:

```csharp
var construtora = Empregador.Novo(Documento.De("11.222.333/0001-81"), "Construtora Horizonte Ltda.");
var obra = Estabelecimento.Novo(construtora, "Rua das Palmeiras, 100 - Santos/SP", "America/Sao_Paulo");
var joao = Trabalhador.Novo("João da Silva", Cpf.De("529.982.247-25"), new DateOnly(2026, 3, 2), "Pedreiro");
var rh = Cpf.De("123.456.789-09"); // quem cuida do ponto no RH

var registro = RegistroDePonto.Abrir(obra, rh, Hora(6, 0));
registro.IncluirTrabalhador(joao, rh, Hora(6, 1));
```

O João lê o termo e autoriza a biometria. A autorização guarda o hash do texto que ele leu:

```csharp
var autorizacao = AutorizacaoBiometrica.Conceder(construtora, joao, TermoDeConsentimento.Novo("2026.1", texto), Hora(7, 0));
```

Às 7h58min31s, ele olha para o tablet da portaria. A marcação vai ao minuto, no formato do Arquivo Fonte de Dados (os campos estão separados por espaço aqui só para ler):

```csharp
var entrada = registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorRosto(autorizacao, Hora(7, 58, 31)), Coletor.DispositivoEletronico);
// entrada.Campos: 000000003 7 2026-11-23T07:58:00-0300 052998224725 2026-11-23T07:58:00-0300 04 0
//                 NSR       tipo  marcação            CPF          gravação                 coletor on-line
// entrada.Hash:   7221a9802299bd7a0cd9b4b9813d9872d5a6a0c07311eab15ce5123e913a41bd
```

No almoço, ele muda de ideia e revoga. Não precisa dar motivo nem pedir aprovação. O modelo do rosto tem de ser apagado; a entrada fica, porque o registro de ponto é guardado pelo prazo legal:

```csharp
autorizacao.Revogar(Hora(12, 30));
// autorizacao.Eventos: ModeloFacialDeveSerEliminado { Motivo = RevogadaPeloTrabalhador }
```

Na saída, o rosto não o identifica mais. O código pessoal identifica, e a saída vale o mesmo. O hash dela é calculado com o da entrada:

```csharp
IdentificacaoDoTrabalhador.PorRosto(autorizacao, Hora(17, 2));
// Sem autorização biométrica vigente, o rosto não identifica o trabalhador. O código pessoal continua valendo.

var saida = registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorCodigoPessoal(construtora, joao, Hora(17, 2)), Coletor.DispositivoEletronico);
// saida.HashAnterior: 7221a9802299bd7a0cd9b4b9813d9872d5a6a0c07311eab15ce5123e913a41bd
```

O comprovante da saída, com o que o art. 79 pede:

```text
Comprovante de Registro de Ponto do Trabalhador
NSR: 000000004
Empregador: Construtora Horizonte Ltda.
CNPJ: 11.222.333/0001-81
Local: Rua das Palmeiras, 100 - Santos/SP
Trabalhador: João da Silva
CPF: 529.982.247-25
Data e horário: 23/11/2026 17:02 (UTC-03:00)
Registro no INPI: BR512026001234-5
Hash (SHA-256): a3f002c3cd4c91cc0a2cfa30633f03affd5102961137424dc5bada210bfea3bc
```

A cadeia confere. Se alguém trocar a entrada para 7h50 nas linhas do arquivo, ela deixa de conferir, e a conferência diz onde:

```csharp
CadeiaDeMarcacoes.Conferir(linhas);
// A marcação do NSR 3 não confere com o hash dela: ela foi alterada, ou alguma antes dela foi alterada, incluída ou removida.
```

Se a pessoa refizer todos os hashes dali em diante, a cadeia volta a conferir: o cálculo é público. O que a denuncia é o comprovante que o trabalhador já tem, porque ele não confere com o registro reescrito (`CadeiaDeMarcacoes.Conferir(linhas, comprovantes)`).

## Decisões

As decisões de projeto estão registradas em [`docs/adr`](docs/adr):

- [ADR-0001 — Linguagem ubíqua em português](docs/adr/0001-linguagem-ubiqua-em-portugues.md)
- [ADR-0002 — A biometria é opcional, por consentimento, e sempre há outro meio](docs/adr/0002-biometria-opcional-por-consentimento.md)
- [ADR-0003 — O registro de ponto só cresce, e cada marcação amarra a anterior](docs/adr/0003-o-registro-so-cresce-e-cada-marcacao-amarra-a-anterior.md)

## Como rodar

Precisa do SDK do .NET 10.

```powershell
dotnet test --solution PontoFacial.slnx
```

O CI roda o mesmo, com a formatação conferida antes: `dotnet format PontoFacial.slnx --verify-no-changes`.

## Roteiro

O projeto é construído em oito partes, cada uma entregue funcionando, testada e documentada.

| Parte | Conteúdo | |
|--:|---|---|
| 1 | O domínio: empregador, estabelecimento e trabalhador; o registro de ponto que não se apaga, com NSR por estabelecimento, hash SHA-256 encadeado e o comprovante do art. 79; a autorização biométrica e a alternativa sem biometria | ✅ |
| 2 | O rosto: detecção e reconhecimento com o ONNX Runtime, o vetor do rosto no lugar da foto, o limiar de aceitação calibrado | |
| 3 | A prova de vida: foto de foto, tela de celular e vídeo recusados; métricas da ISO/IEC 30107-3 | |
| 4 | A jornada pela CLT: tolerância de 5 e 10 minutos, intervalos, hora noturna reduzida, horas extras e o espelho de ponto do art. 84 | |
| 5 | Persistência: PostgreSQL com o vetor do rosto cifrado (AES-GCM, chave por empresa) e cada empresa isolada no banco | |
| 6 | A API REST e os arquivos da Portaria (AFD e AEJ), o comprovante em PDF assinado e os papéis de RH, gestor e trabalhador | |
| 7 | O aplicativo: a câmera do celular, o tablet em modo quiosque e a marcação sem biometria, com acessibilidade | |
| 8 | Erro por grupo e relatório de impacto (RIPD), observabilidade, Docker e entrega contínua | |

## O que está pronto

**Parte 1 — o domínio do ponto e da biometria**

- **CNPJ e CPF conferidos pelos dígitos verificadores**, com ou sem máscara, inclusive o CNPJ alfanumérico que a Receita emite desde julho de 2026.
- **Empregador, estabelecimento e trabalhador** com os tamanhos dos campos do Arquivo Fonte de Dados. O empregador é empresa, pelo CNPJ, ou pessoa, pelo CPF, com o CEI, o CAEPF ou o CNO quando tem. O estabelecimento tem um dos fusos do Brasil, pelo nome da base IANA: Rio Branco está em -05:00, e uma marcação de dezembro de 2018 em São Paulo mostra o horário de verão de então.
- **O registro de ponto que só cresce.** Ele grava os dados do empregador, os ajustes do relógio, a inclusão, a alteração e a exclusão de trabalhadores, os eventos sensíveis e as marcações. Todas as operações dividem a mesma numeração, que começa em 1 em cada estabelecimento. Não há método para apagar nem trocar uma operação, e a lista exposta é só de leitura.
- **Só quem está incluído marca.** O trabalhador excluído não marca mais, as marcações dele ficam, e ele pode ser incluído de novo. A identificação de outro empregador é recusada.
- **Marcação on-line e off-line.** A on-line tem a hora do registro. A off-line guarda quando foi feita e quando chegou: pode ter sido feita antes da última operação gravada, mas não antes da inclusão do trabalhador, e não chega antes de ser feita.
- **O relógio só volta por ajuste**, e o ajuste fica gravado com quem o fez.
- **O registro não restringe marcação** (art. 74). A marcação repetida no mesmo minuto e a de madrugada são gravadas.
- **Hash SHA-256 encadeado**, calculado sobre os campos como o leiaute do registro do tipo 7 os escreve, mais o hash da marcação anterior. Os testes refazem o cálculo por conta própria, e um valor calculado fora do .NET fixa a regra.
- **Conferência da cadeia pelas linhas do arquivo.** Ela aponta a hora trocada, a marcação removida, a repetida e a linha que não é marcação. Com os comprovantes, aponta também quem reescreveu a cadeia inteira.
- **Comprovante do art. 79**, na ordem dos incisos, com o nome do trabalhador como estava quando ele marcou.
- **Autorização biométrica por consentimento.** Ela é de um trabalhador para um empregador, para uma finalidade só, e guarda a versão e o hash SHA-256 do termo que ele leu. Vale da concessão em diante. A revogação não pede motivo nem aprovação. A revogação e o desligamento pedem a eliminação do modelo do rosto, e as marcações ficam.
- **Duas formas de identificar o trabalhador.** O rosto só identifica com autorização vigente no instante. O código pessoal identifica qualquer um. A marcação pelos dois é a mesma linha do arquivo, com o mesmo hash.
- **Regras de arquitetura testadas.** O domínio depende só da biblioteca padrão. As classes são seladas e não há setter público. Os valores lidos de texto não têm construtor público. O domínio não lê o relógio do sistema: o teste procura essas leituras no assembly compilado.

## Licença

[MIT](LICENSE).
