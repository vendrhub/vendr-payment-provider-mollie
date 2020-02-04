using Flurl.Http;
using Vendr.PaymentProviders.Mollie.Api.Models;

namespace Vendr.PaymentProviders.Mollie.Api
{
    public class MollieClient
    {
        private static string BASE_URL = "https://api.mollie.com/v2/";

        private MollieClientConfig _config;

        public MollieClient(MollieClientConfig config)
        {
            _config = config;
        }

        public MollieOrder CreateOrder(MollieCreateOrderRequest request)
        {
            var result = new FlurlRequest($"{BASE_URL}/ sessions")
                .AllowAnyHttpStatus()
                .WithHeader("Authorization", "Bearer " + _config.ApiKey)
                .PostJsonAsync(request)
                .ReceiveJson<MollieOrder>()
                .Result;

            return result;
        }
    }
}
