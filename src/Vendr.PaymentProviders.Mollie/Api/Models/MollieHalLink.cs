using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public class MollieHalLink
    {
        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
