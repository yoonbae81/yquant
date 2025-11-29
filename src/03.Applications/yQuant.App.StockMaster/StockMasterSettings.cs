namespace yQuant.App.StockMaster
{
    public class StockMasterSettings
    {
        public Dictionary<string, CountrySetting> Countries { get; set; } = new();
    }

    public class CountrySetting
    {
        public string RunTime { get; set; } = "07:00:00";
        public Dictionary<string, string> Exchanges { get; set; } = new();
    }
}
