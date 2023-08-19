namespace yQuant.Domains;

public class Money
{
    private readonly decimal amount;
    private readonly string currency;

    public Money(decimal amount, string currency)
    {
        this.amount = amount;
        this.currency = currency;
    }
    public decimal Amount => amount;
    public string Currency => currency;

    public override string ToString()
    {
        return currency + " " + amount;
    }
}