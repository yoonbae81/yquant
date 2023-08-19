namespace yQuant.Domains;

public class Account
{
    private readonly Money baselineBalance;

    public Account(Money baselineBalance)
    {
        this.baselineBalance = baselineBalance;
    }
}