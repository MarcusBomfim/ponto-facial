namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>Por que a prova de vida não passou.</summary>
public enum MotivoDaRecusa
{
    /// <summary>O movimento pedido não foi feito, ou o rosto não voltou para a frente, a tempo.</summary>
    TempoEsgotado = 1,

    /// <summary>O rosto foi para outro lado antes de ir para o pedido.</summary>
    MovimentoTrocado = 2,

    /// <summary>O acompanhamento do rosto se perdeu: o rosto saiu da imagem, ou outro entrou no lugar.</summary>
    RostoPerdido = 3,

    /// <summary>Entre dois quadros passou tempo demais para o acompanhamento valer.</summary>
    QuadrosEspacados = 4,

    /// <summary>O que girou diante da câmera não mostrou o relevo de um rosto.</summary>
    SemRelevo = 5,
}

/// <summary>As frases para quem está diante da câmera.</summary>
public static class MensagensDaRecusa
{
    /// <remarks>
    /// A recusa por relevo diz o mesmo que diria a qualquer pessoa, sem
    /// apontar o que faltou: quem testa um ataque não aprende com a resposta
    /// o que ajustar.
    /// </remarks>
    public static string Da(MotivoDaRecusa motivo) => motivo switch
    {
        MotivoDaRecusa.TempoEsgotado => "O movimento não foi feito a tempo. Tente de novo, seguindo a instrução da tela.",
        MotivoDaRecusa.MovimentoTrocado => "O rosto virou para outro lado. Tente de novo, seguindo a instrução da tela.",
        MotivoDaRecusa.RostoPerdido => "O rosto saiu da câmera no meio da prova. Tente de novo, sem sair do enquadramento.",
        MotivoDaRecusa.QuadrosEspacados => "A câmera falhou no meio da prova. Tente de novo.",
        MotivoDaRecusa.SemRelevo => "Não foi possível confirmar o rosto. Tente de novo ou marque pelo código pessoal.",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Motivo sem mensagem."),
    };
}
