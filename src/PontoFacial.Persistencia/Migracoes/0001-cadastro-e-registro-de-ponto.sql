-- 0001 — O cadastro, o registro de ponto e o tratamento, cada empresa isolada.
--
-- O que o banco consegue garantir sozinho, ele garante: a numeração sem
-- buraco, o hash de cada marcação, a cadeia sem galho, a tabela que só
-- cresce e a empresa que só vê o que é dela (ADR-0012 e ADR-0013). As regras
-- de negócio ficam no domínio; aqui fica o que impede o banco de guardar um
-- registro que o domínio nunca teria gravado.

-- O papel com que a aplicação entra no banco. Sem login: quem implanta cria
-- o usuário de verdade, com a senha, como membro dele.
do $$
begin
    if not exists (select from pg_roles where rolname = 'ponto_aplicacao') then
        create role ponto_aplicacao nologin;
    end if;
end
$$;

-- A empresa em nome de quem a transação trabalha. A aplicação a define no
-- começo de cada transação (set_config local), e ela some no fim: uma conexão
-- que volta ao pool não leva a empresa de um pedido para o seguinte.
create function ponto.empresa_atual() returns uuid
    language sql
    stable
    as $$ select nullif(current_setting('ponto.empregador_id', true), '')::uuid $$;

-- Para as tabelas que só crescem: nenhuma linha muda, nenhuma sai.
create function ponto.recusar_mudanca() returns trigger
    language plpgsql
    as $$
begin
    raise exception 'A tabela % só cresce: o que foi gravado não se altera nem se apaga (Portaria 671, anexo IX, item 7).', tg_table_name
        using errcode = 'PF001';
end
$$;

create table ponto.empregadores (
    id uuid not null,
    documento varchar(14) not null,
    nome varchar(150) not null,
    cei_caepf_ou_cno varchar(14),
    cadastrado_em timestamptz not null default now(),
    constraint pk_empregadores primary key (id),
    constraint uq_empregadores_documento unique (documento),
    constraint ck_empregadores_documento check (documento ~ '^([0-9]{11}|[0-9A-Z]{12}[0-9]{2})$'),
    constraint ck_empregadores_cei_caepf_ou_cno check (cei_caepf_ou_cno ~ '^([0-9]{12}|[0-9]{14})$')
);

comment on table ponto.empregadores is 'Cada empregador é uma empresa do sistema: a segurança por linha separa os dados de uma e de outra.';

create table ponto.estabelecimentos (
    id uuid not null,
    empregador_id uuid not null,
    local varchar(100) not null,
    fuso varchar(40) not null,
    cadastrado_em timestamptz not null default now(),
    constraint pk_estabelecimentos primary key (id),
    constraint uq_estabelecimentos_empregador_id_id unique (empregador_id, id),
    constraint fk_estabelecimentos_empregadores foreign key (empregador_id) references ponto.empregadores (id),
    constraint ck_estabelecimentos_fuso check (fuso ~ '^America/[A-Za-z_]+$')
);

create table ponto.trabalhadores (
    empregador_id uuid not null,
    cpf char(11) not null,
    nome varchar(52) not null,
    admissao date not null,
    cargo varchar(60) not null,
    atualizado_em timestamptz not null default now(),
    constraint pk_trabalhadores primary key (empregador_id, cpf),
    constraint fk_trabalhadores_empregadores foreign key (empregador_id) references ponto.empregadores (id),
    constraint ck_trabalhadores_cpf check (cpf ~ '^[0-9]{11}$')
);

-- O registro de ponto de cada estabelecimento: as operações, em ordem de
-- NSR. Cada instante vai em UTC, com o deslocamento em minutos ao lado: o
-- hash foi calculado sobre a hora local, com o fuso escrito, e é ela que se
-- refaz na leitura.
create table ponto.operacoes (
    empregador_id uuid not null,
    estabelecimento_id uuid not null,
    nsr bigint not null,
    nsr_anterior bigint generated always as (nullif(nsr - 1, 0)) stored,
    tipo smallint not null,
    gravada_em timestamptz not null,
    gravada_em_deslocamento smallint not null,
    responsavel char(11),

    -- Tipo 2: o empregador e o local como estavam na inclusão.
    documento_do_empregador varchar(14),
    nome_do_empregador varchar(150),
    cei_caepf_ou_cno varchar(14),
    local varchar(100),
    fuso varchar(40),

    -- Tipo 4: a hora acertada; a de antes é a da gravação.
    ajustado_para timestamptz,
    ajustado_para_deslocamento smallint,

    -- Tipo 5: o que mudou no trabalhador, e o nome como ficou.
    mudanca char(1),
    nome varchar(52),

    -- Tipo 6: o código do evento sensível.
    evento smallint,

    -- Tipos 5 e 7.
    cpf char(11),

    -- Tipo 7: a marcação, os campos 1 a 7 do arquivo e o hash.
    marcada_em timestamptz,
    marcada_em_deslocamento smallint,
    coletor smallint,
    on_line boolean,
    meio smallint,
    campos char(73),
    hash char(64),
    hash_anterior char(64),

    constraint pk_operacoes primary key (estabelecimento_id, nsr),
    constraint fk_operacoes_estabelecimentos foreign key (empregador_id, estabelecimento_id) references ponto.estabelecimentos (empregador_id, id),
    constraint fk_operacoes_trabalhadores foreign key (empregador_id, cpf) references ponto.trabalhadores (empregador_id, cpf),

    -- Sem buraco na numeração: toda operação, menos a primeira, aponta para a de NSR anterior.
    constraint fk_operacoes_nsr_anterior foreign key (estabelecimento_id, nsr_anterior) references ponto.operacoes (estabelecimento_id, nsr),
    constraint ck_operacoes_nsr check (nsr between 1 and 999999999),
    constraint ck_operacoes_tipo check (tipo in (2, 4, 5, 6, 7)),
    constraint ck_operacoes_abertura check ((nsr = 1) = (tipo = 2)),

    -- A cadeia: cada marcação aponta para o hash de uma marcação do mesmo
    -- registro, duas nunca apontam para a mesma, e só uma não aponta para
    -- nenhuma (ux_operacoes_primeira_marcacao). A única que se pode apontar
    -- ao acrescentar é a última.
    constraint uq_operacoes_hash unique (estabelecimento_id, hash),
    constraint uq_operacoes_hash_anterior unique (estabelecimento_id, hash_anterior),
    constraint fk_operacoes_hash_anterior foreign key (estabelecimento_id, hash_anterior) references ponto.operacoes (estabelecimento_id, hash),

    -- As marcações e as mudanças de cada trabalhador; o tratamento aponta para elas.
    constraint uq_operacoes_cpf_nsr unique (estabelecimento_id, cpf, nsr),

    -- Cada tipo com os seus campos, e só com eles.
    constraint ck_operacoes_responsavel check ((responsavel is not null) = (tipo in (2, 4, 5)) and (responsavel is null or responsavel ~ '^[0-9]{11}$')),
    constraint ck_operacoes_empregador check (
        num_nonnulls(documento_do_empregador, nome_do_empregador, local, fuso) = case when tipo = 2 then 4 else 0 end
        and (cei_caepf_ou_cno is null or tipo = 2)),
    constraint ck_operacoes_ajuste check (num_nonnulls(ajustado_para, ajustado_para_deslocamento) = case when tipo = 4 then 2 else 0 end),
    constraint ck_operacoes_mudanca check (
        num_nonnulls(mudanca, nome) = case when tipo = 5 then 2 else 0 end
        and (mudanca is null or mudanca in ('I', 'A', 'E'))),
    constraint ck_operacoes_evento check ((evento is not null) = (tipo = 6) and (evento is null or evento in (2, 7, 8))),
    constraint ck_operacoes_cpf check ((cpf is not null) = (tipo in (5, 7)) and (cpf is null or cpf ~ '^[0-9]{11}$')),
    constraint ck_operacoes_marcacao check (
        num_nonnulls(marcada_em, marcada_em_deslocamento, coletor, on_line, meio, campos, hash) = case when tipo = 7 then 7 else 0 end
        and (hash_anterior is null or tipo = 7)
        and (coletor is null or coletor between 1 and 5)
        and (meio is null or meio in (1, 2))),

    -- Ao minuto, a resolução do arquivo, num deslocamento que existe.
    constraint ck_operacoes_ao_minuto check (
        date_trunc('minute', gravada_em, 'UTC') = gravada_em
        and (marcada_em is null or date_trunc('minute', marcada_em, 'UTC') = marcada_em)
        and (ajustado_para is null or date_trunc('minute', ajustado_para, 'UTC') = ajustado_para)),
    constraint ck_operacoes_deslocamentos check (
        gravada_em_deslocamento between -720 and 840
        and (marcada_em_deslocamento is null or marcada_em_deslocamento between -720 and 840)
        and (ajustado_para_deslocamento is null or ajustado_para_deslocamento between -720 and 840)),

    -- Os campos 1 a 7 do registro do tipo 7, como o leiaute os escreve, e de acordo com as colunas.
    constraint ck_operacoes_campos check (
        campos ~ '^[0-9]{9}7[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:00[+-][0-9]{4}[0-9]{12}[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:00[+-][0-9]{4}0[1-5][01]$'
        and substr(campos, 1, 9)::bigint = nsr
        and substr(campos, 35, 12) = '0' || cpf
        and substr(campos, 71, 2)::smallint = coletor
        and substr(campos, 73, 1) = case when on_line then '0' else '1' end),

    -- O hash é o SHA-256 dos campos seguidos do hash anterior (anexo IX, item 10): o banco refaz a conta.
    constraint ck_operacoes_hash check (hash = encode(sha256(convert_to(campos || coalesce(hash_anterior, ''), 'UTF8')), 'hex'))
);

comment on table ponto.operacoes is 'O registro de ponto de cada estabelecimento (Portaria 671, anexo IX, item 6). Só cresce.';

create unique index ux_operacoes_primeira_marcacao on ponto.operacoes (estabelecimento_id) where tipo = 7 and hash_anterior is null;

-- A ponta: a última marcação, e quem está incluído.
create index ix_operacoes_marcacoes on ponto.operacoes (estabelecimento_id, nsr) where tipo = 7;
create index ix_operacoes_mudancas on ponto.operacoes (estabelecimento_id, cpf, nsr) where tipo = 5;

create trigger tg_operacoes_so_cresce before update or delete on ponto.operacoes
    for each row execute function ponto.recusar_mudanca();
create trigger tg_operacoes_sem_truncate before truncate on ponto.operacoes
    for each statement execute function ponto.recusar_mudanca();

-- O tratamento do ponto (Portaria 671, art. 82): as marcações incluídas (I),
-- as desconsideradas (D) e os dias abonados (A) de cada trabalhador, em
-- sequência. Também só cresce: tratamento não se desfaz.
create table ponto.tratamentos (
    empregador_id uuid not null,
    estabelecimento_id uuid not null,
    cpf char(11) not null,
    sequencia integer not null,
    sequencia_anterior integer generated always as (nullif(sequencia - 1, 0)) stored,
    tipo char(1) not null,
    marcada_em timestamptz,
    marcada_em_deslocamento smallint,
    nsr bigint,
    dia date,
    motivo varchar(150) not null,
    responsavel char(11) not null,
    tratado_em timestamptz not null,
    tratado_em_deslocamento smallint not null,
    constraint pk_tratamentos primary key (estabelecimento_id, cpf, sequencia),
    constraint fk_tratamentos_estabelecimentos foreign key (empregador_id, estabelecimento_id) references ponto.estabelecimentos (empregador_id, id),
    constraint fk_tratamentos_trabalhadores foreign key (empregador_id, cpf) references ponto.trabalhadores (empregador_id, cpf),
    constraint fk_tratamentos_sequencia_anterior foreign key (estabelecimento_id, cpf, sequencia_anterior) references ponto.tratamentos (estabelecimento_id, cpf, sequencia),

    -- A desconsiderada é uma operação do registro, do mesmo trabalhador.
    constraint fk_tratamentos_operacoes foreign key (estabelecimento_id, cpf, nsr) references ponto.operacoes (estabelecimento_id, cpf, nsr),
    constraint ck_tratamentos_sequencia check (sequencia >= 1),
    constraint ck_tratamentos_tipo check (tipo in ('I', 'D', 'A')),
    constraint ck_tratamentos_incluida check (
        num_nonnulls(marcada_em, marcada_em_deslocamento) = case when tipo = 'I' then 2 else 0 end
        and (marcada_em is null or (date_trunc('minute', marcada_em, 'UTC') = marcada_em and marcada_em <= tratado_em))),
    constraint ck_tratamentos_desconsiderada check ((nsr is not null) = (tipo = 'D')),
    constraint ck_tratamentos_abono check ((dia is not null) = (tipo = 'A')),
    constraint ck_tratamentos_motivo check (length(motivo) > 0 and motivo = btrim(motivo)),
    constraint ck_tratamentos_responsavel check (responsavel ~ '^[0-9]{11}$'),
    constraint ck_tratamentos_deslocamentos check (
        tratado_em_deslocamento between -720 and 840
        and (marcada_em_deslocamento is null or marcada_em_deslocamento between -720 and 840))
);

comment on table ponto.tratamentos is 'O tratamento do ponto (Portaria 671, art. 82), ao lado do registro, que não muda. Só cresce.';

create unique index ux_tratamentos_desconsiderada on ponto.tratamentos (estabelecimento_id, nsr) where tipo = 'D';
create unique index ux_tratamentos_incluida on ponto.tratamentos (estabelecimento_id, cpf, marcada_em) where tipo = 'I';
create unique index ux_tratamentos_abono on ponto.tratamentos (estabelecimento_id, cpf, dia) where tipo = 'A';

create trigger tg_tratamentos_so_cresce before update or delete on ponto.tratamentos
    for each row execute function ponto.recusar_mudanca();
create trigger tg_tratamentos_sem_truncate before truncate on ponto.tratamentos
    for each statement execute function ponto.recusar_mudanca();

-- Cada empresa só vê, e só grava, o que é dela — inclusive o dono das
-- tabelas, que não é superusuário em produção.
alter table ponto.empregadores enable row level security;
alter table ponto.empregadores force row level security;
create policy pl_empregadores_da_empresa on ponto.empregadores
    using (id = ponto.empresa_atual()) with check (id = ponto.empresa_atual());

alter table ponto.estabelecimentos enable row level security;
alter table ponto.estabelecimentos force row level security;
create policy pl_estabelecimentos_da_empresa on ponto.estabelecimentos
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

alter table ponto.trabalhadores enable row level security;
alter table ponto.trabalhadores force row level security;
create policy pl_trabalhadores_da_empresa on ponto.trabalhadores
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

alter table ponto.operacoes enable row level security;
alter table ponto.operacoes force row level security;
create policy pl_operacoes_da_empresa on ponto.operacoes
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

alter table ponto.tratamentos enable row level security;
alter table ponto.tratamentos force row level security;
create policy pl_tratamentos_da_empresa on ponto.tratamentos
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

-- O que a aplicação pode fazer. No registro e no tratamento, ler e
-- acrescentar: sem update, sem delete, sem truncate. No trabalhador, o
-- cadastro muda; o CPF, não.
grant usage on schema ponto to ponto_aplicacao;
grant execute on function ponto.empresa_atual() to ponto_aplicacao;
grant select, insert on ponto.empregadores, ponto.estabelecimentos, ponto.trabalhadores, ponto.operacoes, ponto.tratamentos to ponto_aplicacao;
grant update (nome, admissao, cargo, atualizado_em) on ponto.trabalhadores to ponto_aplicacao;
