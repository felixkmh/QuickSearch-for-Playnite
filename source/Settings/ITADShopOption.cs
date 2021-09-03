namespace QuickSearch
{
    public partial class SearchSettings
    {
        public class ITADShopOption
        {
            public ITADShopOption(string name)
            {
                Name = name;
            }
            public string Name { get; set; }
            public bool Enabled { get; set; } = true;
        }
    }
}