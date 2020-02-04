using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public abstract class MollieOrderLineBase
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("unitPrice")]
        public MollieAmount UnitPrice { get; set; }

        [JsonProperty("discountAmount")]
        public MollieAmount DiscountAmount { get; set; }

        [JsonProperty("totalAmount")]
        public MollieAmount TotalAmount { get; set; }

        [JsonProperty("vatRate")]
        public string VateRate { get; set; }

        [JsonProperty("vatAmount")]
        public MollieAmount VateAmount { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("productUrl")]
        public string ProductUrl { get; set; }

        [JsonProperty("metadata")]
        public object Metadata { get; set; }
    }
}
