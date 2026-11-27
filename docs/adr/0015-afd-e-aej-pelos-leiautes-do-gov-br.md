# ADR-0015 — O AFD e o AEJ saem pelos leiautes do gov.br, e cada leitura em aberto fica escrita

**Status:** aceita

## Contexto

O Auditor-Fiscal do Trabalho pede dois arquivos.

- **O Arquivo Fonte de Dados (AFD)** é o registro de ponto de um estabelecimento. A Portaria 671 manda gerá-lo pelo REP (art. 81).
- **O Arquivo Eletrônico de Jornada (AEJ)** é o que o programa de tratamento fez com as marcações (art. 83, I).

Os anexos V e VI da Portaria, que traziam os leiautes, foram revogados pela Portaria MTP 1.486/2022. Desde então, os leiautes são os publicados no portal gov.br: o do AFD na versão 004, o do AEJ na versão 002. O do AEJ que estava no anexo VI era a versão 001, e mudou.

Os dois leiautes deixam alguns pontos em aberto. Este registro diz como cada um foi lido.

## Decisão

1. **Os dois arquivos são gerados pelo domínio** (`PontoFacial.Dominio.Arquivos`), a partir do registro, do tratamento, do horário e do calendário. Não há regra nova para isso, nem dependência nova. A persistência só lê do banco o que eles pedem.
2. **O AFD tem tamanho fixo.**
   - Cada registro tem o tamanho do leiaute: 302, 331, 73, 118, 36, 137 e 64, e a linha da assinatura, 100.
   - Os registros dos tipos 1 a 5 levam o CRC-16/KERMIT, conferido pelo valor do leiaute ("123456789" dá `2189`), em maiúsculas.
   - O tipo 7 é a linha que a marcação já escreve, com o hash dela.
   - O tipo 3 é do REP-C e do REP-A, e no REP-P a contagem dele no trailer é sempre zero.
3. **O preenchimento dos campos.** O leiaute manda preencher a partir da esquerda e completar com espaços. Foi lido assim:
   - o campo alfanumérico vai alinhado à esquerda, com espaços;
   - o campo numérico com valor vai com zeros à esquerda, como o CPF de 12 posições do registro de marcação, que vai com um zero à frente;
   - o campo numérico sem valor, como o CNO que não existe, vai com espaços.
4. **O AFD de um período** traz as operações gravadas nele, pela data da gravação na hora do estabelecimento. Depois de um ajuste do relógio, uma operação pode ter a gravação fora de ordem. Por isso a persistência lê a faixa inteira de NSRs, conferida, e o domínio escolhe pela data.
5. **O AEJ tem os campos separados por `|`.** Os registros vêm na ordem do leiaute, e uma linha por registro.
   - **O cabeçalho:** o CAEPF tem 14 dígitos e o CNO tem 12. O CEI, que tinha 12 e foi substituído pelo CNO, vai no campo do CNO.
   - **O REP:** é este programa, o REP-P, pelo número no INPI.
   - **Os vínculos:** cada trabalhador que esteve incluído no registro em algum dia do período, com o CPF e o nome.
   - **Os horários contratuais:** um código para cada sequência diferente de entradas e saídas num dia, numerado na ordem em que aparece. A duração vai em minutos, com a hora noturna reduzida.
   - **As marcações:**
     - A marcação do registro tem a origem "O" e o identificador do REP. A incluída tem "I", com o motivo, e a pré-assinalada, "P".
     - As que contam se alternam em entrada e saída, em pares numerados a cada dia de trabalho, e a primeira entrada leva o código do horário.
     - A desconsiderada tem a letra "D", com o motivo e o número do par em que caiu, e não muda a contagem dos pares.
   - **As ausências:** o descanso semanal (1) e a falta não justificada (2). O dia abonado não é falta, e o feriado não é ausência.
   - **O programa de tratamento:** este, pelo nome, pela versão e pelo desenvolvedor. O leiaute diz que o e-mail é numérico, o que é engano dele: o e-mail vai como texto.
6. **Os pontos que o leiaute do AEJ não cobre** ficam assim:
   - **O dia sem horário no contrato**, como o domingo trabalhado, não tem código de horário na primeira entrada.
   - **O vínculo que começa ou acaba no meio do período** só é apurado dentro dele. Quem saiu e voltou no meio do período tem um vínculo só, do começo ao fim, e os dias de fora aparecem como falta.
   - **O registro do tipo 06**, da matrícula no eSocial, é para quem tem mais de um vínculo no mesmo arquivo. Este sistema ainda não guarda a matrícula, e o registro não sai.
   - **O banco de horas e a folga compensatória** ainda não existem no domínio, e não há ausência dos tipos 3 e 4.
   - **O nome do AEJ** não é fixado pelo leiaute. Ele se chama `AEJ_<CNPJ ou CPF>_<início>_<fim>.txt`.
   - **O AEJ é gerado por estabelecimento**, como o AFD. O cabeçalho é do empregador.
7. **O horário contratual tem vigência.** Cada horário vale a partir de uma data, e o espelho ou o AEJ de um período usa o horário que valia nele. Se o horário muda no meio do período, o pedido é recusado, e a mensagem diz as duas partes em que pedir. O horário fica no banco como documento JSON, de leitura estrita, e volta pela fábrica do domínio, com todas as regras.
8. **Os feriados locais** ficam por estabelecimento e são cadastrados pelas regras do calendário: o dia que já é feriado nacional e o repetido são recusados.
9. **Os dois arquivos estão em ISO 8859-1**, e cada linha termina com CR e LF, inclusive a última.
   - O que não cabe na codificação vira "?".
   - O `|` e as quebras de linha dentro de um texto viram espaço, para não quebrar o arquivo.
   - A última linha é `ASSINATURA_DIGITAL_EM_ARQUIVO_P7S`, completada com espaços até 100 caracteres. A assinatura fica no arquivo `.p7s` ao lado, pela ADR-0016.

## Consequências

- Os arquivos gerados do banco são, linha por linha, os gerados do registro e do tratamento em memória. Os testes conferem isso com a semana do João.
- Um banco adulterado não gera arquivo: as operações passam pela mesma conferência da leitura (ADR-0013).
- As leituras dos itens 3 e 6 não foram conferidas com um validador oficial. Se um auditor apontar outra leitura, ela muda num lugar só: `Campos`, no domínio.
- O espelho de ponto e o AEJ ainda não sabem do banco de horas. Quando ele existir, entram os registros do tipo 07 com o tipo 3.
