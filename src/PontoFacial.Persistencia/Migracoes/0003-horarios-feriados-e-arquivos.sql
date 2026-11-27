-- 0003 — O horário contratual de cada trabalhador, com a data em que passou
-- a valer, os feriados locais de cada estabelecimento, e o índice que acha
-- as operações de um período para o AFD (ADR-0015).

-- O horário do contrato vale a partir de uma data, até o seguinte: o espelho
-- de outubro usa o horário que valia em outubro, mesmo que ele tenha mudado
-- em novembro. O horário vai como documento JSON, e a leitura o refaz pelas
-- regras do domínio — a jornada de 44 horas, o intervalo, a virada do dia.
create table ponto.horarios_contratuais (
    empregador_id uuid not null,
    cpf char(11) not null,
    vigente_desde date not null,
    horario jsonb not null,
    gravado_em timestamptz not null default now(),
    constraint pk_horarios_contratuais primary key (empregador_id, cpf, vigente_desde),
    constraint fk_horarios_contratuais_trabalhadores foreign key (empregador_id, cpf) references ponto.trabalhadores (empregador_id, cpf),
    constraint ck_horarios_contratuais_horario check (jsonb_typeof(horario) = 'object')
);

comment on table ponto.horarios_contratuais is 'O horário e a jornada contratual do trabalhador (Portaria 671, art. 84, IV), cada um a partir da data em que passou a valer.';

-- Os feriados do estado e do município; os nacionais estão no domínio, pela lei.
create table ponto.feriados_locais (
    empregador_id uuid not null,
    estabelecimento_id uuid not null,
    data date not null,
    nome varchar(80) not null,
    constraint pk_feriados_locais primary key (estabelecimento_id, data),
    constraint fk_feriados_locais_estabelecimentos foreign key (empregador_id, estabelecimento_id) references ponto.estabelecimentos (empregador_id, id),
    constraint ck_feriados_locais_nome check (length(nome) > 0 and nome = btrim(nome))
);

-- O AFD de um período: as operações gravadas entre duas horas.
create index ix_operacoes_gravada_em on ponto.operacoes (estabelecimento_id, gravada_em);

alter table ponto.horarios_contratuais enable row level security;
alter table ponto.horarios_contratuais force row level security;
create policy pl_horarios_contratuais_da_empresa on ponto.horarios_contratuais
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

alter table ponto.feriados_locais enable row level security;
alter table ponto.feriados_locais force row level security;
create policy pl_feriados_locais_da_empresa on ponto.feriados_locais
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

-- O horário de uma data se corrige gravando de novo a mesma data; o feriado
-- cadastrado por engano sai.
grant select, insert on ponto.horarios_contratuais, ponto.feriados_locais to ponto_aplicacao;
grant update (horario, gravado_em) on ponto.horarios_contratuais to ponto_aplicacao;
grant delete on ponto.feriados_locais to ponto_aplicacao;
