using System.Text.Json;

namespace FarmFreshMarket.Services
{
    public class ReCaptchaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _secretKey;

        public ReCaptchaService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _secretKey = configuration["ReCaptcha:SecretKey"];
        }

        public async Task<bool> VerifyToken(string token)
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    $"https://www.google.com/recaptcha/api/siteverify?secret={_secretKey}&response={token}",
                    null);

                if (!response.IsSuccessStatusCode)
                    return false;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ReCaptchaResponse>(json);

                return result?.Success == true && result.Score >= 0.5;
            }
            catch
            {
                return false;
            }
        }

        private class ReCaptchaResponse
        {
            public bool Success { get; set; }
            public double Score { get; set; }
            public string Action { get; set; }
            public DateTime Challenge_ts { get; set; }
            public string Hostname { get; set; }
        }
    }
}