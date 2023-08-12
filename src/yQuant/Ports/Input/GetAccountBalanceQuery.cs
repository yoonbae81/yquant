namespace yQuant.Ports.Input;

using yQuant.Domain;

internal interface GetAccountBalanceQuery
{
    public Money GetAccountBalance();
}
