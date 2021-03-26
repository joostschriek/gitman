namespace gitman
{
    public class Acting
    {
        public enum Act { Add, Remove }
        public Act Action { get; set; }
        public string Name { get; set; }
    }
}