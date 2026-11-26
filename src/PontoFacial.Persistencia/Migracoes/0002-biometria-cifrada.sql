-- 0002 — A biometria: o termo, a autorização e o modelo do rosto, cifrado
-- com a chave da empresa (ADR-0014).
--
-- O banco nunca vê o vetor do rosto: vê a cifra, o nonce e a versão da chave
-- que cifrou. A chave de cada empresa também está aqui só embrulhada, pela
-- chave mestra, que fica fora do banco.

-- A chave de cada empresa, em versões: a mais nova cifra; as outras só
-- decifram, até tudo ser cifrado de novo com a nova.
create table ponto.chaves_das_empresas (
    empregador_id uuid not null,
    versao integer not null,
    chave_mestra varchar(100) not null,
    chave_embrulhada bytea not null,
    criada_em timestamptz not null default now(),
    constraint pk_chaves_das_empresas primary key (empregador_id, versao),
    constraint fk_chaves_das_empresas_empregadores foreign key (empregador_id) references ponto.empregadores (id),
    constraint ck_chaves_das_empresas_versao check (versao >= 1),
    constraint ck_chaves_das_empresas_chave_mestra check (length(chave_mestra) > 0),

    -- O nonce de 12 bytes, a chave de 32 cifrada e a etiqueta de 16 do AES-GCM.
    constraint ck_chaves_das_empresas_tamanho check (octet_length(chave_embrulhada) = 60)
);

comment on table ponto.chaves_das_empresas is 'A chave AES-256 de cada empresa, embrulhada pela chave mestra, que não fica no banco.';

-- O texto que o trabalhador leu, como foi mostrado. O hash é a prova de qual
-- texto foi aceito (LGPD, art. 8º, § 2º), e o banco o refaz.
create table ponto.termos_de_consentimento (
    empregador_id uuid not null,
    hash char(64) not null,
    versao varchar(20) not null,
    texto text not null,
    constraint pk_termos_de_consentimento primary key (empregador_id, hash),
    constraint fk_termos_de_consentimento_empregadores foreign key (empregador_id) references ponto.empregadores (id),
    constraint ck_termos_de_consentimento_hash check (hash = encode(sha256(convert_to(texto, 'UTF8')), 'hex'))
);

create table ponto.autorizacoes_biometricas (
    id uuid not null,
    empregador_id uuid not null,
    cpf char(11) not null,
    termo_hash char(64) not null,
    concedida_em timestamptz not null,
    concedida_em_deslocamento smallint not null,
    encerrada_em timestamptz,
    encerrada_em_deslocamento smallint,
    motivo smallint,
    vigente boolean generated always as (encerrada_em is null) stored,
    constraint pk_autorizacoes_biometricas primary key (id),

    -- O modelo do rosto aponta para a autorização vigente por estas quatro
    -- colunas: quando ela acaba, a chave deixa de existir, e o banco não
    -- deixa a autorização acabar com o modelo ainda gravado.
    constraint uq_autorizacoes_biometricas_vigencia unique (empregador_id, cpf, id, vigente),
    constraint fk_autorizacoes_biometricas_trabalhadores foreign key (empregador_id, cpf) references ponto.trabalhadores (empregador_id, cpf),
    constraint fk_autorizacoes_biometricas_termos foreign key (empregador_id, termo_hash) references ponto.termos_de_consentimento (empregador_id, hash),
    constraint ck_autorizacoes_biometricas_encerramento check (
        num_nonnulls(encerrada_em, encerrada_em_deslocamento, motivo) in (0, 3)
        and (motivo is null or motivo in (1, 2))
        and (encerrada_em is null or encerrada_em >= concedida_em)),
    constraint ck_autorizacoes_biometricas_deslocamentos check (
        concedida_em_deslocamento between -720 and 840
        and (encerrada_em_deslocamento is null or encerrada_em_deslocamento between -720 and 840))
);

comment on table ponto.autorizacoes_biometricas is 'O consentimento de cada trabalhador para a biometria (LGPD, art. 11, I). Fica depois de encerrado: é a prova do consentimento.';

-- Uma autorização vigente por trabalhador, de cada vez.
create unique index ux_autorizacoes_biometricas_vigente on ponto.autorizacoes_biometricas (empregador_id, cpf) where encerrada_em is null;

-- A autorização só muda uma vez: quando acaba. A encerrada não reabre, e nada
-- se apaga.
create function ponto.encerrar_uma_vez() returns trigger
    language plpgsql
    as $$
begin
    if tg_op = 'DELETE' then
        raise exception 'A autorização biométrica não se apaga: ela é a prova do consentimento (LGPD, art. 8º, § 2º).'
            using errcode = 'PF001';
    end if;

    if old.encerrada_em is not null then
        raise exception 'A autorização biométrica já foi encerrada e não reabre: um novo consentimento é uma nova autorização.'
            using errcode = 'PF002';
    end if;

    return new;
end
$$;

create trigger tg_autorizacoes_biometricas_encerrar_uma_vez before update or delete on ponto.autorizacoes_biometricas
    for each row execute function ponto.encerrar_uma_vez();

-- O modelo do rosto: um por trabalhador, só com a autorização vigente.
create table ponto.cadastros_faciais (
    empregador_id uuid not null,
    cpf char(11) not null,
    autorizacao_id uuid not null,
    autorizacao_vigente boolean not null default true,
    reconhecedor varchar(40) not null,
    dimensao smallint not null,
    versao_da_chave integer not null,
    nonce bytea not null,
    vetor_cifrado bytea not null,
    cadastrado_em timestamptz not null,
    cadastrado_em_deslocamento smallint not null,
    constraint pk_cadastros_faciais primary key (empregador_id, cpf),
    constraint fk_cadastros_faciais_autorizacao foreign key (empregador_id, cpf, autorizacao_id, autorizacao_vigente)
        references ponto.autorizacoes_biometricas (empregador_id, cpf, id, vigente),
    constraint fk_cadastros_faciais_chave foreign key (empregador_id, versao_da_chave) references ponto.chaves_das_empresas (empregador_id, versao),
    constraint ck_cadastros_faciais_vigente check (autorizacao_vigente),
    constraint ck_cadastros_faciais_dimensao check (dimensao between 1 and 4096),

    -- O AES-GCM: o nonce de 12 bytes, e a cifra do tamanho do vetor, em float de 4 bytes, mais a etiqueta de 16.
    constraint ck_cadastros_faciais_nonce check (octet_length(nonce) = 12),
    constraint ck_cadastros_faciais_cifra check (octet_length(vetor_cifrado) = dimensao * 4 + 16),
    constraint ck_cadastros_faciais_deslocamento check (cadastrado_em_deslocamento between -720 and 840)
);

comment on table ponto.cadastros_faciais is 'O modelo do rosto (LGPD, art. 5º, II), cifrado com AES-256-GCM pela chave da empresa. Sai quando a autorização acaba.';

alter table ponto.chaves_das_empresas enable row level security;
alter table ponto.chaves_das_empresas force row level security;
create policy pl_chaves_das_empresas_da_empresa on ponto.chaves_das_empresas
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

alter table ponto.termos_de_consentimento enable row level security;
alter table ponto.termos_de_consentimento force row level security;
create policy pl_termos_de_consentimento_da_empresa on ponto.termos_de_consentimento
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

alter table ponto.autorizacoes_biometricas enable row level security;
alter table ponto.autorizacoes_biometricas force row level security;
create policy pl_autorizacoes_biometricas_da_empresa on ponto.autorizacoes_biometricas
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

alter table ponto.cadastros_faciais enable row level security;
alter table ponto.cadastros_faciais force row level security;
create policy pl_cadastros_faciais_da_empresa on ponto.cadastros_faciais
    using (empregador_id = ponto.empresa_atual()) with check (empregador_id = ponto.empresa_atual());

-- A chave nova entra, a velha sai quando nada mais a usa, e o embrulho muda
-- quando muda a chave mestra. O termo e a autorização ficam; da autorização,
-- só muda o encerramento. O modelo do rosto entra, é refeito e sai.
grant select, insert, delete on ponto.chaves_das_empresas to ponto_aplicacao;
grant update (chave_mestra, chave_embrulhada) on ponto.chaves_das_empresas to ponto_aplicacao;
grant select, insert on ponto.termos_de_consentimento, ponto.autorizacoes_biometricas to ponto_aplicacao;
grant update (encerrada_em, encerrada_em_deslocamento, motivo) on ponto.autorizacoes_biometricas to ponto_aplicacao;
grant select, insert, update, delete on ponto.cadastros_faciais to ponto_aplicacao;
