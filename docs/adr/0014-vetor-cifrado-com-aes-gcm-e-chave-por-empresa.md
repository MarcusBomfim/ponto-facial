# ADR-0014 — O vetor do rosto é cifrado com AES-256-GCM, com uma chave por empresa embrulhada pela chave mestra

**Status:** aceita

## Contexto

O modelo do rosto não é a foto, mas também não é anônimo. Ele identifica a pessoa, e há técnicas que reconstroem um rosto aproximado a partir dele (ADR-0004). É dado biométrico, dado pessoal sensível (LGPD, art. 5º, II), e a lei manda proteger o dado de acesso não autorizado com medidas técnicas (art. 46).

Os riscos que esta parte trata:

- **o banco vaza** — um backup, uma cópia de homologação, um disco: quem tem os arquivos tem tudo o que está neles;
- **uma empresa enxerga a outra** por um defeito, mesmo com a segurança por linha (ADR-0012);
- **alguém com acesso de escrita ao banco** cola o vetor do rosto de um trabalhador na linha de outro, para um marcar pelo outro;
- **o vetor fica no banco depois que a autorização acaba**, mesmo que por um instante, ou para sempre, por um defeito.

## Decisão

1. **AES-256-GCM, da biblioteca padrão do .NET.**
   - O nonce tem 96 bits e é sorteado a cada cifra; a etiqueta tem 128 bits.
   - Com nonce sorteado, a NIST SP 800-38D limita o uso de uma chave a 2³² cifras. Uma empresa cifra um vetor por cadastro, e fica muito longe disso.
2. **Uma chave por empresa, embrulhada pela chave mestra.**
   - A chave da empresa tem 32 bytes sorteados e nasce no primeiro cadastro.
   - No banco, ela só existe embrulhada pela chave mestra, com AES-256-GCM e o contexto `empresa|versão`, ao lado do nome da chave mestra que a embrulhou.
   - A chave mestra não fica no banco. É uma interface (`IChaveMestra`), assíncrona porque em produção é um serviço de chaves, um cofre ou um HSM, que embrulha sem deixar a chave sair dele. A `ChaveMestraLocal` guarda a chave na memória, para desenvolvimento e para quem a guarda num arquivo de segredo.
3. **A cifra é amarrada à linha dela.**
   - Os dados associados do AES-GCM são a empresa, o CPF, a autorização, o reconhecedor, a dimensão e a versão da chave.
   - A cifra do João colada na linha da Maria não abre, nem a de uma empresa copiada para outra.
   - A cifra que não abre vira `ConteudoAdulterado`, e o rosto não identifica ninguém. A falha fecha.
4. **O vetor aberto e a chave aberta duram o mínimo.** Os números vão para bytes em ordem little-endian, são cifrados, e o buffer é zerado. A chave da empresa é desembrulhada a cada operação e zerada no fim; não fica em cache.
5. **O modelo sai na mesma transação em que a autorização acaba.**
   - A revogação e o desligamento pedem a eliminação (`ModeloFacialDeveSerEliminado`), e o repositório a atende antes do commit.
   - O banco garante isso de novo. O modelo aponta para a autorização por uma chave estrangeira que inclui a coluna gerada `vigente`. Quando a autorização acaba, essa chave deixa de existir, e o banco recusa a autorização que acaba com o modelo ainda gravado.
   - O modelo também se apaga a pedido, sem encerrar a autorização (art. 18, VI).
6. **A autorização e o termo ficam como prova.**
   - O texto do termo vai inteiro, e o banco confere que o hash é o SHA-256 dele.
   - Da autorização, só o encerramento muda, uma vez só: um gatilho recusa reabrir, com o código `PF002`, e apagar. Um novo consentimento é uma nova autorização.
   - Há uma autorização vigente de cada vez por trabalhador.
7. **As chaves giram sem parar o sistema.**
   - Girar cria a versão seguinte da chave da empresa, e os modelos novos já saem com ela.
   - Cifrar de novo passa os antigos para a versão nova.
   - Descartar apaga as versões que nenhum modelo usa mais. A chave estrangeira não deixa apagar uma versão em uso.
   - Para trocar a chave mestra, a instalação tem as duas por um tempo: as chaves das empresas são embrulhadas de novo com a nova, e os modelos não mudam.

## Consequências

- Uma cópia do banco não tem nenhum vetor em claro. O teste procura cada trecho de oito bytes do vetor na cifra gravada, e não acha nenhum.
- Quem tem o superusuário pode apagar e trocar linhas, mas não lê nem forja um modelo: para isso, precisaria da chave da empresa, e ela só abre com a chave mestra.
- **O backup continua com o modelo apagado** até a retenção dele acabar, com a chave da empresa embrulhada junto. Apagar a versão da chave no banco não resolve, porque o backup também a tem. O que encerra isso é trocar a chave mestra e destruir a antiga quando os backups feitos com ela expirarem: daí em diante, nenhuma chave das empresas nesses backups desembrulha. O procedimento, com o prazo de retenção, é da Parte 8.
- O processo da aplicação vê o vetor aberto enquanto compara: a comparação é na memória dele. A cifra protege o que está guardado, e não o que está em uso.
- Cada leitura desembrulha a chave da empresa. Com a chave mestra local, é decifrar 32 bytes na memória. Com um serviço de chaves, é uma chamada de rede por operação, e a Parte 8 pode precisar de um cache curto da chave aberta.
