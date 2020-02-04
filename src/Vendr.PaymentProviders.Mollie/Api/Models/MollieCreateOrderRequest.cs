using Newtonsoft.Json;
using System;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public class MollieCreateOrderRequest : MollieOrderBase<MollieCreateOrderLine>
    {
        [JsonIgnore]
        public override DateTime ExpiresAt { get; set; }

        [JsonProperty("expiresAt")]
        public string ExpiresAtFormatted
        {
            get => ExpiresAt.ToString("yyyy-MM-dd");
        }
    }

    public class MollieCreateOrderLine : MollieOrderLineBase
    { }
}
