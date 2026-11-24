# ADR-0003 — O registro de ponto só cresce, e cada marcação amarra a anterior

**Status:** aceita

## Contexto

O ponto é prova: numa reclamação trabalhista, é o registro que diz se houve hora extra. Por isso a Portaria MTP nº 671/2021 trata o registrador de ponto por programa (REP-P) com regras que um cadastro comum não tem:

- O registrador grava, no Armazenamento de Registro de Ponto, cinco tipos de operação: os dados do empregador, os ajustes do relógio, a inclusão, a alteração e a exclusão de trabalhadores, os eventos sensíveis e as marcações (anexo IX, item 6).
- Cada estabelecimento tem a sua sequência de NSR, que começa em 1 na primeira operação e sobe de um em um (anexo IX, item 6, observação).
- Nada do que foi gravado pode ser apagado ou alterado pelo prazo legal (anexo IX, item 7).
- O sistema não pode restringir a marcação, marcar sozinho, exigir autorização para hora extra nem ter dispositivo que altere o que o trabalhador registrou (art. 74).
- Cada marcação leva um hash SHA-256, e o comprovante do trabalhador traz esse hash (art. 79, VIII).

O leiaute do Arquivo Fonte de Dados, publicado no gov.br, diz como o hash é calculado: sobre o NSR, o tipo, a data e hora da marcação, o CPF, a data e hora da gravação, o coletor e o indicador de on-line, "e o código hash (SHA-256) do registro anterior, caso exista". A data e hora vai ao minuto, no formato `AAAA-MM-ddThh:mm:00ZZZZZ`.

## Decisão

1. **O `RegistroDePonto` é uma lista de operações que só cresce.** Ele grava as cinco operações, todas com o NSR da mesma sequência, e não tem método para apagar ou trocar nenhuma. A lista que ele expõe é só de leitura, inclusive para quem tenta convertê-la.
2. **O hash é calculado sobre os campos como o leiaute os escreve**, e não sobre os valores: NSR com 9 dígitos, CPF com 12, a data e hora com os segundos em `00` e o fuso sem dois-pontos, o coletor com 2 dígitos. Os sete campos são exatamente as posições 1 a 73 da linha do tipo 7, e o hash ocupa as 64 seguintes: a linha do arquivo é o que foi calculado e o resultado.
3. **"Registro anterior" é a marcação anterior do mesmo estabelecimento.** Só o registro do tipo 7 tem hash; as outras operações levam NSR, mas não entram na cadeia. A primeira marcação não tem anterior.
4. **As horas são as do registro, ao minuto, no fuso do estabelecimento.** A marcação on-line tem a hora da gravação. A off-line guarda as duas — quando foi feita e quando chegou —, e a feita pode ser anterior à última operação gravada; a gravação, não. O relógio do registro só volta por um ajuste, que fica gravado com quem o fez.
5. **O registro não filtra marcação.** Marcação repetida, no mesmo minuto ou de madrugada, é gravada. Quem trata o ponto pode indicar a marcação indevida, mas não apagá-la (art. 82).
6. **O domínio não lê o relógio.** A hora chega de quem chama, e um teste de arquitetura procura, no assembly compilado, as leituras do relógio da biblioteca padrão.
7. **A conferência trabalha sobre as linhas do arquivo.** `CadeiaDeMarcacoes.Conferir` refaz o hash de cada linha com o da anterior e diz em qual linha a cadeia quebrou. Ela também recebe comprovantes, e confere se o hash de cada um é o que o registro tem para o mesmo NSR.

## Consequências

- Alterar uma marcação quebra a cadeia nela. Incluir ou remover uma marcação no meio quebra a cadeia na seguinte, porque o hash dela foi calculado com o da anterior.
- **A cadeia sozinha não denuncia quem reescreve tudo.** O cálculo é público e não usa segredo: quem altera uma marcação e refaz todos os hashes dali em diante tem uma cadeia que confere. O que denuncia essa reescrita é o comprovante. Cada trabalhador recebe o hash da própria marcação, e um comprovante que não confere com o registro aponta a fraude. Faltam a assinatura eletrônica do arquivo e do comprovante (arts. 87 e 88), que vêm com o AFD e o PDF.
- Duas marcações no mesmo minuto têm a mesma data e hora no arquivo; o NSR as distingue. Os segundos que o coletor mostra não chegam ao registro, porque o leiaute não os tem.
- O meio de identificação, rosto ou código pessoal, fica na marcação, mas fora do hash e do arquivo. Para a Portaria, as duas marcações são iguais.
- O leiaute não diz se o hash vai em letras maiúsculas ou minúsculas. Aqui vai em minúsculas. Isso será conferido com o validador do Ministério quando o AFD for gerado; mudar a escrita muda o hash, e o teste com o valor calculado fora do .NET avisa.
