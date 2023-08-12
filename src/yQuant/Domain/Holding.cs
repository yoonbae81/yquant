namespace yQuant.Domain;

internal class Holding
{
    private readonly string ticker;

    public Holding(string ticker)
    {
        this.ticker = ticker;
    }

    public string Ticker => ticker;
}
