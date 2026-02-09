namespace FarmFreshMarket.Services
{
    public interface ISmsService
    {
        Task<bool> SendSmsAsync(string phoneNumber, string message);
        Task<bool> Send2FACodeAsync(string phoneNumber, string code, string purpose);
    }
}