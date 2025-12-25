namespace yQuant.Core.Ports.Output.Policies;

public interface IStrategyPolicyMapper
{
    string GetSizingPolicyName(string strategy);
}
