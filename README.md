# Ponto Facial

[![CI](https://github.com/MarcusBomfim/ponto-facial/actions/workflows/ci.yml/badge.svg)](https://github.com/MarcusBomfim/ponto-facial/actions/workflows/ci.yml)
[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-blue.svg)](LICENSE)

Um ponto eletrônico com reconhecimento facial, feito dentro da Portaria MTP nº 671/2021 e da LGPD. O trabalhador diz quem é, olha para a câmera do celular ou do tablet da portaria e vira o rosto para onde a tela pede; o rosto confirma, e a prova de vida garante que era ele ali, e não uma foto dele. O sistema grava a marcação numa sequência que não se apaga, amarrada por hash, e entrega o comprovante. Quem não quer biometria marca pelo código pessoal, e a marcação vale o mesmo.

C# 14 · .NET 10 · ONNX Runtime · YuNet · SFace · SkiaSharp · xUnit v3 · GitHub Actions

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

O João lê o termo e autoriza a biometria. A autorização guarda o hash do texto que ele leu. Depois, cadastra o rosto com três capturas, que têm de conferir entre si; o cadastro guarda a média delas:

```csharp
var autorizacao = AutorizacaoBiometrica.Conceder(construtora, joao, TermoDeConsentimento.Novo("2026.1", texto), Hora(7, 0));
var cadastro = CadastroFacial.Cadastrar(autorizacao, capturas, limiar, Hora(7, 5));
```

Às 7h58min31s, ele digita o CPF no tablet da portaria e faz a prova de vida: olha para a câmera, vira o rosto para onde a tela pede e volta. O rosto que fez os movimentos é conferido com o cadastro dele, e só então identifica o João. A marcação vai ao minuto, no formato do Arquivo Fonte de Dados (os campos estão separados por espaço aqui só para ler):

```csharp
var conferencia = cadastro.Conferir(provaNaPortaria, limiar);
var entrada = registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorRosto(autorizacao, conferencia), Coletor.DispositivoEletronico);
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
IdentificacaoDoTrabalhador.PorRosto(autorizacao, cadastro.Conferir(provaNaSaida, limiar));
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

### O rosto

As capturas do exemplo acima saem do `ReconhecedorFacial`. Ele lê a foto, acha o rosto com o YuNet, alinha os olhos, o nariz e a boca no molde de 112×112 e transforma o rosto em 128 números com o SFace. A foto não fica. A seção está em [`ExemploDoReadmePeloRostoTests`](tests/PontoFacial.Rosto.Testes/ExemploDoReadmePeloRostoTests.cs), com retratos da NASA em domínio público:

```csharp
using var reconhecedor = new ReconhecedorFacial(ReconhecedorFacial.PastaPadrao);

var em2004 = reconhecedor.Capturar(File.ReadAllBytes("sunita-williams-2004.jpg")).Modelo;
var em2012 = reconhecedor.Capturar(File.ReadAllBytes("sunita-williams-2012.jpg")).Modelo;
var pontes = reconhecedor.Capturar(File.ReadAllBytes("marcos-pontes.jpg")).Modelo;

em2004.Similaridade(em2012);  // 0,78: a mesma astronauta, oito anos depois, com outro traje e outra luz
em2004.Similaridade(pontes);  // 0,05: outra pessoa

reconhecedor.Capturar(File.ReadAllBytes("tripulacao-expedicao-33.jpg")).Mensagem;
// Há mais de um rosto na imagem: só quem vai marcar o ponto deve aparecer.
```

Quem decide entre as duas é o limiar, e ele não foi copiado de lugar nenhum. A ferramenta [`PontoFacial.Calibracao`](tools/PontoFacial.Calibracao) comparou todas as 13.233 fotos da LFW com todas, em um minuto e meio. Isso deu 230.802 comparações da mesma pessoa e 84 milhões entre pessoas diferentes. Para falsa aceitação de 1:10.000, o limiar é **0,4317**, com falsa rejeição de **0,83%**. O [relatório](docs/calibracao/lfw-sface-2021dec.md) traz a curva inteira, os intervalos de confiança e o que a LFW não representa (ADR-0006).

### A prova de vida

Uma foto do João dá o mesmo vetor que o rosto dele. Por isso o rosto só identifica numa prova de vida aprovada. A tela pede três movimentos sorteados entre 56 desafios — vire para a esquerda, olhe para cima, vire para a direita —, e o `ProvaDeVidaNaCamera` acompanha nove pontos do rosto quadro a quadro:

```csharp
var prova = new ProvaDeVidaNaCamera(reconhecedor, Desafio.Sortear(), LimiaresCalibrados.ProvaDeVidaNaLfw);

// A cada quadro da câmera, 15 por segundo ou mais:
prova.Processar(quadro, instante);
prova.Instrucao;  // "Vire o rosto devagar para a sua esquerda."

// No fim, o rosto do começo e o do fim são conferidos com o cadastro:
var conferencia = cadastro.Conferir(prova.Resultado!, limiar);
```

O que separa o rosto da foto é o relevo do nariz. A homografia dos olhos e dos cantos da boca é o movimento exato de qualquer coisa plana; o nariz de verdade não a acompanha. Papel e tela só se curvam numa direção por vez, e por isso o relevo é medido de lado — contra as bochechas — e de cima a baixo — contra a raiz do nariz e o centro da boca —, valendo o menor (ADR-0007).

O critério foi medido com a ferramenta [`PontoFacial.EnsaioDaProvaDeVida`](tools/PontoFacial.EnsaioDaProvaDeVida). Ela imprimiu fotos da LFW em tamanho real e as pôs diante de uma webcam simulada, raio a raio, nas formas que um atacante usaria. Foram 300 tentativas por espécie, com um atacante que segue a tela:

| Espécie de ataque | Fizeram os movimentos | Aceitas |
|---|--:|--:|
| foto parada | 0 | 0 |
| foto plana inclinada, até na diagonal | 40 | 0 |
| foto curvada de lado | 21 | 0 |
| foto curvada de cima para baixo | 137 | 0 |
| foto curvada na diagonal | 74 | 3 |
| foto dobrada na linha do nariz | 26 | 0 |
| foto dobrada na altura do nariz | 156 | 0 |
| troca de fotos | 0 | 0 |

Para APCER de até 1% na pior espécie, como a ISO/IEC 30107-3 manda medir, o relevo mínimo é **0,157**. A foto enrolada na diagonal é o ponto fraco, registrado na ADR-0009. Uma gravação de vídeo acerta o sorteio uma vez em 56.

**A falsa rejeição não foi medida.** Medi-la pede gente de verdade fazendo o desafio diante de uma câmera, e as bases públicas de vídeo com o rosto virando são só de pesquisa ou não têm o movimento. A ferramenta mede com sessões gravadas, e a medida com o pessoal de uma empresa fica para a Parte 8. Até lá, o código pessoal está sempre disponível. O [relatório](docs/ensaio/prova-de-vida-lfw.md) traz cada espécie, onde cada ataque parou e o que o ensaio não mede: a luz, as máscaras e os rostos injetados no lugar da câmera (ADR-0008).

## Decisões

As decisões de projeto estão registradas em [`docs/adr`](docs/adr):

- [ADR-0001 — Linguagem ubíqua em português](docs/adr/0001-linguagem-ubiqua-em-portugues.md)
- [ADR-0002 — A biometria é opcional, por consentimento, e sempre há outro meio](docs/adr/0002-biometria-opcional-por-consentimento.md)
- [ADR-0003 — O registro de ponto só cresce, e cada marcação amarra a anterior](docs/adr/0003-o-registro-so-cresce-e-cada-marcacao-amarra-a-anterior.md)
- [ADR-0004 — O limiar sai da falsa aceitação tolerada, e o rosto confirma quem o trabalhador diz ser](docs/adr/0004-limiar-pela-falsa-aceitacao-e-verificacao-um-para-um.md)
- [ADR-0005 — O rosto com YuNet e SFace no ONNX Runtime, em código gerenciado](docs/adr/0005-rosto-com-yunet-e-sface-no-onnx-runtime.md)
- [ADR-0006 — O limiar de partida vem da LFW, e se recalibra com a população real](docs/adr/0006-limiar-calibrado-na-lfw-e-recalibrado-com-a-populacao-real.md)
- [ADR-0007 — A prova de vida pede movimentos sorteados e mede o relevo do nariz](docs/adr/0007-prova-de-vida-por-movimento-sorteado-e-relevo-do-nariz.md)
- [ADR-0008 — O ataque à prova de vida é medido numa câmera simulada, e a falsa rejeição, com gente](docs/adr/0008-ensaio-simulado-da-prova-de-vida.md)
- [ADR-0009 — Só a prova de vida aprovada identifica pelo rosto](docs/adr/0009-so-a-prova-de-vida-aprovada-identifica-pelo-rosto.md)

## Como rodar

Precisa do SDK do .NET 10.

```powershell
dotnet test --solution PontoFacial.slnx
```

A primeira compilação baixa os dois modelos de rosto do OpenCV Zoo (37 MB) para `modelos/` e confere o SHA-256 de cada um. O CI roda o mesmo, com a formatação conferida antes: `dotnet format PontoFacial.slnx --verify-no-changes`.

Para calibrar o limiar numa base de fotos com uma pasta por pessoa, como a LFW:

```powershell
dotnet run --project tools/PontoFacial.Calibracao -c Release -- <pasta da base> docs/calibracao/relatorio.md
```

Para ensaiar a prova de vida com as fotos da mesma base, e com sessões gravadas por gente de verdade, se houver (uma pasta por sessão, com os quadros e um `sessao.txt` com o desafio seguido e os quadros por segundo):

```powershell
dotnet run --project tools/PontoFacial.EnsaioDaProvaDeVida -c Release -- <pasta da base> docs/ensaio/relatorio.md --genuinas <pasta das sessões>
```

## Roteiro

O projeto é construído em oito partes, cada uma entregue funcionando, testada e documentada.

| Parte | Conteúdo | |
|--:|---|---|
| 1 | O domínio: empregador, estabelecimento e trabalhador; o registro de ponto que não se apaga, com NSR por estabelecimento, hash SHA-256 encadeado e o comprovante do art. 79; a autorização biométrica e a alternativa sem biometria | ✅ |
| 2 | O rosto: detecção e reconhecimento com o ONNX Runtime, o vetor do rosto no lugar da foto, o limiar de aceitação calibrado | ✅ |
| 3 | A prova de vida: foto de foto, tela de celular e vídeo recusados; métricas da ISO/IEC 30107-3 | ✅ |
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

**Parte 2 — o rosto**

- **Da foto ao vetor, em código gerenciado.** O YuNet acha o rosto e cinco pontos dele; o SFace transforma o rosto alinhado em 128 números. Os dois rodam no ONNX Runtime, com o pré e o pós-processamento do OpenCV refeitos em C#: a grade de três passos, a confiança pela média geométrica, a supressão das caixas repetidas, a semelhança que leva os pontos ao molde do ArcFace e o recorte bilinear. Os modelos têm licença MIT e Apache 2.0; os do InsightFace, só de pesquisa, ficaram de fora.
- **A foto de celular de pé.** A orientação gravada pela câmera no EXIF é aplicada antes da detecção.
- **Detecção na cópia reduzida, alinhamento na foto inteira.** O YuNet acha rostos de até uns 300 pixels; o rosto de uma selfie passa disso.
- **A captura só vale com um rosto, perto e inteiro**, e cada recusa vem com a frase para quem está diante da câmera: nenhum rosto, mais de um, rosto longe, rosto cortado na borda, imagem ilegível.
- **O vetor no lugar da foto.** O domínio guarda o `ModeloFacial`, com norma 1 e o nome do reconhecedor; vetores de reconhecedores diferentes não se comparam. Um teste de arquitetura procura, em todos os tipos do domínio, onde uma foto caberia.
- **Cadastro com três capturas que conferem entre si**, só com autorização vigente. O modelo é a média delas, e uma captura de outro rosto recusa o cadastro inteiro.
- **Verificação um para um.** O trabalhador diz quem é, o rosto confirma, e só a conferência que alcançou o limiar, do cadastro dele e com a autorização vigente no instante da captura, identifica na marcação.
- **O limiar sai da falsa aceitação tolerada**, com a regra de três para saber se as comparações bastam e o intervalo de 95% de Wilson em cada taxa. As similaridades são contadas em faixas de 0,0001, e a taxa em cada borda é exata.
- **Calibrado na LFW inteira:** 0,4317 para 1:10.000, com 0,83% de falsa rejeição. Na mesma calibração, o ponto de 1:1.000 dá 0,3655, perto do valor publicado pelo OpenCV. A ferramenta aponta os pares suspeitos — a mesma foto em duas pastas, o nome escrito de dois jeitos — que distorcem a ponta da curva.
- **Modelos baixados pelo build**, do commit fixado do OpenCV Zoo, com o SHA-256 conferido; o CI os guarda em cache.

**Parte 3 — a prova de vida**

- **Movimentos sorteados.** Três entre esquerda, direita, cima e baixo, com pelo menos um para o lado: 56 desafios, sorteados pelo gerador criptográfico. A tela diz o que fazer a cada momento, e o rosto fica 0,3 s no auge antes de voltar.
- **Os pontos são acompanhados, e não detectados de novo.** O fluxo óptico de Lucas e Kanade, em pirâmide e conferido na volta, segue nove pontos do rosto a partir do quadro de referência. O detector só confere que os pontos continuam dentro da caixa do rosto. Um protótipo mostrou por quê: numa foto inclinada, o detector põe o nariz onde um rosto teria nariz, e a foto parecia ter relevo.
- **O relevo pela homografia.** O movimento dos olhos e dos cantos da boca é o de um plano; o nariz que não o acompanha está fora dele. O relevo é o menor entre o lateral (o nariz contra as bochechas) e o vertical (contra a raiz do nariz e o centro da boca): papel e tela só se curvam numa direção por vez.
- **A sessão recusa cedo e diz por quê** — tempo esgotado, lado errado, rosto perdido, câmera falhando —, menos quando falta relevo: aí a frase é a mesma para qualquer um, para não ensinar o ataque.
- **Só a prova aprovada identifica pelo rosto.** A conferência compara com o cadastro o rosto do começo e o do fim da prova, e a identificação recusa a conferência de um vetor solto: "uma foto dele conferiria do mesmo jeito".
- **As taxas da ISO/IEC 30107-3**: o APCER por espécie de ataque, com o intervalo de 95% de Wilson, a pior espécie, o BPCER quando há sessões genuínas, e o critério calibrado pela pior espécie com a regra de três.
- **O ensaio numa câmera simulada.** A ferramenta põe as fotos da LFW em tamanho real diante de uma webcam de 640×480 — planas, curvadas em três eixos, dobradas, trocadas —, com o tremor da mão e o ruído do sensor, e um atacante que segue a tela. Cada imagem é feita raio a raio, e o resto é o sistema de verdade.

## Licença

[MIT](LICENSE).
