# ADR-0011 — O tratamento fica ao lado do registro, e o espelho mostra os dois

**Status:** aceita

## Contexto

O registro de ponto só cresce, e nenhuma marcação muda depois de gravada (ADR-0003). Mas as pessoas esquecem de marcar a saída, marcam duas vezes por engano, faltam com atestado. O ponto de um mês precisa dar conta disso sem mexer no que o trabalhador registrou.

A Portaria 671 separa as duas coisas. O registro é o do REP; o tratamento é do "programa de tratamento de registro de ponto". O art. 82 limita esse tratamento a duas coisas:

- acrescentar informações para complementar omissões no registro de ponto, inclusive ausências;
- indicar marcações indevidas.

O art. 84 manda o espelho mostrar, ao lado das marcações do registro, as tratadas: incluídas, desconsideradas e pré-assinaladas.

## Decisão

1. **O tratamento é um objeto próprio, por trabalhador e registro.** Ele faz três coisas:
   - **incluir** uma marcação que faltou;
   - **desconsiderar** uma marcação do registro, pelo NSR dela;
   - **abonar** um dia, para a ausência justificada.

   Não há editar nem apagar.
2. **Cada tratamento guarda o motivo, quem tratou e quando.** O motivo é obrigatório. Um tratamento não se desfaz: um engano se corrige com outro tratamento, e os dois ficam.
3. **O registro não muda.** A marcação desconsiderada continua lá, com o NSR e o hash dela; o espelho a mostra com a letra D, e ela não conta. A incluída aparece com a letra I e não ganha NSR, porque não foi marcada no coletor. Uma inclusão não pode repetir o minuto de uma marcação que já conta, nem estar no futuro.
4. **O intervalo pré-assinalado é do horário, não do tratamento.** Quando o horário pré-assinala o intervalo (art. 74, § 2º, da CLT) e a pessoa marca só a entrada e a saída, a apuração lança o intervalo, e o espelho o mostra com a letra P.
5. **O espelho segue os incisos do art. 84, na ordem.**
   - o empregador, com o CEI, CAEPF ou CNO quando há;
   - o trabalhador, com a admissão e o cargo;
   - a emissão e o período;
   - o horário do contrato;
   - as marcações com as letras;
   - a duração de cada jornada com a hora noturna reduzida.

   Em seguida vêm a apuração de cada dia, os totais e a lista dos tratamentos do período, cada um com o motivo, o responsável e a data.
6. **O espelho, por enquanto, é texto, uma informação por linha**, como o comprovante do art. 79. O PDF assinado e o Arquivo Eletrônico de Jornada (AEJ) são da Parte 6, que lê o mesmo objeto.

## Consequências

- O trabalhador vê o que mudou no próprio ponto e por quê: a marcação que o RH desconsiderou continua no espelho, com o motivo.
- Uma auditoria refaz a apuração a partir do registro e dos tratamentos. Os dois só crescem.
- O tratamento ainda não tem papéis — quem pode incluir, quem aprova. Isso entra com a API e os perfis de RH, gestor e trabalhador, na Parte 6.
- O dia com marcação ímpar fica pendente, sem conta, até alguém tratar. A apuração não adivinha qual marcação falta.
