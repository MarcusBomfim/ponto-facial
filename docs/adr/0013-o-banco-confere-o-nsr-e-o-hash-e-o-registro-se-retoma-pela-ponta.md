# ADR-0013 — No banco, o registro só cresce, o próprio banco confere o NSR e o hash, e o registro se retoma pela ponta

**Status:** aceita

## Contexto

A ADR-0003 fez o registro de ponto só crescer e amarrou cada marcação à anterior pelo hash. Em memória, isso vale porque não há método para alterar nem apagar. No banco, um `update` resolveria. A Portaria 671 manda guardar o registro sem alteração (anexo IX, item 7), e o banco tem de guardar exatamente o que o domínio gravou.

Há também o tamanho. Uma obra com 200 trabalhadores e quatro marcações por dia passa de 200 mil operações por ano. Ler o registro inteiro para acrescentar uma marcação não se sustenta.

E há a concorrência. Na troca de turno, dez pessoas marcam no mesmo minuto, de coletores diferentes, e ninguém pode ser recusado (art. 74). Nenhum NSR pode se repetir, e nenhuma marcação pode se perder.

## Decisão

1. **Uma tabela, `ponto.operacoes`, com uma linha por operação.** Cada tipo tem as suas colunas, e as restrições dizem quais colunas cada tipo preenche e quais deixa vazias.
   - Cada instante vai em UTC, com o deslocamento em minutos ao lado.
   - O hash foi calculado sobre a hora local com o fuso escrito (`07:58:00-0300`), e é ela que a leitura refaz, sem depender da base de fusos.
2. **O banco garante a numeração.**
   - A chave primária é o par estabelecimento e NSR.
   - Cada operação tem uma chave estrangeira para a de NSR anterior, numa coluna gerada: não há como gravar o NSR 6 sem o 5.
   - O NSR 1 é a inclusão do empregador, e só ela.
3. **O banco garante o hash.**
   - A marcação guarda os campos 1 a 7 do registro do tipo 7, como o arquivo os escreve.
   - Um `check` confere o formato dos campos e que eles batem com as colunas: o NSR, o CPF, o coletor, o on-line.
   - Outro `check` refaz o SHA-256 dos campos e do hash anterior. Os hashes do exemplo do README conferem no banco.
4. **O banco garante a cadeia sem galho.**
   - O hash anterior é uma chave estrangeira para o hash de uma marcação do mesmo registro.
   - Duas marcações nunca apontam para a mesma.
   - Só uma marcação não aponta para nenhuma: a primeira.

   Com isso, a única marcação a que uma nova pode se amarrar é a última.
5. **Só cresce.** Um gatilho recusa `update`, `delete` e `truncate`, inclusive do dono das tabelas, com o código `PF001`. O papel da aplicação nem tem a permissão (ADR-0012).
6. **O registro se retoma pela ponta.**
   - A ponta é o que o domínio precisa para continuar: o último NSR, a hora do relógio, o hash da última marcação e quem está incluído.
   - A hora do relógio é a gravação da última operação, ou a hora acertada, se ela foi um ajuste.
   - Quem está incluído sai da última mudança de cada CPF.
   - O banco calcula a ponta por índices, e o domínio retoma o registro por ela (`RegistroDePonto.Retomar`), com as regras de sempre.
   - Só o que a operação acrescentou vai para o banco.
7. **Cada estabelecimento tem uma fila.** Quem grava no registro ou no tratamento de um estabelecimento espera um bloqueio consultivo pelo id dele, até o fim da transação (`pg_advisory_xact_lock`). Duas gravações no mesmo estabelecimento acontecem uma depois da outra, cada uma com a ponta que a anterior deixou. Estabelecimentos diferentes gravam em paralelo.
8. **A leitura confere de novo.**
   - O domínio refaz cada marcação lida a partir das colunas e compara os campos e o hash com os gravados.
   - Na página lida, a leitura confere que os NSRs vêm seguidos e que cada marcação aponta para a de antes.
   - O superusuário pode desligar gatilhos e chaves estrangeiras (`session_replication_role = replica`) e alterar o que quiser, mas é pego na leitura.
   - Quem reescreve a cadeia inteira, com todos os hashes refeitos, é pego pelos comprovantes dos trabalhadores (ADR-0003).
9. **O tratamento segue o mesmo desenho** em `ponto.tratamentos`.
   - Só cresce, numa sequência sem buraco por trabalhador.
   - A marcação desconsiderada é uma chave estrangeira para uma operação do mesmo trabalhador.
   - A leitura refaz o tratamento repetindo cada passo gravado pelos métodos do domínio.

   Para isso, o tratamento passou a depender das marcações do trabalhador, e não do registro inteiro. O `TratamentoDoPonto.Do` de antes continua valendo para o registro em memória.

## Consequências

- Marcar não fica mais caro com o número de marcações: é uma inserção e algumas consultas por índice. Com 200 mil operações num estabelecimento, no PostgreSQL 17.6, cada uma das três consultas da ponta levou menos de 0,1 ms, todas pelos índices (`explain analyze`). A lista de quem está incluído cresce com as inclusões, alterações e exclusões de trabalhadores, que são poucas perto das marcações.
- O registro retomado só tem em memória o que fez desde a retomada. O comprovante de uma marcação antiga se lê do banco, com o nome que o trabalhador tinha quando marcou. O tratamento de um registro retomado também se lê do banco, e `TratamentoDoPonto.Do` recusa o registro retomado em vez de devolver um tratamento sem as marcações de antes.
- A fila põe em série as gravações do mesmo estabelecimento. O teste grava 40 marcações ao mesmo tempo: todas entram, com a numeração e a cadeia inteiras.
- Dentro da fila, uma marcação on-line feita um minuto antes de outra, que chegue depois dela, é recusada pelo relógio do registro. O relógio só anda para frente, e a hora da marcação on-line é a da identificação. Na Parte 6, a API pega a hora do registro dentro da fila, e não no começo do pedido.
- Restrições e gatilhos não param o superusuário, e as réplicas e os backups ficam com a mesma proteção: só a leitura, que confere tudo de novo.
