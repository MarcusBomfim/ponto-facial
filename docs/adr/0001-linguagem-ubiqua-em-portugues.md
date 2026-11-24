# ADR-0001 — Linguagem ubíqua em português

**Status:** aceita

## Contexto

O domínio deste sistema é o ponto do trabalhador no Brasil, e quem o regula escreve em português: a CLT, a Portaria MTP nº 671/2021 e a LGPD. A portaria fala em *marcação*, *registro*, *NSR*, *comprovante*, *espelho de ponto*, *Arquivo Fonte de Dados*; a LGPD, em *dado sensível*, *titular*, *consentimento*, *eliminação*. CPF, CNPJ, CEI, CAEPF e CNO são do Brasil, e a regra dos dígitos verificadores também.

A convenção do ecossistema .NET é nomear tudo em inglês. Traduzir *NSR* para *sequence number* ou *comprovante* para *receipt* obrigaria quem confere o código contra a portaria a traduzir de volta, artigo por artigo.

## Decisão

O código do domínio usa os termos da lei e de quem trabalha com ponto, em português: `Marcacao`, `RegistroDePonto`, `Nsr`, `ComprovanteDeRegistro`, `AutorizacaoBiometrica`, `Cpf`. O que é mecanismo do framework continua com o nome do framework — `IReadOnlyList`, `DateTimeOffset`, `ArgumentNullException`.

A regra de análise **CA1710**, que exige o sufixo `Exception` em exceções, foi desligada no `.editorconfig`: ela pressupõe nomes em inglês, e `ExcecaoDeDominio` já diz o que é.

## Consequências

- Um requisito lido na portaria — "cada estabelecimento terá sua própria sequência de NSR" — é reconhecível no código e no nome do teste que o verifica.
- Quem não lê português terá mais dificuldade com o domínio. Para um sistema cujas regras são brasileiras, e cujo fiscal é o auditor do trabalho brasileiro, é a troca certa.
- Mistura de idiomas na mesma linha (`Marcacoes.Where(...)`) é inevitável e aceita: se o termo vem da lei, como a lei escreve; se vem da biblioteca, como ela nomeou.
- Acentos ficam fora dos identificadores (`Marcacao`, não `Marcação`): o C# os aceita, mas eles quebram busca, ferramenta de linha de comando e teclado de quem não está no padrão ABNT.
