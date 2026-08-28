using AutonomiaSaaS.Modules.BusinessCore.Domain;

namespace AutonomiaSaaS.Modules.BusinessCore.Domain;

/// <summary>
/// Lançada quando algo tenta mover uma AgentTask para um estado que a máquina
/// de estados (seção 6 da arquitetura) não permite a partir do estado atual.
/// Isso deve ser tratado como bug de quem chama, não como situação esperada —
/// por isso é uma exceção, não um retorno bool silencioso: uma transição
/// inválida geralmente indica uma corrida entre dois workers processando a
/// mesma tarefa, o que merece aparecer em log/alerta, não ser engolido.
/// </summary>
public sealed class InvalidStateTransitionException : Exception
{
    public AgentTaskStatus CurrentStatus { get; }
    public AgentTaskStatus AttemptedStatus { get; }

    public InvalidStateTransitionException(AgentTaskStatus currentStatus, AgentTaskStatus attemptedStatus)
        : base($"Não é permitido transicionar de '{currentStatus}' para '{attemptedStatus}'.")
    {
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }
}
