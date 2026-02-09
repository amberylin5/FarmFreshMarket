namespace FarmFreshMarket.Models
{
    public class SmsSettings
    {
        public string AccountSid { get; set; }
        public string AuthToken { get; set; }
        public string FromNumber { get; set; }
        public bool UseSimulation { get; set; } = true;
    }
}