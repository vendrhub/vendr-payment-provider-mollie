using Newtonsoft.Json;
using System.Collections.Generic;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public class MollieOrderEmbed
    {
        [JsonProperty("payments")]
        public IEnumerable<MolliePayment> Payments { get; set; }

        [JsonProperty("refunds")]
        public IEnumerable<MollieRefund> Refunds { get; set; }
    }
}
