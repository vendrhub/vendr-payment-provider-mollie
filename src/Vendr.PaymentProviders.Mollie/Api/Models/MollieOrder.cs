using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public class MollieOrder : MollieOrderBase<MollieOrderLine>, IMollieHalResource
    {
        public static class Statuses
        {
            public const string Created = "created";
            public const string Paid = "paid";
            public const string Authorized = "authorized";
            public const string Canceled = "canceled";
            public const string Shipping = "shipping";
            public const string Completed = "completed";
            public const string Expired = "expired";
        }

        public static class Modes
        {
            public const string Test = "test";
            public const string Live = "live";
        }

        [JsonProperty("resource")]
        public string Resource { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("profileId")]
        public string ProfileId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("isCancelable")]
        public bool IsCancelable { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("expiredAt")]
        public DateTime ExpiredAt { get; set; }

        [JsonProperty("paidAt")]
        public DateTime PaidAt { get; set; }

        [JsonProperty("authorizedAt")]
        public DateTime AuthorizedAt { get; set; }

        [JsonProperty("canceledAt")]
        public DateTime CanceledAt { get; set; }

        [JsonProperty("completedAt")]
        public DateTime CompletedAt { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("_embed")]
        public MollieOrderEmbed Embed { get; set; }

        [JsonProperty("_links")]
        public IDictionary<string, MollieHalLink> Links { get; set; }
    }
}
