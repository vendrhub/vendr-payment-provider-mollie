using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public class MollieAmount
    {
        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("value")]
        public decimal Value { get; set; }
    }
}
